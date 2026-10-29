using SQLite;

namespace GestionEscuelaEspana.Models
{
    /// <summary>Configuración general de la aplicación (fila única).</summary>
    public class ConfiguracionApp
    {
        public const int IdUnico = 1;

        [PrimaryKey]
        public int Id { get; set; } = IdUnico;

        // --- Datos de la escuela ---
        public string NombreEscuela { get; set; } = "Escuela de Manejo España";
        public string TelefonoEscuela { get; set; } = string.Empty;

        /// <summary>Código de país usado para normalizar teléfonos locales al abrir WhatsApp (506 = Costa Rica).</summary>
        public string CodigoPais { get; set; } = "506";

        // --- SMTP (la contraseña se guarda aparte, en el almacenamiento seguro del sistema) ---
        public bool SmtpHabilitado { get; set; }
        public string SmtpServidor { get; set; } = string.Empty;
        public int SmtpPuerto { get; set; } = 587;
        public bool SmtpUsarSsl { get; set; } = true;
        public string SmtpUsuario { get; set; } = string.Empty;
        public string RemitenteNombre { get; set; } = "Escuela de Manejo España";
        public string RemitenteCorreo { get; set; } = string.Empty;

        public ConfiguracionApp Clonar() => (ConfiguracionApp)MemberwiseClone();
    }

    /// <summary>Bitácora de correos enviados (o simulados) por la aplicación.</summary>
    public class RegistroCorreo : IEntidad
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string Destinatario { get; set; } = string.Empty;
        public string Asunto { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public bool Exitoso { get; set; }
        public bool Simulado { get; set; }
        public string Detalle { get; set; } = string.Empty;
    }
}
