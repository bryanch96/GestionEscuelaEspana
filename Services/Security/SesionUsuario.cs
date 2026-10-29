using System.Security.Claims;
using GestionEscuelaEspana.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace GestionEscuelaEspana.Services.Security
{
    /// <summary>Mantiene el usuario autenticado durante la vida de la aplicación.</summary>
    public interface ISesionUsuario
    {
        Usuario? Actual { get; }
        bool Autenticado { get; }
        bool EsAdministrador { get; }
        bool TieneRol(params string[] roles);
        void Establecer(Usuario? usuario);
        event Action? Cambio;
    }

    public sealed class SesionUsuario : ISesionUsuario
    {
        public Usuario? Actual { get; private set; }
        public bool Autenticado => Actual != null;
        public bool EsAdministrador => TieneRol(Roles.Administrador);
        public event Action? Cambio;

        public bool TieneRol(params string[] roles) => Actual != null && roles.Contains(Actual.Rol);

        public void Establecer(Usuario? usuario)
        {
            Actual = usuario;
            Cambio?.Invoke();
        }
    }

    /// <summary>
    /// Puente entre la sesión local y el sistema de autorización de Blazor
    /// (habilita &lt;AuthorizeView&gt; y [Authorize(Roles = ...)] en las páginas).
    /// </summary>
    public sealed class AppAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
    {
        private readonly ISesionUsuario _sesion;

        public AppAuthenticationStateProvider(ISesionUsuario sesion)
        {
            _sesion = sesion;
            _sesion.Cambio += AlCambiarSesion;
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var usuario = _sesion.Actual;
            if (usuario == null)
                return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

            var identidad = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.NombreUsuario),
                new Claim(ClaimTypes.GivenName, usuario.NombreCompleto),
                new Claim(ClaimTypes.Email, usuario.Correo),
                new Claim(ClaimTypes.Role, usuario.Rol),
            }, authenticationType: "Local");

            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identidad)));
        }

        private void AlCambiarSesion() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

        public void Dispose() => _sesion.Cambio -= AlCambiarSesion;
    }
}
