using System.Globalization;
using System.Net;
using System.Text;
using GestionEscuelaEspana.Models;

namespace GestionEscuelaEspana.Services.Email
{
    /// <summary>
    /// Plantillas HTML de los correos. Todo dato ingresado por usuarios se codifica con HtmlEncode
    /// para evitar inyección de HTML en el cuerpo del mensaje.
    /// </summary>
    public static class PlantillasCorreo
    {
        private static readonly CultureInfo Cultura = new("es-CR");

        private static string E(string? texto) => WebUtility.HtmlEncode(texto ?? string.Empty);

        private static string Envolver(string escuela, string titulo, string colorTitulo, string contenido) => $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;background:#f1f5f9;padding:24px;">
              <div style="max-width:560px;margin:auto;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,.06);">
                <div style="background:{colorTitulo};color:#ffffff;padding:18px 24px;">
                  <div style="font-size:13px;opacity:.85;">{E(escuela)}</div>
                  <div style="font-size:20px;font-weight:700;">{E(titulo)}</div>
                </div>
                <div style="padding:24px;color:#334155;font-size:15px;line-height:1.55;">{contenido}</div>
                <div style="padding:14px 24px;background:#f8fafc;color:#94a3b8;font-size:12px;">
                  Este es un mensaje automático de {E(escuela)}.
                </div>
              </div>
            </div>
            """;

        private static string FilaDetalle(string etiqueta, string valor) =>
            $"<tr><td style=\"padding:6px 12px 6px 0;color:#64748b;\">{E(etiqueta)}</td><td style=\"padding:6px 0;font-weight:600;\">{E(valor)}</td></tr>";

        private static string TablaCita(Cita cita)
        {
            var sb = new StringBuilder("<table style=\"border-collapse:collapse;margin:12px 0;\">");
            sb.Append(FilaDetalle("Fecha", cita.Fecha.ToString("dddd d 'de' MMMM 'de' yyyy", Cultura)));
            sb.Append(FilaDetalle("Horario", $"{cita.HoraInicio:hh\\:mm} a {cita.HoraFin:hh\\:mm}"));
            sb.Append(FilaDetalle("Plantel", cita.Plantel));
            sb.Append(FilaDetalle("Vehículo", cita.TipoVehiculo));
            sb.Append(FilaDetalle("Instructor", cita.InstructorNombre));
            sb.Append(FilaDetalle("Precio", "₡" + cita.Precio.ToString("N0", Cultura)));
            sb.Append(FilaDetalle("Estado", cita.Estado));
            sb.Append("</table>");
            return sb.ToString();
        }

        public static MensajeCorreo ConfirmacionCita(Cita cita, string escuela, bool esActualizacion)
        {
            var titulo = esActualizacion ? "Su cita fue actualizada" : "Confirmación de cita";
            var contenido = $"""
                <p>Hola <strong>{E(cita.ClienteNombre)}</strong>,</p>
                <p>{(esActualizacion ? "Le informamos que los detalles de su cita cambiaron." : "Su cita de práctica de manejo quedó agendada con éxito.")} Estos son los detalles:</p>
                {TablaCita(cita)}
                {(string.IsNullOrWhiteSpace(cita.Observaciones) ? "" : $"<p><em>Observaciones:</em> {E(cita.Observaciones)}</p>")}
                <p>Le recomendamos presentarse 15 minutos antes con su cédula y licencia de aprendizaje.</p>
                """;
            return new MensajeCorreo
            {
                Para = cita.ClienteCorreo,
                NombrePara = cita.ClienteNombre,
                Asunto = $"{titulo} – {cita.Fecha:dd/MM/yyyy} {cita.HoraInicio:hh\\:mm}",
                CuerpoHtml = Envolver(escuela, titulo, "#0284c7", contenido),
                CuerpoTexto = $"{titulo}: {cita.Fecha:dd/MM/yyyy} de {cita.HoraInicio:hh\\:mm} a {cita.HoraFin:hh\\:mm}, plantel {cita.Plantel}, instructor {cita.InstructorNombre}.",
                Categoria = "Cita"
            };
        }

        public static MensajeCorreo CancelacionCita(Cita cita, string escuela)
        {
            var contenido = $"""
                <p>Hola <strong>{E(cita.ClienteNombre)}</strong>,</p>
                <p>Le informamos que la siguiente cita fue <strong>cancelada</strong>:</p>
                {TablaCita(cita)}
                {(string.IsNullOrWhiteSpace(cita.MotivoCancelacion) ? "" : $"<p><em>Motivo:</em> {E(cita.MotivoCancelacion)}</p>")}
                <p>Contáctenos para reprogramarla en el horario que mejor le convenga.</p>
                """;
            return new MensajeCorreo
            {
                Para = cita.ClienteCorreo,
                NombrePara = cita.ClienteNombre,
                Asunto = $"Cita cancelada – {cita.Fecha:dd/MM/yyyy}",
                CuerpoHtml = Envolver(escuela, "Cita cancelada", "#dc2626", contenido),
                CuerpoTexto = $"Su cita del {cita.Fecha:dd/MM/yyyy} a las {cita.HoraInicio:hh\\:mm} fue cancelada.",
                Categoria = "Cita"
            };
        }

        public static MensajeCorreo CodigoRecuperacion(Usuario usuario, string codigo, int minutosVigencia, bool porBloqueo, string escuela)
        {
            var intro = porBloqueo
                ? "Detectamos varios intentos fallidos de inicio de sesión en su cuenta, por lo que la <strong>bloqueamos por seguridad</strong>."
                : "Recibimos una solicitud para restablecer la contraseña de su cuenta.";
            var contenido = $"""
                <p>Hola <strong>{E(usuario.NombreCompleto)}</strong>,</p>
                <p>{intro}</p>
                <p>Use este código en la opción <em>"¿Olvidó su contraseña?"</em> de la aplicación:</p>
                <div style="font-size:32px;font-weight:800;letter-spacing:8px;text-align:center;background:#f1f5f9;border-radius:10px;padding:16px;margin:16px 0;color:#0f172a;">{E(codigo)}</div>
                <p>El código vence en <strong>{minutosVigencia} minutos</strong> y solo puede usarse una vez.</p>
                <p style="color:#64748b;font-size:13px;">Si usted no hizo esta solicitud, ignore este mensaje y avise al administrador.</p>
                """;
            return new MensajeCorreo
            {
                Para = usuario.Correo,
                NombrePara = usuario.NombreCompleto,
                Asunto = porBloqueo ? "Cuenta bloqueada – código de recuperación" : "Código para restablecer su contraseña",
                CuerpoHtml = Envolver(escuela, "Recuperación de contraseña", "#7c3aed", contenido),
                CuerpoTexto = $"Su código de recuperación es {codigo}. Vence en {minutosVigencia} minutos.",
                Categoria = "Recuperación"
            };
        }

        public static MensajeCorreo Seguimiento(Estudiante estudiante, NotaSeguimiento nota, IReadOnlyList<AdjuntoCorreo> adjuntos, string escuela)
        {
            var estrellas = nota.Calificacion > 0 ? new string('★', nota.Calificacion) + new string('☆', 5 - nota.Calificacion) : null;
            var textoHtml = E(nota.Texto).Replace("\n", "<br/>");
            var contenido = $"""
                <p>Hola <strong>{E(estudiante.NombreCompleto)}</strong>,</p>
                <p>Su instructor <strong>{E(nota.AutorNombre)}</strong> le comparte la siguiente retroalimentación del {nota.Fecha.ToString("d 'de' MMMM 'de' yyyy", Cultura)}:</p>
                {(string.IsNullOrWhiteSpace(nota.Titulo) ? "" : $"<h3 style=\"margin:12px 0 4px;color:#0f172a;\">{E(nota.Titulo)}</h3>")}
                {(estrellas == null ? "" : $"<div style=\"color:#f59e0b;font-size:20px;\">{estrellas}</div>")}
                <div style="background:#f8fafc;border-left:4px solid #10b981;padding:12px 16px;margin:12px 0;border-radius:6px;">{textoHtml}</div>
                {(adjuntos.Count > 0 ? $"<p>Se adjuntan {adjuntos.Count} archivo(s) de evidencia (fotografías/videos).</p>" : "")}
                <p>¡Siga practicando! Estamos para apoyarle.</p>
                """;
            return new MensajeCorreo
            {
                Para = estudiante.Correo,
                NombrePara = estudiante.NombreCompleto,
                Asunto = string.IsNullOrWhiteSpace(nota.Titulo) ? "Retroalimentación de su práctica de manejo" : $"Retroalimentación: {nota.Titulo}",
                CuerpoHtml = Envolver(escuela, "Seguimiento de su aprendizaje", "#10b981", contenido),
                CuerpoTexto = nota.Texto,
                Adjuntos = adjuntos,
                Categoria = "Seguimiento"
            };
        }

        public static MensajeCorreo MensajeDirecto(Estudiante estudiante, string asunto, string mensaje, string remitente, string escuela)
        {
            var contenido = $"""
                <p>Hola <strong>{E(estudiante.NombreCompleto)}</strong>,</p>
                <div style="white-space:normal;">{E(mensaje).Replace("\n", "<br/>")}</div>
                <p style="margin-top:20px;">Saludos,<br/><strong>{E(remitente)}</strong></p>
                """;
            return new MensajeCorreo
            {
                Para = estudiante.Correo,
                NombrePara = estudiante.NombreCompleto,
                Asunto = asunto,
                CuerpoHtml = Envolver(escuela, asunto, "#1e293b", contenido),
                CuerpoTexto = mensaje,
                Categoria = "Mensaje directo"
            };
        }

        public static MensajeCorreo Prueba(string destinatario, string escuela) => new()
        {
            Para = destinatario,
            Asunto = "Prueba de configuración SMTP",
            CuerpoHtml = Envolver(escuela, "¡Configuración correcta!", "#10b981", "<p>Si recibió este mensaje, el envío de correos de la aplicación funciona correctamente.</p>"),
            CuerpoTexto = "Prueba de configuración SMTP correcta.",
            Categoria = "Prueba"
        };
    }
}
