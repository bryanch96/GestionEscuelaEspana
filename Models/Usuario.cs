using SQLite;

namespace GestionEscuelaEspana.Models
{
    /// <summary>Cuenta de acceso a la aplicación.</summary>
    public class Usuario : IEntidad
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Unique, Collation("NOCASE")]
        public string NombreUsuario { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;

        [Indexed, Collation("NOCASE")]
        public string Correo { get; set; } = string.Empty;
        public string Rol { get; set; } = Roles.Recepcionista;

        /// <summary>Hash PBKDF2 en formato "iteraciones.salt.hash" (Base64). Nunca se guarda la contraseña.</summary>
        public string PasswordHash { get; set; } = string.Empty;

        public bool Activo { get; set; } = true;

        /// <summary>Se activa al 5.º intento fallido: exige recuperar la contraseña por correo.</summary>
        public bool BloqueadoPorSeguridad { get; set; }

        /// <summary>Instructor asociado (solo para usuarios con rol Instructor).</summary>
        public int InstructorId { get; set; }

        public DateTime FechaCreacion { get; set; }
        public DateTime? UltimoAcceso { get; set; }
    }

    /// <summary>Código temporal de recuperación de contraseña (se guarda solo su hash).</summary>
    public class CodigoRecuperacion : IEntidad
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int UsuarioId { get; set; }
        public string CodigoHash { get; set; } = string.Empty;
        public DateTime ExpiraUtc { get; set; }
        public DateTime CreadoUtc { get; set; }
        public int IntentosFallidos { get; set; }
        public bool Usado { get; set; }
    }

    /// <summary>
    /// Estado global de seguridad del dispositivo (fila única). Persiste el contador de intentos
    /// fallidos para que reiniciar la aplicación no permita evadir el bloqueo.
    /// </summary>
    public class EstadoSeguridad
    {
        public const int IdUnico = 1;

        [PrimaryKey]
        public int Id { get; set; } = IdUnico;
        public int IntentosFallidosConsecutivos { get; set; }
        public DateTime? BloqueadoHastaUtc { get; set; }
        public DateTime? UltimoIntentoUtc { get; set; }
    }
}
