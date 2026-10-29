using GestionEscuelaEspana.Services.Plataforma;
using Microsoft.Extensions.Logging;

namespace GestionEscuelaEspana.Infrastructure.Maui
{
    // Implementaciones de las abstracciones de plataforma usando las APIs de .NET MAUI Essentials.

    public sealed class MauiAppPaths : IAppPaths
    {
        // Se conserva la ubicación original de la base de datos para no perder los datos existentes.
        public string RutaBaseDatos { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "escuela_espana.db3");

        public string CarpetaDatos => FileSystem.Current.AppDataDirectory;
    }

    public sealed class MauiAppLauncher : IAppLauncher
    {
        private readonly ILogger<MauiAppLauncher> _logger;

        public MauiAppLauncher(ILogger<MauiAppLauncher> logger) => _logger = logger;

        public async Task<bool> AbrirAsync(string uri)
        {
            try
            {
                return await MainThread.InvokeOnMainThreadAsync(() => Launcher.Default.OpenAsync(new Uri(uri)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo abrir {Uri}", uri);
                return false;
            }
        }
    }

    /// <summary>Usa Keychain (iOS/Mac), Keystore (Android) o DPAPI/credenciales del paquete (Windows).</summary>
    public sealed class MauiSecretStore : ISecretStore
    {
        public Task<string?> ObtenerAsync(string clave) => SecureStorage.Default.GetAsync(clave);
        public Task GuardarAsync(string clave, string valor) => SecureStorage.Default.SetAsync(clave, valor);
        public void Eliminar(string clave) => SecureStorage.Default.Remove(clave);
    }

    public sealed class MauiShareService : IShareService
    {
        public Task CompartirArchivoAsync(string ruta, string titulo) =>
            MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = titulo,
                File = new ShareFile(ruta)
            }));
    }

    public sealed class MauiMediaService : IMediaService
    {
        private static readonly FilePickerFileType TiposMultimedia = new(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = new[] { "image/*", "video/*" },
            [DevicePlatform.iOS] = new[] { "public.image", "public.movie" },
            [DevicePlatform.MacCatalyst] = new[] { "public.image", "public.movie" },
            [DevicePlatform.WinUI] = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".mp4", ".mov", ".avi", ".wmv", ".mkv" },
        });

        public bool CamaraDisponible => MediaPicker.Default.IsCaptureSupported;

        public async Task<IReadOnlyList<ArchivoSeleccionado>> SeleccionarArchivosAsync()
        {
            var resultados = await ConPermisosAsync(() => MainThread.InvokeOnMainThreadAsync(() => FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Seleccione fotografías o videos",
                FileTypes = TiposMultimedia
            })));

            var lista = new List<ArchivoSeleccionado>();
            foreach (var archivo in resultados ?? Enumerable.Empty<FileResult?>())
            {
                if (archivo != null) lista.Add(await CopiarATemporalAsync(archivo));
            }
            return lista;
        }

        public async Task<ArchivoSeleccionado?> TomarFotoAsync()
        {
            await SolicitarPermisoCamaraAsync();
            var foto = await ConPermisosAsync(() => MainThread.InvokeOnMainThreadAsync(() => MediaPicker.Default.CapturePhotoAsync()));
            return foto == null ? null : await CopiarATemporalAsync(foto);
        }

        public async Task<ArchivoSeleccionado?> GrabarVideoAsync()
        {
            await SolicitarPermisoCamaraAsync();
            var video = await ConPermisosAsync(() => MainThread.InvokeOnMainThreadAsync(() => MediaPicker.Default.CaptureVideoAsync()));
            return video == null ? null : await CopiarATemporalAsync(video);
        }

        private static async Task SolicitarPermisoCamaraAsync()
        {
            var estado = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.Camera>);
            if (estado != PermissionStatus.Granted)
                throw new UnauthorizedAccessException("No se otorgó permiso para usar la cámara.");
        }

        /// <summary>Traduce la excepción de permisos de MAUI a una neutral para que la UI no dependa de la plataforma.</summary>
        private static async Task<T> ConPermisosAsync<T>(Func<Task<T>> accion)
        {
            try
            {
                return await accion();
            }
            catch (PermissionException ex)
            {
                throw new UnauthorizedAccessException(ex.Message, ex);
            }
        }

        /// <summary>Copia el archivo a la caché de la app: en móviles el origen puede ser un stream temporal.</summary>
        private static async Task<ArchivoSeleccionado> CopiarATemporalAsync(FileResult archivo)
        {
            var destino = Path.Combine(FileSystem.Current.CacheDirectory, $"{Guid.NewGuid():N}{Path.GetExtension(archivo.FileName)}");
            await using (var origen = await archivo.OpenReadAsync())
            await using (var salida = File.Create(destino))
                await origen.CopyToAsync(salida);

            var contentType = string.IsNullOrWhiteSpace(archivo.ContentType) ? InferirContentType(archivo.FileName) : archivo.ContentType;
            return new ArchivoSeleccionado(archivo.FileName, destino, contentType, new FileInfo(destino).Length);
        }

        private static string InferirContentType(string nombre) => Path.GetExtension(nombre).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".wmv" => "video/x-ms-wmv",
            ".mkv" => "video/x-matroska",
            _ => "application/octet-stream"
        };
    }
}
