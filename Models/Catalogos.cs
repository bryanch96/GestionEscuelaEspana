namespace GestionEscuelaEspana.Models
{
    /// <summary>
    /// Estados posibles de una cita. Se exponen como constantes para evitar "strings mágicos"
    /// repartidos por la interfaz y la lógica de negocio.
    /// </summary>
    public static class EstadosCita
    {
        public const string Pendiente = "Pendiente";
        public const string Completada = "Completada";
        public const string Cancelada = "Cancelada";
        public const string NoAsistio = "No asistió";

        public static readonly IReadOnlyList<string> Todos = new[] { Pendiente, Completada, Cancelada, NoAsistio };

        /// <summary>Clase CSS asociada al estado (para los badges de color).</summary>
        public static string CssClase(string? estado) => estado switch
        {
            Pendiente => "pendiente",
            Completada => "completada",
            Cancelada => "cancelada",
            NoAsistio => "no-asistio",
            _ => "neutro"
        };
    }

    /// <summary>Estados laborales de un instructor.</summary>
    public static class EstadosInstructor
    {
        public const string Activo = "Activo";
        public const string Vacaciones = "Vacaciones";
        public const string Incapacitado = "Incapacitado";
        public const string Inactivo = "Inactivo";

        public static readonly IReadOnlyList<string> Todos = new[] { Activo, Vacaciones, Incapacitado, Inactivo };
    }

    /// <summary>Catálogo de tipos de vehículo y la categoría de licencia que exige cada uno.</summary>
    public static class TiposVehiculo
    {
        public const string OtroVehiculo = "Otro vehículo";

        /// <summary>Tipo de vehículo → categoría de licencia requerida al instructor.</summary>
        public static readonly IReadOnlyDictionary<string, string> CategoriaRequerida = new Dictionary<string, string>
        {
            ["Automóvil (B-1)"] = "B1",
            ["Motocicleta (A-2)"] = "A2",
            ["Motocicleta (A-3)"] = "A3",
        };

        /// <summary>Opciones del selector, incluida la opción libre "Otro vehículo".</summary>
        public static readonly IReadOnlyList<string> Opciones = CategoriaRequerida.Keys.Append(OtroVehiculo).ToList();

        public static bool EsPredefinido(string? tipo) => tipo != null && CategoriaRequerida.ContainsKey(tipo);
    }

    /// <summary>Categorías de licencia que puede impartir un instructor.</summary>
    public static class CategoriasLicencia
    {
        public static readonly IReadOnlyList<string> Todas = new[] { "A1", "A2", "A3", "B1", "B2", "B3", "C1", "C2", "D1", "D2", "D3", "E1", "E2" };
    }

    public static class Planteles
    {
        public static readonly IReadOnlyList<string> Todos = new[] { "San José", "Heredia", "Cartago", "Alajuela", "Guanacaste", "Puntarenas", "Limón" };
    }

    /// <summary>Roles de usuario de la aplicación.</summary>
    public static class Roles
    {
        public const string Administrador = "Administrador";
        public const string Recepcionista = "Recepcionista";
        public const string Instructor = "Instructor";

        public static readonly IReadOnlyList<string> Todos = new[] { Administrador, Recepcionista, Instructor };

        // Combinaciones usadas en atributos [Authorize(Roles = ...)]
        public const string Gestion = Administrador + "," + Recepcionista;
        public const string Docencia = Administrador + "," + Instructor;
    }
}
