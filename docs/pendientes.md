# Pendientes

Lista corta de lo que se sabe que falta. No es un plan: cada punto necesita su spec aprobada antes de tocar código.
Estado a 19 de noviembre de 2026. La v1.5.0 está publicada desde el 12 de noviembre (`docs/specs/release-v1.5.0.md`);
todo lo de abajo va camino de la v1.6.0, que aún no tiene spec de release.

El plan de la primera auditoría (que numeraba «siete specs») no se guardó en el repositorio y no se va a
reconstruir. De sus specs 6 (`ux-quick-wins.md`) y 7 solo se conocía lo que aparecía aquí, y ya está hecho.

## Hecho camino a v1.6.0

Todo está fusionado en `main`.

- `docs/specs/audit-3.md`: arreglos de la tercera auditoría. Hecho y verificado a mano por el autor el 15 de
  noviembre de 2026 (PR #13, #14 y #15).
- `docs/specs/responsive-layout.md`: nada se recorta entre 640×400 y pantalla completa. Hecho y verificado a mano
  por el autor el 15 de noviembre de 2026 (PR #16).
- `docs/specs/island-title-and-close.md`: título desplazable y botones de ocultar y salir en la isla. Probado a
  mano por el autor (PR #19, con el botón «Salir» en el PR #20).
- `docs/specs/ascii-digits.md` (PR #22), `single-seek-path.md` (#23), `playlist-serialization.md` (#24),
  `write-after-advance.md` (#25) y `write-sessions.md` (#26): hechos el 16 de noviembre de 2026, con la pasada
  manual del autor ese día donde hacía falta.
- `docs/specs/lyrics-online-translation.md`: **descartada** por el autor el 15 de noviembre de 2026 (PR #21). La app
  sigue siendo 100 % local y sin red.

## Falta la prueba a mano del autor

- `docs/specs/settings-panel.md` (PR #17): probada en parte el 15 de noviembre de 2026. Quedan Mayús + clic (el
  autor tiene que decir qué esperaba) y la lista de temas desde Configuración con ratón y teclado.
- `docs/specs/background-playback.md` (PR #20): sobre todo el icono de la bandeja, su menú y la instancia única
  (lista en la spec, «Resultado»).
- `docs/specs/remove-and-advance.md`: quitar una pista ya no deja hueco en el empalme (era el punto 9 de esta
  lista), el borrado va dentro de una sesión de escritura, y un avance automático hacia una pista ausente ya no
  deja sonando una pista distinta de la que se muestra (el caso vecino de R6).
- `docs/specs/keep-browse-scope.md`: tras guardar etiquetas, la vista sigue en el álbum o artista abierto (era el
  punto 1).
- El rediseño según los mockups (`docs/specs/redesign-mockups.md`): está hecho en un commit local del autor, sin
  subir ni PR. Su spec registra capturas, pero no una prueba a mano.

## Sin spec todavía

1. **Los `Bitmap` de las carátulas no se liberan con `Dispose`.** Desde el 19 de noviembre de 2026 se liberan las
   carátulas grandes que llegan tarde: la de «ahora suena» tras cambiar de pista, y la vista previa del editor
   cuando ya no hace falta. `LoadLargeAsync` no las guarda en caché y nadie las ha recibido. Siguen quedando para
   el recolector `MainViewModel.NowPlayingCover` (la enlazan la ventana principal y la isla), la vista previa del
   editor al cambiarla y las miniaturas que expulsa `LruCache`. No es una fuga, pero la memoria nativa tarda en
   devolverse. Antes de seguir hay que **medir**: memoria privada del proceso tras cambiar de pista muchas veces
   (`tools/medir-cpu.ps1` solo mide CPU). Solo si sale un problema real, `LruCache` con aviso de expulsión o un
   contador de referencias. Un `Dispose` sobre un bitmap que alguna vista todavía dibuja es peor que el problema.

## Aplazado a propósito

- **R6 y el congelamiento con unidades de red** (`playback-resilience.md`, `ui-responsiveness.md`): spec propia.
  Hace asíncrono y cancelable el arranque de una pista, que es el cambio más delicado de todos. R6 no se ha
  reproducido nunca: se empieza por intentarlo con el retraso temporal en `Post`, como dice su criterio 10. Su caso
  vecino de `OnAdvancedToNext` ya está resuelto (`remove-and-advance.md`).
- **Resto de la auditoría de solo lectura:** del enfoque F (rendimiento) solo se midió el relleno por palabra
  (`lyrics-render-perf.md`). La auditoría del 19 de noviembre de 2026 repasó las convenciones de CLAUDE.md (A),
  audio y concurrencia (C), Windows nativo (E), la coherencia de la documentación (G) y la higiene del repositorio
  (H). Sus hallazgos están arreglados aquí o en la documentación.

## Notas cortas (sin prisa)

- **Rutas locales en los binarios publicados.** Los DLL y los `.pdb` de los zip de v1.4.0 y v1.5.0 llevan la ruta
  de compilación del equipo del autor (carpeta de usuario incluida). Decidido el 19 de noviembre de 2026: la spec
  de la próxima release publicará con `dotnet publish … -p:ContinuousIntegrationBuild=true` (solo en ese comando,
  no en un `Directory.Build.props`, para no cambiar los builds de desarrollo ni del CI). Además añadirá una fila de
  comprobación: buscar la carpeta de usuario en los DLL y `.pdb` del zip.
- `WindowNative.Restore`, `IsForeground` y `ForceForeground` ya llaman a Win32 dentro de métodos con
  `[SupportedOSPlatform("windows")]`, como pide CLAUDE.md (19 de noviembre de 2026).
