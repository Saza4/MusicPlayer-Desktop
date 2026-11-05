# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Reproductor de música local de escritorio en **C# / .NET 8 / Avalonia 11**, pensado como referencia de Clean Architecture, MVVM y pruebas unitarias. El código, los comentarios, los mensajes de commit y la UI están en español; mantén esa convención.

## Comandos

```bash
dotnet build                                   # compila la solución (MusicPlayer.sln)
dotnet run --project src/MusicPlayer.Desktop   # ejecuta la app
dotnet test                                    # todas las pruebas (xUnit)
dotnet test tests/MusicPlayer.Core.Tests       # un solo proyecto de pruebas
dotnet test --filter "FullyQualifiedName~PlayQueueTests"            # una clase
dotnet test --filter "FullyQualifiedName~PlayQueueTests.NombreTest" # un test
```

El CI (`.github/workflows/ci.yml`) ejecuta `restore` → `build -c Release` → `test` en Windows y Ubuntu.

**Binarios nativos de BASS:** no están en el repo. Deben estar en `src/MusicPlayer.Audio/native/win-x64/` (`bass.dll`, `bassflac.dll`, `bass_aac.dll`, `bassmix.dll`) o `native/linux-x64/` (`.so`). Sin ellos la app compila y las pruebas pasan, pero la app no arranca. La conversión de formatos usa el `ffmpeg` instalado en el sistema (opcional).

## Arquitectura

Cuatro proyectos en `src/`, con todas las dependencias apuntando hacia **Core**:

- **MusicPlayer.Core** — modelos, interfaces (`Interfaces/`) y servicios con la lógica pura (cola, filtro, orden, parser `.lrc`, combinación de letras, sugerencias de etiquetas, planificación de conversiones). Sin dependencias de paquetes: no debe conocer Avalonia, TagLib, BASS ni el sistema de archivos más allá de lo que piden sus interfaces.
- **MusicPlayer.Audio** — `BassAudioPlayer` (ManagedBass + BASSmix) implementa `IAudioPlayer`, incluida la reproducción sin silencios vía `PrepareNext`/`AdvancedToNext`.
- **MusicPlayer.Infrastructure** — implementaciones con TagLibSharp (lectura/escritura de etiquetas, carátulas, letras), repositorios JSON, proveedores de archivos y `FfmpegAudioConverter`.
- **MusicPlayer.Desktop** — Avalonia + CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`). `MainViewModel` orquesta reproducción, biblioteca y navegación; la UI casi entera vive en `Views/MainWindow.axaml`.

Pruebas: `tests/MusicPlayer.Core.Tests` (referencia solo Core) y `tests/MusicPlayer.Infrastructure.Tests` (referencia solo Infrastructure). No hay pruebas de Desktop ni de Audio, así que la lógica testeable debe vivir en Core.

### Puntos clave que cruzan archivos

- **Composición única:** todo el registro de DI está en `src/MusicPlayer.Desktop/CompositionRoot.cs` (todo singleton, `ValidateOnBuild = true`). Un servicio o ViewModel nuevo debe registrarse ahí o la app falla al arrancar.
- **Proveedores composite/decorator:** las letras se resuelven como `CompanionLyricsProvider(CompositeLyricsProvider[LrcFile, Embedded])` (el companion añade traducción/pronunciación desde `cancion.<idioma>.lrc` / `cancion.rom.lrc`). Las carátulas como `CachedCoverArtProvider(CompositeCoverArtProvider[Embedded, Folder])`; esa misma instancia se expone como `ICoverArtProvider` y `ICoverArtCache`, así que tras escribir una carátula hay que invalidar la caché. Una fuente nueva se añade como nuevo proveedor, sin modificar los existentes.
- **Hilos:** los eventos de `IAudioPlayer` llegan fuera del hilo de UI; `MainViewModel` los reenvía con `Dispatcher.UIThread.Post`. El escaneo de la biblioteca corre en segundo plano.
- **Escritura segura en audio:** toda edición de etiquetas/carátulas/letras pasa por `SafeFileEdit.Run` (copia temporal con la misma extensión → editar → verificar → `File.Replace`). En Windows hay que llamar `IAudioPlayer.Unload()` antes de reemplazar o borrar el archivo que suena.
- **Persistencia JSON:** `library.json`, `playlists.json` y `settings.json` en `%APPDATA%\MusicPlayer` o `~/.config/MusicPlayer` (`AppPaths`). Se escriben de forma atómica (`AtomicJson`), si el JSON está dañado se guarda una copia `.corrupt`, y los repositorios usan DTOs propios para que el formato en disco no dependa de los modelos de Core.
- **Identidad de pistas:** editar etiquetas no cambia la identidad de la pista (conserva sus playlists). Los archivos movidos o borrados se marcan como no disponibles y `TrackRelocator` los vuelve a enlazar por título, artista, álbum y duración al agregar la carpeta nueva.
