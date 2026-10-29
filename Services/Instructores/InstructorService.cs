using GestionEscuelaEspana.Data.Repositorios;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Common;
using GestionEscuelaEspana.Services.Plataforma;

namespace GestionEscuelaEspana.Services.Instructores
{
    /// <summary>Indicadores de desempeño de un instructor en un periodo.</summary>
    public sealed record RendimientoInstructor(
        int InstructorId,
        int Total,
        int Completadas,
        int Pendientes,
        int Canceladas,
        int NoAsistio,
        double HorasImpartidas,
        decimal Ingresos,
        Cita? ProximaCita,
        int CitasHoy)
    {
        /// <summary>% de citas cerradas que se completaron (excluye pendientes).</summary>
        public double TasaCumplimiento
        {
            get
            {
                var cerradas = Completadas + Canceladas + NoAsistio;
                return cerradas == 0 ? 0 : Math.Round(100.0 * Completadas / cerradas, 1);
            }
        }

        public static RendimientoInstructor Vacio(int id) => new(id, 0, 0, 0, 0, 0, 0, 0, null, 0);
    }

    public interface IInstructorService
    {
        Task<List<Instructor>> ListarAsync(bool soloActivos = false);
        Task<Instructor?> ObtenerAsync(int id);
        Task<Resultado<Instructor>> GuardarAsync(Instructor instructor);
        Task<Resultado> CambiarEstadoAsync(int id, string estado);

        /// <summary>Elimina solo si no tiene historial; si lo tiene, sugiere desactivarlo para conservar los datos.</summary>
        Task<Resultado> EliminarAsync(int id);

        Task<RendimientoInstructor> ObtenerRendimientoAsync(int id, DateTime desde, DateTime hasta);
        Task<Dictionary<int, RendimientoInstructor>> ObtenerRendimientosAsync(DateTime desde, DateTime hasta);
        Task<List<Cita>> ObtenerAgendaAsync(int id, DateTime desde, DateTime hasta);
    }

    /// <summary>Gestión de instructores con buenas prácticas de control de empleados.</summary>
    public sealed class InstructorService : IInstructorService
    {
        private readonly IRepositorio<Instructor> _instructores;
        private readonly ICitaRepositorio _citas;
        private readonly IClock _reloj;

        public InstructorService(IRepositorio<Instructor> instructores, ICitaRepositorio citas, IClock reloj)
        {
            _instructores = instructores;
            _citas = citas;
            _reloj = reloj;
        }

        public async Task<List<Instructor>> ListarAsync(bool soloActivos = false)
        {
            var lista = soloActivos
                ? await _instructores.ListarAsync(i => i.Estado == EstadosInstructor.Activo)
                : await _instructores.ListarAsync();
            return lista.OrderBy(i => i.Nombre).ToList();
        }

        public Task<Instructor?> ObtenerAsync(int id) => _instructores.ObtenerAsync(id);

        public async Task<Resultado<Instructor>> GuardarAsync(Instructor instructor)
        {
            instructor.Nombre = instructor.Nombre.Trim();
            instructor.Identificacion = instructor.Identificacion.Trim();
            instructor.Telefono = instructor.Telefono.Trim();
            instructor.Correo = instructor.Correo.Trim();

            var errores = Validar(instructor);
            if (errores.Count > 0) return Resultado<Instructor>.Fallo(string.Join(" ", errores));

            if (!string.IsNullOrEmpty(instructor.Identificacion))
            {
                var identificacion = instructor.Identificacion;
                var id = instructor.Id;
                if (await _instructores.ContarAsync(i => i.Identificacion == identificacion && i.Id != id) > 0)
                    return Resultado<Instructor>.Fallo("Ya existe un instructor con esa identificación.");
            }

            if (instructor.Id == 0 && instructor.FechaIngreso == default) instructor.FechaIngreso = _reloj.Now.Date;
            instructor.FechaModificacion = _reloj.Now;
            await _instructores.GuardarAsync(instructor);
            return Resultado<Instructor>.Ok(instructor, $"Instructor {instructor.Nombre} guardado.");
        }

        public static List<string> Validar(Instructor i)
        {
            var errores = new List<string>();
            if (string.IsNullOrWhiteSpace(i.Nombre)) errores.Add("El nombre es obligatorio.");
            if (!Validaciones.EsTelefonoValido(i.Telefono)) errores.Add("El teléfono no es válido.");
            if (!string.IsNullOrEmpty(i.Correo) && !Validaciones.EsCorreoValido(i.Correo)) errores.Add("El correo no es válido.");
            if (!string.IsNullOrEmpty(i.ContactoEmergenciaTelefono) && !Validaciones.EsTelefonoValido(i.ContactoEmergenciaTelefono))
                errores.Add("El teléfono de emergencia no es válido.");
            if (i.Categorias.Count == 0) errores.Add("Indique al menos una categoría de licencia.");
            if (i.Dias.Count == 0) errores.Add("Seleccione al menos un día laboral.");
            if (i.HoraSalida <= i.HoraEntrada) errores.Add("La hora de salida debe ser posterior a la de entrada.");
            if (i.MaxCitasPorDia is < 1 or > 24) errores.Add("La capacidad diaria debe estar entre 1 y 24 citas.");
            if (!EstadosInstructor.Todos.Contains(i.Estado)) errores.Add("El estado no es válido.");
            return errores;
        }

