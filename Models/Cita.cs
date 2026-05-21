using System;
using SQLite;

namespace GestionEscuelaEspana.Models
{
    public class Cita
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string ClienteNombre { get; set; } = string.Empty;
        public string ClienteTelefono { get; set; } = string.Empty;

        
        public string TipoVehiculo { get; set; } = string.Empty; 
        public string Plantel { get; set; } = string.Empty;     
        public int InstructorId { get; set; }                   // Relación
        [Ignore]
        public string InstructorNombre { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public decimal Precio { get; set; }
        public string Estado { get; set; } = "Pendiente"; 
    }
}