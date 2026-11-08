# Especificación: release v1.4.0

Estado: **preparada** (8 de noviembre de 2026). La release se deja como borrador en GitHub; la publica el autor.

## Contexto

La última release publicada es v1.3.0 (6 de noviembre de 2026). Desde entonces `main` acumula tres funciones que los usuarios aún no tienen: la **letra progresiva** del modo inmersivo (PR #4), las **capas de letra incrustadas** (PR #5) y los **botones de acción** (PR #6), más una limpieza interna (PR #7). Las notas dicen lo que hace el código y qué se probó y qué no, como las de v1.3.0.

## Decisiones

1. **Versión v1.4.0.** Funciones nuevas y nada incompatible: un `settings.json`, `library.json` o `playlists.json` anterior carga igual (`AppSettings` no cambia; `Folders` antiguo se ignora). Es un salto menor según SemVer. Release normal (**Latest**), no pre-release.
2. **El zip incluye las DLL de BASS**, como los anteriores. El autor debe confirmar en un4seen.com que puede redistribuirlas antes de publicar.
3. **Borrador en GitHub** (`gh release create --draft`): el tag no existe hasta que el autor publica, y con *Immutable releases* no se podrían cambiar los archivos después.
4. **Sin `<Version>` en el `.csproj`:** la versión vive en el tag, el título y el nombre del zip.
5. **Integridad:** SHA-256 del zip en las notas; el zip no está firmado y las notas lo dicen.

## Criterios de aceptación

1. `main` (3920bc9) compila con `dotnet build -c Release` (solo la CS8602 conocida) y pasa 813 pruebas (640 Core + 173 Infrastructure).
2. `docs/notas-de-version/v1.4.0.md` está en español, no afirma nada que el código no haga e incluye novedades, «Al actualizar», requisitos, qué se probó y qué no, cómo verificar la descarga (SHA-256) y los contribuyentes.
3. El zip `MusicPlayer-v1.4.0-win-x64.zip` se construye con `dotnet publish -c Release -r win-x64 --self-contained false`, con la misma estructura que el de v1.3.0 (49 archivos, carpeta raíz `win-x64/`, rutas con `/`), con las cuatro DLL de BASS y sin el código de depuración (`MUSICPLAYER_ISLAND_FORCE`, `MUSICPLAYER_LYRICS_*`).
4. La release es un **borrador** titulado «MusicPlayer v1.4.0», con el zip adjunto (menor de 2 GiB) y apuntando a un commit de `main`.
5. Docs al día: reporte (cabecera, tablas, secciones 4.3.x, 5, 6 y 10) y README.

## Fuera de alcance

Firmar el ejecutable, un instalador, la versión en el `.csproj`, un workflow de release automático, un build para Linux y cualquier cambio de código de la app.

## Casos límite

- **Zip sin las DLL de BASS:** la app no inicia. Se comprueba el contenido y se arranca el exe publicado.
- **Zip creado con Windows PowerShell 5.1:** `Compress-Archive` escribe rutas con `\`, distinto del zip anterior. Se usa PowerShell 7 (`pwsh`).
- **Un `cancion.es.lrc` junto al audio** gana sobre la capa incrustada; las notas lo dicen.
- **Linux o Windows sin .NET 8:** el zip es solo para Windows x64 y necesita el runtime; las notas lo dicen.
- **Promesas no probadas:** `Tab` real, estado pulsado y deshabilitado de los botones, arrastre táctil y Linux. Las notas lo dicen.

## Resultado

Zip construido con el código de `main` (3920bc9).

| Comprobación | Resultado |
|---|---|
| `dotnet build -c Release` | 0 errores, solo la CS8602 conocida |
| `dotnet test -c Release` | 813 en verde (640 Core + 173 Infrastructure) |
| `dotnet publish -c Release -r win-x64 --self-contained false` | 49 archivos, **el mismo conjunto** que el zip de v1.3.0 |
| Zip `MusicPlayer-v1.4.0-win-x64.zip` | 11 788 299 bytes, carpeta raíz `win-x64/`, sin rutas con barra invertida |
| SHA-256 | `811541779C14A2D45508C97121EDCE76AD8181C0F28825E2F573A72C2C9AE5FC` |
| DLL de BASS en el zip | `bass.dll`, `bass_aac.dll`, `bassflac.dll` y `bassmix.dll` |
| Código de depuración | `MUSICPLAYER_ISLAND_FORCE` y `MUSICPLAYER_LYRICS_*` **no** están en `MusicPlayer.Desktop.dll` |
| Arranque del exe publicado | Arranca (ventana «Music Player»), cierre limpio con código de salida 0 |
| Datos del usuario | `settings.json` y `library.json` idénticos antes y después (mismo hash) |

El zip se construyó una sola vez: es el que se adjunta al borrador, con el SHA-256 de las notas.
