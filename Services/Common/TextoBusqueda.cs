using System.Globalization;
using System.Text;

namespace GestionEscuelaEspana.Services.Common
{
    /// <summary>Búsqueda de texto tolerante: ignora mayúsculas, tildes y espacios sobrantes ("jose" encuentra "José").</summary>
    public static class TextoBusqueda
    {
        public static string Normalizar(string? texto)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;
            var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            return new string(descompuesto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        }

        /// <summary>Indica si alguno de los campos contiene el texto buscado.</summary>
        public static bool Coincide(string? busqueda, params string?[] campos)
        {
            var t = Normalizar(busqueda);
            return t.Length == 0 || campos.Any(c => Normalizar(c).Contains(t));
        }
    }
}
