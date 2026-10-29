using GestionEscuelaEspana.Data;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Common;
using GestionEscuelaEspana.Services.Plataforma;

namespace GestionEscuelaEspana.Services.Configuracion
{
    public interface IConfiguracionService
    {
        /// <summary>Configuración vigente (copia en caché; modificarla no afecta hasta llamar a GuardarAsync).</summary>
        Task<ConfiguracionApp> ObtenerAsync();
        Task<Resultado> GuardarAsync(ConfiguracionApp configuracion);
        Task<string?> ObtenerPasswordSmtpAsync();
        Task GuardarPasswordSmtpAsync(string? password);
        Task<bool> SmtpConfiguradoAsync();
    }

    public sealed class ConfiguracionService : IConfiguracionService
    {
        private const string ClavePasswordSmtp = "smtp_password";

        private readonly AppDatabase _db;
        private readonly ISecretStore _secretos;
        private ConfiguracionApp? _cache;

        public ConfiguracionService(AppDatabase db, ISecretStore secretos)
        {
            _db = db;
            _secretos = secretos;
        }

        public async Task<ConfiguracionApp> ObtenerAsync()
        {
            if (_cache == null)
            {
                var conexion = await _db.ObtenerConexionAsync();
                _cache = await conexion.FindAsync<ConfiguracionApp>(ConfiguracionApp.IdUnico) ?? new ConfiguracionApp();
            }
            return _cache.Clonar();
        }

        public async Task<Resultado> GuardarAsync(ConfiguracionApp configuracion)
        {
            if (string.IsNullOrWhiteSpace(configuracion.NombreEscuela))
                return Resultado.Fallo("El nombre de la escuela es obligatorio.");
            if (!configuracion.CodigoPais.All(char.IsDigit) || configuracion.CodigoPais.Length is < 1 or > 4)
                return Resultado.Fallo("El código de país debe tener entre 1 y 4 dígitos (ej. 506).");

            if (configuracion.SmtpHabilitado)
            {
                if (string.IsNullOrWhiteSpace(configuracion.SmtpServidor))
                    return Resultado.Fallo("Indique el servidor SMTP.");
                if (configuracion.SmtpPuerto is < 1 or > 65535)
                    return Resultado.Fallo("El puerto SMTP no es válido.");
                if (!Validaciones.EsCorreoValido(configuracion.RemitenteCorreo))
                    return Resultado.Fallo("Indique un correo de remitente válido.");
            }

            configuracion.Id = ConfiguracionApp.IdUnico;
            var conexion = await _db.ObtenerConexionAsync();
            await conexion.InsertOrReplaceAsync(configuracion);
            _cache = configuracion.Clonar();
            return Resultado.Ok("Configuración guardada.");
        }

        public Task<string?> ObtenerPasswordSmtpAsync() => _secretos.ObtenerAsync(ClavePasswordSmtp);

        public async Task GuardarPasswordSmtpAsync(string? password)
        {
            if (string.IsNullOrEmpty(password))
                _secretos.Eliminar(ClavePasswordSmtp);
            else
                await _secretos.GuardarAsync(ClavePasswordSmtp, password);
        }

        public async Task<bool> SmtpConfiguradoAsync()
        {
            var config = await ObtenerAsync();
            return config.SmtpHabilitado && !string.IsNullOrWhiteSpace(config.SmtpServidor);
        }
    }
}
