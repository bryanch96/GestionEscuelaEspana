using System.Globalization;
using System.Text;
using GestionEscuelaEspana.Models;

namespace GestionEscuelaEspana.Services.Citas
{
    /// <summary>Genera el historial en CSV compatible con Excel en español (separador ";" y BOM UTF-8).</summary>
    public static class ExportadorCitas
    {
        private const char Separador = ';';

        public static string ACsv(IEnumerable<Cita> citas)
        {
            var cultura = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(Separador, "Id", "Fecha", "Inicio", "Fin", "Cliente", "Teléfono", "Correo", "Vehículo",
                "Plantel", "Instructor", "Precio", "Estado", "Motivo cancelación", "Observaciones", "Creado por"));

            foreach (var c in citas)
            {
                sb.AppendLine(string.Join(Separador,
                    c.Id.ToString(cultura),
                    c.Fecha.ToString("yyyy-MM-dd", cultura),
                    c.HoraInicio.ToString(@"hh\:mm", cultura),
                    c.HoraFin.ToString(@"hh\:mm", cultura),
                    Escapar(c.ClienteNombre),
                    Escapar(c.ClienteTelefono),
                    Escapar(c.ClienteCorreo),
                    Escapar(c.TipoVehiculo),
                    Escapar(c.Plantel),
                    Escapar(c.InstructorNombre),
                    c.Precio.ToString("0.##", cultura),
                    Escapar(c.Estado),
                    Escapar(c.MotivoCancelacion),
                    Escapar(c.Observaciones),
                    Escapar(c.CreadoPor)));
            }
            return sb.ToString();
        }

        /// <summary>Escapa comillas/separadores y neutraliza fórmulas (inyección CSV en Excel).</summary>
        public static string Escapar(string? valor)
        {
            valor ??= string.Empty;
            if (valor.Length > 0 && "=+-@\t\r".Contains(valor[0])) valor = "'" + valor;
            var requiereComillas = valor.IndexOfAny(new[] { Separador, '"', '\n', '\r' }) >= 0;
            valor = valor.Replace("\"", "\"\"");
            return requiereComillas ? $"\"{valor}\"" : valor;
        }
    }
}
