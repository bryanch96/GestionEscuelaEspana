// Utilidades JavaScript mínimas para la interfaz (sonido de alarmas y portapapeles).
window.escuela = (function () {
    let contexto = null;
    let intervalo = null;

    function pitido() {
        try {
            contexto = contexto || new (window.AudioContext || window.webkitAudioContext)();
            const ahora = contexto.currentTime;
            [0, 0.25, 0.5].forEach(function (desfase) {
                const osc = contexto.createOscillator();
                const gan = contexto.createGain();
                osc.type = "sine";
                osc.frequency.value = 880;
                gan.gain.setValueAtTime(0.0001, ahora + desfase);
                gan.gain.exponentialRampToValueAtTime(0.35, ahora + desfase + 0.02);
                gan.gain.exponentialRampToValueAtTime(0.0001, ahora + desfase + 0.18);
                osc.connect(gan).connect(contexto.destination);
                osc.start(ahora + desfase);
                osc.stop(ahora + desfase + 0.2);
            });
            if (navigator.vibrate) navigator.vibrate([200, 100, 200]);
        } catch (e) {
            console.warn("No se pudo reproducir el sonido de la alarma", e);
        }
    }

    return {
        sonarAlarma: function () {
            if (intervalo) return;
            pitido();
            intervalo = setInterval(pitido, 1500);
        },
        detenerAlarma: function () {
            if (intervalo) clearInterval(intervalo);
            intervalo = null;
        },
        copiar: function (texto) {
            if (navigator.clipboard) return navigator.clipboard.writeText(texto);
        }
    };
})();
