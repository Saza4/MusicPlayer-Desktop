# Spec: quitar una pista y avanzar hacia una pista ausente

Estado: implementada el 19 de noviembre de 2026, **a falta de la prueba a mano del autor** (lista en «Resultado»).
Compila con 0 errores y la advertencia CS8602 de siempre, y pasan las 1061 pruebas (850 en Core, 211 en
Infrastructure). No añade pruebas: la lógica nueva vive en `MainViewModel` (Desktop) y se apoya en `WriteSessions`,
que ya tiene las suyas en Core.

## Context

Dos fallos de `docs/pendientes.md` más un hallazgo de la auditoría del 19 de noviembre de 2026. Los tres afectan al
empalme sin silencio o a la coherencia entre lo que suena y lo que se muestra:

| Dónde | Qué pasaba |
| ----- | ---------- |
| `MainViewModel.RemoveTrackAsync` (antiguo punto 9) | `PrepareNext(null)` al empezar y `UpdateGapless()` solo si todo salía bien. Si el guardado fallaba, la pista siguiente quedaba sin preparar: silencio en el siguiente cambio de pista. |
| El mismo método (auditoría) | Borraba el audio sin sesión de escritura (CLAUDE.md lo exige para cualquier herramienta que escriba en audio). Mientras esperaba a `LibraryService`, un avance automático podía volver a preparar el archivo que se iba a borrar, y en Windows el borrado fallaba. |
| `MainViewModel.OnAdvancedToNext` (caso vecino de R6, `pendientes.md`, «Aplazado») | Si la cola cambiaba justo antes del empalme y el archivo de la pista que tocaba ya no existía, `StartPlayback` salía sin hacer nada: sonaba la pista preparada antes del cambio y se mostraba la anterior. |

## FEATURE: Quitar una pista no deja hueco y lo que suena es lo que se muestra

### ACCEPTANCE CRITERIA

1. «Quitar de la biblioteca» y «Eliminar del disco…» van dentro de una sesión de `WriteSessions`, abierta después
   de detener la pista si es la que suena y cerrada en el `finally`.
2. Si la pista quitada es la que suena, se detiene y **no** se reanuda al cerrar la sesión.
3. Mientras la sesión está abierta no se prepara ningún archivo (`UpdateGapless` ya lo respeta).
4. Al cerrarse la sesión, salga bien o mal, se vuelve a preparar la siguiente pista (`ResumeInterrupted` llama a
   `UpdateGapless`).
5. `StartPlayback` devuelve `false` si no pudo arrancar. Las llamadas que no lo necesitan lo ignoran.
6. En `OnAdvancedToNext`, si la pista que toca no puede arrancar, se busca la siguiente disponible como al terminar
   una pista (`MoveNextAfterEnd(CheckAvailable)`). Si no hay ninguna, se detiene, y la línea de estado conserva el
   aviso de la pista no disponible.

### OUT OF SCOPE

- R6 y el congelamiento con unidades de red: arranque asíncrono y cancelable, con su propia spec, aplazado.
- `ConfirmPruneAsync` («Quitar no disponibles»): no borra archivos ni suelta la pista preparada, así que no deja
  hueco.

### EDGE CASES

- **Se borra el archivo y falla el guardado:** la pista sigue en la cola. Al cerrar la sesión se intenta preparar;
  `BassAudioPlayer.PrepareNext` no abre un archivo que no existe (`Open` devuelve `null`) y al terminar la actual
  `OnTrackEnded` la salta con `CheckAvailable`.
- **Otra herramienta escribe a la vez:** el contador de `WriteSessions` hace que solo la última sesión en cerrarse
  reanude lo que se interrumpió.
- **`Begin` falla porque `OnWriting` lanza:** la sesión se cierra sola (`WriteSessions.Begin`) y el `catch` lo
  anota en la línea de estado.

## Resultado

- `src/MusicPlayer.Desktop/ViewModels/MainViewModel.cs`: `RemoveTrackAsync` (sesión y `finally`),
  `StartPlayback` (`bool`) y `OnAdvancedToNext` (rama de la cola cambiada).
- Prueba a mano pendiente (Windows):
  1. Mientras suena una pista con otra detrás, quitar una tercera de la biblioteca: el paso a la siguiente sigue sin
     silencio.
  2. Quitar la pista que suena: se detiene y no vuelve.
  3. «Eliminar del disco…» sobre una pista que no suena: se borra y la reproducción no se interrumpe.
  4. El avance hacia una pista ausente con la cola recién cambiada no se puede provocar a mano con fiabilidad, como
     el caso de `write-after-advance.md`: basta con comprobar que las pruebas 1 a 3 no tienen regresiones.
