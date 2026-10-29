using System.Globalization;

namespace GestionEscuelaEspana.Components.Shared
{
    /// <summary>Formatos de presentación comunes (moneda, fechas y horas en español de Costa Rica).</summary>
    public static class Formato
    {
        public static readonly CultureInfo Cultura = new("es-CR");

        public static string Colones(decimal monto) => "₡" + monto.ToString("N0", Cultura);

        public static string Hora(TimeSpan hora) => DateTime.Today.Add(hora).ToString("h:mm tt", Cultura);

        public static string Rango(TimeSpan inicio, TimeSpan fin) => $"{Hora(inicio)} – {Hora(fin)}";

        public static string Fecha(DateTime fecha) => fecha.ToString("dd/MM/yyyy", Cultura);

        public static string FechaLarga(DateTime fecha)
        {
            var texto = fecha.ToString("dddd, d 'de' MMMM 'de' yyyy", Cultura);
            return char.ToUpper(texto[0]) + texto[1..];
        }

        /// <summary>"Hoy", "Mañana", "Ayer" o el día de la semana con fecha corta.</summary>
        public static string DiaRelativo(DateTime fecha)
        {
            var dias = (fecha.Date - DateTime.Today).Days;
            return dias switch
            {
                0 => "Hoy",
                1 => "Mañana",
                -1 => "Ayer",
                _ => fecha.ToString("ddd d MMM", Cultura)
            };
        }

        public static string NombreDia(DayOfWeek dia) => Cultura.DateTimeFormat.GetAbbreviatedDayName(dia).TrimEnd('.');

        public static string Duracion(TimeSpan t) =>
            t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");

        public static string TamanoArchivo(long bytes) => bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
            _ => $"{bytes / (1024.0 * 1024):0.#} MB"
        };
    }
}
