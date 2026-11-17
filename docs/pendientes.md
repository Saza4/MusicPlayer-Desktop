# Pendientes

Lista corta de lo que se sabe que falta y todavía no tiene spec. No es un plan: cada punto necesita su spec aprobada
antes de tocar código. Estado a 12 de noviembre de 2026: los puntos 5 a 9 y las notas del final salen de la
auditoría de solo lectura previa a la release v1.5.0, que sale con ellos sin arreglar
(`docs/specs/release-v1.5.0.md`).

El plan de la primera auditoría (que numeraba «siete specs») no se guardó en el repositorio y no se va a
reconstruir. De sus specs 6 (`ux-quick-wins.md`) y 7 solo se conoce lo que aparece aquí.

## Con spec aprobada (15 de noviembre de 2026, camino a v1.6.0)

Salen del feedback de uso y de la tercera auditoría de solo lectura.

- `docs/specs/audit-3.md`: arreglos de la auditoría. **Hecho y verificado a mano por el autor** el 15 de
  noviembre de 2026 (PR #13, #14 y #15). Resolvió los antiguos puntos 4 (`HideFaded`) y 8
  (aviso de `playlists.json`) de la lista de abajo, que ya no están.
- `docs/specs/playlist-serialization.md`: las operaciones de `PlaylistManager` van una a la vez, como las de
  `LibraryService`. **Hecho** el 16 de noviembre de 2026 (pruebas en Core; no necesita prueba a mano). Era el
  antiguo punto 2 de la lista de abajo.
- `docs/specs/single-seek-path.md`: en pausa, «Anterior» y el clic en una línea de la letra mueven la barra y el
  tiempo (un solo camino de salto, `MainViewModel.SeekTo`). **Hecho** el 16 de noviembre de 2026 y probado
  a mano por el autor ese día. Era el antiguo punto 6 de la lista de abajo.
- `docs/specs/ascii-digits.md`: los parsers solo toman por número los dígitos `0`-`9`. **Hecho** el 16 de
  noviembre de 2026 (pruebas en Core; no necesita prueba a mano). Era el antiguo punto 7 de la lista de abajo.
- `docs/specs/settings-panel.md`: área de Configuración y reproducir con un clic. **Código hecho**; falta la
  prueba a mano del autor (lista en la spec, «Resultado»). Resolvió también el antiguo punto 5 de abajo (los
  paneles de edición decían «se detendrá»).
- `docs/specs/responsive-layout.md`: nada se recorta entre 640×400 y pantalla completa. **Hecho y verificado
  a mano por el autor** el 15 de noviembre de 2026 (PR #16).
- `docs/specs/island-title-and-close.md`: título desplazable y botones de ocultar y salir en la isla. **Código
  hecho**, con el botón «Salir» (M4) incluido en el PR de `background-playback.md`. Probado a mano por el autor (PR #19), que
  pidió además 30 fps y poder arrastrar la barra de progreso de la isla (hechos).
- `docs/specs/background-playback.md`: cerrar oculta a la bandeja, e instancia única. **Código hecho**; falta la
  prueba a mano del autor (lista en la spec, «Resultado»), sobre todo el icono de la bandeja y su menú.
- `docs/specs/lyrics-online-translation.md`: traducir la letra en línea, opt-in.

## Sin spec todavía

1. **Conservar el álbum o artista abierto tras guardar etiquetas.** Hoy, al guardar, la vista vuelve a la lista.
   Es lo único que se conoce de la antigua spec de mejoras de uso (anotado en `ui-responsiveness.md`, «Out of
   scope»).
3. **Los `Bitmap` de las carátulas no se liberan con `Dispose`.** La carátula grande de «ahora suena»
   (`MainViewModel.NowPlayingCover`), la vista previa del editor (`TagEditorViewModel.CoverPreview`), las
   miniaturas que salen de la caché (`LruCache` no avisa de lo que expulsa; `CoverArtLoader.Invalidate` solo las
   quita) y una carátula grande que llega tras cancelarse su carga quedan para el recolector. No es una fuga, pero
   la memoria nativa tarda en devolverse y no se ha medido cuánto. Antes de cambiar nada, medir: un `Dispose` de
   un bitmap que una fila todavía dibuja es peor que el problema.
9. **Quitar una pista con el guardado fallido deja un hueco en el empalme.** `MainViewModel.RemoveTrackAsync`
   llama a `PrepareNext(null)` al empezar y solo vuelve a `UpdateGapless()` si todo sale bien; en el `catch` no.

## Decidido el 15 de noviembre de 2026

- **Escribir en una pista tras un avance automático** (hueco anotado en `audit-3.md`, M2): si la pista avanzó sola
  y su aviso aún no se atendió, `OnTagsSaving` compara con la pista anterior, no descarga la nueva y la escritura
  falla con el archivo abierto (el original queda intacto). **No entra en `background-playback.md`**: no tiene que
  ver con la ventana oculta, y esa spec ya cambia el ciclo de vida de la app. Va como arreglo pequeño aparte:
  que `OnTagsSaving` pregunte al reproductor qué archivo tiene abierto (una propiedad nueva en `IAudioPlayer`) en
  lugar de fiarse de `CurrentTrack`.

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
