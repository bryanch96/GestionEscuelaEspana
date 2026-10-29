namespace GestionEscuelaEspana.Services.Email
{
    public sealed record AdjuntoCorreo(string NombreArchivo, string RutaLocal, string ContentType);

    /// <summary>Correo a enviar. El cuerpo HTML se genera con <see cref="PlantillasCorreo"/>.</summary>
    public sealed class MensajeCorreo
    {
        public required string Para { get; init; }
        public string NombrePara { get; init; } = string.Empty;
        public required string Asunto { get; init; }
        public required string CuerpoHtml { get; init; }
        public string CuerpoTexto { get; init; } = string.Empty;
        public IReadOnlyList<AdjuntoCorreo> Adjuntos { get; init; } = Array.Empty<AdjuntoCorreo>();

        /// <summary>Clasificación para la bitácora (Cita, Recuperación, Seguimiento…).</summary>
        public string Categoria { get; init; } = "General";
    }

    public sealed record ResultadoEnvio(bool Exito, bool Simulado, string Mensaje);

    /// <summary>Estrategia concreta de entrega de correos (SMTP real o simulado).</summary>
    public interface IEmailSender
    {
        Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default);
    }

    /// <summary>Fachada usada por la aplicación: valida, elige el canal, envía y registra en bitácora.</summary>
    public interface IEmailService
    {
        Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default);

        /// <summary>Tamaño máximo total de adjuntos admitido por la mayoría de proveedores SMTP.</summary>
        long TamanoMaximoAdjuntosBytes { get; }
    }
}
