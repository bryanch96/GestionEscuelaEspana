namespace GestionEscuelaEspana.Services.Plataforma
{
    // Abstracciones de las capacidades del sistema operativo. Las implementaciones MAUI viven en
    // Infrastructure/Maui; así la lógica de negocio no depende de la plataforma y se puede probar.

    /// <summary>Rutas de almacenamiento local de la aplicación.</summary>
    public interface IAppPaths
    {
        /// <summary>Ruta completa del archivo SQLite.</summary>
        string RutaBaseDatos { get; }

        /// <summary>Carpeta privada de datos de la app (adjuntos, bandeja de salida, exportaciones).</summary>
        string CarpetaDatos { get; }
    }

    /// <summary>Abre URIs externas (WhatsApp, mailto:, navegador).</summary>
    public interface IAppLauncher
    {
        Task<bool> AbrirAsync(string uri);
    }

    /// <summary>Almacenamiento cifrado del sistema (Keychain, Keystore, DPAPI) para secretos como la contraseña SMTP.</summary>
    public interface ISecretStore
    {
        Task<string?> ObtenerAsync(string clave);
        Task GuardarAsync(string clave, string valor);
        void Eliminar(string clave);
    }

    /// <summary>Archivo elegido por el usuario (foto/video) ya copiado a una ruta temporal legible.</summary>
    public sealed record ArchivoSeleccionado(string NombreOriginal, string RutaTemporal, string ContentType, long TamanoBytes);

    /// <summary>Acceso a galería y cámara del dispositivo.</summary>
    public interface IMediaService
    {
        Task<IReadOnlyList<ArchivoSeleccionado>> SeleccionarArchivosAsync();
        Task<ArchivoSeleccionado?> TomarFotoAsync();
        Task<ArchivoSeleccionado?> GrabarVideoAsync();

        /// <summary>Indica si el dispositivo tiene cámara. Los métodos lanzan <see cref="UnauthorizedAccessException"/> si se niega el permiso.</summary>
        bool CamaraDisponible { get; }
    }

    /// <summary>Hoja nativa de "compartir" para entregar archivos generados (CSV, etc.).</summary>
    public interface IShareService
    {
        Task CompartirArchivoAsync(string ruta, string titulo);
    }

    /// <summary>Reloj inyectable: permite probar bloqueos, alarmas y vencimientos sin depender de la hora real.</summary>
    public interface IClock
    {
        DateTime Now { get; }
        DateTime UtcNow { get; }
    }

    public sealed class RelojSistema : IClock
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
