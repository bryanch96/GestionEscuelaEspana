using GestionEscuelaEspana.Components.Shared;
using GestionEscuelaEspana.Infrastructure.Maui;
using GestionEscuelaEspana.Services;
using GestionEscuelaEspana.Services.Plataforma;
using Microsoft.Extensions.Logging;
using Radzen;

namespace GestionEscuelaEspana
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddRadzenComponents();

            // Servicios específicos de la plataforma (MAUI Essentials)
            builder.Services.AddSingleton<IAppPaths, MauiAppPaths>();
            builder.Services.AddSingleton<IAppLauncher, MauiAppLauncher>();
            builder.Services.AddSingleton<ISecretStore, MauiSecretStore>();
            builder.Services.AddSingleton<IMediaService, MauiMediaService>();
            builder.Services.AddSingleton<IShareService, MauiShareService>();

            // Datos, seguridad y módulos de negocio (la base de datos se crea al primer uso)
            builder.Services.AddEscuelaServices();
            builder.Services.AddScoped<AccionesCita>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
