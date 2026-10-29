using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Common;

namespace GestionEscuelaEspana.Services.Citas
{
    /// <summary>Resultado de validar una cita: los errores impiden guardar; las advertencias requieren confirmación.</summary>
    public sealed class ValidacionCita
    {
        public List<string> Errores { get; } = new();
        public List<string> Advertencias { get; } = new();
        public bool EsValida => Errores.Count == 0;
    }

    /// <summary>
    /// Reglas de negocio de la agenda (funciones puras, sin acceso a datos, para poder probarlas):
    /// datos obligatorios, traslapes del instructor, disponibilidad, capacidad diaria y licencias.
    /// </summary>
    public static class ReglasAgenda
    {
        private static readonly string[] EstadosQueOcupanAgenda = { EstadosCita.Pendiente, EstadosCita.Completada };

        public static ValidacionCita Validar(Cita cita, Instructor? instructor, IEnumerable<Cita> citasDelInstructorEseDia, DateTime ahora)
        {
            var v = new ValidacionCita();

            // --- Datos del cliente ---
            if (string.IsNullOrWhiteSpace(cita.ClienteNombre)) v.Errores.Add("El nombre del cliente es obligatorio.");
            if (!Validaciones.EsTelefonoValido(cita.ClienteTelefono)) v.Errores.Add("El número de celular no es válido (8 a 15 dígitos).");
            if (!string.IsNullOrWhiteSpace(cita.ClienteCorreo) && !Validaciones.EsCorreoValido(cita.ClienteCorreo))
                v.Errores.Add("El correo electrónico del cliente no es válido.");

            // --- Datos operativos ---
            if (string.IsNullOrWhiteSpace(cita.TipoVehiculo) || cita.TipoVehiculo == TiposVehiculo.OtroVehiculo)
                v.Errores.Add("Especifique el tipo de vehículo.");
            if (string.IsNullOrWhiteSpace(cita.Plantel)) v.Errores.Add("Seleccione el plantel de pruebas.");
            if (!EstadosCita.Todos.Contains(cita.Estado)) v.Errores.Add("El estado de la cita no es válido.");
            if (cita.Precio < 0) v.Errores.Add("El precio no puede ser negativo.");
            if (cita.HoraFin <= cita.HoraInicio) v.Errores.Add("La hora de finalización debe ser posterior a la hora de inicio.");
            if (cita.Id == 0 && cita.Estado == EstadosCita.Pendiente && cita.InicioCompleto < ahora)
                v.Errores.Add("No se puede agendar una cita pendiente en una fecha u hora que ya pasó.");

            if (instructor == null)
            {
                v.Errores.Add("Seleccione el instructor asignado.");
                return v;
            }

            // --- Traslapes (error): un instructor no puede estar en dos citas a la vez ---
            if (EstadosQueOcupanAgenda.Contains(cita.Estado))
            {
                var choque = citasDelInstructorEseDia.FirstOrDefault(o =>
                    o.Id != cita.Id && EstadosQueOcupanAgenda.Contains(o.Estado) && o.SeTraslapaCon(cita));
                if (choque != null)
                    v.Errores.Add($"{instructor.Nombre} ya tiene una cita de {choque.HoraInicio:hh\\:mm} a {choque.HoraFin:hh\\:mm} con {choque.ClienteNombre}.");
            }

            // --- Advertencias de control de empleados ---
            if (!instructor.EstaActivo)
                v.Advertencias.Add($"{instructor.Nombre} está en estado \"{instructor.Estado}\".");
            if (!instructor.TrabajaEl(cita.Fecha.DayOfWeek))
                v.Advertencias.Add($"{instructor.Nombre} no labora los {cita.Fecha.ToString("dddd", new System.Globalization.CultureInfo("es-CR"))}.");
            if (cita.HoraInicio < instructor.HoraEntrada || cita.HoraFin > instructor.HoraSalida)
                v.Advertencias.Add($"El horario está fuera de la jornada de {instructor.Nombre} ({instructor.HoraEntrada:hh\\:mm}–{instructor.HoraSalida:hh\\:mm}).");

            var ocupadas = citasDelInstructorEseDia.Count(o => o.Id != cita.Id && EstadosQueOcupanAgenda.Contains(o.Estado));
            if (instructor.MaxCitasPorDia > 0 && ocupadas >= instructor.MaxCitasPorDia)
                v.Advertencias.Add($"{instructor.Nombre} ya alcanzó su capacidad de {instructor.MaxCitasPorDia} citas para ese día.");

            if (TiposVehiculo.CategoriaRequerida.TryGetValue(cita.TipoVehiculo, out var categoria) && !instructor.TieneCategoria(categoria))
                v.Advertencias.Add($"{instructor.Nombre} no tiene registrada la licencia {categoria} requerida para {cita.TipoVehiculo}.");
            if (instructor.VencimientoLicencia is { } vence && vence.Date < cita.Fecha.Date)
                v.Advertencias.Add($"La licencia de {instructor.Nombre} vence el {vence:dd/MM/yyyy}, antes de la cita.");

            if (cita.DuracionHoras > 4)
                v.Advertencias.Add("La cita dura más de 4 horas; verifique el horario.");

            return v;
        }
    }
}
