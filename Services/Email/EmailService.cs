using GestionEscuelaEspana.Data.Repositorios;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Common;
using GestionEscuelaEspana.Services.Configuracion;
using Microsoft.Extensions.Logging;

namespace GestionEscuelaEspana.Services.Email
{
    /// <summary>
    /// Fachada de correo: valida el destinatario y los adjuntos, decide si enviar por SMTP real o simular
    /// (según la configuración), captura errores de red sin romper la UI y registra cada envío en la bitácora.
    /// </summary>
    public sealed class EmailService : IEmailService
    {
        private readonly IConfiguracionService _configuracion;
        private readonly SmtpEmailSender _smtp;
        private readonly BandejaSalidaEmailSender _simulado;
        private readonly IRepositorio<RegistroCorreo> _bitacora;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguracionService configuracion, SmtpEmailSender smtp, BandejaSalidaEmailSender simulado,
            IRepositorio<RegistroCorreo> bitacora, ILogger<EmailService> logger)
        {
            _configuracion = configuracion;
            _smtp = smtp;
            _simulado = simulado;
            _bitacora = bitacora;
            _logger = logger;
        }

        public long TamanoMaximoAdjuntosBytes => 20L * 1024 * 1024;

        public async Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
        {
            ResultadoEnvio resultado;

            if (!Validaciones.EsCorreoValido(mensaje.Para))
            {
                resultado = new ResultadoEnvio(false, false, $"La dirección de correo \"{mensaje.Para}\" no es válida.");
            }
            else if (CalcularTamanoAdjuntos(mensaje) > TamanoMaximoAdjuntosBytes)
            {
                resultado = new ResultadoEnvio(false, false,
                    $"Los adjuntos superan el máximo de {TamanoMaximoAdjuntosBytes / (1024 * 1024)} MB permitido por correo.");
            }
            else
            {
                try
                {
                    var emisor = await _configuracion.SmtpConfiguradoAsync() ? (IEmailSender)_smtp : _simulado;
                    resultado = await emisor.EnviarAsync(mensaje, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al enviar correo a {Destinatario}", mensaje.Para);
                    resultado = new ResultadoEnvio(false, false, $"No se pudo enviar el correo: {ex.Message}");
                }
            }

            await RegistrarAsync(mensaje, resultado);
            return resultado;
        }

        private static long CalcularTamanoAdjuntos(MensajeCorreo mensaje) =>
            mensaje.Adjuntos.Sum(a => File.Exists(a.RutaLocal) ? new FileInfo(a.RutaLocal).Length : 0);

        private async Task RegistrarAsync(MensajeCorreo mensaje, ResultadoEnvio resultado)
        {
            try
            {
                await _bitacora.GuardarAsync(new RegistroCorreo
                {
                    Fecha = DateTime.Now,
                    Destinatario = mensaje.Para,
                    Asunto = mensaje.Asunto,
                    Categoria = mensaje.Categoria,
                    Exitoso = resultado.Exito,
                    Simulado = resultado.Simulado,
                    Detalle = resultado.Mensaje
                });
            }
            catch (Exception ex)
            {
                // La bitácora nunca debe impedir el flujo principal.
                _logger.LogWarning(ex, "No se pudo registrar el envío de correo en la bitácora");
            }
        }
    }
}
