using SQLite;

namespace GestionEscuelaEspana.Models
{
    /// <summary>Alumno de la escuela. Se crea automáticamente al agendar su primera cita.</summary>
    public class Estudiante : IEntidad
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string NombreCompleto { get; set; } = string.Empty;
        public string Identificacion { get; set; } = string.Empty;

        [Indexed]
        public string Telefono { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Notas { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
        public DateTime FechaRegistro { get; set; }

        public Estudiante Clonar() => (Estudiante)MemberwiseClone();
    }
}
