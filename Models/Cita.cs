using SQLite;

namespace GestionEscuelaEspana.Models
{
    /// <summary>Cita práctica de manejo agendada para un estudiante.</summary>
    public class Cita : IEntidad
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // --- Datos del cliente (se guardan desnormalizados para conservar el historial tal como se agendó) ---
        [Indexed]
        public int EstudianteId { get; set; }
        public string ClienteNombre { get; set; } = string.Empty;
        public string ClienteTelefono { get; set; } = string.Empty;
        public string ClienteCorreo { get; set; } = string.Empty;

        // --- Datos operativos ---
        /// <summary>Tipo de vehículo del catálogo o texto libre cuando se eligió "Otro vehículo".</summary>
        public string TipoVehiculo { get; set; } = string.Empty;
        public string Plantel { get; set; } = string.Empty;

        [Indexed]
        public int InstructorId { get; set; }

        [Ignore]
        public string InstructorNombre { get; set; } = string.Empty;

        [Indexed]
        public DateTime Fecha { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public decimal Precio { get; set; }

        [Indexed]
        public string Estado { get; set; } = EstadosCita.Pendiente;

        public string Observaciones { get; set; } = string.Empty;
        public string MotivoCancelacion { get; set; } = string.Empty;

        // --- Auditoría ---
        public bool ConfirmacionEnviada { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        public string CreadoPor { get; set; } = string.Empty;

        // --- Propiedades calculadas (no persistidas) ---
        [Ignore] public DateTime InicioCompleto => Fecha.Date.Add(HoraInicio);
        [Ignore] public DateTime FinCompleto => Fecha.Date.Add(HoraFin);
        [Ignore] public double DuracionHoras => Math.Max(0, (HoraFin - HoraInicio).TotalHours);

        /// <summary>Indica si dos citas se traslapan en el tiempo (mismo día).</summary>
        public bool SeTraslapaCon(Cita otra) =>
            Fecha.Date == otra.Fecha.Date && HoraInicio < otra.HoraFin && otra.HoraInicio < HoraFin;

        /// <summary>Copia superficial, útil para editar sin alterar la instancia original.</summary>
        public Cita Clonar() => (Cita)MemberwiseClone();
    }
}
