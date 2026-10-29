using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Common;

namespace GestionEscuelaEspana.Data.Repositorios
{
    /// <summary>Criterios de búsqueda de citas. Todos son opcionales y se combinan con AND.</summary>
    public sealed class CitaFiltro
    {
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public int? InstructorId { get; set; }
        public int? EstudianteId { get; set; }
        public IReadOnlyCollection<string>? Estados { get; set; }
        public string? Plantel { get; set; }
        public string? TipoVehiculo { get; set; }

        /// <summary>Texto libre: nombre, teléfono o correo del cliente.</summary>
        public string? Texto { get; set; }
    }

    public interface ICitaRepositorio : IRepositorio<Cita>
    {
        /// <summary>Busca citas aplicando los filtros en SQL y completa el nombre del instructor.</summary>
        Task<List<Cita>> BuscarAsync(CitaFiltro filtro);

        /// <summary>Citas de un instructor en una fecha (para detectar traslapes y carga diaria).</summary>
        Task<List<Cita>> DelInstructorEnFechaAsync(int instructorId, DateTime fecha);
    }

    public sealed class CitaRepositorio : Repositorio<Cita>, ICitaRepositorio
    {
        public CitaRepositorio(AppDatabase database) : base(database) { }

        public async Task<List<Cita>> BuscarAsync(CitaFiltro filtro)
        {
            var consulta = await ConsultaAsync();

            if (filtro.Desde is { } desde)
            {
                var d = desde.Date;
                consulta = consulta.Where(c => c.Fecha >= d);
            }
            if (filtro.Hasta is { } hasta)
            {
                var h = hasta.Date.AddDays(1);
                consulta = consulta.Where(c => c.Fecha < h);
            }
            if (filtro.InstructorId is { } instructorId)
                consulta = consulta.Where(c => c.InstructorId == instructorId);
            if (filtro.EstudianteId is { } estudianteId)
                consulta = consulta.Where(c => c.EstudianteId == estudianteId);
            if (filtro.Estados is { Count: > 0 } estados)
            {
                var lista = estados.ToList();
                consulta = consulta.Where(c => lista.Contains(c.Estado));
            }
            if (!string.IsNullOrWhiteSpace(filtro.Plantel))
            {
                var plantel = filtro.Plantel;
                consulta = consulta.Where(c => c.Plantel == plantel);
            }
            if (!string.IsNullOrWhiteSpace(filtro.TipoVehiculo))
            {
                var tipo = filtro.TipoVehiculo;
                consulta = consulta.Where(c => c.TipoVehiculo == tipo);
            }
            var citas = await consulta.ToListAsync();

            // El texto se filtra en memoria (sobre el resultado ya acotado por SQL): sqlite-net traduce Contains a
            // instr(), que distingue mayúsculas; así la búsqueda ignora mayúsculas y tildes ("jose" encuentra "José").
            if (!string.IsNullOrWhiteSpace(filtro.Texto))
                citas = citas.Where(c => TextoBusqueda.Coincide(filtro.Texto, c.ClienteNombre, c.ClienteTelefono, c.ClienteCorreo)).ToList();

            await CompletarInstructoresAsync(citas);
            return citas.OrderByDescending(c => c.Fecha).ThenByDescending(c => c.HoraInicio).ToList();
        }

        public async Task<List<Cita>> DelInstructorEnFechaAsync(int instructorId, DateTime fecha)
        {
            var inicio = fecha.Date;
            var fin = inicio.AddDays(1);
            var consulta = await ConsultaAsync();
            return await consulta.Where(c => c.InstructorId == instructorId && c.Fecha >= inicio && c.Fecha < fin).ToListAsync();
        }

        /// <summary>Resuelve los nombres de instructor con una sola consulta (evita N+1).</summary>
        private async Task CompletarInstructoresAsync(List<Cita> citas)
        {
            if (citas.Count == 0) return;
            var db = await Database.ObtenerConexionAsync();
            var nombres = (await db.Table<Instructor>().ToListAsync()).ToDictionary(i => i.Id, i => i.Nombre);
            foreach (var cita in citas)
                cita.InstructorNombre = nombres.TryGetValue(cita.InstructorId, out var nombre) ? nombre : "Sin asignar";
        }
    }
}
