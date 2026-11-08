# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Reproductor de música local en C# / .NET 8 / Avalonia 11. El detalle de funciones, decisiones, limitaciones y estado está en `docs/reporte-reproductor.md`.

## Comandos

```bash
dotnet build                                   # compila la solución (hoy: 0 errores, 1 advertencia CS8602 conocida)
dotnet test                                    # 482 pruebas en Core y 64 en Infrastructure
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

Los fps (30 pedidos, ~24 reales), las medidas y los tiempos están en `ProgressiveLyricsMetrics`; la lógica (relleno, pausas, resorte, desplazamiento, atenuación) en Core.

## Arquitectura

`Desktop` (Avalonia + CommunityToolkit.Mvvm) → `Audio` (ManagedBass) e `Infrastructure` (TagLibSharp, JSON, ffmpeg) → `Core` (modelos, interfaces y lógica sin paquetes externos). La lógica testeable va en Core: no hay pruebas de Desktop ni de Audio.

## Convenciones

- Código, comentarios, mensajes de commit y textos de la UI en español.
- **DI solo en `src/MusicPlayer.Desktop/CompositionRoot.cs`** (singletons, `ValidateOnBuild`): todo servicio o ViewModel nuevo se registra ahí.
- **Escritura atómica:** los JSON de usuario se guardan con `AtomicJson` (temporal `.tmp` y `File.Move`; si el JSON está dañado queda como `.corrupt`). Los archivos de audio se editan solo con `SafeFileEdit.Run` (copia temporal, editar, verificar y `File.Replace`).
- **Colores solo con recursos del tema:** no escribas literales de color (`#RRGGBB`) en el XAML de la interfaz. Usa los pinceles `ThemeXxxBrush` de `Themes/ThemeBrushes.axaml` (los colores los fija `ThemeService`). Excepciones deliberadas: los grises de los botones de icono y el bloque del modo inmersivo.
- **Liberar el archivo antes de escribirlo o borrarlo (Windows):** el reproductor debe soltarlo con `PrepareNext(null)` y, si es la pista que suena, con `Unload()` (vía `ClearCurrentTrack`). Los editores lo piden con su evento `Saving`, que `MainViewModel` conecta a `OnTagsSaving`; una herramienta nueva que escriba en audio debe seguir ese camino.
- **Selectores y diálogos dentro de `ModalTracker.Begin()`:** todo selector nativo o ventana de diálogo nueva se abre dentro de `using (_modals.Begin())`. Así la isla flotante no aparece encima mientras la ventana principal está desactivada por el diálogo.
- **Win32 solo tras `OperatingSystem.IsWindows()`:** las llamadas nativas (`NonActivatingWindow`, `WindowNative`) van detrás de esa guarda, con `[SupportedOSPlatform("windows")]`, para que el CI de Ubuntu compile y Linux arranque.
