# Pendientes

Lista corta de lo que se sabe que falta y todavía no tiene spec. No es un plan: cada punto necesita su spec aprobada
antes de tocar código. Estado a 11 de noviembre de 2026.

El plan de la primera auditoría (que numeraba «siete specs») no se guardó en el repositorio y no se va a
reconstruir. De sus specs 6 (`ux-quick-wins.md`) y 7 solo se conoce lo que aparece aquí.

## Con spec propuesta

- **Pruebas sin esperas reales y documentación al día:** `docs/specs/test-waits-and-docs.md`, pendiente de aprobar.

## Sin spec todavía

1. **Conservar el álbum o artista abierto tras guardar etiquetas.** Hoy, al guardar, la vista vuelve a la lista.
   Es lo único que se conoce de la antigua spec de mejoras de uso (anotado en `ui-responsiveness.md`, «Out of
   scope»).
2. **`PlaylistManager` no serializa sus operaciones.** Dos operaciones a la vez parten de la misma lista
   (`_playlists`) y la segunda en guardar descarta el cambio de la primera. `LibraryService` ya lo resuelve con
   `ExclusiveAsync`. Anotado en `library-data-safety-2.md`. Camino real: «Nueva playlist…» desde el menú de una
   canción hace `CreateAsync` y luego `AddTracksAsync`; si entre los dos llega otro cambio, se pierde uno.
3. **Los `Bitmap` de las carátulas no se liberan con `Dispose`.** La carátula grande de «ahora suena»
   (`MainViewModel.NowPlayingCover`), la vista previa del editor (`TagEditorViewModel.CoverPreview`), las
   miniaturas que salen de la caché (`LruCache` no avisa de lo que expulsa; `CoverArtLoader.Invalidate` solo las
   quita) y una carátula grande que llega tras cancelarse su carga quedan para el recolector. No es una fuga, pero
   la memoria nativa tarda en devolverse y no se ha medido cuánto. Antes de cambiar nada, medir: un `Dispose` de
   un bitmap que una fila todavía dibuja es peor que el problema.
4. **`IslandWindow.HideFaded` es `async void` con una espera.** Si la ventana se cierra durante el fundido (salir
   de la app con la isla ocultándose), la continuación llama a `Hide()` sobre una ventana cerrada. No verificable
   por lectura: depende de si Avalonia sigue despachando tras `Exit`. Si se confirma, basta comprobar un indicador
   de «cerrada» tras el `await`.

## Aplazado a propósito

- **R6 y el congelamiento con unidades de red** (`playback-resilience.md`, `ui-responsiveness.md`): spec propia,
  después de la release. Hace asíncrono y cancelable el arranque de una pista, que es el cambio más delicado de
  todos. R6 no se ha reproducido nunca: se empieza por intentarlo con el retraso temporal en `Post`, como dice su
  criterio 10.
- **Resto de la auditoría de solo lectura:** del enfoque F (rendimiento) solo se midió el relleno por palabra
  (`lyrics-render-perf.md`); el XAML no se ha repasado línea a línea contra las reglas de colores y botones.
