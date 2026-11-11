# Spec: robustez de datos de la biblioteca (derivada de la auditoría)

Estado: implementado (C1, S2, S5). S1 sigue pendiente: no hay un archivo que haga lanzar a TagLib otra excepción, así que queda como no reproducible. Pruebas: `LibraryDataSafetyTests` (Core) y `JsonLibraryConcurrentSaveTests` (Infrastructure).

## Context

La auditoría encontró riesgos de pérdida de datos y de basura en la biblioteca. Esta spec cubre solo lo que el triage
marcó como prioritario: C1, S2, S5 y S1. Cada punto exige una prueba que falle antes de tocar el código; si la prueba
no se puede escribir o no falla, el hallazgo se descarta. Correcciones al informe: son 8 hallazgos de largo plazo (L1-L8,
no 7); L4 (Espacio en botones) está documentado como decisión de diseño y queda fuera; S1 y S8 son hipótesis hasta tener un
archivo que haga lanzar a TagLib.

## FEATURE: Biblioteca que no se pierde ni se ensucia

### OVERVIEW
Evitar que un fallo de lectura acabe sobrescribiendo `library.json` / `playlists.json`, que los temporales
`~tags-*` / `~conv-*` entren como pistas, y que los guardados concurrentes se pisen.

### ACCEPTANCE CRITERIA
1. **C1a.** Si `ILibraryRepository.LoadAsync` lanza (IOException, UnauthorizedAccessException), `LibraryService` queda en
   estado «carga fallida»: `ScanFolderAsync`, `AddFilesAsync`, `ApplyTags*` y `RemoveTracksAsync` no llaman a `SaveAsync`
   y la interfaz muestra un aviso con la causa. Una carga posterior correcta rehabilita los guardados.
2. **C1b.** `MainViewModel.InitializeAsync` aísla la carga de ajustes: si `settings.json` falla, la biblioteca y las
   playlists se cargan igual (con ajustes por defecto y aviso).
3. **C1c.** Mismo comportamiento para `PlaylistManager`/`JsonPlaylistRepository`: carga fallida bloquea guardados.
4. **S2.** `LibraryScanner.ScanAsync` ignora archivos cuyo nombre empieza por `~tags-` o `~conv-`
   (sin distinguir mayúsculas). No se borra nada.
5. **S5.** Las operaciones que leen y reasignan `_current` en `LibraryService` se serializan (`SemaphoreSlim`), y
   `JsonLibraryRepository.SaveAsync` usa un `.tmp` único por escritura. Dos `SaveAsync` simultáneos no lanzan y el
   resultado final contiene los cambios de ambos.
6. **S1 (condicional).** Solo si existe un archivo (generado en código, sin subir audio real) que haga lanzar a TagLib una
   excepción no contemplada: `ScanAsync` omite ese archivo, cuenta los omitidos y devuelve el resto. Si no se puede
   provocar, se cierra como «no reproducible».
7. Cada criterio tiene una prueba en `tests/` que falla antes del cambio. Sin audio nuevo en el repositorio
   (excepción única: `silencio.m4a`).
8. `dotnet build` sin errores nuevos y `dotnet test` en verde; se actualizan README, CLAUDE.md y el reporte con el
   nuevo recuento de pruebas.

### OUT OF SCOPE
- S3 (ffmpeg huérfano al cerrar), S4, S6-S10 y todos los L: van en specs aparte.
- Vigilar carpetas, cancelar el escaneo, Papelera, aplicar `[offset:]`.
- Cambiar el formato de `library.json` o `playlists.json`.

### EDGE CASES
- Carga fallida y luego el usuario reinicia la app con el archivo ya libre: carga normal.
- Carga correcta de un archivo inexistente (primera vez): no es «fallida»; se puede guardar.
- JSON corrupto (`JsonException`): sigue el camino actual (`.corrupt` + vacío) y sí permite guardar.
- Nombre `~conv-x.mp3` dentro de subcarpetas: también se ignora.
- Pista legítima cuyo título real empiece por `~tags-`: caso aceptado como no soportado.

## Verificación
- `dotnet test tests/MusicPlayer.Core.Tests --filter "FullyQualifiedName~LibraryServiceTests"` y `LibraryScannerTests`.
- Pruebas nuevas con repositorio falso que lanza `IOException`, y con dos `SaveAsync` concurrentes sobre un directorio temporal.
- Prueba manual en Windows: bloquear `library.json` con otro proceso, arrancar, agregar carpeta y comprobar que el archivo no cambia.

## Orden de trabajo
1. Pruebas rojas para 1-5 (sesión distinta de la del auditor).
2. Implementar y pasar a verde.
3. Segunda pasada de auditoría solo sobre los archivos no revisados.
4. Ajustar la skill `auditor` (no reportar decisiones documentadas, verificar conteos del resumen) y añadir a CLAUDE.md la línea sobre `~tags-`/`~conv-`.
