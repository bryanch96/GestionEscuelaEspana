using SQLite;

namespace GestionEscuelaEspana.Models
{
    public class Instructor
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Licencia { get; set; } = string.Empty;
    }
}
