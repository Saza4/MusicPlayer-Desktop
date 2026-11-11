# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Reproductor de música local en C# / .NET 8 / Avalonia 11. El detalle de funciones, decisiones, limitaciones y estado está en `docs/reporte-reproductor.md`.

## Comandos

```bash
dotnet build                                   # compila la solución (hoy: 0 errores, 1 advertencia CS8602 conocida)
dotnet test                                    # 712 pruebas en Core y 191 en Infrastructure
dotnet test tests/MusicPlayer.Core.Tests --filter "FullyQualifiedName~PlayQueueTests"   # una clase
dotnet test tests/MusicPlayer.Core.Tests --filter "FullyQualifiedName~PlayQueueTests.Sequential_PlaysInOrderAndStopsAtTheEnd"  # un test
dotnet run --project src/MusicPlayer.Desktop   # ejecuta la app (necesita los binarios de BASS en src/MusicPlayer.Audio/native/)
```

El CI hace `dotnet build -c Release` y `dotnet test --no-build -c Release` en Windows y Ubuntu.

Isla flotante (solo Windows, spec en `docs/specs/dynamic-island.md`): en Debug, `MUSICPLAYER_ISLAND_FORCE=1` la muestra aunque la ventana principal esté activa, para probar el hover a mano. Ese código no existe en Release (`#if DEBUG`).

Letra progresiva del modo inmersivo (spec en `docs/specs/lyrics-progressive.md`). Variables solo de Debug (`#if DEBUG`):
- `MUSICPLAYER_LYRICS_OVERRIDE=<ruta .lrc>`: usa ese `.lrc` en lugar de la letra de cualquier pista (con su `.es.lrc` y `.rom.lrc` si están al lado). Sirve para probar con letra sintética sin tocar la biblioteca; no se suben letras reales al repositorio.
- `MUSICPLAYER_LYRICS_STATS=<ruta>`: cada 5 s escribe fotogramas por segundo, cuántos traen una posición nueva y el desvío frente al reloj.
- `MUSICPLAYER_RENDER_OVERLAY=DirtyRects,RenderTimeGraph,…` (valores de `RendererDebugOverlays`): dibuja encima lo que Avalonia repinta.

`.lrc` enhanced (`<mm:ss.xx>` por palabra; specs `docs/specs/lrc-enhanced.md` y `docs/specs/lrc-enhanced-fill.md`): `LrcLyricParser` los lee a `LyricLine.Words` y `LyricsTiming.FillPoint` da el punto de relleno por palabra (solo la fila original; pronunciación y traducción siguen la fracción de la línea). Se afina a mano con `WordPauseHold` (Core) y `FillSpring`, `FillLookahead` y `FillSnapDistance` (`ProgressiveLyricsMetrics`).

Los fps (30 pedidos, ~24 reales), las medidas y los tiempos están en `ProgressiveLyricsMetrics`; la lógica (relleno, pausas, resorte, desplazamiento, atenuación) en Core.

Capas de letra (traducción y pronunciación incrustadas; spec en `docs/specs/lyrics-layers.md`). **Excepción a la regla de no subir audio:** `tests/MusicPlayer.Infrastructure.Tests/Fixtures/silencio.m4a` (0,1 s de silencio generado con ffmpeg, sin derechos), porque un M4A no se arma a mano en la prueba. El MP3 y el FLAC de prueba se generan en código (`TestAudio`). No añadas otros archivos de audio.

Capturas de la app para verificar cambios visuales (Windows): `AppPaths` usa `Environment.GetFolderPath(ApplicationData)`, así que cambiar la variable `APPDATA` del proceso **no** redirige los datos; la app abre los `settings.json`, `library.json` y temas reales del usuario. Para probar otro tema hay que cambiar `ThemeId` en el `settings.json` real, con copia de seguridad previa y restaurándolo al terminar (comprobar el hash). Las teclas sintéticas (`SendKeys`, `keybd_event`) no movían el foco: para ver `:focus-visible` se enfoca el control por código con `Focus(NavigationMethod.Tab)` en un cambio temporal que no se sube.

