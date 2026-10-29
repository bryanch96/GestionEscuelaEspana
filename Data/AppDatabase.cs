using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Plataforma;
using Microsoft.Extensions.Logging;
using SQLite;

namespace GestionEscuelaEspana.Data
{
    /// <summary>
    /// Punto único de acceso a la base de datos SQLite local.
    /// La conexión se abre de forma diferida y, en la primera apertura, crea o completa el esquema
    /// automáticamente (sqlite-net agrega las columnas nuevas a tablas existentes), por lo que no
    /// se requiere ningún sistema externo de migraciones.
    /// </summary>
    public sealed class AppDatabase
    {
        /// <summary>Versión de los ajustes de datos de una sola vez (se guarda en PRAGMA user_version).</summary>
        private const int VersionDatos = 1;

        private static readonly Type[] Tablas =
        {
            typeof(Usuario), typeof(CodigoRecuperacion), typeof(EstadoSeguridad),
            typeof(ConfiguracionApp), typeof(RegistroCorreo),
            typeof(Instructor), typeof(Estudiante), typeof(Cita),
            typeof(NotaSeguimiento), typeof(AdjuntoSeguimiento)
        };

        private readonly IAppPaths _paths;
        private readonly ILogger<AppDatabase> _logger;
        private readonly SemaphoreSlim _candado = new(1, 1);
        private SQLiteAsyncConnection? _conexion;

        public AppDatabase(IAppPaths paths, ILogger<AppDatabase> logger)
        {
            _paths = paths;
            _logger = logger;
        }

