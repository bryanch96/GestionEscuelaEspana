using GestionEscuelaEspana.Data.Repositorios;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Common;
using GestionEscuelaEspana.Services.Plataforma;

namespace GestionEscuelaEspana.Services.Security
{
    public interface IUsuarioService
    {
        Task<bool> ExistenUsuariosAsync();
        Task<List<Usuario>> ListarAsync();
        Task<Resultado> CrearAsync(Usuario usuario, string password);
        Task<Resultado> ActualizarAsync(Usuario usuario);
        Task<Resultado> CambiarPasswordAsync(int usuarioId, string actual, string nueva);
        Task<Resultado> EstablecerPasswordAsync(int usuarioId, string nueva);
        Task<Resultado> DesbloquearAsync(int usuarioId);
        Task<Resultado> EliminarAsync(int usuarioId, int usuarioActualId);

        /// <summary>Crea el primer administrador (solo permitido si aún no existen usuarios).</summary>
        Task<Resultado> CrearAdministradorInicialAsync(Usuario usuario, string password);
    }

    /// <summary>Gestión de perfiles de usuario, roles y credenciales.</summary>
    public sealed class UsuarioService : IUsuarioService
    {
        private readonly IRepositorio<Usuario> _usuarios;
        private readonly IPasswordHasher _hasher;
        private readonly ISesionUsuario _sesion;
        private readonly IClock _reloj;

        public UsuarioService(IRepositorio<Usuario> usuarios, IPasswordHasher hasher, ISesionUsuario sesion, IClock reloj)
        {
            _usuarios = usuarios;
            _hasher = hasher;
            _sesion = sesion;
            _reloj = reloj;
        }

        public async Task<bool> ExistenUsuariosAsync() => await _usuarios.ContarAsync() > 0;

        public async Task<List<Usuario>> ListarAsync() =>
            (await _usuarios.ListarAsync()).OrderBy(u => u.NombreCompleto).ToList();

        public async Task<Resultado> CrearAdministradorInicialAsync(Usuario usuario, string password)
        {
            if (await ExistenUsuariosAsync())
                return Resultado.Fallo("La configuración inicial ya fue realizada.");
            usuario.Rol = Roles.Administrador;
            return await CrearAsync(usuario, password);
        }

        public async Task<Resultado> CrearAsync(Usuario usuario, string password)
        {
            Normalizar(usuario);
            var error = await ValidarAsync(usuario);
            if (error != null) return Resultado.Fallo(error);

            var errores = PoliticaPassword.Validar(password);
            if (errores.Count > 0) return Resultado.Fallo(string.Join(" ", errores));

            usuario.Id = 0;
            usuario.PasswordHash = _hasher.Hash(password);
            usuario.FechaCreacion = _reloj.Now;
            usuario.Activo = true;
            await _usuarios.GuardarAsync(usuario);
            return Resultado.Ok($"Usuario \"{usuario.NombreUsuario}\" creado.");
        }

        public async Task<Resultado> ActualizarAsync(Usuario cambios)
        {
            var existente = await _usuarios.ObtenerAsync(cambios.Id);
            if (existente == null) return Resultado.Fallo("El usuario no existe.");

            Normalizar(cambios);
            var error = await ValidarAsync(cambios);
            if (error != null) return Resultado.Fallo(error);

            // Nunca dejar el sistema sin un administrador activo.
            var dejaDeSerAdminActivo = existente.Rol == Roles.Administrador && existente.Activo &&
                                       (cambios.Rol != Roles.Administrador || !cambios.Activo);
            if (dejaDeSerAdminActivo && await ContarAdministradoresActivosAsync() <= 1)
                return Resultado.Fallo("Debe existir al menos un administrador activo.");

            existente.NombreUsuario = cambios.NombreUsuario;
            existente.NombreCompleto = cambios.NombreCompleto;
            existente.Correo = cambios.Correo;
            existente.Rol = cambios.Rol;
            existente.Activo = cambios.Activo;
            existente.InstructorId = cambios.Rol == Roles.Instructor ? cambios.InstructorId : 0;
            await _usuarios.GuardarAsync(existente);

            if (_sesion.Actual?.Id == existente.Id) _sesion.Establecer(existente);
            return Resultado.Ok("Usuario actualizado.");
        }

