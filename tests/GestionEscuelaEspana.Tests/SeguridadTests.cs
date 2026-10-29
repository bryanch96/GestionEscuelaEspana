using System.Text.RegularExpressions;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Security;
using Xunit;

namespace GestionEscuelaEspana.Tests
{
    public class PoliticaBloqueoTests
    {
        private readonly PoliticaBloqueo _politica = new();

        [Theory]
        [InlineData(1, 2)]
        [InlineData(2, 1)]
        public void Antes_del_tercer_fallo_no_bloquea(int fallos, int restantes)
        {
            var d = _politica.Evaluar(fallos);
            Assert.Null(d.BloqueoTemporal);
            Assert.False(d.BloquearCuenta);
            Assert.Equal(restantes, d.IntentosRestantes);
        }

        [Fact]
        public void Tercer_fallo_bloquea_3_minutos() => Assert.Equal(TimeSpan.FromMinutes(3), _politica.Evaluar(3).BloqueoTemporal);

        [Fact]
        public void Cuarto_fallo_bloquea_10_minutos()
        {
            var d = _politica.Evaluar(4);
            Assert.Equal(TimeSpan.FromMinutes(10), d.BloqueoTemporal);
            Assert.False(d.BloquearCuenta);
        }

        [Fact]
        public void Quinto_fallo_bloquea_la_cuenta() => Assert.True(_politica.Evaluar(5).BloquearCuenta);
    }

    public class PasswordTests
    {
        private readonly Pbkdf2PasswordHasher _hasher = new();

        [Fact]
        public void Verifica_la_contraseña_correcta_y_rechaza_la_incorrecta()
        {
            var hash = _hasher.Hash("Segura123");
            Assert.True(_hasher.Verificar("Segura123", hash));
            Assert.False(_hasher.Verificar("segura123", hash));
            Assert.DoesNotContain("Segura123", hash);
        }

        [Fact]
        public void La_sal_es_distinta_en_cada_hash() => Assert.NotEqual(_hasher.Hash("Igual123"), _hasher.Hash("Igual123"));

        [Fact]
        public void Un_hash_malformado_no_lanza_excepcion() => Assert.False(_hasher.Verificar("x", "no-es-un-hash"));

        [Theory]
        [InlineData("corta1A", false)]
        [InlineData("sinmayuscula1", false)]
        [InlineData("SINMINUSCULA1", false)]
        [InlineData("SinNumeros", false)]
        [InlineData("Valida2026", true)]
        public void Politica_de_complejidad(string password, bool valida) =>
            Assert.Equal(valida, PoliticaPassword.Validar(password).Count == 0);
    }

    public class AutenticacionTests
    {
        private static async Task<Usuario> CrearAdminAsync(Entorno e)
        {
            var admin = new Usuario { NombreUsuario = "admin", NombreCompleto = "Ana Admin", Correo = "ana@escuela.cr" };
            var r = await e.Get<IUsuarioService>().CrearAdministradorInicialAsync(admin, "Clave2026");
            Assert.True(r.Exito, r.Mensaje);
            return admin;
        }

        [Fact]
        public async Task Login_correcto_por_usuario_o_correo_sin_distinguir_mayusculas()
        {
            await using var e = new Entorno();
            await CrearAdminAsync(e);
            var auth = e.Get<IAuthService>();

            Assert.True((await auth.IniciarSesionAsync("ADMIN", "Clave2026")).Exito);
            Assert.True((await auth.IniciarSesionAsync("Ana@Escuela.cr", "Clave2026")).Exito);
            Assert.True(e.Get<ISesionUsuario>().EsAdministrador);
        }

