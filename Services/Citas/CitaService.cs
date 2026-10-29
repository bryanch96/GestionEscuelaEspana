using GestionEscuelaEspana.Data.Repositorios;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Common;
using GestionEscuelaEspana.Services.Configuracion;
using GestionEscuelaEspana.Services.Email;
using GestionEscuelaEspana.Services.Estudiantes;
using GestionEscuelaEspana.Services.Plataforma;
using GestionEscuelaEspana.Services.Security;

namespace GestionEscuelaEspana.Services.Citas
{
    public sealed class ResultadoGuardarCita
    {
        public bool Exito { get; init; }
        public string Mensaje { get; init; } = string.Empty;
        public ValidacionCita Validacion { get; init; } = new();

        /// <summary>La cita es válida pero tiene advertencias que el usuario debe confirmar.</summary>
        public bool RequiereConfirmacion { get; init; }

        /// <summary>Resultado del envío del correo al cliente (null si no se envió).</summary>
        public ResultadoEnvio? Correo { get; init; }
        public Cita? Cita { get; init; }
    }

    public interface ICitaService
    {
        Task<List<Cita>> BuscarAsync(CitaFiltro filtro);
        Task<Cita?> ObtenerAsync(int id);
        Task<ValidacionCita> ValidarAsync(Cita cita);
        Task<ResultadoGuardarCita> GuardarAsync(Cita cita, bool enviarCorreo, bool aceptarAdvertencias);
        Task<Resultado> CambiarEstadoAsync(int id, string estado, string? motivo = null, bool notificarCliente = true);
        Task<Resultado> EliminarAsync(int id);
        Task<ResultadoEnvio> ReenviarConfirmacionAsync(int id);
    }

    /// <summary>Orquesta el agendamiento: validación, ficha del estudiante, auditoría y notificación por correo.</summary>
    public sealed class CitaService : ICitaService
    {
        private readonly ICitaRepositorio _citas;
        private readonly IRepositorio<Instructor> _instructores;
        private readonly IEstudianteService _estudiantes;
        private readonly IEmailService _email;
        private readonly IConfiguracionService _configuracion;
        private readonly ISesionUsuario _sesion;
        private readonly IClock _reloj;

        public CitaService(ICitaRepositorio citas, IRepositorio<Instructor> instructores, IEstudianteService estudiantes,
            IEmailService email, IConfiguracionService configuracion, ISesionUsuario sesion, IClock reloj)
        {
            _citas = citas;
            _instructores = instructores;
            _estudiantes = estudiantes;
            _email = email;
            _configuracion = configuracion;
            _sesion = sesion;
            _reloj = reloj;
        }

        public Task<List<Cita>> BuscarAsync(CitaFiltro filtro) => _citas.BuscarAsync(filtro);

        public async Task<Cita?> ObtenerAsync(int id)
        {
            var cita = await _citas.ObtenerAsync(id);
            if (cita != null)
                cita.InstructorNombre = (await _instructores.ObtenerAsync(cita.InstructorId))?.Nombre ?? "Sin asignar";
            return cita;
        }

        public async Task<ValidacionCita> ValidarAsync(Cita cita)
        {
            var instructor = cita.InstructorId > 0 ? await _instructores.ObtenerAsync(cita.InstructorId) : null;
            var mismoDia = instructor != null ? await _citas.DelInstructorEnFechaAsync(instructor.Id, cita.Fecha) : new List<Cita>();
            return ReglasAgenda.Validar(cita, instructor, mismoDia, _reloj.Now);
        }

        public async Task<ResultadoGuardarCita> GuardarAsync(Cita cita, bool enviarCorreo, bool aceptarAdvertencias)
        {
            cita.ClienteNombre = cita.ClienteNombre.Trim();
            cita.ClienteTelefono = cita.ClienteTelefono.Trim();
            cita.ClienteCorreo = cita.ClienteCorreo.Trim();
            cita.TipoVehiculo = cita.TipoVehiculo.Trim();
            cita.Fecha = cita.Fecha.Date;

            var validacion = await ValidarAsync(cita);
            if (!validacion.EsValida)
                return new ResultadoGuardarCita { Mensaje = "Corrija los errores del formulario.", Validacion = validacion };
            if (validacion.Advertencias.Count > 0 && !aceptarAdvertencias)
                return new ResultadoGuardarCita { Mensaje = "Revise las advertencias antes de continuar.", Validacion = validacion, RequiereConfirmacion = true };

            var anterior = cita.Id == 0 ? null : await _citas.ObtenerAsync(cita.Id);
            var esNueva = anterior == null;

            var estudiante = await _estudiantes.ObtenerOCrearAsync(cita.ClienteNombre, cita.ClienteTelefono, cita.ClienteCorreo);
            cita.EstudianteId = estudiante.Id;

            var ahora = _reloj.Now;
            if (esNueva)
            {
                cita.Id = 0;
                cita.FechaCreacion = ahora;
                cita.CreadoPor = _sesion.Actual?.NombreUsuario ?? string.Empty;
            }
            if (cita.Estado != EstadosCita.Cancelada) cita.MotivoCancelacion = string.Empty;
            cita.FechaModificacion = ahora;
            await _citas.GuardarAsync(cita);

            cita.InstructorNombre = (await _instructores.ObtenerAsync(cita.InstructorId))?.Nombre ?? string.Empty;

            // Se notifica al cliente en citas nuevas o cuando cambia algo que le afecta.
            ResultadoEnvio? correo = null;
            var hayCambiosRelevantes = esNueva || CambiosRelevantes(anterior!, cita);
            if (enviarCorreo && hayCambiosRelevantes && !string.IsNullOrWhiteSpace(cita.ClienteCorreo))
            {
                correo = await NotificarAsync(cita, esActualizacion: !esNueva);
                if (correo.Exito && !cita.ConfirmacionEnviada)
                {
                    cita.ConfirmacionEnviada = true;
                    await _citas.GuardarAsync(cita);
                }
            }

            return new ResultadoGuardarCita
            {
                Exito = true,
                Mensaje = esNueva ? "Cita agendada correctamente." : "Cambios guardados.",
                Validacion = validacion,
                Correo = correo,
                Cita = cita
            };
        }

