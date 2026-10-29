using System.Globalization;

namespace GestionEscuelaEspana.Services.Herramientas
{
    /// <summary>
    /// Motor de una calculadora básica (estilo calculadora de escritorio) independiente de la UI.
    /// Usa <see cref="decimal"/> para evitar errores de redondeo en montos de dinero y no evalúa
    /// texto arbitrario (sin "eval"), por lo que es seguro.
    /// </summary>
    public sealed class CalculadoraEngine
    {
        private const int MaxDigitos = 16;
        private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

        private decimal? _acumulado;
        private char? _operador;
        private decimal? _ultimoOperando;
        private char? _ultimoOperador;
        private bool _nuevaEntrada = true;

        public string Pantalla { get; private set; } = "0";
        public string Expresion { get; private set; } = string.Empty;
        public bool EnError { get; private set; }

        public decimal Valor => decimal.Parse(Pantalla, Invariante);

        /// <summary>Pantalla con separadores de miles para mostrar al usuario.</summary>
        public string PantallaFormateada
        {
            get
            {
                if (EnError) return Pantalla;
                var partes = Pantalla.Split('.');
                var entero = decimal.Parse(partes[0] == "-" || partes[0] == "-0" ? "0" : partes[0], Invariante);
                var signo = Pantalla.StartsWith('-') ? "-" : string.Empty;
                var texto = Math.Abs(entero).ToString("#,0", CultureInfo.GetCultureInfo("es-CR"));
                return partes.Length > 1 ? $"{signo}{texto},{partes[1]}" : $"{signo}{texto}";
            }
        }

        public void Digito(int d)
        {
            if (d is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(d));
            if (EnError) Limpiar();

            if (_nuevaEntrada)
            {
                Pantalla = d.ToString(Invariante);
                _nuevaEntrada = false;
            }
            else if (Pantalla == "0")
            {
                Pantalla = d.ToString(Invariante);
            }
            else if (Pantalla.Count(char.IsDigit) < MaxDigitos)
            {
                Pantalla += d.ToString(Invariante);
            }
        }

        public void Punto()
        {
            if (EnError) Limpiar();
            if (_nuevaEntrada)
            {
                Pantalla = "0.";
                _nuevaEntrada = false;
            }
            else if (!Pantalla.Contains('.'))
            {
                Pantalla += ".";
            }
        }

        /// <summary>Operadores admitidos: + − × ÷ (también acepta - * /).</summary>
        public void Operador(char op)
        {
            if (EnError) return;
            op = NormalizarOperador(op);

            if (_operador != null && _acumulado != null && !_nuevaEntrada)
            {
                if (!Calcular(_acumulado.Value, Valor, _operador.Value, out var resultado)) return;
                _acumulado = resultado;
                Pantalla = Formatear(resultado);
            }
            else if (_operador == null || _acumulado == null)
            {
                _acumulado = Valor;
            }

            _operador = op;
            _nuevaEntrada = true;
            Expresion = $"{Formatear(_acumulado!.Value)} {op}";
        }

        public void Igual()
        {
            if (EnError) return;

            decimal a, b;
            char op;
            if (_operador != null && _acumulado != null)
            {
                a = _acumulado.Value;
                b = _nuevaEntrada ? _acumulado.Value : Valor;
                op = _operador.Value;
            }
            else if (_ultimoOperador != null && _ultimoOperando != null)
            {
                // Pulsar "=" repetidamente repite la última operación (2 + 3 = = → 8).
                a = Valor;
                b = _ultimoOperando.Value;
                op = _ultimoOperador.Value;
            }
            else
            {
                return;
            }

            if (!Calcular(a, b, op, out var resultado)) return;

            Expresion = $"{Formatear(a)} {op} {Formatear(b)} =";
            Pantalla = Formatear(resultado);
            _ultimoOperando = b;
            _ultimoOperador = op;
            _acumulado = null;
            _operador = null;
            _nuevaEntrada = true;
        }

        /// <summary>Con + o − calcula el porcentaje sobre el acumulado (100 + 13 % → 113); si no, divide entre 100.</summary>
        public void Porcentaje()
        {
            if (EnError) return;
            var valor = _acumulado != null && _operador is '+' or '−'
                ? _acumulado.Value * Valor / 100m
                : Valor / 100m;
            Pantalla = Formatear(valor);
            _nuevaEntrada = false;
        }

        public void CambiarSigno()
        {
            if (EnError || Pantalla == "0") return;
            Pantalla = Pantalla.StartsWith('-') ? Pantalla[1..] : "-" + Pantalla;
        }

        public void Retroceso()
        {
            if (EnError) { Limpiar(); return; }
            if (_nuevaEntrada) return;
            Pantalla = Pantalla.Length <= 1 || (Pantalla.Length == 2 && Pantalla.StartsWith('-')) ? "0" : Pantalla[..^1];
        }

        /// <summary>CE: borra solo la entrada actual.</summary>
        public void LimpiarEntrada()
        {
            if (EnError) { Limpiar(); return; }
            Pantalla = "0";
            _nuevaEntrada = true;
        }

        /// <summary>C: reinicia por completo.</summary>
        public void Limpiar()
        {
            _acumulado = null;
            _operador = null;
            _ultimoOperando = null;
            _ultimoOperador = null;
            _nuevaEntrada = true;
            EnError = false;
            Pantalla = "0";
            Expresion = string.Empty;
        }

        /// <summary>Procesa una tecla del teclado físico. Devuelve true si la tecla fue reconocida.</summary>
        public bool ProcesarTecla(string tecla)
        {
            switch (tecla)
            {
                case { Length: 1 } when char.IsDigit(tecla[0]): Digito(tecla[0] - '0'); return true;
                case "." or ",": Punto(); return true;
                case "+" or "-" or "*" or "/": Operador(tecla[0]); return true;
                case "Enter" or "=": Igual(); return true;
                case "%": Porcentaje(); return true;
                case "Backspace": Retroceso(); return true;
                case "Escape": Limpiar(); return true;
                case "Delete": LimpiarEntrada(); return true;
                default: return false;
            }
        }

        private bool Calcular(decimal a, decimal b, char op, out decimal resultado)
        {
            resultado = 0;
            try
            {
                if (op == '÷' && b == 0)
                {
                    MostrarError("No se puede dividir entre 0");
                    return false;
                }
                resultado = op switch
                {
                    '+' => a + b,
                    '−' => a - b,
                    '×' => a * b,
                    '÷' => a / b,
                    _ => throw new InvalidOperationException($"Operador desconocido {op}")
                };
                return true;
            }
            catch (OverflowException)
            {
                MostrarError("Desbordamiento");
                return false;
            }
        }

        private void MostrarError(string mensaje)
        {
            EnError = true;
            Pantalla = mensaje;
            Expresion = string.Empty;
            _acumulado = null;
            _operador = null;
            _nuevaEntrada = true;
        }

        private static char NormalizarOperador(char op) => op switch
        {
            '-' => '−',
            '*' or 'x' or 'X' => '×',
            '/' => '÷',
            '+' or '−' or '×' or '÷' => op,
            _ => throw new ArgumentException($"Operador no admitido: {op}", nameof(op))
        };

        private static string Formatear(decimal valor)
        {
            var redondeado = Math.Round(valor, 10);
            return redondeado.ToString("0.##########", Invariante);
        }
    }
}
