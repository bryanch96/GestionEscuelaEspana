# Escuela de Manejo España — Sistema de Control Operativo

Aplicación multiplataforma (Windows, Android, iOS y macOS) construida con **.NET MAUI Blazor Hybrid**, **Radzen Blazor** y **SQLite** local.

## Arquitectura

```
Models/                 Entidades SQLite y catálogos (estados, roles, vehículos, planteles)
Data/                   AppDatabase (conexión y creación automática del esquema) + repositorios
Services/               Lógica de negocio desacoplada de la UI y de la plataforma
  Common/               Resultado, validaciones, búsqueda tolerante a tildes
  Security/             Hash PBKDF2, política de bloqueo, autenticación, recuperación, usuarios
  Email/                SMTP (MailKit), envío simulado (.eml), plantillas HTML, bitácora
  Comunicacion/         Enlaces de WhatsApp (wa.me) y mailto
  Citas/ Instructores/ Estudiantes/ Seguimiento/ Dashboard/ Herramientas/ Configuracion/
  Plataforma/           Interfaces de capacidades del dispositivo (rutas, cámara, lanzador, secretos)
Infrastructure/Maui/    Implementaciones MAUI de esas interfaces
Components/             Páginas y componentes Blazor
tests/                  Pruebas xUnit de la lógica de negocio (net8.0)
```

Se aplican los principios SOLID: cada servicio expone una interfaz y se registra por inyección de dependencias (`Services/ServiceCollectionExtensions.cs`). La UI nunca accede directamente a SQLite ni a las APIs del sistema operativo.

## Base de datos

- Archivo `escuela_espana.db3` en la carpeta local de la aplicación. Es la misma ruta que usaba la versión anterior, así que los datos existentes se conservan.
- El esquema se crea o completa solo la primera vez que se usa (`AppDatabase`), sin herramientas externas de migración. Las columnas nuevas se agregan automáticamente y se rellenan con valores por defecto.
- Las citas de versiones anteriores generan automáticamente las fichas de estudiante.

## Primer uso

Al abrir la app sin usuarios aparece **Configuración inicial**, donde se crea el administrador principal. No hay contraseñas predeterminadas.

## Seguridad

| Intentos fallidos consecutivos | Acción |
|---|---|
| 1–2 | Aviso con los intentos restantes |
| 3.º | Aplicación bloqueada **3 minutos** |
| 4.º | Aplicación bloqueada **10 minutos** |
| 5.º | **Cuenta bloqueada** y envío automático del código de recuperación al correo del usuario |

- El contador se guarda en SQLite, por lo que reiniciar la app no evita el bloqueo.
- Las contraseñas se guardan con PBKDF2-SHA256 (100 000 iteraciones y sal por usuario).
- **¿Olvidó su contraseña?** comprueba que el correo esté registrado y envía un código de 6 dígitos de un solo uso. El código vence en 15 minutos, admite 5 intentos y solo puede pedirse uno por minuto.
- Roles: **Administrador** (todo), **Recepcionista** (agenda, instructores, historial) e **Instructor** (historial y Profesor en Línea; si está vinculado a un instructor, solo ve sus propias citas).

## Correo electrónico (SMTP)

En **Configuración** se define el servidor (hay preajustes para Gmail, Outlook y Yahoo). La contraseña se guarda cifrada en el almacenamiento seguro del dispositivo.
Mientras SMTP esté desactivado, cada correo se **simula**: se guarda completo, con adjuntos, como archivo `.eml` en `BandejaSalida/`. Todo envío queda registrado en la bitácora.

> En Gmail se necesita una *contraseña de aplicación* (con la verificación en dos pasos activada).

## Módulos

- **Dashboard**: indicadores clave y tarjetas interactivas. Pendientes permite ver, modificar o cancelar; Completadas abre el historial rápido; Instructores abre el perfil y la edición. Incluye reloj destacado, **calculadora** (admite teclado y tiene atajo de IVA) y **alarmas/temporizadores**.
- **Instructores**: estado laboral, licencias y su vencimiento, días y horario, capacidad diaria, carga del día, cumplimiento, horas impartidas y contacto de emergencia.
- **Agendar citas**: correo del cliente con copia digital, opción **"Otro vehículo"** con campo libre, detección de traslapes, advertencias de disponibilidad y licencia, y vista de la agenda del instructor.
- **Profesor en Línea**: búsqueda y selección de estudiantes, notas con calificación, fotos y videos (galería o cámara), envío por correo con **doble confirmación**, y chat por correo o **WhatsApp** (`https://wa.me/...`).
- **Historial**: filtros por fecha (con atajos), instructor, estado, plantel y texto; resumen de totales; paginación; acciones rápidas; reagendar; exportación a CSV.

## Pruebas

```bash
dotnet test tests/GestionEscuelaEspana.Tests
```

Cubren la política de bloqueo, el flujo completo de bloqueo y recuperación, las reglas de agenda, el agendamiento con notificación, la actualización de bases antiguas, el envío simulado, el seguimiento con adjuntos, la calculadora, las alarmas, los enlaces de WhatsApp y la exportación CSV.

## Limitaciones conocidas

- Las alarmas suenan mientras la app está abierta. Para avisar con la app cerrada harían falta notificaciones locales nativas (por ejemplo, `Plugin.LocalNotification`).
- En Windows, el almacenamiento seguro de la contraseña SMTP requiere la app empaquetada (MSIX, que es la configuración por defecto).
