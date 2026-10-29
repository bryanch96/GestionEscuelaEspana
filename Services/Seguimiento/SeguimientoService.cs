using GestionEscuelaEspana.Data.Repositorios;
using GestionEscuelaEspana.Models;
using GestionEscuelaEspana.Services.Common;
using GestionEscuelaEspana.Services.Configuracion;
using GestionEscuelaEspana.Services.Email;
using GestionEscuelaEspana.Services.Plataforma;
using GestionEscuelaEspana.Services.Security;

namespace GestionEscuelaEspana.Services.Seguimiento
{
    public interface ISeguimientoService
    {
        Task<List<NotaSeguimiento>> ListarAsync(int estudianteId);
        Task<Resultado<NotaSeguimiento>> GuardarNotaAsync(NotaSeguimiento nota, IEnumerable<ArchivoSeleccionado> nuevosAdjuntos);
        Task<Resultado> EliminarNotaAsync(int notaId);
        Task<Resultado> EliminarAdjuntoAsync(int adjuntoId);

        /// <summary>Envía la nota (texto + fotos/videos) al correo registrado del estudiante.</summary>
        Task<ResultadoEnvio> EnviarAsync(int notaId);
        Task<ResultadoEnvio> EnviarMensajeDirectoAsync(int estudianteId, string asunto, string mensaje);

        /// <summary>Miniatura en formato data URL para previsualizar imágenes dentro del WebView.</summary>
        Task<string?> ObtenerMiniaturaAsync(string rutaLocal, string contentType);
    }

    /// <summary>Notas pedagógicas con evidencia multimedia, almacenadas localmente por estudiante.</summary>
    public sealed class SeguimientoService : ISeguimientoService
    {
        public const long TamanoMaximoArchivo = 100L * 1024 * 1024;
        private const long TamanoMaximoMiniatura = 6L * 1024 * 1024;

        private readonly IRepositorio<NotaSeguimiento> _notas;
        private readonly IRepositorio<AdjuntoSeguimiento> _adjuntos;
        private readonly IRepositorio<Estudiante> _estudiantes;
        private readonly IEmailService _email;
        private readonly IConfiguracionService _configuracion;
        private readonly ISesionUsuario _sesion;
        private readonly IAppPaths _paths;
        private readonly IClock _reloj;

        public SeguimientoService(IRepositorio<NotaSeguimiento> notas, IRepositorio<AdjuntoSeguimiento> adjuntos,
            IRepositorio<Estudiante> estudiantes, IEmailService email, IConfiguracionService configuracion,
            ISesionUsuario sesion, IAppPaths paths, IClock reloj)
        {
            _notas = notas;
            _adjuntos = adjuntos;
            _estudiantes = estudiantes;
            _email = email;
            _configuracion = configuracion;
            _sesion = sesion;
            _paths = paths;
            _reloj = reloj;
        }

        public async Task<List<NotaSeguimiento>> ListarAsync(int estudianteId)
        {
            var notas = await _notas.ListarAsync(n => n.EstudianteId == estudianteId);
            if (notas.Count == 0) return notas;

            var ids = notas.Select(n => n.Id).ToList();
            var adjuntos = await _adjuntos.ListarAsync(a => ids.Contains(a.NotaId));
            var porNota = adjuntos.ToLookup(a => a.NotaId);
            foreach (var nota in notas) nota.Adjuntos = porNota[nota.Id].ToList();

            return notas.OrderByDescending(n => n.Fecha).ToList();
        }

        public async Task<Resultado<NotaSeguimiento>> GuardarNotaAsync(NotaSeguimiento nota, IEnumerable<ArchivoSeleccionado> nuevosAdjuntos)
        {
            var archivos = nuevosAdjuntos.ToList();
            nota.Texto = nota.Texto.Trim();
            nota.Titulo = nota.Titulo.Trim();

            if (nota.EstudianteId <= 0 || await _estudiantes.ObtenerAsync(nota.EstudianteId) == null)
                return Resultado<NotaSeguimiento>.Fallo("Seleccione un estudiante.");
            if (string.IsNullOrWhiteSpace(nota.Texto) && archivos.Count == 0 && nota.Id == 0)
                return Resultado<NotaSeguimiento>.Fallo("Escriba una nota o adjunte al menos una fotografía o video.");
            if (nota.Calificacion is < 0 or > 5)
                return Resultado<NotaSeguimiento>.Fallo("La calificación debe estar entre 1 y 5.");

            var invalido = archivos.FirstOrDefault(a => !EsMultimedia(a.ContentType));
            if (invalido != null)
                return Resultado<NotaSeguimiento>.Fallo($"\"{invalido.NombreOriginal}\" no es una fotografía o video.");
            var grande = archivos.FirstOrDefault(a => a.TamanoBytes > TamanoMaximoArchivo);
            if (grande != null)
                return Resultado<NotaSeguimiento>.Fallo($"\"{grande.NombreOriginal}\" supera el máximo de {TamanoMaximoArchivo / (1024 * 1024)} MB.");

            if (nota.Id == 0)
            {
                nota.Fecha = _reloj.Now;
                nota.AutorUsuarioId = _sesion.Actual?.Id ?? 0;
                nota.AutorNombre = _sesion.Actual?.NombreCompleto ?? "Instructor";
            }
            await _notas.GuardarAsync(nota);

            var carpeta = Path.Combine(_paths.CarpetaDatos, "Adjuntos", nota.EstudianteId.ToString());
            Directory.CreateDirectory(carpeta);

            foreach (var archivo in archivos)
            {
                var destino = Path.Combine(carpeta, $"{Guid.NewGuid():N}{Path.GetExtension(archivo.NombreOriginal)}");
                await using (var origen = File.OpenRead(archivo.RutaTemporal))
                await using (var salida = File.Create(destino))
                    await origen.CopyToAsync(salida);

                var adjunto = new AdjuntoSeguimiento
                {
                    NotaId = nota.Id,
                    NombreArchivo = Path.GetFileName(archivo.NombreOriginal),
                    RutaLocal = destino,
                    ContentType = archivo.ContentType,
                    TamanoBytes = new FileInfo(destino).Length
                };
                await _adjuntos.GuardarAsync(adjunto);
                nota.Adjuntos.Add(adjunto);
            }

            return Resultado<NotaSeguimiento>.Ok(nota, "Nota de seguimiento guardada.");
        }

