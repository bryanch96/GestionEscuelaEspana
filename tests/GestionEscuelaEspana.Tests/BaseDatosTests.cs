using GestionEscuelaEspana.Data;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Email;
using GestionEscuelaEspana.Services.Seguimiento;
using GestionEscuelaEspana.Services.Plataforma;
using SQLite;
using Xunit;

namespace GestionEscuelaEspana.Tests
{
    public class BaseDatosTests
    {
        [Fact]
        public async Task Actualiza_una_base_de_la_version_anterior_sin_perder_datos()
        {
            await using var e = new Entorno();

            // Simula el archivo creado por la versión original de la app (solo las columnas que existían).
            using (var vieja = new SQLiteConnection(e.Rutas.RutaBaseDatos))
            {
                vieja.Execute("CREATE TABLE Instructor (Id integer primary key autoincrement not null, Nombre varchar, Telefono varchar, Licencia varchar)");
                vieja.Execute("CREATE TABLE Cita (Id integer primary key autoincrement not null, ClienteNombre varchar, ClienteTelefono varchar, TipoVehiculo varchar, Plantel varchar, InstructorId integer, Fecha bigint, HoraInicio bigint, HoraFin bigint, Precio float, Estado varchar)");
                vieja.Execute("INSERT INTO Instructor (Nombre, Telefono, Licencia) VALUES ('Luis', '8888-0000', 'B1, A3')");
                foreach (var (cliente, tel) in new[] { ("Ana", "8111-1111"), ("Ana R.", "8111 1111"), ("Beto", "8222-2222") })
                    vieja.Execute("INSERT INTO Cita (ClienteNombre, ClienteTelefono, TipoVehiculo, Plantel, InstructorId, Fecha, HoraInicio, HoraFin, Precio, Estado) VALUES (?, ?, 'Automóvil (B-1)', 'Heredia', 1, ?, ?, ?, 15000, 'Pendiente')",
                        cliente, tel, new DateTime(2026, 5, 4).Ticks, TimeSpan.FromHours(8).Ticks, TimeSpan.FromHours(9).Ticks);
            }

            var db = await e.Get<AppDatabase>().ObtenerConexionAsync();

            var instructor = await db.Table<Instructor>().FirstAsync();
            Assert.Equal(EstadosInstructor.Activo, instructor.Estado);
            Assert.Equal(new[] { "B1", "A3" }, instructor.Categorias);
            Assert.Equal(TimeSpan.FromHours(7), instructor.HoraEntrada);
            Assert.Equal(string.Empty, instructor.Correo);

            var citas = await db.Table<Cita>().ToListAsync();
            Assert.Equal(3, citas.Count);
            Assert.All(citas, c => Assert.True(c.EstudianteId > 0));
            Assert.All(citas, c => Assert.Equal(string.Empty, c.ClienteCorreo));

            // Los dos registros con el mismo teléfono (con distinto formato) quedan como un único estudiante.
            Assert.Equal(2, await db.Table<Estudiante>().CountAsync());
            Assert.Equal(1, await db.ExecuteScalarAsync<int>("PRAGMA user_version;"));
        }

        [Fact]
        public async Task Correo_simulado_guarda_eml_y_registra_en_bitacora_cuando_no_hay_smtp()
        {
            await using var e = new Entorno(correoReal: true);
            var r = await e.Get<IEmailService>().EnviarAsync(PlantillasCorreo.Prueba("ana@correo.com", "Escuela <Test>"));

            Assert.True(r.Exito);
            Assert.True(r.Simulado);
            var eml = Assert.Single(Directory.GetFiles(Path.Combine(e.Rutas.CarpetaDatos, "BandejaSalida"), "*.eml"));
            Assert.Contains("Escuela &lt;Test&gt;", await File.ReadAllTextAsync(eml)); // HTML codificado.

            var db = await e.Get<AppDatabase>().ObtenerConexionAsync();
            Assert.Equal(1, await db.Table<RegistroCorreo>().CountAsync());

            var invalido = await e.Get<IEmailService>().EnviarAsync(PlantillasCorreo.Prueba("no-es-correo", "x"));
            Assert.False(invalido.Exito);
        }

        [Fact]
        public async Task Seguimiento_guarda_adjuntos_y_envia_al_correo_del_estudiante()
        {
            await using var e = new Entorno();
            var db = await e.Get<AppDatabase>().ObtenerConexionAsync();
            var estudiante = new Estudiante { NombreCompleto = "Sofía", Correo = "sofia@correo.com", Telefono = "8000-1111" };
            await db.InsertAsync(estudiante);

            var foto = Path.Combine(e.Rutas.CarpetaDatos, "foto.jpg");
            await File.WriteAllBytesAsync(foto, new byte[] { 1, 2, 3 });
            var seguimiento = e.Get<ISeguimientoService>();

            var nota = new NotaSeguimiento { EstudianteId = estudiante.Id, Titulo = "Rampa", Texto = "Buen control del embrague.", Calificacion = 4 };
            var guardada = await seguimiento.GuardarNotaAsync(nota, new[] { new ArchivoSeleccionado("foto.jpg", foto, "image/jpeg", 3) });
            Assert.True(guardada.Exito, guardada.Mensaje);

            var adjunto = Assert.Single((await seguimiento.ListarAsync(estudiante.Id)).Single().Adjuntos);
            Assert.True(File.Exists(adjunto.RutaLocal));

            var envio = await seguimiento.EnviarAsync(nota.Id);
            Assert.True(envio.Exito);
            var correo = Assert.Single(e.Correo.Enviados);
            Assert.Equal("sofia@correo.com", correo.Para);
            Assert.Single(correo.Adjuntos);
            Assert.True((await seguimiento.ListarAsync(estudiante.Id)).Single().Enviada);

            // Un archivo que no es foto ni video se rechaza.
            var pdf = await seguimiento.GuardarNotaAsync(new NotaSeguimiento { EstudianteId = estudiante.Id, Texto = "x" },
                new[] { new ArchivoSeleccionado("a.pdf", foto, "application/pdf", 3) });
            Assert.False(pdf.Exito);
        }
    }
}
