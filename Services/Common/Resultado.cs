namespace GestionEscuelaEspana.Services.Common
{
    /// <summary>Resultado de una operación de negocio: evita usar excepciones para el flujo normal de validación.</summary>
    public class Resultado
    {
        public bool Exito { get; init; }
        public string Mensaje { get; init; } = string.Empty;

        public static Resultado Ok(string mensaje = "") => new() { Exito = true, Mensaje = mensaje };
        public static Resultado Fallo(string mensaje) => new() { Exito = false, Mensaje = mensaje };
    }

    public class Resultado<T> : Resultado
    {
        public T? Valor { get; init; }

        public static Resultado<T> Ok(T valor, string mensaje = "") => new() { Exito = true, Valor = valor, Mensaje = mensaje };
        public static new Resultado<T> Fallo(string mensaje) => new() { Exito = false, Mensaje = mensaje };
    }
}
