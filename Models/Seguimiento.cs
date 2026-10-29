using SQLite;

namespace GestionEscuelaEspana.Models
{
    /// <summary>Nota pedagógica que un instructor registra sobre el desempeño de un estudiante.</summary>
    public class NotaSeguimiento : IEntidad
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int EstudianteId { get; set; }
        public int AutorUsuarioId { get; set; }
        public string AutorNombre { get; set; } = string.Empty;

        public string Titulo { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;

        /// <summary>Calificación opcional de 1 a 5 del desempeño en la sesión (0 = sin calificar).</summary>
        public int Calificacion { get; set; }

        public DateTime Fecha { get; set; }
        public bool Enviada { get; set; }
        public DateTime? FechaEnvio { get; set; }

        [Ignore]
        public List<AdjuntoSeguimiento> Adjuntos { get; set; } = new();
    }

    /// <summary>Fotografía o video asociado a una nota de seguimiento. El archivo vive en el almacenamiento local.</summary>
    public class AdjuntoSeguimiento : IEntidad
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int NotaId { get; set; }
        public string NombreArchivo { get; set; } = string.Empty;
        public string RutaLocal { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long TamanoBytes { get; set; }

        [Ignore] public bool EsImagen => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        [Ignore] public bool EsVideo => ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    }
}
