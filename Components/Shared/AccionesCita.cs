using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Citas;
using GestionEscuelaEspana.Services.Comunicacion;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace GestionEscuelaEspana.Components.Shared
{
    /// <summary>
    /// Acciones de cita reutilizadas por el dashboard, el historial y los diálogos (evita duplicar lógica de UI).
    /// </summary>
    public sealed class AccionesCita
    {
        private readonly ICitaService _citas;
        private readonly ICanalesComunicacion _canales;
        private readonly DialogService _dialogos;
        private readonly NotificationService _notificaciones;

        public AccionesCita(ICitaService citas, ICanalesComunicacion canales, DialogService dialogos, NotificationService notificaciones)
        {
            _citas = citas;
            _canales = canales;
            _dialogos = dialogos;
            _notificaciones = notificaciones;
        }

        /// <summary>Abre el detalle de la cita. Devuelve true si la cita cambió.</summary>
        public async Task<bool> VerDetalleAsync(int citaId)
        {
            var resultado = await _dialogos.OpenAsync<CitaDetalle>("Detalle de la cita",
                new Dictionary<string, object?> { [nameof(CitaDetalle.CitaId)] = citaId },
                new DialogOptions { Width = "620px", CloseDialogOnOverlayClick = true });
            return resultado is true;
        }

        public async Task<bool> CambiarEstadoAsync(Cita cita, string estado)
        {
            string? motivo = null;
            var notificar = false;

            if (estado == EstadosCita.Cancelada)
            {
                var respuesta = await _dialogos.OpenAsync<MotivoCancelacionDialog>("Cancelar cita",
                    new Dictionary<string, object?>
                    {
                        [nameof(MotivoCancelacionDialog.Cliente)] = cita.ClienteNombre,
                        [nameof(MotivoCancelacionDialog.TieneCorreo)] = !string.IsNullOrWhiteSpace(cita.ClienteCorreo)
                    },
                    new DialogOptions { Width = "480px" });
                if (respuesta is not MotivoCancelacionDialog.Respuesta r) return false;
                motivo = r.Motivo;
                notificar = r.Notificar;
            }

            var resultado = await _citas.CambiarEstadoAsync(cita.Id, estado, motivo, notificar);
            Notificar(resultado.Exito, resultado.Exito ? "Estado actualizado" : "No se pudo actualizar", resultado.Mensaje);
            return resultado.Exito;
        }

        public async Task<bool> EliminarAsync(Cita cita)
        {
            var confirmar = await _dialogos.Confirm(
                $"Se eliminará definitivamente la cita de {cita.ClienteNombre} del {Formato.Fecha(cita.Fecha)}. Esta acción no se puede deshacer.",
                "Eliminar cita", new ConfirmOptions { OkButtonText = "Eliminar", CancelButtonText = "Cancelar" });
            if (confirmar != true) return false;

            var resultado = await _citas.EliminarAsync(cita.Id);
            Notificar(resultado.Exito, resultado.Exito ? "Cita eliminada" : "No permitido", resultado.Mensaje);
            return resultado.Exito;
        }

        public async Task ReenviarCorreoAsync(Cita cita)
        {
            var envio = await _citas.ReenviarConfirmacionAsync(cita.Id);
            Notificar(envio.Exito, envio.Exito ? "Correo enviado" : "Correo no enviado", envio.Mensaje);
        }

        public async Task WhatsAppRecordatorioAsync(Cita cita)
        {
            var mensaje = cita.Estado == EstadosCita.Pendiente
                ? $"Hola {cita.ClienteNombre}, le recordamos su cita de manejo el {Formato.FechaLarga(cita.Fecha)} a las {Formato.Hora(cita.HoraInicio)} en el plantel {cita.Plantel}. ¡Le esperamos!"
                : $"Hola {cita.ClienteNombre}, le saludamos de la escuela de manejo.";
            var resultado = await _canales.AbrirWhatsAppAsync(cita.ClienteTelefono, mensaje);
            if (!resultado.Exito) Notificar(false, "WhatsApp", resultado.Mensaje);
        }

        public void Notificar(bool exito, string titulo, string detalle) =>
            _notificaciones.Notify(new NotificationMessage
            {
                Severity = exito ? NotificationSeverity.Success : NotificationSeverity.Error,
                Summary = titulo,
                Detail = detalle,
                Duration = exito ? 4000 : 8000
            });
    }
}
