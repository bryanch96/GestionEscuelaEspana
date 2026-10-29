using System.Security.Cryptography;

namespace GestionEscuelaEspana.Services.Security
{
    public interface IPasswordHasher
    {
        string Hash(string valor);
        bool Verificar(string valor, string hashAlmacenado);
    }

    /// <summary>
    /// Hash de contraseñas con PBKDF2-SHA256, sal aleatoria por usuario y comparación en tiempo constante.
    /// Formato almacenado: "{iteraciones}.{salBase64}.{hashBase64}".
    /// </summary>
    public sealed class Pbkdf2PasswordHasher : IPasswordHasher
    {
        private const int TamanoSal = 16;
        private const int TamanoHash = 32;
        private const int Iteraciones = 100_000;
        private static readonly HashAlgorithmName Algoritmo = HashAlgorithmName.SHA256;

        public string Hash(string valor)
        {
            ArgumentNullException.ThrowIfNull(valor);
            var sal = RandomNumberGenerator.GetBytes(TamanoSal);
            var hash = Rfc2898DeriveBytes.Pbkdf2(valor, sal, Iteraciones, Algoritmo, TamanoHash);
            return $"{Iteraciones}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
        }

        public bool Verificar(string valor, string hashAlmacenado)
        {
            if (string.IsNullOrEmpty(valor) || string.IsNullOrEmpty(hashAlmacenado)) return false;

            var partes = hashAlmacenado.Split('.');
            if (partes.Length != 3 || !int.TryParse(partes[0], out var iteraciones)) return false;

            try
            {
                var sal = Convert.FromBase64String(partes[1]);
                var esperado = Convert.FromBase64String(partes[2]);
                var calculado = Rfc2898DeriveBytes.Pbkdf2(valor, sal, iteraciones, Algoritmo, esperado.Length);
                return CryptographicOperations.FixedTimeEquals(calculado, esperado);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }

    /// <summary>Reglas mínimas de complejidad de contraseñas.</summary>
    public static class PoliticaPassword
    {
        public const int LongitudMinima = 8;

        public static IReadOnlyList<string> Validar(string? password)
        {
            var errores = new List<string>();
            password ??= string.Empty;
            if (password.Length < LongitudMinima) errores.Add($"Debe tener al menos {LongitudMinima} caracteres.");
            if (!password.Any(char.IsUpper)) errores.Add("Debe incluir al menos una letra mayúscula.");
            if (!password.Any(char.IsLower)) errores.Add("Debe incluir al menos una letra minúscula.");
            if (!password.Any(char.IsDigit)) errores.Add("Debe incluir al menos un número.");
            return errores;
        }
    }
}
