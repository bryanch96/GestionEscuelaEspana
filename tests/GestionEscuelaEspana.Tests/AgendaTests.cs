using GestionEscuelaEspana.Data.Repositorios;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Citas;
using GestionEscuelaEspana.Services.Instructores;
using Xunit;

namespace GestionEscuelaEspana.Tests
{
    public class ReglasAgendaTests
    {
        // Jueves 29/10/2026, 9:00 a. m.
        private static readonly DateTime Ahora = new(2026, 10, 29, 9, 0, 0);

        private static Instructor Instructor() => new()
        {
            Id = 1, Nombre = "Carlos", Telefono = "88887777", Licencia = "B1",
            DiasLaborales = "1,2,3,4,5", HoraEntrada = TimeSpan.FromHours(7), HoraSalida = TimeSpan.FromHours(17), MaxCitasPorDia = 2
        };

        private static Cita Cita(int hora, int duracion = 1, string vehiculo = "Automóvil (B-1)", int id = 0) => new()
        {
            Id = id, ClienteNombre = "María", ClienteTelefono = "8888-1111", TipoVehiculo = vehiculo, Plantel = "Heredia",
            InstructorId = 1, Fecha = Ahora.Date.AddDays(1), HoraInicio = TimeSpan.FromHours(hora),
            HoraFin = TimeSpan.FromHours(hora + duracion), Precio = 15000
        };

        [Fact]
        public void Cita_valida_sin_advertencias()
        {
            var v = ReglasAgenda.Validar(Cita(10), Instructor(), Array.Empty<Cita>(), Ahora);
            Assert.True(v.EsValida);
            Assert.Empty(v.Advertencias);
        }

        [Fact]
        public void Traslape_con_otra_cita_del_instructor_es_error()
        {
            var existente = Cita(10, 2, id: 99);
            var v = ReglasAgenda.Validar(Cita(11), Instructor(), new[] { existente }, Ahora);
            Assert.Contains(v.Errores, e => e.Contains("ya tiene una cita"));
        }

        [Fact]
        public void Una_cita_cancelada_no_ocupa_la_agenda()
        {
            var cancelada = Cita(10, 2, id: 99);
            cancelada.Estado = EstadosCita.Cancelada;
            Assert.True(ReglasAgenda.Validar(Cita(11), Instructor(), new[] { cancelada }, Ahora).EsValida);
        }

        [Fact]
        public void Advierte_fuera_de_jornada_capacidad_y_licencia()
        {
            var ocupadas = new[] { Cita(7, id: 50), Cita(8, id: 51) };
            var v = ReglasAgenda.Validar(Cita(16, 2, "Motocicleta (A-2)"), Instructor(), ocupadas, Ahora);
            Assert.True(v.EsValida);
            Assert.Contains(v.Advertencias, a => a.Contains("fuera de la jornada"));
            Assert.Contains(v.Advertencias, a => a.Contains("capacidad"));
            Assert.Contains(v.Advertencias, a => a.Contains("licencia A2"));
        }

        [Fact]
        public void Advierte_si_el_instructor_no_labora_ese_dia()
        {
            var cita = Cita(10);
            cita.Fecha = new DateTime(2026, 11, 1); // Domingo
            Assert.Contains(ReglasAgenda.Validar(cita, Instructor(), Array.Empty<Cita>(), Ahora).Advertencias, a => a.Contains("no labora"));
        }

        [Fact]
        public void Errores_de_datos_basicos()
        {
            var cita = Cita(10, vehiculo: TiposVehiculo.OtroVehiculo);
            cita.ClienteCorreo = "no-es-correo";
            cita.HoraFin = cita.HoraInicio;
            cita.Fecha = Ahora.Date.AddDays(-1);
            var v = ReglasAgenda.Validar(cita, null, Array.Empty<Cita>(), Ahora);

            Assert.Contains(v.Errores, e => e.Contains("correo"));
            Assert.Contains(v.Errores, e => e.Contains("tipo de vehículo"));
            Assert.Contains(v.Errores, e => e.Contains("finalización"));
            Assert.Contains(v.Errores, e => e.Contains("ya pasó"));
            Assert.Contains(v.Errores, e => e.Contains("instructor"));
        }

        [Fact]
        public void Otro_vehiculo_con_texto_libre_es_valido_y_no_exige_licencia()
        {
            var v = ReglasAgenda.Validar(Cita(10, vehiculo: "Pick-up 4x4"), Instructor(), Array.Empty<Cita>(), Ahora);
            Assert.True(v.EsValida);
            Assert.Empty(v.Advertencias);
        }
    }

