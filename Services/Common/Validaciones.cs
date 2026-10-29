using System.Net.Mail;
using System.Text.RegularExpressions;

namespace GestionEscuelaEspana.Services.Common
{
    /// <summary>Validaciones de formato reutilizadas por formularios y servicios.</summary>
    public static partial class Validaciones
    {
        public static bool EsCorreoValido(string? correo)
        {
            if (string.IsNullOrWhiteSpace(correo)) return false;
            correo = correo.Trim();
            if (!CorreoRegex().IsMatch(correo)) return false;
            try
            {
                return new MailAddress(correo).Address.Equals(correo, StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>Acepta teléfonos con 8 a 15 dígitos (con o sin +, espacios o guiones).</summary>
        public static bool EsTelefonoValido(string? telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono)) return false;
            if (!TelefonoRegex().IsMatch(telefono.Trim())) return false;
            var digitos = telefono.Count(char.IsDigit);
            return digitos is >= 8 and <= 15;
        }

        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")]
        private static partial Regex CorreoRegex();

        [GeneratedRegex(@"^\+?[\d\s\-()]+$")]
        private static partial Regex TelefonoRegex();
    }
}