        public async Task<Resultado> EliminarNotaAsync(int notaId)
        {
            foreach (var adjunto in await _adjuntos.ListarAsync(a => a.NotaId == notaId))
                await EliminarAdjuntoAsync(adjunto.Id);
            await _notas.EliminarAsync(notaId);
            return Resultado.Ok("Nota eliminada.");
        }

        public async Task<Resultado> EliminarAdjuntoAsync(int adjuntoId)
        {
            var adjunto = await _adjuntos.ObtenerAsync(adjuntoId);
            if (adjunto == null) return Resultado.Fallo("El archivo no existe.");
            try
            {
                if (File.Exists(adjunto.RutaLocal)) File.Delete(adjunto.RutaLocal);
            }
            catch (IOException)
            {
                // Si el archivo está en uso, se elimina el registro igualmente; el archivo huérfano no afecta.
            }
            await _adjuntos.EliminarAsync(adjuntoId);
            return Resultado.Ok("Archivo eliminado.");
        }

        public async Task<ResultadoEnvio> EnviarAsync(int notaId)
        {
            var nota = await _notas.ObtenerAsync(notaId);
            if (nota == null) return new ResultadoEnvio(false, false, "La nota no existe.");

            var estudiante = await _estudiantes.ObtenerAsync(nota.EstudianteId);
            if (estudiante == null) return new ResultadoEnvio(false, false, "El estudiante no existe.");
            if (!Validaciones.EsCorreoValido(estudiante.Correo))
                return new ResultadoEnvio(false, false, $"{estudiante.NombreCompleto} no tiene un correo electrónico válido registrado.");

            var adjuntos = (await _adjuntos.ListarAsync(a => a.NotaId == notaId))
                .Where(a => File.Exists(a.RutaLocal))
                .Select(a => new AdjuntoCorreo(a.NombreArchivo, a.RutaLocal, a.ContentType))
                .ToList();

            var config = await _configuracion.ObtenerAsync();
            var resultado = await _email.EnviarAsync(PlantillasCorreo.Seguimiento(estudiante, nota, adjuntos, config.NombreEscuela));

            if (resultado.Exito)
            {
                nota.Enviada = true;
                nota.FechaEnvio = _reloj.Now;
                await _notas.GuardarAsync(nota);
            }
            return resultado;
        }

        public async Task<ResultadoEnvio> EnviarMensajeDirectoAsync(int estudianteId, string asunto, string mensaje)
        {
            if (string.IsNullOrWhiteSpace(asunto) || string.IsNullOrWhiteSpace(mensaje))
                return new ResultadoEnvio(false, false, "Escriba el asunto y el mensaje.");

            var estudiante = await _estudiantes.ObtenerAsync(estudianteId);
            if (estudiante == null) return new ResultadoEnvio(false, false, "El estudiante no existe.");

            var config = await _configuracion.ObtenerAsync();
            var remitente = _sesion.Actual?.NombreCompleto ?? config.NombreEscuela;
            return await _email.EnviarAsync(PlantillasCorreo.MensajeDirecto(estudiante, asunto.Trim(), mensaje.Trim(), remitente, config.NombreEscuela));
        }

        public async Task<string?> ObtenerMiniaturaAsync(string rutaLocal, string contentType)
        {
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || !File.Exists(rutaLocal)) return null;
            if (new FileInfo(rutaLocal).Length > TamanoMaximoMiniatura) return null;
            var bytes = await File.ReadAllBytesAsync(rutaLocal);
            return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }

        private static bool EsMultimedia(string contentType) =>
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    }
}
