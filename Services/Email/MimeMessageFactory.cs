using GestionEscuelaEspana.Models;
using MimeKit;

namespace GestionEscuelaEspana.Services.Email
{
    /// <summary>Construye el mensaje MIME (HTML + texto plano + adjuntos) compartido por los distintos emisores.</summary>
    public static class MimeMessageFactory
    {
        public static async Task<MimeMessage> CrearAsync(MensajeCorreo mensaje, ConfiguracionApp config, CancellationToken ct)
        {
            var remitente = string.IsNullOrWhiteSpace(config.RemitenteCorreo) ? config.SmtpUsuario : config.RemitenteCorreo;
            if (string.IsNullOrWhiteSpace(remitente)) remitente = "no-reply@escuela.local";

            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(config.RemitenteNombre, remitente));
            mime.To.Add(new MailboxAddress(mensaje.NombrePara, mensaje.Para));
            mime.Subject = mensaje.Asunto;

            var cuerpo = new BodyBuilder
            {
                HtmlBody = mensaje.CuerpoHtml,
                TextBody = string.IsNullOrWhiteSpace(mensaje.CuerpoTexto) ? null : mensaje.CuerpoTexto
            };

            foreach (var adjunto in mensaje.Adjuntos)
            {
                ct.ThrowIfCancellationRequested();
                var contenido = await File.ReadAllBytesAsync(adjunto.RutaLocal, ct);
                cuerpo.Attachments.Add(adjunto.NombreArchivo, contenido, ContentType.Parse(adjunto.ContentType));
            }

            mime.Body = cuerpo.ToMessageBody();
            return mime;
        }
    }
}