## Arquitectura

`Desktop` (Avalonia + CommunityToolkit.Mvvm) → `Audio` (ManagedBass) e `Infrastructure` (TagLibSharp, JSON, ffmpeg) → `Core` (modelos, interfaces y lógica sin paquetes externos). La lógica testeable va en Core: no hay pruebas de Desktop ni de Audio.

## Convenciones

- Código, comentarios, mensajes de commit y textos de la UI en español.
- **DI solo en `src/MusicPlayer.Desktop/CompositionRoot.cs`** (singletons, `ValidateOnBuild`): todo servicio o ViewModel nuevo se registra ahí.
- **Escritura atómica:** los JSON de usuario se guardan con `AtomicJson` (temporal `.tmp` y `File.Move`; si el JSON está dañado queda como `.corrupt`). Excepción: `JsonLibraryRepository` tiene su propia versión de ese mismo patrón y no usa `AtomicJson`; un repositorio JSON nuevo debe usar `AtomicJson`. Los archivos de audio se editan solo con `SafeFileEdit.Run` (copia temporal, editar, verificar y `File.Replace`).
- **Colores solo con recursos del tema:** no escribas literales de color (`#RRGGBB`) en el XAML de la interfaz. Usa los pinceles `ThemeXxxBrush` de `Themes/ThemeBrushes.axaml` (los colores los fija `ThemeService`). Excepciones deliberadas: los grises de los botones de icono y el bloque del modo inmersivo.
- **Botones de acción (spec en `docs/specs/action-buttons.md`):** los botones de la barra superior y el «+» de COLECCIONES usan `Classes="action"` (`action only` para solo icono) de `Themes/ActionButtonStyles.axaml`. Es opt-in: no toques los `ButtonBackground*` globales de `FluentOverrides.axaml`, que dan el aspecto de los demás botones. Un botón nuevo de ese tipo lleva `ToolTip.Tip` y `AutomationProperties.Name`, y su texto va en un `TextBlock.label` para que el modo `compact` (barra superior por debajo de 1100 px, en `MainWindow.axaml.cs`) pueda ocultarlo. El anillo de foco usa `ThemeFocusRingBrush`, no el acento, porque solo el texto tiene 3:1 garantizado contra el fondo.
- **Datos de la biblioteca (spec en `docs/specs/library-data-safety.md`):** si la carga de `library.json` o `playlists.json` falla por E/S, `LibraryService` y `PlaylistManager` se niegan a guardar hasta una carga correcta (no sobrescribir lo que no se pudo leer). Las operaciones de `LibraryService` que tocan `_current` se serializan con `ExclusiveAsync`; un servicio nuevo que lo modifique debe usarlo. Los temporales `~tags-*` y `~conv-*` no son música: `LibraryScanner.IsTemporary` los ignora, y un temporal nuevo de la app debe llevar uno de esos prefijos.
- **Liberar el archivo antes de escribirlo o borrarlo (Windows):** el reproductor debe soltarlo con `PrepareNext(null)` y, si es la pista que suena, con `Unload()` (vía `ClearCurrentTrack`). Los editores lo piden con su evento `Saving`, que `MainViewModel` conecta a `OnTagsSaving`, y avisan al terminar (bien o mal) con `Finished`, que se conecta a `ResumeInterrupted` para volver a cargar la pista en el mismo punto (spec en `docs/specs/playback-resilience.md`); una herramienta nueva que escriba en audio debe seguir ese camino con los dos eventos.
- **Selectores y diálogos dentro de `ModalTracker.Begin()`:** todo selector nativo o ventana de diálogo nueva se abre dentro de `using (_modals.Begin())`. Así la isla flotante no aparece encima mientras la ventana principal está desactivada por el diálogo.
- **Win32 solo tras `OperatingSystem.IsWindows()`:** las llamadas nativas (`NonActivatingWindow`, `WindowNative`) van detrás de esa guarda, con `[SupportedOSPlatform("windows")]`, para que el CI de Ubuntu compile y Linux arranque.
