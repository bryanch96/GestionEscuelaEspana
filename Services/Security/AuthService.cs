using GestionEscuelaEspana.Data.Repositorios;
using GestionEscuelaEspana.Data;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Plataforma;
using Microsoft.Extensions.Logging;

namespace GestionEscuelaEspana.Services.Security
{
    public enum TipoResultadoLogin
    {
        Exito,
        CredencialesInvalidas,
        AplicacionBloqueada,
        CuentaBloqueada,
        CuentaInactiva
    }

    public sealed record ResultadoLogin(
        TipoResultadoLogin Tipo,
        string Mensaje,
        DateTime? BloqueadoHastaUtc = null,
        int IntentosRestantes = 0)
    {
        public bool Exito => Tipo == TipoResultadoLogin.Exito;
    }

    public interface IAuthService
    {
        Task<ResultadoLogin> IniciarSesionAsync(string usuarioOCorreo, string password);
        void CerrarSesion();

        /// <summary>Fecha (UTC) hasta la que la aplicación está bloqueada, o null si no lo está.</summary>
        Task<DateTime?> ObtenerBloqueoVigenteAsync();
    }

    /// <summary>
    /// Autenticación local contra la tabla de usuarios, con bloqueo escalonado por intentos fallidos.
    /// El contador es global al dispositivo (protege la aplicación, no solo una cuenta) y se persiste en SQLite.
    /// </summary>
    public sealed class AuthService : IAuthService
    {
        private readonly AppDatabase _db;
        private readonly IRepositorio<Usuario> _usuarios;
        private readonly IPasswordHasher _hasher;
        private readonly PoliticaBloqueo _politica;
        private readonly IRecuperacionPasswordService _recuperacion;
        private readonly ISesionUsuario _sesion;
        private readonly IClock _reloj;
        private readonly ILogger<AuthService> _logger;
        private readonly SemaphoreSlim _candado = new(1, 1);

        // Hash ficticio para igualar el tiempo de respuesta cuando el usuario no existe (evita enumeración por tiempo).
        private readonly Lazy<string> _hashFicticio;

        public AuthService(AppDatabase db, IRepositorio<Usuario> usuarios, IPasswordHasher hasher, PoliticaBloqueo politica,
            IRecuperacionPasswordService recuperacion, ISesionUsuario sesion, IClock reloj, ILogger<AuthService> logger)
        {
            _db = db;
            _usuarios = usuarios;
            _hasher = hasher;
            _politica = politica;
            _recuperacion = recuperacion;
            _sesion = sesion;
            _reloj = reloj;
            _logger = logger;
            _hashFicticio = new Lazy<string>(() => _hasher.Hash(Guid.NewGuid().ToString()));
        }

        public async Task<DateTime?> ObtenerBloqueoVigenteAsync()
        {
            var estado = await ObtenerEstadoAsync();
            return estado.BloqueadoHastaUtc is { } hasta && hasta > _reloj.UtcNow ? hasta : null;
        }

        public async Task<ResultadoLogin> IniciarSesionAsync(string usuarioOCorreo, string password)
        {
            usuarioOCorreo = (usuarioOCorreo ?? string.Empty).Trim();

            // Serializa los intentos para que el contador no tenga condiciones de carrera.
            await _candado.WaitAsync();
            try
            {
                var conexion = await _db.ObtenerConexionAsync();
                var estado = await ObtenerEstadoAsync();
                var ahora = _reloj.UtcNow;

                if (estado.BloqueadoHastaUtc is { } bloqueo && bloqueo > ahora)
                    return new ResultadoLogin(TipoResultadoLogin.AplicacionBloqueada, MensajeBloqueo(bloqueo - ahora), bloqueo);

                var usuario = await BuscarUsuarioAsync(usuarioOCorreo);

                if (usuario is { BloqueadoPorSeguridad: true })
                    return new ResultadoLogin(TipoResultadoLogin.CuentaBloqueada,
                        "La cuenta está bloqueada por seguridad. Revise su correo o use \"¿Olvidó su contraseña?\" para restablecerla.");

                var passwordValida = _hasher.Verificar(password ?? string.Empty, usuario?.PasswordHash ?? _hashFicticio.Value);

                if (usuario != null && passwordValida)
                {
                    if (!usuario.Activo)
                        return new ResultadoLogin(TipoResultadoLogin.CuentaInactiva, "La cuenta está desactivada. Contacte al administrador.");

                    estado.IntentosFallidosConsecutivos = 0;
                    estado.BloqueadoHastaUtc = null;
                    estado.UltimoIntentoUtc = ahora;
                    await conexion.UpdateAsync(estado);

                    usuario.UltimoAcceso = _reloj.Now;
                    await _usuarios.GuardarAsync(usuario);

                    _sesion.Establecer(usuario);
                    _logger.LogInformation("Inicio de sesión correcto de {Usuario}", usuario.NombreUsuario);
                    return new ResultadoLogin(TipoResultadoLogin.Exito, $"Bienvenido, {usuario.NombreCompleto}.");
                }

                return await RegistrarFalloAsync(estado, usuario, ahora);
            }
            finally
            {
                _candado.Release();
            }
        }

