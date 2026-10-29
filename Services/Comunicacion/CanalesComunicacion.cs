using System.Text;
using GestionEscuelaEspana.Services.Common;
using GestionEscuelaEspana.Services.Configuracion;
using GestionEscuelaEspana.Services.Plataforma;

namespace GestionEscuelaEspana.Services.Comunicacion
{
    public interface ICanalesComunicacion
    {
        /// <summary>Abre WhatsApp (app o web) con un chat al número indicado y un mensaje opcional precargado.</summary>
        Task<Resultado> AbrirWhatsAppAsync(string telefono, string? mensaje = null);

        /// <summary>Abre la app de correo predeterminada del dispositivo con el borrador listo.</summary>
        Task<Resultado> AbrirCorreoAsync(string correo, string? asunto = null, string? cuerpo = null);
    }

    /// <summary>Construcción de enlaces de comunicación (funciones puras, fáciles de probar).</summary>
    public static class EnlacesComunicacion
    {
        /// <summary>
        /// Convierte un teléfono local o internacional al formato que exige wa.me (solo dígitos, con código de país).
        /// Ejemplos con código 506: "8888-7777" → "50688887777"; "+1 (305) 555-0100" → "13055550100".
        /// </summary>
        public static string? NormalizarTelefono(string? telefono, string codigoPais)
        {
            if (string.IsNullOrWhiteSpace(telefono)) return null;
            var limpio = telefono.Trim();
            var internacional = limpio.StartsWith('+') || limpio.StartsWith("00");
            var digitos = new string(limpio.Where(char.IsDigit).ToArray());
            if (digitos.StartsWith("00")) digitos = digitos[2..];

            if (!internacional && digitos.Length <= 8)
                digitos = codigoPais + digitos;

            return digitos.Length is >= 8 and <= 15 ? digitos : null;
        }

        public static string? WhatsApp(string telefono, string codigoPais, string? mensaje = null)
        {
            var numero = NormalizarTelefono(telefono, codigoPais);
            if (numero == null) return null;
            var url = $"https://wa.me/{numero}";
            return string.IsNullOrWhiteSpace(mensaje) ? url : $"{url}?text={Uri.EscapeDataString(mensaje)}";
        }

        public static string Mailto(string correo, string? asunto = null, string? cuerpo = null)
        {
            var sb = new StringBuilder("mailto:").Append(Uri.EscapeDataString(correo.Trim()));
            var parametros = new List<string>();
            if (!string.IsNullOrWhiteSpace(asunto)) parametros.Add("subject=" + Uri.EscapeDataString(asunto));
            if (!string.IsNullOrWhiteSpace(cuerpo)) parametros.Add("body=" + Uri.EscapeDataString(cuerpo));
            if (parametros.Count > 0) sb.Append('?').Append(string.Join('&', parametros));
            return sb.ToString();
        }
    }

    public sealed class CanalesComunicacion : ICanalesComunicacion
    {
        private readonly IAppLauncher _launcher;
        private readonly IConfiguracionService _configuracion;

        public CanalesComunicacion(IAppLauncher launcher, IConfiguracionService configuracion)
        {
            _launcher = launcher;
            _configuracion = configuracion;
        }

        public async Task<Resultado> AbrirWhatsAppAsync(string telefono, string? mensaje = null)
        {
            var config = await _configuracion.ObtenerAsync();
            var url = EnlacesComunicacion.WhatsApp(telefono, config.CodigoPais, mensaje);
            if (url == null) return Resultado.Fallo("El número de teléfono no es válido para WhatsApp.");

            return await _launcher.AbrirAsync(url)
                ? Resultado.Ok("Abriendo WhatsApp…")
                : Resultado.Fallo("No se pudo abrir WhatsApp en este dispositivo.");
        }

        public async Task<Resultado> AbrirCorreoAsync(string correo, string? asunto = null, string? cuerpo = null)
        {
            if (!Validaciones.EsCorreoValido(correo)) return Resultado.Fallo("El correo electrónico no es válido.");
            return await _launcher.AbrirAsync(EnlacesComunicacion.Mailto(correo, asunto, cuerpo))
                ? Resultado.Ok("Abriendo la aplicación de correo…")
                : Resultado.Fallo("No hay una aplicación de correo configurada en este dispositivo.");
        }
    }
}
