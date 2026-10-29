using System.Globalization;
using GestionEscuelaEspana.Data.Repositorios;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Plataforma;

namespace GestionEscuelaEspana.Services.Dashboard
{
    public sealed record PuntoMensual(string Mes, int Numero, int Completadas, int Pendientes, int Canceladas, decimal Ingresos);

    public sealed record InstructorDestacado(string Nombre, int Completadas, double Horas);

    /// <summary>Instantánea con los indicadores clave del negocio para el panel principal.</summary>
    public sealed class ResumenDashboard
    {
        public int Pendientes { get; init; }
        public int Completadas { get; init; }
        public int Canceladas { get; init; }
        public int InstructoresActivos { get; init; }
        public int EstudiantesActivos { get; init; }
        public int CitasHoy { get; init; }
        public int CitasHoyCompletadas { get; init; }
        public decimal IngresosMes { get; init; }
        public decimal IngresosMesAnterior { get; init; }
        public double TasaCancelacionMes { get; init; }
        public double? VariacionIngresos => IngresosMesAnterior == 0 ? null
            : Math.Round((double)((IngresosMes - IngresosMesAnterior) / IngresosMesAnterior * 100), 1);
        public IReadOnlyList<Cita> ProximasCitas { get; init; } = Array.Empty<Cita>();
        public IReadOnlyList<Cita> CitasVencidasSinCerrar { get; init; } = Array.Empty<Cita>();
        public Cita? CitaInminente { get; init; }
        public IReadOnlyList<PuntoMensual> SerieMensual { get; init; } = Array.Empty<PuntoMensual>();
        public IReadOnlyList<InstructorDestacado> TopInstructores { get; init; } = Array.Empty<InstructorDestacado>();
    }

    public interface IDashboardService
    {
        Task<ResumenDashboard> ObtenerResumenAsync();
    }

    public sealed class DashboardService : IDashboardService
    {
        private static readonly CultureInfo Cultura = new("es-CR");

        private readonly ICitaRepositorio _citas;
        private readonly IRepositorio<Instructor> _instructores;
        private readonly IRepositorio<Estudiante> _estudiantes;
        private readonly IClock _reloj;

        public DashboardService(ICitaRepositorio citas, IRepositorio<Instructor> instructores, IRepositorio<Estudiante> estudiantes, IClock reloj)
        {
            _citas = citas;
            _instructores = instructores;
            _estudiantes = estudiantes;
            _reloj = reloj;
        }

        public async Task<ResumenDashboard> ObtenerResumenAsync()
        {
            var ahora = _reloj.Now;
            var hoy = ahora.Date;
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
            var inicioMesAnterior = inicioMes.AddMonths(-1);
            var inicioAnio = new DateTime(hoy.Year, 1, 1);
            var desde = inicioMesAnterior < inicioAnio ? inicioMesAnterior : inicioAnio;

            // Conteos globales: consultas COUNT indexadas, sin traer filas a memoria.
            var pendientes = _citas.ContarAsync(c => c.Estado == EstadosCita.Pendiente);
            var completadas = _citas.ContarAsync(c => c.Estado == EstadosCita.Completada);
            var canceladas = _citas.ContarAsync(c => c.Estado == EstadosCita.Cancelada);
            var instructores = _instructores.ContarAsync(i => i.Estado == EstadosInstructor.Activo);
            var estudiantes = _estudiantes.ContarAsync(e => e.Activo);
            var recientes = _citas.BuscarAsync(new CitaFiltro { Desde = desde });
            var vencidas = _citas.BuscarAsync(new CitaFiltro { Hasta = hoy, Estados = new[] { EstadosCita.Pendiente } });
            await Task.WhenAll(pendientes, completadas, canceladas, instructores, estudiantes, recientes, vencidas);

            var citas = recientes.Result;
            var delMes = citas.Where(c => c.Fecha >= inicioMes && c.Fecha < inicioMes.AddMonths(1)).ToList();
            var cerradasMes = delMes.Count(c => c.Estado != EstadosCita.Pendiente);

            var futurasPendientes = citas
                .Where(c => c.Estado == EstadosCita.Pendiente && c.FinCompleto >= ahora)
                .OrderBy(c => c.InicioCompleto)
                .ToList();

            return new ResumenDashboard
            {
                Pendientes = pendientes.Result,
                Completadas = completadas.Result,
                Canceladas = canceladas.Result,
                InstructoresActivos = instructores.Result,
                EstudiantesActivos = estudiantes.Result,
                CitasHoy = citas.Count(c => c.Fecha.Date == hoy && c.Estado != EstadosCita.Cancelada),
                CitasHoyCompletadas = citas.Count(c => c.Fecha.Date == hoy && c.Estado == EstadosCita.Completada),
                IngresosMes = delMes.Where(c => c.Estado == EstadosCita.Completada).Sum(c => c.Precio),
                IngresosMesAnterior = citas.Where(c => c.Fecha >= inicioMesAnterior && c.Fecha < inicioMes && c.Estado == EstadosCita.Completada).Sum(c => c.Precio),
                TasaCancelacionMes = cerradasMes == 0 ? 0 : Math.Round(100.0 * delMes.Count(c => c.Estado is EstadosCita.Cancelada or EstadosCita.NoAsistio) / cerradasMes, 1),
                ProximasCitas = futurasPendientes.Where(c => c.Fecha.Date <= hoy.AddDays(3)).ToList(),
                CitasVencidasSinCerrar = vencidas.Result.Where(c => c.FinCompleto < ahora).OrderBy(c => c.InicioCompleto).ToList(),
                CitaInminente = futurasPendientes.FirstOrDefault(c => c.InicioCompleto > ahora && (c.InicioCompleto - ahora).TotalMinutes <= 180),
                SerieMensual = Enumerable.Range(1, hoy.Month).Select(m =>
                {
                    var delMesM = citas.Where(c => c.Fecha.Year == hoy.Year && c.Fecha.Month == m).ToList();
                    var nombre = Cultura.DateTimeFormat.GetAbbreviatedMonthName(m).TrimEnd('.');
                    return new PuntoMensual(char.ToUpper(nombre[0]) + nombre[1..], m,
                        delMesM.Count(c => c.Estado == EstadosCita.Completada),
                        delMesM.Count(c => c.Estado == EstadosCita.Pendiente),
                        delMesM.Count(c => c.Estado is EstadosCita.Cancelada or EstadosCita.NoAsistio),
                        delMesM.Where(c => c.Estado == EstadosCita.Completada).Sum(c => c.Precio));
                }).ToList(),
                TopInstructores = delMes.Where(c => c.Estado == EstadosCita.Completada)
                    .GroupBy(c => c.InstructorNombre)
                    .Select(g => new InstructorDestacado(g.Key, g.Count(), Math.Round(g.Sum(c => c.DuracionHoras), 1)))
                    .OrderByDescending(x => x.Completadas).Take(3).ToList()
            };
        }
    }
}
