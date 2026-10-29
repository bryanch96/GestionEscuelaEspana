using GestionEscuelaEspana.Services.Configuracion;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace GestionEscuelaEspana.Services.Email
{
    /// <summary>Envío real vía SMTP con MailKit (recomendado por Microsoft frente a System.Net.Mail).</summary>
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguracionService _configuracion;

        public SmtpEmailSender(IConfiguracionService configuracion) => _configuracion = configuracion;

        public async Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
        {
            var config = await _configuracion.ObtenerAsync();
            var password = await _configuracion.ObtenerPasswordSmtpAsync();
            var mime = await MimeMessageFactory.CrearAsync(mensaje, config, ct);

            using var cliente = new SmtpClient { Timeout = 30_000 };

            // 465 usa SSL implícito; el resto (587/25) negocia STARTTLS cuando está disponible.
            var seguridad = !config.SmtpUsarSsl
                ? SecureSocketOptions.StartTlsWhenAvailable
                : config.SmtpPuerto == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

            await cliente.ConnectAsync(config.SmtpServidor, config.SmtpPuerto, seguridad, ct);
            if (!string.IsNullOrWhiteSpace(config.SmtpUsuario))
                await cliente.AuthenticateAsync(config.SmtpUsuario, password ?? string.Empty, ct);

            await cliente.SendAsync(mime, ct);
            await cliente.DisconnectAsync(quit: true, ct);

            return new ResultadoEnvio(true, false, $"Correo enviado a {mensaje.Para}.");
        }
    }
}
