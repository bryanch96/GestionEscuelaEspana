using GestionEscuelaEspana.Data.Repositorios;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Common;
using GestionEscuelaEspana.Services.Plataforma;

namespace GestionEscuelaEspana.Services.Estudiantes
{
    public interface IEstudianteService
    {
        Task<List<Estudiante>> BuscarAsync(string? texto = null, bool soloActivos = true);
        Task<Estudiante?> ObtenerAsync(int id);
        Task<Resultado<Estudiante>> GuardarAsync(Estudiante estudiante);

        /// <summary>Localiza al estudiante por teléfono (o nombre) y lo crea si no existe; actualiza su correo si cambió.</summary>
        Task<Estudiante> ObtenerOCrearAsync(string nombre, string telefono, string correo);
    }

    public sealed class EstudianteService : IEstudianteService
    {
        private readonly IRepositorio<Estudiante> _estudiantes;
        private readonly IClock _reloj;

        public EstudianteService(IRepositorio<Estudiante> estudiantes, IClock reloj)
        {
            _estudiantes = estudiantes;
            _reloj = reloj;
        }

        public async Task<List<Estudiante>> BuscarAsync(string? texto = null, bool soloActivos = true)
        {
            var lista = soloActivos ? await _estudiantes.ListarAsync(e => e.Activo) : await _estudiantes.ListarAsync();
            if (!string.IsNullOrWhiteSpace(texto))
                lista = lista.Where(e => TextoBusqueda.Coincide(texto, e.NombreCompleto, e.Telefono, e.Correo, e.Identificacion)).ToList();
            return lista.OrderBy(e => e.NombreCompleto).ToList();
        }

        public Task<Estudiante?> ObtenerAsync(int id) => _estudiantes.ObtenerAsync(id);

        public async Task<Resultado<Estudiante>> GuardarAsync(Estudiante estudiante)
        {
            estudiante.NombreCompleto = estudiante.NombreCompleto.Trim();
            estudiante.Telefono = estudiante.Telefono.Trim();
            estudiante.Correo = estudiante.Correo.Trim();

            if (string.IsNullOrWhiteSpace(estudiante.NombreCompleto))
                return Resultado<Estudiante>.Fallo("El nombre del estudiante es obligatorio.");
            if (!string.IsNullOrEmpty(estudiante.Telefono) && !Validaciones.EsTelefonoValido(estudiante.Telefono))
                return Resultado<Estudiante>.Fallo("El teléfono no es válido.");
            if (!string.IsNullOrEmpty(estudiante.Correo) && !Validaciones.EsCorreoValido(estudiante.Correo))
                return Resultado<Estudiante>.Fallo("El correo electrónico no es válido.");

            if (estudiante.Id == 0) estudiante.FechaRegistro = _reloj.Now;
            await _estudiantes.GuardarAsync(estudiante);
            return Resultado<Estudiante>.Ok(estudiante, "Estudiante guardado.");
        }

        public async Task<Estudiante> ObtenerOCrearAsync(string nombre, string telefono, string correo)
        {
            nombre = nombre.Trim();
            telefono = telefono.Trim();
            correo = correo.Trim();

            var digitos = SoloDigitos(telefono);
            var candidatos = await _estudiantes.ListarAsync();
            var existente = !string.IsNullOrEmpty(digitos)
                ? candidatos.FirstOrDefault(e => SoloDigitos(e.Telefono) == digitos)
                : candidatos.FirstOrDefault(e => TextoBusqueda.Normalizar(e.NombreCompleto) == TextoBusqueda.Normalizar(nombre));

            if (existente == null)
            {
                existente = new Estudiante { NombreCompleto = nombre, Telefono = telefono, Correo = correo, FechaRegistro = _reloj.Now };
                await _estudiantes.GuardarAsync(existente);
                return existente;
            }

            // Mantiene la ficha al día con los datos más recientes capturados en la cita.
            var cambio = false;
            if (!string.IsNullOrEmpty(correo) && !correo.Equals(existente.Correo, StringComparison.OrdinalIgnoreCase)) { existente.Correo = correo; cambio = true; }
            if (!string.IsNullOrEmpty(nombre) && nombre != existente.NombreCompleto) { existente.NombreCompleto = nombre; cambio = true; }
            if (!existente.Activo) { existente.Activo = true; cambio = true; }
            if (cambio) await _estudiantes.GuardarAsync(existente);
            return existente;
        }

        private static string SoloDigitos(string valor) => new(valor.Where(char.IsDigit).ToArray());

    }
}
