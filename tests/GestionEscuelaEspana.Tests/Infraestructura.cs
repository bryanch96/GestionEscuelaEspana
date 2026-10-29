using GestionEscuelaEspana.Data;
using GestionEscuelaEspana.Services;
using GestionEscuelaEspana.Services.Email;
using GestionEscuelaEspana.Services.Plataforma;
using Microsoft.Extensions.DependencyInjection;

namespace GestionEscuelaEspana.Tests
{
    public sealed class RelojFalso : IClock
    {
        public DateTime Now { get; set; } = new(2026, 10, 29, 9, 0, 0);
        public DateTime UtcNow => Now.ToUniversalTime();
        public void Avanzar(TimeSpan t) => Now = Now.Add(t);
    }

    public sealed class RutasTemporales : IAppPaths, IDisposable
    {
        public string CarpetaDatos { get; } = Path.Combine(Path.GetTempPath(), "escuela-tests", Guid.NewGuid().ToString("N"));
        public string RutaBaseDatos => Path.Combine(CarpetaDatos, "prueba.db3");

        public RutasTemporales() => Directory.CreateDirectory(CarpetaDatos);

        public void Dispose()
        {
            try { Directory.Delete(CarpetaDatos, recursive: true); } catch (IOException) { }
        }
    }

    public sealed class SecretosEnMemoria : ISecretStore
    {
        private readonly Dictionary<string, string> _valores = new();
        public Task<string?> ObtenerAsync(string clave) => Task.FromResult(_valores.TryGetValue(clave, out var v) ? v : null);
        public Task GuardarAsync(string clave, string valor) { _valores[clave] = valor; return Task.CompletedTask; }
        public void Eliminar(string clave) => _valores.Remove(clave);
    }

    public sealed class LanzadorFalso : IAppLauncher
    {
        public List<string> Abiertos { get; } = new();
        public Task<bool> AbrirAsync(string uri) { Abiertos.Add(uri); return Task.FromResult(true); }
    }

    /// <summary>Captura los correos en lugar de enviarlos.</summary>
    public sealed class CorreoFalso : IEmailService
    {
        public List<MensajeCorreo> Enviados { get; } = new();
        public long TamanoMaximoAdjuntosBytes => 20L * 1024 * 1024;

        public Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
        {
            Enviados.Add(mensaje);
            return Task.FromResult(new ResultadoEnvio(true, true, "capturado"));
        }
    }

    /// <summary>Contenedor de servicios real (mismo registro que la app) con dobles de prueba para la plataforma.</summary>
    public sealed class Entorno : IAsyncDisposable
    {
        public RelojFalso Reloj { get; } = new();
        public RutasTemporales Rutas { get; } = new();
        public CorreoFalso Correo { get; } = new();
        public LanzadorFalso Lanzador { get; } = new();
        public ServiceProvider Proveedor { get; }

        public Entorno(bool correoReal = false)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEscuelaServices();
            services.AddSingleton<IAppPaths>(Rutas);
            services.AddSingleton<ISecretStore, SecretosEnMemoria>();
            services.AddSingleton<IAppLauncher>(Lanzador);
            services.AddSingleton<IClock>(Reloj);
            if (!correoReal) services.AddSingleton<IEmailService>(Correo);
            Proveedor = services.BuildServiceProvider();
        }

        public T Get<T>() where T : notnull => Proveedor.GetRequiredService<T>();

        public async ValueTask DisposeAsync()
        {
            var db = await Get<AppDatabase>().ObtenerConexionAsync();
            await db.CloseAsync();
            await Proveedor.DisposeAsync();
            Rutas.Dispose();
        }
    }
}
