using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Citas;
using GestionEscuelaEspana.Services.Comunicacion;
using GestionEscuelaEspana.Services.Herramientas;
using Xunit;

namespace GestionEscuelaEspana.Tests
{
    public class CalculadoraTests
    {
        private static CalculadoraEngine Teclear(string teclas)
        {
            var c = new CalculadoraEngine();
            foreach (var t in teclas.Split(' ')) c.ProcesarTecla(t);
            return c;
        }

        [Theory]
        [InlineData("2 + 3 Enter", "5")]
        [InlineData("2 + 3 * 4 Enter", "20")]   // Evaluación secuencial, como una calculadora de escritorio.
        [InlineData(". 1 + . 2 Enter", "0.3")]  // decimal: sin errores de coma flotante.
        [InlineData("1 0 0 + 1 3 % Enter", "113")]
        [InlineData("2 + 3 Enter Enter", "8")]  // "=" repetido repite la operación.
        [InlineData("1 2 3 Backspace", "12")]
        [InlineData("9 / 4 Enter", "2.25")]
        public void Operaciones(string teclas, string esperado) => Assert.Equal(esperado, Teclear(teclas).Pantalla);

        [Fact]
        public void Division_entre_cero_muestra_error_y_se_recupera()
        {
            var c = Teclear("5 / 0 Enter");
            Assert.True(c.EnError);
            c.ProcesarTecla("7");
            Assert.False(c.EnError);
            Assert.Equal("7", c.Pantalla);
        }

        [Fact]
        public void Formato_con_separador_de_miles() => Assert.Equal("1 234 567,5", Teclear("1 2 3 4 5 6 7 . 5").PantallaFormateada.Replace(' ', ' ').Replace(' ', ' '));
    }

    public class AlarmaServiceTests
    {
        [Fact]
        public void Temporizador_se_dispara_al_vencer_y_se_puede_pausar()
        {
            var reloj = new RelojFalso();
            using var servicio = new AlarmaService(reloj);
            var disparados = new List<Recordatorio>();
            servicio.Disparado += disparados.Add;

            var t = servicio.AgregarTemporizador(TimeSpan.FromMinutes(5), "Clase");
            reloj.Avanzar(TimeSpan.FromMinutes(2));
            servicio.Pausar(t.Id);
            reloj.Avanzar(TimeSpan.FromMinutes(10));
            servicio.Revisar();
            Assert.Empty(disparados);
            Assert.Equal(TimeSpan.FromMinutes(3), t.Restante(reloj.Now));

            servicio.Reanudar(t.Id);
            reloj.Avanzar(TimeSpan.FromMinutes(3));
            servicio.Revisar();
            servicio.Revisar(); // No se dispara dos veces.
            Assert.Single(disparados);
        }

        [Fact]
        public void Alarma_para_una_hora_ya_pasada_se_programa_para_mañana()
        {
            var reloj = new RelojFalso(); // 9:00
            using var servicio = new AlarmaService(reloj);
            var a = servicio.AgregarAlarma(TimeSpan.FromHours(8), "Abrir");
            Assert.Equal(reloj.Now.Date.AddDays(1).AddHours(8), a.Vence);
        }
    }

    public class ComunicacionTests
    {
        [Theory]
        [InlineData("8888-7777", "50688887777")]
        [InlineData("+1 (305) 555-0100", "13055550100")]
        [InlineData("00 34 612 345 678", "34612345678")]
        [InlineData("506 8888 7777", "50688887777")]
        [InlineData("123", null)]
        public void Normaliza_telefonos_para_whatsapp(string entrada, string? esperado) =>
            Assert.Equal(esperado, EnlacesComunicacion.NormalizarTelefono(entrada, "506"));

        [Fact]
        public void Enlace_wa_me_con_mensaje_codificado() =>
            Assert.Equal("https://wa.me/50688887777?text=Hola%20Ana%2C%20%C2%BFc%C3%B3mo%20est%C3%A1%3F",
                EnlacesComunicacion.WhatsApp("8888 7777", "506", "Hola Ana, ¿cómo está?"));

        [Fact]
        public void Enlace_mailto() =>
            Assert.Equal("mailto:ana%40correo.com?subject=Hola&body=L%C3%ADnea%201",
                EnlacesComunicacion.Mailto("ana@correo.com", "Hola", "Línea 1"));
    }

    public class ExportadorTests
    {
        [Fact]
        public void Escapa_separadores_y_neutraliza_formulas()
        {
            Assert.Equal("\"Pérez; Ana\"", ExportadorCitas.Escapar("Pérez; Ana"));
            Assert.Equal("'=HYPERLINK(1)", ExportadorCitas.Escapar("=HYPERLINK(1)"));
            Assert.Equal("\"dice \"\"hola\"\"\"", ExportadorCitas.Escapar("dice \"hola\""));
        }

        [Fact]
        public void Genera_encabezado_y_una_fila_por_cita()
        {
            var csv = ExportadorCitas.ACsv(new[] { new Cita { Id = 7, ClienteNombre = "Ana", Fecha = new DateTime(2026, 10, 29), Precio = 15000.5m } });
            var lineas = csv.TrimEnd().Split(Environment.NewLine);
            Assert.Equal(2, lineas.Length);
            Assert.StartsWith("7;2026-10-29;", lineas[1]);
            Assert.Contains(";15000.5;", lineas[1]);
        }
    }
}
