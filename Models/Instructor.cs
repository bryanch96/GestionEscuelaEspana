using SQLite;

namespace GestionEscuelaEspana.Models
{
    /// <summary>Empleado que imparte las lecciones prácticas.</summary>
    public class Instructor : IEntidad
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // --- Datos personales y de contacto ---
        public string Nombre { get; set; } = string.Empty;
        public string Identificacion { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string ContactoEmergenciaNombre { get; set; } = string.Empty;
        public string ContactoEmergenciaTelefono { get; set; } = string.Empty;

        // --- Datos laborales ---
        /// <summary>Categorías de licencia autorizadas, separadas por coma (ej. "B1,A2").</summary>
        public string Licencia { get; set; } = string.Empty;
        public DateTime? VencimientoLicencia { get; set; }
        public DateTime FechaIngreso { get; set; }

        [Indexed]
        public string Estado { get; set; } = EstadosInstructor.Activo;

        // --- Disponibilidad ---
        /// <summary>Días laborales como lista de <see cref="DayOfWeek"/> separada por coma (ej. "1,2,3,4,5,6" = lunes a sábado).</summary>
        public string DiasLaborales { get; set; } = "1,2,3,4,5,6";
        public TimeSpan HoraEntrada { get; set; } = new(7, 0, 0);
        public TimeSpan HoraSalida { get; set; } = new(17, 0, 0);
        public int MaxCitasPorDia { get; set; } = 8;

        public string Notas { get; set; } = string.Empty;
        public DateTime FechaModificacion { get; set; }

        // --- Ayudantes (no persistidos) ---
        [Ignore]
        public bool EstaActivo => Estado == EstadosInstructor.Activo;

        [Ignore]
        public IReadOnlyList<string> Categorias
        {
            get => Licencia.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                           .Select(c => c.Replace("-", string.Empty).ToUpperInvariant()).Distinct().ToList();
            set => Licencia = string.Join(",", value ?? Array.Empty<string>());
        }

        [Ignore]
        public IReadOnlyList<DayOfWeek> Dias
        {
            get => DiasLaborales.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Where(d => int.TryParse(d, out var n) && n is >= 0 and <= 6)
                                .Select(d => (DayOfWeek)int.Parse(d)).Distinct().OrderBy(d => d).ToList();
            set => DiasLaborales = string.Join(",", (value ?? Array.Empty<DayOfWeek>()).Select(d => (int)d));
        }

        public bool TieneCategoria(string categoria) =>
            Categorias.Contains(categoria.Replace("-", string.Empty).ToUpperInvariant());

        public bool TrabajaEl(DayOfWeek dia) => Dias.Contains(dia);

        public Instructor Clonar() => (Instructor)MemberwiseClone();
    }
}
