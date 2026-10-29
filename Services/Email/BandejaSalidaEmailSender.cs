using GestionEscuelaEspana.Services.Configuracion;
using GestionEscuelaEspana.Services.Plataforma;

namespace GestionEscuelaEspana.Services.Email
{
    /// <summary>
    /// Envío simulado: mientras no haya SMTP configurado, el correo completo (con adjuntos) se guarda como
    /// archivo .eml en la carpeta "BandejaSalida" de la app. Puede abrirse con cualquier cliente de correo.
    /// </summary>
    public sealed class BandejaSalidaEmailSender : IEmailSender
    {
        private readonly IConfiguracionService _configuracion;
        private readonly IAppPaths _paths;

        public BandejaSalidaEmailSender(IConfiguracionService configuracion, IAppPaths paths)
        {
            _configuracion = configuracion;
            _paths = paths;
        }

        public string Carpeta => Path.Combine(_paths.CarpetaDatos, "BandejaSalida");

        public async Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
        {
            var config = await _configuracion.ObtenerAsync();
            var mime = await MimeMessageFactory.CrearAsync(mensaje, config, ct);

            Directory.CreateDirectory(Carpeta);
            var nombre = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.eml";
            var ruta = Path.Combine(Carpeta, nombre);
            await mime.WriteToAsync(ruta, ct);

            return new ResultadoEnvio(true, true, $"SMTP no configurado: correo simulado guardado en {ruta}");
        }
    }
}