        public async Task<Resultado> CambiarEstadoAsync(int id, string estado, string? motivo = null, bool notificarCliente = true)
        {
            if (!EstadosCita.Todos.Contains(estado)) return Resultado.Fallo("Estado no válido.");
            var cita = await ObtenerAsync(id);
            if (cita == null) return Resultado.Fallo("La cita no existe.");
            if (cita.Estado == estado) return Resultado.Ok();

            cita.Estado = estado;
            cita.MotivoCancelacion = estado == EstadosCita.Cancelada ? (motivo ?? string.Empty).Trim() : string.Empty;
            cita.FechaModificacion = _reloj.Now;
            await _citas.GuardarAsync(cita);

            var mensaje = $"La cita de {cita.ClienteNombre} quedó como \"{estado}\".";
            if (estado == EstadosCita.Cancelada && notificarCliente && Validaciones.EsCorreoValido(cita.ClienteCorreo))
            {
                var config = await _configuracion.ObtenerAsync();
                var envio = await _email.EnviarAsync(PlantillasCorreo.CancelacionCita(cita, config.NombreEscuela));
                mensaje += envio.Exito ? " Se notificó al cliente por correo." : $" No se pudo notificar: {envio.Mensaje}";
            }
            return Resultado.Ok(mensaje);
        }

        public async Task<Resultado> EliminarAsync(int id)
        {
            if (!_sesion.EsAdministrador)
                return Resultado.Fallo("Solo un administrador puede eliminar citas. Puede cancelarla en su lugar.");
            await _citas.EliminarAsync(id);
            return Resultado.Ok("Cita eliminada del historial.");
        }

        public async Task<ResultadoEnvio> ReenviarConfirmacionAsync(int id)
        {
            var cita = await ObtenerAsync(id);
            if (cita == null) return new ResultadoEnvio(false, false, "La cita no existe.");
            if (string.IsNullOrWhiteSpace(cita.ClienteCorreo)) return new ResultadoEnvio(false, false, "La cita no tiene correo del cliente.");

            var envio = cita.Estado == EstadosCita.Cancelada
                ? await _email.EnviarAsync(PlantillasCorreo.CancelacionCita(cita, (await _configuracion.ObtenerAsync()).NombreEscuela))
                : await NotificarAsync(cita, esActualizacion: false);

            if (envio.Exito && !cita.ConfirmacionEnviada)
            {
                cita.ConfirmacionEnviada = true;
                await _citas.GuardarAsync(cita);
            }
            return envio;
        }

        private async Task<ResultadoEnvio> NotificarAsync(Cita cita, bool esActualizacion)
        {
            var config = await _configuracion.ObtenerAsync();
            var mensaje = cita.Estado == EstadosCita.Cancelada
                ? PlantillasCorreo.CancelacionCita(cita, config.NombreEscuela)
                : PlantillasCorreo.ConfirmacionCita(cita, config.NombreEscuela, esActualizacion);
            return await _email.EnviarAsync(mensaje);
        }

        private static bool CambiosRelevantes(Cita a, Cita b) =>
            a.Fecha.Date != b.Fecha.Date || a.HoraInicio != b.HoraInicio || a.HoraFin != b.HoraFin ||
            a.Plantel != b.Plantel || a.InstructorId != b.InstructorId || a.TipoVehiculo != b.TipoVehiculo ||
            a.Estado != b.Estado || a.Precio != b.Precio ||
            !string.Equals(a.ClienteCorreo, b.ClienteCorreo, StringComparison.OrdinalIgnoreCase);
    }
}
