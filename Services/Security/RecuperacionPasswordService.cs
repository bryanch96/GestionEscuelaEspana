using System.Security.Cryptography;
using GestionEscuelaEspana.Data.Repositorios;
using GestionEscuelaEspana.Data;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Common;
using GestionEscuelaEspana.Services.Configuracion;
using GestionEscuelaEspana.Services.Email;
using GestionEscuelaEspana.Services.Plataforma;

namespace GestionEscuelaEspana.Services.Security
{
    public enum MotivoRecuperacion
    {
        SolicitudUsuario,
        BloqueoPorIntentos
    }

    public interface IRecuperacionPasswordService
    {
        /// <summary>Valida que el correo esté registrado y le envía un código de recuperación.</summary>
        Task<Resultado> SolicitarCodigoAsync(string correo);

        /// <summary>Genera y envía un código al usuario indicado (usado también por el bloqueo automático).</summary>
        Task<Resultado> EnviarCodigoAsync(Usuario usuario, MotivoRecuperacion motivo);

        /// <summary>Verifica el código y establece la nueva contraseña; desbloquea la cuenta.</summary>
        Task<Resultado> RestablecerAsync(string correo, string codigo, string nuevaPassword);
    }

    /// <summary>
    /// Flujo "Olvidé mi contraseña" con código de 6 dígitos de un solo uso:
    /// se guarda solo su hash, vence en 15 minutos, admite 5 intentos y hay un tiempo mínimo entre solicitudes.
    /// </summary>
    public sealed class RecuperacionPasswordService : IRecuperacionPasswordService
    {
        public const int MinutosVigencia = 15;
        public const int MaxIntentosCodigo = 5;
        public static readonly TimeSpan EsperaEntreSolicitudes = TimeSpan.FromSeconds(60);

        private readonly AppDatabase _db;
        private readonly IRepositorio<Usuario> _usuarios;
        private readonly IRepositorio<CodigoRecuperacion> _codigos;
        private readonly IPasswordHasher _hasher;
        private readonly IEmailService _email;
        private readonly IConfiguracionService _configuracion;
        private readonly IClock _reloj;

        public RecuperacionPasswordService(AppDatabase db, IRepositorio<Usuario> usuarios, IRepositorio<CodigoRecuperacion> codigos,
            IPasswordHasher hasher, IEmailService email, IConfiguracionService configuracion, IClock reloj)
        {
            _db = db;
            _usuarios = usuarios;
            _codigos = codigos;
            _hasher = hasher;
            _email = email;
            _configuracion = configuracion;
            _reloj = reloj;
        }

        public async Task<Resultado> SolicitarCodigoAsync(string correo)
        {
            correo = (correo ?? string.Empty).Trim();
            if (!Validaciones.EsCorreoValido(correo))
                return Resultado.Fallo("Ingrese un correo electrónico válido.");

            var usuario = await _usuarios.PrimeroAsync(u => u.Correo == correo);
            if (usuario == null)
                return Resultado.Fallo("El correo indicado no está registrado en el sistema.");
            if (!usuario.Activo)
                return Resultado.Fallo("La cuenta asociada está desactivada. Contacte al administrador.");

            var ultimo = (await _codigos.ListarAsync(c => c.UsuarioId == usuario.Id))
                .OrderByDescending(c => c.CreadoUtc).FirstOrDefault();
            if (ultimo != null && _reloj.UtcNow - ultimo.CreadoUtc < EsperaEntreSolicitudes)
                return Resultado.Fallo("Ya se envió un código hace unos segundos. Espere un minuto antes de solicitar otro.");

            return await EnviarCodigoAsync(usuario, MotivoRecuperacion.SolicitudUsuario);
        }

        public async Task<Resultado> EnviarCodigoAsync(Usuario usuario, MotivoRecuperacion motivo)
        {
            await InvalidarCodigosAsync(usuario.Id);

            var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            await _codigos.GuardarAsync(new CodigoRecuperacion
            {
                UsuarioId = usuario.Id,
                CodigoHash = _hasher.Hash(codigo),
                CreadoUtc = _reloj.UtcNow,
                ExpiraUtc = _reloj.UtcNow.AddMinutes(MinutosVigencia)
            });

            var config = await _configuracion.ObtenerAsync();
            var mensaje = PlantillasCorreo.CodigoRecuperacion(usuario, codigo, MinutosVigencia,
                motivo == MotivoRecuperacion.BloqueoPorIntentos, config.NombreEscuela);
            var envio = await _email.EnviarAsync(mensaje);

            if (!envio.Exito) return Resultado.Fallo(envio.Mensaje);

            return Resultado.Ok(envio.Simulado
                ? $"Código generado. {envio.Mensaje}"
                : $"Enviamos un código de verificación a {Enmascarar(usuario.Correo)}.");
        }

        public async Task<Resultado> RestablecerAsync(string correo, string codigo, string nuevaPassword)
        {
            correo = (correo ?? string.Empty).Trim();
            codigo = (codigo ?? string.Empty).Trim();

            var errores = PoliticaPassword.Validar(nuevaPassword);
            if (errores.Count > 0) return Resultado.Fallo(string.Join(" ", errores));

            var usuario = await _usuarios.PrimeroAsync(u => u.Correo == correo);
            if (usuario == null) return Resultado.Fallo("El correo indicado no está registrado en el sistema.");

            var vigente = (await _codigos.ListarAsync(c => c.UsuarioId == usuario.Id && !c.Usado))
                .OrderByDescending(c => c.CreadoUtc).FirstOrDefault();

            if (vigente == null || vigente.ExpiraUtc <= _reloj.UtcNow)
                return Resultado.Fallo("El código venció o no existe. Solicite uno nuevo.");

            if (!_hasher.Verificar(codigo, vigente.CodigoHash))
            {
                vigente.IntentosFallidos++;
                if (vigente.IntentosFallidos >= MaxIntentosCodigo) vigente.Usado = true;
                await _codigos.GuardarAsync(vigente);
                return Resultado.Fallo(vigente.Usado
                    ? "Demasiados intentos con un código incorrecto. Solicite un código nuevo."
                    : $"Código incorrecto. Le quedan {MaxIntentosCodigo - vigente.IntentosFallidos} intento(s).");
            }

            vigente.Usado = true;
            await _codigos.GuardarAsync(vigente);

            usuario.PasswordHash = _hasher.Hash(nuevaPassword);
            usuario.BloqueadoPorSeguridad = false;
            await _usuarios.GuardarAsync(usuario);

            // Una recuperación exitosa demuestra la identidad: se levanta también el bloqueo de la aplicación.
            var conexion = await _db.ObtenerConexionAsync();
            await conexion.ExecuteAsync("UPDATE EstadoSeguridad SET IntentosFallidosConsecutivos = 0, BloqueadoHastaUtc = NULL WHERE Id = ?",
                EstadoSeguridad.IdUnico);

            return Resultado.Ok("Contraseña actualizada. Ya puede iniciar sesión.");
        }

        private async Task InvalidarCodigosAsync(int usuarioId)
        {
            var conexion = await _db.ObtenerConexionAsync();
            await conexion.ExecuteAsync("UPDATE CodigoRecuperacion SET Usado = 1 WHERE UsuarioId = ? AND Usado = 0", usuarioId);
        }

        /// <summary>juan.perez@correo.com → ju*******@correo.com</summary>
        public static string Enmascarar(string correo)
        {
            var arroba = correo.IndexOf('@');
            if (arroba <= 2) return correo;
            return correo[..2] + new string('*', arroba - 2) + correo[arroba..];
        }
    }
}
