# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Reproductor de música local en C# / .NET 8 / Avalonia 11. El detalle de funciones, decisiones, limitaciones y estado está en `docs/reporte-reproductor.md`.

## Comandos

```bash
dotnet build                                   # compila la solución (hoy: 0 errores, 1 advertencia CS8602 conocida)
dotnet test                                    # 216 pruebas en Core y 39 en Infrastructure
dotnet test tests/MusicPlayer.Core.Tests --filter "FullyQualifiedName~PlayQueueTests"   # una clase
dotnet test tests/MusicPlayer.Core.Tests --filter "FullyQualifiedName~PlayQueueTests.Sequential_PlaysInOrderAndStopsAtTheEnd"  # un test
dotnet run --project src/MusicPlayer.Desktop   # ejecuta la app (necesita los binarios de BASS en src/MusicPlayer.Audio/native/)
```

El CI hace `dotnet build -c Release` y `dotnet test --no-build -c Release` en Windows y Ubuntu.

## Arquitectura

`Desktop` (Avalonia + CommunityToolkit.Mvvm) → `Audio` (ManagedBass) e `Infrastructure` (TagLibSharp, JSON, ffmpeg) → `Core` (modelos, interfaces y lógica sin paquetes externos). La lógica testeable va en Core: no hay pruebas de Desktop ni de Audio.

## Convenciones

- Código, comentarios, mensajes de commit y textos de la UI en español.
- **DI solo en `src/MusicPlayer.Desktop/CompositionRoot.cs`** (singletons, `ValidateOnBuild`): todo servicio o ViewModel nuevo se registra ahí.
- **Escritura atómica:** los JSON de usuario se guardan con `AtomicJson` (temporal `.tmp` y `File.Move`; si el JSON está dañado queda como `.corrupt`). Los archivos de audio se editan solo con `SafeFileEdit.Run` (copia temporal, editar, verificar y `File.Replace`).
- **Liberar el archivo antes de escribirlo o borrarlo (Windows):** el reproductor debe soltarlo con `PrepareNext(null)` y, si es la pista que suena, con `Unload()` (vía `ClearCurrentTrack`). Los editores lo piden con su evento `Saving`, que `MainViewModel` conecta a `OnTagsSaving`; una herramienta nueva que escriba en audio debe seguir ese camino.