    public class CitaServiceTests
    {
        private static async Task<Instructor> CrearInstructorAsync(Entorno e)
        {
            var r = await e.Get<IInstructorService>().GuardarAsync(new Instructor
            {
                Nombre = "Carlos Mora", Telefono = "8888-7777", Licencia = "B1,A2", DiasLaborales = "1,2,3,4,5,6"
            });
            Assert.True(r.Exito, r.Mensaje);
            return r.Valor!;
        }

        [Fact]
        public async Task Agendar_crea_estudiante_y_envia_copia_al_correo_del_cliente()
        {
            await using var e = new Entorno();
            var instructor = await CrearInstructorAsync(e);
            var citas = e.Get<ICitaService>();

            var cita = new Cita
            {
                ClienteNombre = "Laura Jiménez", ClienteTelefono = "8777-6655", ClienteCorreo = "laura@correo.com",
                TipoVehiculo = "Pick-up 4x4", Plantel = "Cartago", InstructorId = instructor.Id,
                Fecha = e.Reloj.Now.Date.AddDays(1), HoraInicio = TimeSpan.FromHours(10), HoraFin = TimeSpan.FromHours(11), Precio = 20000
            };
            var r = await citas.GuardarAsync(cita, enviarCorreo: true, aceptarAdvertencias: false);

            Assert.True(r.Exito, string.Join(" ", r.Validacion.Errores.Concat(r.Validacion.Advertencias)));
            Assert.True(r.Cita!.EstudianteId > 0);
            Assert.True(r.Cita.ConfirmacionEnviada);
            var correo = Assert.Single(e.Correo.Enviados);
            Assert.Equal("laura@correo.com", correo.Para);
            Assert.Contains("Pick-up 4x4", correo.CuerpoHtml);

            // Editar solo las observaciones no reenvía correo; cambiar la hora sí (como actualización).
            var guardada = await citas.ObtenerAsync(r.Cita.Id);
            guardada!.Observaciones = "Traer lentes";
            await citas.GuardarAsync(guardada, true, true);
            Assert.Single(e.Correo.Enviados);

            guardada.HoraInicio = TimeSpan.FromHours(12);
            guardada.HoraFin = TimeSpan.FromHours(13);
            await citas.GuardarAsync(guardada, true, true);
            Assert.Equal(2, e.Correo.Enviados.Count);
            Assert.Contains("actualizada", e.Correo.Enviados[1].Asunto);
        }

        [Fact]
        public async Task Advertencias_requieren_confirmacion_y_filtros_del_historial()
        {
            await using var e = new Entorno();
            var instructor = await CrearInstructorAsync(e);
            var citas = e.Get<ICitaService>();

            // A las 6 p. m. está fuera de la jornada (7–17 h): requiere confirmación.
            var cita = new Cita
            {
                ClienteNombre = "Pedro", ClienteTelefono = "8111-2222", TipoVehiculo = "Automóvil (B-1)", Plantel = "Heredia",
                InstructorId = instructor.Id, Fecha = e.Reloj.Now.Date.AddDays(2), HoraInicio = TimeSpan.FromHours(18), HoraFin = TimeSpan.FromHours(19)
            };
            var r = await citas.GuardarAsync(cita, false, aceptarAdvertencias: false);
            Assert.False(r.Exito);
            Assert.True(r.RequiereConfirmacion);
            Assert.True((await citas.GuardarAsync(cita, false, aceptarAdvertencias: true)).Exito);

            Assert.Single(await citas.BuscarAsync(new CitaFiltro { Texto = "pedro" }));
            Assert.Single(await citas.BuscarAsync(new CitaFiltro { Texto = "PÉDRO" }));
            Assert.Single(await citas.BuscarAsync(new CitaFiltro { InstructorId = instructor.Id, Estados = new[] { EstadosCita.Pendiente } }));
            Assert.Empty(await citas.BuscarAsync(new CitaFiltro { Estados = new[] { EstadosCita.Completada } }));
            Assert.Empty(await citas.BuscarAsync(new CitaFiltro { Hasta = e.Reloj.Now.Date }));

            var todas = await citas.BuscarAsync(new CitaFiltro());
            Assert.Equal("Carlos Mora", todas[0].InstructorNombre);
        }

        [Fact]
        public async Task No_se_elimina_un_instructor_con_historial()
        {
            await using var e = new Entorno();
            var instructor = await CrearInstructorAsync(e);
            await e.Get<ICitaService>().GuardarAsync(new Cita
            {
                ClienteNombre = "Ana", ClienteTelefono = "8999-0000", TipoVehiculo = "Automóvil (B-1)", Plantel = "Heredia",
                InstructorId = instructor.Id, Fecha = e.Reloj.Now.Date.AddDays(1), HoraInicio = TimeSpan.FromHours(9), HoraFin = TimeSpan.FromHours(10)
            }, false, true);

            Assert.False((await e.Get<IInstructorService>().EliminarAsync(instructor.Id)).Exito);
        }
    }
}
