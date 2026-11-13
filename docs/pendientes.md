# Pendientes

Lista corta de lo que se sabe que falta y todavía no tiene spec. No es un plan: cada punto necesita su spec aprobada
antes de tocar código. Estado a 12 de noviembre de 2026: los puntos 5 a 9 y las notas del final salen de la
auditoría de solo lectura previa a la release v1.5.0, que sale con ellos sin arreglar
(`docs/specs/release-v1.5.0.md`).

El plan de la primera auditoría (que numeraba «siete specs») no se guardó en el repositorio y no se va a
reconstruir. De sus specs 6 (`ux-quick-wins.md`) y 7 solo se conoce lo que aparece aquí.

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
5. **Los paneles de edición dicen «se detendrá».** `MainWindow.axaml`, en «Editar etiquetas» («Si esta canción
   suena, se detendrá») y en «Limpiar etiquetas» («Si suena alguna de estas canciones, se detendrá»). Desde
   `playback-resilience.md` la pista se reanuda: el texto debe decir que se nota un corte breve.
6. **En pausa, dos saltos no mueven la barra.** `MainViewModel.Previous` (`_player.Seek(TimeSpan.Zero)`) y
   `LyricsViewModel.SeekTo` saltan en el reproductor sin pasar por `PositionSeconds`, y `OnTick` no escribe en
   pausa: la barra, el tiempo y el punto de partida de ← → se quedan donde estaban hasta reanudar. Es la misma
   familia que A1 de `audit-followup.md`, que solo se arregló en `ResumePlayback`. Pasos: pausar pasado el
   segundo 3 y pulsar «Anterior»; o pausar y pulsar una línea de la letra.
7. **Dígitos que no son `0`-`9`.** En .NET, `\d` acepta cualquier dígito Unicode (`０１`, `١٢`) y `int.Parse` no.
   `FileNameParser` (`TagText.cs`, `^(?<n>\d{1,3})…` y `uint.Parse`) lanza `FormatException` con un archivo
   llamado `０１ - título.mp3`, y «Limpiar etiquetas» falla para toda la lista visible. `LrcLyricParser` (las dos
   expresiones de marcas `[mm:ss.xx]` y `ToTimeSpan`) lanza con `[０１:２３.００]`: la pista se queda sin letra e
   «Incrustar letra (.lrc)…» falla con un mensaje en inglés. Arreglo: `[0-9]`, como ya hace la marca por palabra
   (`WordTag`), con una prueba en Core por cada parser.
8. **El aviso de `playlists.json` ilegible se pisa al arrancar.** `MainViewModel.InitializeAsync` llama a
   `Playlists.InitializeAsync()` (que avisa por `Notified`) y enseguida a `ApplySnapshot`, que escribe el resumen
   de la biblioteca en la línea de estado. El motivo solo se ve al intentar cambiar una playlist. Arreglo: guardar
   el aviso y escribirlo al final, como ya se hace con `settingsProblem`.
9. **Quitar una pista con el guardado fallido deja un hueco en el empalme.** `MainViewModel.RemoveTrackAsync`
   llama a `PrepareNext(null)` al empezar y solo vuelve a `UpdateGapless()` si todo sale bien; en el `catch` no.

## Aplazado a propósito

- **R6 y el congelamiento con unidades de red** (`playback-resilience.md`, `ui-responsiveness.md`): spec propia,
  después de la release. Hace asíncrono y cancelable el arranque de una pista, que es el cambio más delicado de
  todos. R6 no se ha reproducido nunca: se empieza por intentarlo con el retraso temporal en `Post`, como dice su
  criterio 10. Va con ellos un caso vecino de `MainViewModel.OnAdvancedToNext`: si la cola cambió justo antes
  del empalme y el archivo de la pista que le toca ya no existe, `StartPlayback` sale sin hacer nada y suena una
  pista distinta de la que se muestra.
- **Resto de la auditoría de solo lectura:** del enfoque F (rendimiento) solo se midió el relleno por palabra
  (`lyrics-render-perf.md`). El XAML se repasó el 12 de noviembre de 2026 contra las reglas de colores y
  botones, sin hallazgos; las pruebas (`tests/`) solo se revisaron con búsquedas.

## Notas cortas (sin prisa)

- **Rutas locales en los binarios publicados.** Los DLL y los `.pdb` de los zip de v1.4.0 y v1.5.0 llevan la ruta
  de compilación del equipo del autor (carpeta de usuario incluida). Desde la próxima release, publicar con
  `-p:ContinuousIntegrationBuild=true` (rutas neutras) y comprobarlo como una fila más de la tabla de la spec.
- **`WindowNative.Restore`, `IsForeground` y `ForceForeground`** llaman a Win32 tras la guarda
  `OperatingSystem.IsWindows()`, pero no dentro de un método con `[SupportedOSPlatform("windows")]` como pide
  CLAUDE.md (sí lo hacen `SetRegionOnWindows` y `CursorOnWindows`). Compila en Ubuntu: es coherencia.