        public void CerrarSesion() => _sesion.Establecer(null);

        private async Task<ResultadoLogin> RegistrarFalloAsync(EstadoSeguridad estado, Usuario? usuario, DateTime ahora)
        {
            var conexion = await _db.ObtenerConexionAsync();
            estado.IntentosFallidosConsecutivos++;
            estado.UltimoIntentoUtc = ahora;

            var decision = _politica.Evaluar(estado.IntentosFallidosConsecutivos);
            estado.BloqueadoHastaUtc = decision.BloqueoTemporal is { } duracion ? ahora.Add(duracion) : null;

            _logger.LogWarning("Intento de inicio de sesión fallido #{Intento}", estado.IntentosFallidosConsecutivos);

            if (decision.BloquearCuenta && usuario != null)
            {
                usuario.BloqueadoPorSeguridad = true;
                await _usuarios.GuardarAsync(usuario);

                // La cuenta queda protegida: se reinicia el contador global para no castigar a otros usuarios del equipo.
                estado.IntentosFallidosConsecutivos = 0;
                await conexion.UpdateAsync(estado);

                var envio = await _recuperacion.EnviarCodigoAsync(usuario, MotivoRecuperacion.BloqueoPorIntentos);
                var detalleCorreo = envio.Exito
                    ? "Se envió un correo con un código para restablecer la contraseña."
                    : "No fue posible enviar el correo de recuperación; contacte al administrador.";

                return new ResultadoLogin(TipoResultadoLogin.CuentaBloqueada,
                    $"Demasiados intentos fallidos. La cuenta fue bloqueada por seguridad. {detalleCorreo}",
                    estado.BloqueadoHastaUtc);
            }

            await conexion.UpdateAsync(estado);

            if (estado.BloqueadoHastaUtc is { } hasta)
                return new ResultadoLogin(TipoResultadoLogin.AplicacionBloqueada,
                    $"Intento fallido #{estado.IntentosFallidosConsecutivos}. {MensajeBloqueo(hasta - ahora)}", hasta);

            return new ResultadoLogin(TipoResultadoLogin.CredencialesInvalidas,
                $"Usuario o contraseña incorrectos. Le quedan {decision.IntentosRestantes} intento(s) antes de un bloqueo temporal.",
                IntentosRestantes: decision.IntentosRestantes);
        }

        private async Task<Usuario?> BuscarUsuarioAsync(string usuarioOCorreo)
        {
            if (string.IsNullOrEmpty(usuarioOCorreo)) return null;
            // Las columnas usan COLLATE NOCASE: la comparación no distingue mayúsculas.
            return await _usuarios.PrimeroAsync(u => u.NombreUsuario == usuarioOCorreo || u.Correo == usuarioOCorreo);
        }

        private async Task<EstadoSeguridad> ObtenerEstadoAsync()
        {
            var conexion = await _db.ObtenerConexionAsync();
            return await conexion.FindAsync<EstadoSeguridad>(EstadoSeguridad.IdUnico) ?? new EstadoSeguridad();
        }

        private static string MensajeBloqueo(TimeSpan restante)
        {
            var minutos = Math.Max(1, (int)Math.Ceiling(restante.TotalMinutes));
            return $"La aplicación está bloqueada por seguridad. Intente de nuevo en {minutos} minuto(s).";
        }
    }
}