        public async Task<Resultado> CambiarEstadoAsync(int id, string estado)
        {
            if (!EstadosInstructor.Todos.Contains(estado)) return Resultado.Fallo("Estado no válido.");
            var instructor = await _instructores.ObtenerAsync(id);
            if (instructor == null) return Resultado.Fallo("El instructor no existe.");

            instructor.Estado = estado;
            instructor.FechaModificacion = _reloj.Now;
            await _instructores.GuardarAsync(instructor);

            var pendientes = await _citas.ContarAsync(c => c.InstructorId == id && c.Estado == EstadosCita.Pendiente);
            var aviso = estado != EstadosInstructor.Activo && pendientes > 0
                ? $" Atención: tiene {pendientes} cita(s) pendiente(s) que conviene reasignar."
                : string.Empty;
            return Resultado.Ok($"{instructor.Nombre} ahora está \"{estado}\".{aviso}");
        }

        public async Task<Resultado> EliminarAsync(int id)
        {
            var total = await _citas.ContarAsync(c => c.InstructorId == id);
            if (total > 0)
                return Resultado.Fallo($"El instructor tiene {total} cita(s) en el historial. Cámbielo a \"Inactivo\" para conservar los registros.");
            await _instructores.EliminarAsync(id);
            return Resultado.Ok("Instructor eliminado.");
        }

        public async Task<RendimientoInstructor> ObtenerRendimientoAsync(int id, DateTime desde, DateTime hasta)
        {
            var citas = await _citas.BuscarAsync(new CitaFiltro { InstructorId = id, Desde = desde, Hasta = hasta });
            var proxima = await ObtenerProximaCitaAsync(id);
            var hoy = await _citas.DelInstructorEnFechaAsync(id, _reloj.Now);
            return Calcular(id, citas, proxima, hoy.Count(c => c.Estado is EstadosCita.Pendiente or EstadosCita.Completada));
        }

        public async Task<Dictionary<int, RendimientoInstructor>> ObtenerRendimientosAsync(DateTime desde, DateTime hasta)
        {
            var citas = await _citas.BuscarAsync(new CitaFiltro { Desde = desde, Hasta = hasta });
            var hoy = _reloj.Now.Date;
            var futuras = await _citas.BuscarAsync(new CitaFiltro { Desde = hoy, Estados = new[] { EstadosCita.Pendiente } });
            var ahora = _reloj.Now;

            // La carga de hoy cuenta las citas que ocupan agenda (pendientes y completadas), no las canceladas.
            var deHoy = citas.Concat(futuras).DistinctBy(c => c.Id)
                .Where(c => c.Fecha.Date == hoy && c.Estado is EstadosCita.Pendiente or EstadosCita.Completada).ToList();

            return citas.Concat(futuras).Select(c => c.InstructorId).Distinct().ToDictionary(id => id, id =>
            {
                var proxima = futuras.Where(c => c.InstructorId == id && c.InicioCompleto >= ahora).MinBy(c => c.InicioCompleto);
                return Calcular(id, citas.Where(c => c.InstructorId == id), proxima, deHoy.Count(c => c.InstructorId == id));
            });
        }

        public Task<List<Cita>> ObtenerAgendaAsync(int id, DateTime desde, DateTime hasta) =>
            _citas.BuscarAsync(new CitaFiltro { InstructorId = id, Desde = desde, Hasta = hasta });

        private async Task<Cita?> ObtenerProximaCitaAsync(int id)
        {
            var ahora = _reloj.Now;
            var futuras = await _citas.BuscarAsync(new CitaFiltro { InstructorId = id, Desde = ahora.Date, Estados = new[] { EstadosCita.Pendiente } });
            return futuras.Where(c => c.InicioCompleto >= ahora).MinBy(c => c.InicioCompleto);
        }

        /// <summary>Cálculo puro de indicadores a partir de las citas del periodo.</summary>
        public static RendimientoInstructor Calcular(int id, IEnumerable<Cita> citas, Cita? proxima, int citasHoy)
        {
            var lista = citas.ToList();
            var completadas = lista.Where(c => c.Estado == EstadosCita.Completada).ToList();
            return new RendimientoInstructor(
                id,
                lista.Count,
                completadas.Count,
                lista.Count(c => c.Estado == EstadosCita.Pendiente),
                lista.Count(c => c.Estado == EstadosCita.Cancelada),
                lista.Count(c => c.Estado == EstadosCita.NoAsistio),
                Math.Round(completadas.Sum(c => c.DuracionHoras), 1),
                completadas.Sum(c => c.Precio),
                proxima,
                citasHoy);
        }
    }
}
