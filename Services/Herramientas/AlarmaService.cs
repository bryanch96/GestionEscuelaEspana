using GestionEscuelaEspana.Services.Plataforma;

namespace GestionEscuelaEspana.Services.Herramientas
{
    public enum TipoRecordatorio
    {
        Temporizador,
        Alarma
    }

    /// <summary>Temporizador (cuenta regresiva) o alarma (hora específica del día).</summary>
    public sealed class Recordatorio
    {
        public Guid Id { get; } = Guid.NewGuid();
        public TipoRecordatorio Tipo { get; init; }
        public string Etiqueta { get; init; } = string.Empty;
        public TimeSpan DuracionOriginal { get; init; }
        public DateTime Vence { get; internal set; }
        public bool Pausado { get; internal set; }
        public TimeSpan RestanteAlPausar { get; internal set; }
        public bool Sonando { get; internal set; }

        public TimeSpan Restante(DateTime ahora)
        {
            if (Pausado) return RestanteAlPausar;
            var r = Vence - ahora;
            return r < TimeSpan.Zero ? TimeSpan.Zero : r;
        }

        /// <summary>Avance de 0 a 1 (solo temporizadores), útil para barras de progreso.</summary>
        public double Progreso(DateTime ahora) =>
            Tipo == TipoRecordatorio.Temporizador && DuracionOriginal > TimeSpan.Zero
                ? Math.Clamp(1 - Restante(ahora).TotalSeconds / DuracionOriginal.TotalSeconds, 0, 1)
                : 0;
    }

    public interface IAlarmaService
    {
        IReadOnlyList<Recordatorio> Recordatorios { get; }
        Recordatorio AgregarTemporizador(TimeSpan duracion, string etiqueta);

        /// <summary>Programa una alarma para la próxima vez que llegue la hora indicada (hoy o mañana).</summary>
        Recordatorio AgregarAlarma(TimeSpan horaDelDia, string etiqueta);
        void Pausar(Guid id);
        void Reanudar(Guid id);
        void Posponer(Guid id, TimeSpan tiempo);
        void Eliminar(Guid id);

        /// <summary>Revisa vencimientos; lo invoca el temporizador interno cada segundo.</summary>
        void Revisar();

        /// <summary>Se dispara (desde un hilo de fondo) cuando un recordatorio llega a cero.</summary>
        event Action<Recordatorio>? Disparado;

        /// <summary>Notifica cada segundo mientras haya recordatorios activos (para refrescar cuentas regresivas).</summary>
        event Action? Tick;
    }

    /// <summary>
    /// Servicio en memoria de temporizadores y alarmas, compartido por toda la app (singleton) para que
    /// sigan corriendo al cambiar de página. Nota: si el sistema operativo suspende la app en segundo plano,
    /// el aviso se mostrará al volver; para avisos con la app cerrada se requieren notificaciones locales nativas.
    /// </summary>
    public sealed class AlarmaService : IAlarmaService, IDisposable
    {
        private readonly IClock _reloj;
        private readonly List<Recordatorio> _recordatorios = new();
        private readonly object _candado = new();
        private readonly Timer _timer;

        public AlarmaService(IClock reloj)
        {
            _reloj = reloj;
            _timer = new Timer(_ => Revisar(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        public event Action<Recordatorio>? Disparado;
        public event Action? Tick;

        public IReadOnlyList<Recordatorio> Recordatorios
        {
            get { lock (_candado) return _recordatorios.OrderBy(r => r.Restante(_reloj.Now)).ToList(); }
        }

        public Recordatorio AgregarTemporizador(TimeSpan duracion, string etiqueta)
        {
            if (duracion <= TimeSpan.Zero || duracion > TimeSpan.FromHours(24))
                throw new ArgumentOutOfRangeException(nameof(duracion), "La duración debe estar entre 1 segundo y 24 horas.");

            var r = new Recordatorio
            {
                Tipo = TipoRecordatorio.Temporizador,
                Etiqueta = string.IsNullOrWhiteSpace(etiqueta) ? "Temporizador" : etiqueta.Trim(),
                DuracionOriginal = duracion,
                Vence = _reloj.Now.Add(duracion)
            };
            lock (_candado) _recordatorios.Add(r);
            return r;
        }

        public Recordatorio AgregarAlarma(TimeSpan horaDelDia, string etiqueta)
        {
            var ahora = _reloj.Now;
            var vence = ahora.Date.Add(horaDelDia);
            if (vence <= ahora) vence = vence.AddDays(1);

            var r = new Recordatorio
            {
                Tipo = TipoRecordatorio.Alarma,
                Etiqueta = string.IsNullOrWhiteSpace(etiqueta) ? "Alarma" : etiqueta.Trim(),
                Vence = vence
            };
            lock (_candado) _recordatorios.Add(r);
            return r;
        }

        public void Pausar(Guid id) => Modificar(id, r =>
        {
            if (r.Pausado || r.Tipo != TipoRecordatorio.Temporizador) return;
            r.RestanteAlPausar = r.Restante(_reloj.Now);
            r.Pausado = true;
        });

        public void Reanudar(Guid id) => Modificar(id, r =>
        {
            if (!r.Pausado) return;
            r.Vence = _reloj.Now.Add(r.RestanteAlPausar);
            r.Pausado = false;
        });

        public void Posponer(Guid id, TimeSpan tiempo) => Modificar(id, r =>
        {
            r.Vence = _reloj.Now.Add(tiempo);
            r.Sonando = false;
            r.Pausado = false;
        });

        public void Eliminar(Guid id)
        {
            lock (_candado) _recordatorios.RemoveAll(r => r.Id == id);
        }

        public void Revisar()
        {
            List<Recordatorio> vencidos;
            bool hayActivos;
            var ahora = _reloj.Now;
            lock (_candado)
            {
                vencidos = _recordatorios.Where(r => !r.Pausado && !r.Sonando && r.Vence <= ahora).ToList();
                foreach (var r in vencidos) r.Sonando = true;
                hayActivos = _recordatorios.Count > 0;
            }

            foreach (var r in vencidos) Disparado?.Invoke(r);
            if (hayActivos) Tick?.Invoke();
        }

        private void Modificar(Guid id, Action<Recordatorio> accion)
        {
            lock (_candado)
            {
                var r = _recordatorios.FirstOrDefault(x => x.Id == id);
                if (r != null) accion(r);
            }
        }

        public void Dispose() => _timer.Dispose();
    }
}
