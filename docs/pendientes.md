# Pendientes

Lista corta de lo que se sabe que falta. No es un plan: cada punto necesita su spec aprobada antes de tocar código.
Estado a 19 de noviembre de 2026. La v1.5.0 está publicada desde el 12 de noviembre (`docs/specs/release-v1.5.0.md`);
la v1.6.0 está preparada (`docs/specs/release-v1.6.0.md`), a falta del borrador en GitHub y de publicarla.

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

- Probados a mano por el autor el 19 de noviembre de 2026, sin fallos (sin detalle paso a paso):
  - `docs/specs/settings-panel.md` (PR #17), incluida la lista de temas desde Configuración. Mayús + clic se queda
    como está: selecciona sin reproducir, igual que Ctrl.
  - `docs/specs/background-playback.md` (PR #20): bandeja, su menú e instancia única.
  - `docs/specs/remove-and-advance.md` (PR #27): quitar una pista sin hueco en el empalme (era el punto 9) y avance
    coherente hacia una pista ausente (el caso vecino de R6).
  - `docs/specs/keep-browse-scope.md` (PR #27): la vista sigue en el álbum o artista tras guardar (era el punto 1).
  - `docs/specs/redesign-mockups.md` (PR #29): el rediseño según los mockups. Linux sigue sin probar.

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

- **Rutas locales en los binarios publicados:** resuelto en la v1.6.0 (`docs/specs/release-v1.6.0.md`, decisión 7).
  `ContinuousIntegrationBuild` no bastaba (Avalonia deja la ruta del `.pdb` en `MusicPlayer.Desktop.dll`): se
  publica además desde una unidad virtual (`subst`).
- `WindowNative.Restore`, `IsForeground` y `ForceForeground` ya llaman a Win32 dentro de métodos con
  `[SupportedOSPlatform("windows")]`, como pide CLAUDE.md (19 de noviembre de 2026).