        public async Task<Resultado> CambiarPasswordAsync(int usuarioId, string actual, string nueva)
        {
            var usuario = await _usuarios.ObtenerAsync(usuarioId);
            if (usuario == null) return Resultado.Fallo("El usuario no existe.");
            if (!_hasher.Verificar(actual, usuario.PasswordHash)) return Resultado.Fallo("La contraseña actual no es correcta.");
            if (actual == nueva) return Resultado.Fallo("La nueva contraseña debe ser distinta a la actual.");
            return await EstablecerPasswordAsync(usuarioId, nueva);
        }

        public async Task<Resultado> EstablecerPasswordAsync(int usuarioId, string nueva)
        {
            var errores = PoliticaPassword.Validar(nueva);
            if (errores.Count > 0) return Resultado.Fallo(string.Join(" ", errores));

            var usuario = await _usuarios.ObtenerAsync(usuarioId);
            if (usuario == null) return Resultado.Fallo("El usuario no existe.");

            usuario.PasswordHash = _hasher.Hash(nueva);
            usuario.BloqueadoPorSeguridad = false;
            await _usuarios.GuardarAsync(usuario);
            return Resultado.Ok("Contraseña actualizada.");
        }

        public async Task<Resultado> DesbloquearAsync(int usuarioId)
        {
            var usuario = await _usuarios.ObtenerAsync(usuarioId);
            if (usuario == null) return Resultado.Fallo("El usuario no existe.");
            usuario.BloqueadoPorSeguridad = false;
            await _usuarios.GuardarAsync(usuario);
            return Resultado.Ok($"La cuenta de {usuario.NombreCompleto} fue desbloqueada.");
        }

        public async Task<Resultado> EliminarAsync(int usuarioId, int usuarioActualId)
        {
            if (usuarioId == usuarioActualId) return Resultado.Fallo("No puede eliminar su propia cuenta.");
            var usuario = await _usuarios.ObtenerAsync(usuarioId);
            if (usuario == null) return Resultado.Fallo("El usuario no existe.");
            if (usuario.Rol == Roles.Administrador && usuario.Activo && await ContarAdministradoresActivosAsync() <= 1)
                return Resultado.Fallo("Debe existir al menos un administrador activo.");

            await _usuarios.EliminarAsync(usuarioId);
            return Resultado.Ok("Usuario eliminado.");
        }

        private Task<int> ContarAdministradoresActivosAsync() =>
            _usuarios.ContarAsync(u => u.Rol == Roles.Administrador && u.Activo);

        private static void Normalizar(Usuario u)
        {
            u.NombreUsuario = u.NombreUsuario.Trim();
            u.NombreCompleto = u.NombreCompleto.Trim();
            u.Correo = u.Correo.Trim();
        }

        private async Task<string?> ValidarAsync(Usuario u)
        {
            if (u.NombreUsuario.Length < 3 || u.NombreUsuario.Any(char.IsWhiteSpace))
                return "El nombre de usuario debe tener al menos 3 caracteres y no contener espacios.";
            if (string.IsNullOrWhiteSpace(u.NombreCompleto)) return "El nombre completo es obligatorio.";
            if (!Validaciones.EsCorreoValido(u.Correo)) return "El correo electrónico no es válido (se usa para recuperar la contraseña).";
            if (!Roles.Todos.Contains(u.Rol)) return "Seleccione un rol válido.";

            var nombre = u.NombreUsuario;
            var correo = u.Correo;
            var id = u.Id;
            if (await _usuarios.ContarAsync(x => x.NombreUsuario == nombre && x.Id != id) > 0)
                return "Ya existe un usuario con ese nombre de usuario.";
            if (await _usuarios.ContarAsync(x => x.Correo == correo && x.Id != id) > 0)
                return "Ya existe un usuario con ese correo electrónico.";
            return null;
        }
    }
}
