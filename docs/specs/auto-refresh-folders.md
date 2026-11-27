# Spec: las carpetas agregadas se actualizan solas

Estado: implementada el 27 de noviembre de 2026 (rama `feat/auto-refresh-folders`), y **probada a mano por el autor** el mismo día.
Compila con 0 errores y la advertencia CS8602 de siempre, y pasan las 1117 pruebas (903 en Core, 214 en
Infrastructure), 39 de ellas nuevas.

## Context

Hasta ahora la app no recordaba qué carpetas se habían agregado: `ScanFolderAsync` escaneaba la elegida y solo
guardaba las pistas (`LibrarySnapshot.Folders` se retiró en el PR #7 porque nada lo leía). Si se copiaba música nueva
a una carpeta ya agregada, había que pulsar «Agregar carpeta» y elegirla otra vez (README, «Limitaciones», y
`reporte-reproductor.md`, sección 9).

Decisiones del autor (27 de noviembre de 2026):

- La biblioteca se revisa **al abrir la app y cada vez que la ventana principal se activa**, con un mínimo de 60 s
  entre revisiones. Sin vigilancia en vivo (`FileSystemWatcher`): nada corre mientras la app está en segundo plano.
- **Solo se agrega.** Una pista cuyo archivo desapareció no se quita: queda «no disponible» y se limpia con el botón
  de siempre de la barra de aviso, como hasta ahora. Así una unidad desconectada no hace perder pistas ni playlists.

## FEATURE: Revisión automática de las carpetas agregadas

### ACCEPTANCE CRITERIA

1. **Las carpetas se recuerdan.** «Agregar carpeta» guarda la carpeta en `library.json` (clave `Folders`), en el mismo
   guardado que las pistas (`CommitAsync`, dentro de `ExclusiveAsync`). Si el guardado falla, ni las pistas ni la
   carpeta cambian en memoria.
2. **Sin duplicados ni solapes.** Las rutas se comparan con `FilePaths.Comparer`, como absolutas y sin separador
   final. Agregar una carpeta que ya está, o que está dentro de otra ya agregada, no añade nada. Agregar una carpeta
   que contiene otras ya agregadas las sustituye por ella. La regla es una función pura en Core (`LibraryFolders`).
3. **Compatibilidad.** Un `library.json` sin `Folders` carga igual (lista vacía). Uno anterior al PR #7 que aún
   trae `Folders` la recupera. Al cargar se descartan las entradas vacías o que no son rutas absolutas, y se aplica
   la regla del criterio 2.
4. **Revisión (`LibraryService.RefreshFoldersAsync`).** Para cada carpeta guardada lista sus audios (las mismas
   extensiones de siempre y sin los temporales `~tags-*`/`~conv-*`), y lee las etiquetas **solo de las rutas que la
   biblioteca no conoce**: las pistas conocidas no se vuelven a leer.
5. **Solo agrega.** La revisión no quita ninguna pista ni borra ningún archivo: no llama a
   `CleanStaleTemporariesAsync`, que sigue siendo exclusiva de «Agregar carpeta».
6. **Archivos movidos.** Si una ruta nueva es una pista ausente movida de sitio, se reubica con `TrackRelocator`
   (conserva `Id`, `AddedAt` y sus playlists), igual que al agregar carpeta.
7. **Copias a medias.** Un archivo que otro programa tiene abierto para escribir (una copia en curso) se deja para la
   próxima revisión, para no guardar etiquetas o duración de un archivo incompleto.
8. **Carpeta no accesible.** Una carpeta que no existe o deja de responder (unidad desconectada, carpeta renombrada)
   se salta sin error y sigue guardada: cuando vuelva, se revisa.
9. **Sin cambios, sin guardado.** Si no hay pistas nuevas ni reubicadas, no se escribe `library.json` ni se rehace la
   lista en pantalla.
10. **Un único guardado** con todo lo encontrado en todas las carpetas, por `CommitAsync` dentro de `ExclusiveAsync`.
    Si la carga de la biblioteca falló al arrancar, no se revisa nada.
11. **Cuándo.** Al terminar el arranque (en segundo plano, sin retrasarlo) y al activarse la ventana principal. Entre
    el final de una revisión y el comienzo de la siguiente pasan al menos 60 s; nunca hay dos a la vez; no se revisa
    con una sesión de escritura abierta (`WriteSessions.IsOpen`). La regla es una clase de Core con el reloj como
    parámetro (`FolderRefreshPolicy`), registrada en `CompositionRoot.cs`.
12. **Qué se ve.** Si hubo cambios, la lista se rehace conservando la selección (`ApplySnapshot`) y la línea de estado
    dice «N canciones nuevas» (y las reubicadas, si las hay). Si al arrancar se mostró un aviso, el mensaje se añade a
    él en lugar de taparlo. Tras cada revisión se comprueba la disponibilidad (`CheckAvailabilityAsync`), así que lo
    borrado mientras tanto aparece como no disponible. Un error se muestra en la línea de estado y no cierra la app.

### OUT OF SCOPE

- Vigilancia en vivo de las carpetas (`FileSystemWatcher`) y revisión con la app en segundo plano.
- Quitar carpetas de la lista o verla en la interfaz (anotado en `docs/pendientes.md`, «Sin spec todavía», 2).
- Volver a leer las etiquetas de un archivo conocido que otro programa editó: para eso, «Agregar carpeta» de nuevo.
- Un ajuste para desactivar la revisión.

### EDGE CASES

- **Varias activaciones seguidas** (alternar ventanas, cerrar el selector de carpetas): la regla de 60 s y «nunca dos
  a la vez» las reduce a una.
- **«Agregar carpeta» durante una revisión:** `ExclusiveAsync` las pone en fila; la que llega segunda ya no encuentra
  nada nuevo.
- **La ventana se activa antes de que la biblioteca haya cargado:** no se revisa ni cuenta como revisión.
- **Conversión en curso:** el `~conv-…` se ignora; el archivo final lo agrega la conversión o la revisión, y quien
  llegue segundo conserva su `Id`.
- **Linux:** no hay bloqueo obligatorio de archivos, así que el criterio 7 no detecta copias a medias allí.

## Resultado

- Core: `Models/LibrarySnapshot.cs` (`Folders`, propiedad `init` para no cambiar el constructor),
  `Models/RefreshResult.cs`, `Services/LibraryFolders.cs` (criterios 2 y 3), `Services/FolderRefreshPolicy.cs`
  (criterio 11), `Services/LibraryScanner.cs` (`ListAudioFilesAsync`, `ReadSettledFilesAsync`; `ScanAsync` comparte
  el recorrido) y `Services/LibraryService.cs` (`RefreshFoldersAsync`; `ScanFolderAsync` y la revisión comparten
  `MergeAsync`).
- Infrastructure: `JsonLibraryRepository` escribe y lee `Folders` (limpiado con `LibraryFolders.Clean`).
- Desktop: `FolderRefreshPolicy` registrada en `CompositionRoot.cs`; `MainViewModel.RefreshFoldersAsync` (al final
  de `InitializeAsync` y desde `OnWindowActivated`); `MainWindow.axaml.cs` solo reenvía `Activated`.
- Pruebas: `LibraryFoldersTests` (10), `FolderRefreshPolicyTests` (6), `LibraryServiceRefreshTests` (20) y 3 en
  `JsonLibraryRepositoryTests`. La de copias a medias solo comprueba algo en Windows.
- Prueba a mano hecha por el autor (Windows): agregar una carpeta, cerrar la app, copiar canciones en ella y abrirla → salen
  sin volver a agregarla, con «N canciones nuevas». Con la app abierta, copiar más, esperar un minuto y volver a la
  ventana → salen. Renombrar la carpeta o desconectar la unidad → sin errores, las pistas quedan no disponibles y las
  playlists intactas; al devolverla, vuelven a estar disponibles.