        /// <summary>Devuelve la conexión lista para usar, inicializando el esquema la primera vez.</summary>
        public async Task<SQLiteAsyncConnection> ObtenerConexionAsync()
        {
            if (_conexion != null) return _conexion;

            await _candado.WaitAsync();
            try
            {
                if (_conexion == null)
                {
                    var directorio = Path.GetDirectoryName(_paths.RutaBaseDatos);
                    if (!string.IsNullOrEmpty(directorio)) Directory.CreateDirectory(directorio);

                    var conexion = new SQLiteAsyncConnection(_paths.RutaBaseDatos,
                        SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

                    await InicializarEsquemaAsync(conexion);
                    _conexion = conexion;
                    _logger.LogInformation("Base de datos lista en {Ruta}", _paths.RutaBaseDatos);
                }
                return _conexion;
            }
            finally
            {
                _candado.Release();
            }
        }

        private async Task InicializarEsquemaAsync(SQLiteAsyncConnection db)
        {
            // WAL mejora la concurrencia lectura/escritura y la fluidez de la UI.
            await db.ExecuteScalarAsync<string>("PRAGMA journal_mode=WAL;");

            await db.CreateTablesAsync(CreateFlags.None, Tablas);

            await CompletarValoresNulosAsync(db);
            await AsegurarFilasUnicasAsync(db);

            var version = await db.ExecuteScalarAsync<int>("PRAGMA user_version;");
            if (version < 1) await MigrarEstudiantesDesdeCitasAsync(db);
            if (version < VersionDatos) await db.ExecuteAsync($"PRAGMA user_version = {VersionDatos};");
        }

        /// <summary>
        /// Las columnas agregadas a tablas que ya tenían datos quedan en NULL. Se rellenan con valores por
        /// defecto para que los modelos (con propiedades no anulables) se lean siempre de forma consistente.
        /// </summary>
        private static async Task CompletarValoresNulosAsync(SQLiteAsyncConnection db)
        {
            var ahora = DateTime.Now.Ticks;
            var sentencias = new[]
            {
                "UPDATE Cita SET ClienteCorreo = '' WHERE ClienteCorreo IS NULL",
                "UPDATE Cita SET Observaciones = '' WHERE Observaciones IS NULL",
                "UPDATE Cita SET MotivoCancelacion = '' WHERE MotivoCancelacion IS NULL",
                "UPDATE Cita SET CreadoPor = '' WHERE CreadoPor IS NULL",
                "UPDATE Cita SET EstudianteId = 0 WHERE EstudianteId IS NULL",
                "UPDATE Cita SET ConfirmacionEnviada = 0 WHERE ConfirmacionEnviada IS NULL",
                "UPDATE Cita SET FechaCreacion = Fecha WHERE FechaCreacion IS NULL",
                "UPDATE Cita SET FechaModificacion = Fecha WHERE FechaModificacion IS NULL",
                "UPDATE Instructor SET Identificacion = '' WHERE Identificacion IS NULL",
                "UPDATE Instructor SET Correo = '' WHERE Correo IS NULL",
                "UPDATE Instructor SET ContactoEmergenciaNombre = '' WHERE ContactoEmergenciaNombre IS NULL",
                "UPDATE Instructor SET ContactoEmergenciaTelefono = '' WHERE ContactoEmergenciaTelefono IS NULL",
                "UPDATE Instructor SET Notas = '' WHERE Notas IS NULL",
                "UPDATE Instructor SET Estado = 'Activo' WHERE Estado IS NULL",
                "UPDATE Instructor SET DiasLaborales = '1,2,3,4,5,6' WHERE DiasLaborales IS NULL",
                $"UPDATE Instructor SET HoraEntrada = {TimeSpan.FromHours(7).Ticks} WHERE HoraEntrada IS NULL",
                $"UPDATE Instructor SET HoraSalida = {TimeSpan.FromHours(17).Ticks} WHERE HoraSalida IS NULL",
                "UPDATE Instructor SET MaxCitasPorDia = 8 WHERE MaxCitasPorDia IS NULL",
                $"UPDATE Instructor SET FechaIngreso = {ahora} WHERE FechaIngreso IS NULL",
                $"UPDATE Instructor SET FechaModificacion = {ahora} WHERE FechaModificacion IS NULL",
            };

            foreach (var sql in sentencias)
                await db.ExecuteAsync(sql);
        }

        private static async Task AsegurarFilasUnicasAsync(SQLiteAsyncConnection db)
        {
            if (await db.FindAsync<EstadoSeguridad>(EstadoSeguridad.IdUnico) == null)
                await db.InsertAsync(new EstadoSeguridad());

            if (await db.FindAsync<ConfiguracionApp>(ConfiguracionApp.IdUnico) == null)
                await db.InsertAsync(new ConfiguracionApp());
        }

        /// <summary>
        /// Ajuste de una sola vez: las citas creadas antes de existir el catálogo de estudiantes
        /// generan su ficha de estudiante (agrupando por teléfono, o por nombre si no hay teléfono).
        /// </summary>
        private static async Task MigrarEstudiantesDesdeCitasAsync(SQLiteAsyncConnection db)
        {
            var citas = await db.Table<Cita>().Where(c => c.EstudianteId == 0).ToListAsync();
            if (citas.Count == 0) return;

            await db.RunInTransactionAsync(tx =>
            {
                var grupos = citas.GroupBy(c => string.IsNullOrWhiteSpace(c.ClienteTelefono)
                    ? "n:" + c.ClienteNombre.Trim().ToLowerInvariant()
                    : "t:" + new string(c.ClienteTelefono.Where(char.IsDigit).ToArray()));

                foreach (var grupo in grupos)
                {
                    var masReciente = grupo.OrderByDescending(c => c.Fecha).First();
                    var estudiante = new Estudiante
                    {
                        NombreCompleto = masReciente.ClienteNombre.Trim(),
                        Telefono = masReciente.ClienteTelefono.Trim(),
                        Correo = masReciente.ClienteCorreo.Trim(),
                        FechaRegistro = grupo.Min(c => c.Fecha),
                    };
                    tx.Insert(estudiante);

                    foreach (var cita in grupo)
                    {
                        cita.EstudianteId = estudiante.Id;
                        tx.Update(cita);
                    }
                }
            });
        }
    }
}
