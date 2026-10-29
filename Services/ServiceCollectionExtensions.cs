using GestionEscuelaEspana.Data;
using GestionEscuelaEspana.Data.Repositorios;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Citas;
using GestionEscuelaEspana.Services.Comunicacion;
using GestionEscuelaEspana.Services.Configuracion;
using GestionEscuelaEspana.Services.Dashboard;
using GestionEscuelaEspana.Services.Email;
using GestionEscuelaEspana.Services.Estudiantes;
using GestionEscuelaEspana.Services.Herramientas;
using GestionEscuelaEspana.Services.Instructores;
using GestionEscuelaEspana.Services.Plataforma;
using GestionEscuelaEspana.Services.Security;
using GestionEscuelaEspana.Services.Seguimiento;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace GestionEscuelaEspana.Services
{
    /// <summary>
    /// Registro de dependencias de la aplicación (independiente de la plataforma).
    /// Las implementaciones de <see cref="IAppPaths"/>, <see cref="IAppLauncher"/>, <see cref="ISecretStore"/>,
    /// <see cref="IMediaService"/> e <see cref="IShareService"/> se registran aparte, en MauiProgram.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddEscuelaServices(this IServiceCollection services)
        {
            // Infraestructura
            services.AddSingleton<IClock, RelojSistema>();
            services.AddSingleton<AppDatabase>();
            services.AddSingleton(typeof(IRepositorio<>), typeof(Repositorio<>));
            services.AddSingleton<ICitaRepositorio, CitaRepositorio>();

            // Seguridad
            services.AddAuthorizationCore();
            services.AddSingleton<ISesionUsuario, SesionUsuario>();
            services.AddScoped<AuthenticationStateProvider, AppAuthenticationStateProvider>();
            services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
            services.AddSingleton(new PoliticaBloqueo());
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IRecuperacionPasswordService, RecuperacionPasswordService>();
            services.AddSingleton<IUsuarioService, UsuarioService>();

            // Configuración, correo y comunicación
            services.AddSingleton<IConfiguracionService, ConfiguracionService>();
            services.AddSingleton<SmtpEmailSender>();
            services.AddSingleton<BandejaSalidaEmailSender>();
            services.AddSingleton<IEmailService, EmailService>();
            services.AddSingleton<ICanalesComunicacion, CanalesComunicacion>();

            // Módulos de negocio
            services.AddSingleton<IEstudianteService, EstudianteService>();
            services.AddSingleton<IInstructorService, InstructorService>();
            services.AddSingleton<ICitaService, CitaService>();
            services.AddSingleton<ISeguimientoService, SeguimientoService>();
            services.AddSingleton<IDashboardService, DashboardService>();

            // Herramientas del dashboard
            services.AddSingleton<IAlarmaService, AlarmaService>();

            return services;
        }
    }
}