        [Fact]
        public async Task Bloqueo_escalonado_3_10_minutos_y_bloqueo_de_cuenta_con_correo()
        {
            await using var e = new Entorno();
            await CrearAdminAsync(e);
            var auth = e.Get<IAuthService>();

            // Fallos 1 y 2: solo advierten.
            Assert.Equal(TipoResultadoLogin.CredencialesInvalidas, (await auth.IniciarSesionAsync("admin", "mala")).Tipo);
            Assert.Equal(TipoResultadoLogin.CredencialesInvalidas, (await auth.IniciarSesionAsync("admin", "mala")).Tipo);

            // Fallo 3: 3 minutos de bloqueo; ni siquiera la contraseña correcta entra.
            var tercero = await auth.IniciarSesionAsync("admin", "mala");
            Assert.Equal(TipoResultadoLogin.AplicacionBloqueada, tercero.Tipo);
            Assert.Equal(e.Reloj.UtcNow.AddMinutes(3), tercero.BloqueadoHastaUtc);
            Assert.Equal(TipoResultadoLogin.AplicacionBloqueada, (await auth.IniciarSesionAsync("admin", "Clave2026")).Tipo);

            // Fallo 4 (tras esperar): 10 minutos.
            e.Reloj.Avanzar(TimeSpan.FromMinutes(3));
            var cuarto = await auth.IniciarSesionAsync("admin", "mala");
            Assert.Equal(TipoResultadoLogin.AplicacionBloqueada, cuarto.Tipo);
            Assert.Equal(e.Reloj.UtcNow.AddMinutes(10), cuarto.BloqueadoHastaUtc);

            // Fallo 5: la cuenta se bloquea y se envía automáticamente el correo de recuperación.
            e.Reloj.Avanzar(TimeSpan.FromMinutes(10));
            var quinto = await auth.IniciarSesionAsync("admin", "mala");
            Assert.Equal(TipoResultadoLogin.CuentaBloqueada, quinto.Tipo);
            var correo = Assert.Single(e.Correo.Enviados);
            Assert.Equal("ana@escuela.cr", correo.Para);
            Assert.Equal("Recuperación", correo.Categoria);

            // Aunque pase el bloqueo temporal, la cuenta exige recuperar la contraseña.
            e.Reloj.Avanzar(TimeSpan.FromMinutes(10));
            Assert.Equal(TipoResultadoLogin.CuentaBloqueada, (await auth.IniciarSesionAsync("admin", "Clave2026")).Tipo);

            // Recuperación con el código recibido: desbloquea y permite entrar con la nueva contraseña.
            var codigo = Regex.Match(correo.CuerpoTexto, @"\b\d{6}\b").Value;
            var recuperacion = e.Get<IRecuperacionPasswordService>();
            var incorrecto = codigo == "000000" ? "111111" : "000000";
            Assert.False((await recuperacion.RestablecerAsync("ana@escuela.cr", incorrecto, "Nueva2026")).Exito);
            Assert.True((await recuperacion.RestablecerAsync("ana@escuela.cr", codigo, "Nueva2026")).Exito);
            Assert.True((await auth.IniciarSesionAsync("admin", "Nueva2026")).Exito);

            // El código es de un solo uso.
            Assert.False((await recuperacion.RestablecerAsync("ana@escuela.cr", codigo, "Otra2026x")).Exito);
        }

        [Fact]
        public async Task El_bloqueo_persiste_aunque_se_reinicie_la_aplicacion()
        {
            await using var e = new Entorno();
            await CrearAdminAsync(e);
            for (var i = 0; i < 3; i++) await e.Get<IAuthService>().IniciarSesionAsync("admin", "mala");

            // Un nuevo AuthService (como tras reiniciar) lee el estado desde SQLite.
            var nuevo = new AuthService(e.Get<Data.AppDatabase>(), e.Get<Data.Repositorios.IRepositorio<Usuario>>(), e.Get<IPasswordHasher>(),
                e.Get<PoliticaBloqueo>(), e.Get<IRecuperacionPasswordService>(), new SesionUsuario(), e.Reloj,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance);
            Assert.NotNull(await nuevo.ObtenerBloqueoVigenteAsync());
        }

        [Fact]
        public async Task Recuperacion_valida_que_el_correo_este_registrado_y_limita_solicitudes()
        {
            await using var e = new Entorno();
            await CrearAdminAsync(e);
            var recuperacion = e.Get<IRecuperacionPasswordService>();

            Assert.False((await recuperacion.SolicitarCodigoAsync("nadie@escuela.cr")).Exito);
            Assert.True((await recuperacion.SolicitarCodigoAsync("ana@escuela.cr")).Exito);
            Assert.False((await recuperacion.SolicitarCodigoAsync("ana@escuela.cr")).Exito); // Antes de 60 s.
            e.Reloj.Avanzar(TimeSpan.FromSeconds(61));
            Assert.True((await recuperacion.SolicitarCodigoAsync("ana@escuela.cr")).Exito);
        }

        [Fact]
        public async Task No_se_puede_dejar_el_sistema_sin_administrador_activo()
        {
            await using var e = new Entorno();
            var admin = await CrearAdminAsync(e);
            admin.Rol = Roles.Recepcionista;
            Assert.False((await e.Get<IUsuarioService>().ActualizarAsync(admin)).Exito);
        }
    }
}
