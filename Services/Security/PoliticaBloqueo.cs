namespace GestionEscuelaEspana.Services.Security
{
    /// <summary>Acción a tomar tras un intento de inicio de sesión fallido.</summary>
    /// <param name="BloqueoTemporal">Tiempo que la aplicación queda bloqueada (null = sin bloqueo).</param>
    /// <param name="BloquearCuenta">Bloquear la cuenta y enviar correo de recuperación.</param>
    /// <param name="IntentosRestantes">Intentos disponibles antes del primer bloqueo.</param>
    public sealed record DecisionBloqueo(TimeSpan? BloqueoTemporal, bool BloquearCuenta, int IntentosRestantes);

    /// <summary>
    /// Política escalonada de bloqueo por intentos fallidos consecutivos:
    /// 3.º fallo → 3 minutos, 4.º fallo → 10 minutos, 5.º fallo → bloqueo de la cuenta y correo de recuperación
    /// (además de 10 minutos de bloqueo de la aplicación para frenar ataques de fuerza bruta).
    /// </summary>
    public sealed class PoliticaBloqueo
    {
        public int FallosPrimerBloqueo { get; init; } = 3;
        public TimeSpan DuracionPrimerBloqueo { get; init; } = TimeSpan.FromMinutes(3);

        public int FallosSegundoBloqueo { get; init; } = 4;
        public TimeSpan DuracionSegundoBloqueo { get; init; } = TimeSpan.FromMinutes(10);

        public int FallosBloqueoCuenta { get; init; } = 5;

        public DecisionBloqueo Evaluar(int fallosConsecutivos)
        {
            if (fallosConsecutivos >= FallosBloqueoCuenta)
                return new DecisionBloqueo(DuracionSegundoBloqueo, BloquearCuenta: true, IntentosRestantes: 0);

            if (fallosConsecutivos >= FallosSegundoBloqueo)
                return new DecisionBloqueo(DuracionSegundoBloqueo, BloquearCuenta: false, IntentosRestantes: 0);

            if (fallosConsecutivos >= FallosPrimerBloqueo)
                return new DecisionBloqueo(DuracionPrimerBloqueo, BloquearCuenta: false, IntentosRestantes: 0);

            return new DecisionBloqueo(null, BloquearCuenta: false, IntentosRestantes: FallosPrimerBloqueo - fallosConsecutivos);
        }
    }
}
