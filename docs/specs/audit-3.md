# Spec: arreglos de la tercera auditoría (15 de noviembre de 2026)

Estado: **aprobada por el autor el 15 de noviembre de 2026**. Sin código todavía.

## Context

Auditoría de solo lectura del 15 de noviembre de 2026 (skill `auditor`, enfoques A, B y C, más D y E en lo que
toca a la ventana principal y la isla), hecha junto con el plan de respuesta al feedback de uso. No se compiló ni
se ejecutó nada: cada hallazgo describe la condición que lo provoca. No hay hallazgos críticos.

Revisado: `README.md`, `CLAUDE.md`, `docs/reporte-reproductor.md` (secciones 3, 3.1, 4, 7 a 11),
`docs/pendientes.md`, `docs/diseño.md`, `docs/specs/dynamic-island.md`, `App.axaml.cs`, `CompositionRoot.cs`,
`MainWindow.axaml` y su código, `IslandWindow` (XAML y código), `IslandController`, `IslandMetrics`,
`ModalTracker`, `AppSettings`, `SettingsService`, `JsonSettingsRepository`, `MainViewModel` (reproducción),
`BassAudioPlayer`, `LyricsViewModel`, `EmbedLyricsViewModel`, `CompanionLyricsProvider`, `AtomicJson`,
`SafeFileEdit`, `LibraryScanner` y `LibraryService` (escaneo).

Sin revisar a fondo: enfoques F (rendimiento), G (pruebas) y H (higiene), `ProgressiveLyricsView`, los editores de
etiquetas y carátulas, `PlayQueue`, `tests/` y Linux.

## Hallazgos

### H1. Los avisos de fin de pista no saben a qué pista pertenecen

- **Archivo y línea:** `src/MusicPlayer.Desktop/ViewModels/MainViewModel.cs:227-230`, `879-893`, `642-667`.
- **Criterio:** enfoque C (callbacks de BASS y estado de la interfaz).
- **Severidad:** corto plazo.
- **Evidencia:**
  ```csharp
  _player.TrackEnded += (_, _) =>
      Dispatcher.UIThread.Post(() => OnTrackEnded());
  ```
  ```csharp
  private void OnTrackEnded()
  {
      if (_queue.MoveNextAfterEnd(CheckAvailable) is not { } next) return;
  ```
- **Cómo reproducirlo:** el aviso se encola desde el hilo de BASS; si antes de procesarse el usuario elige otra
  pista (`PlayTrack` reinicia la cola y carga), `OnTrackEnded` avanza la cola nueva y arranca la siguiente: la
  pista elegida se salta. Si lo que ocurre entre medias es `ClearCurrentTrack` por una escritura
  (`InterruptForWriting`), el aviso vuelve a abrir un archivo; con «repetir una», `CurrentTrack` es `null`, la
  comparación por Id falla y se llama a `StartPlayback` con la pista que se está escribiendo. Solo comprobable en
  Windows con la app abierta y un retraso temporal en el `Post`. Prueba: una regla pura «aviso vigente» en Core
  (generación del aviso frente a generación actual).
- **Fix sugerido:** un número de generación que sube en `StartPlayback`, `ResumePlayback` y `ClearCurrentTrack`;
  el `Post` captura el valor y el manejador descarta el aviso si ya no coincide.

### H2. Callbacks de BASS y `PrepareNext(null)`/`Release` sin candado común

- **Archivo y línea:** `src/MusicPlayer.Audio/BassAudioPlayer.cs:132-137`, `208-226`, `229-248`, `274-290`.
- **Criterio:** enfoque C; CLAUDE.md, «Liberar el archivo antes de escribirlo o borrarlo».
- **Severidad:** corto plazo.
- **Evidencia:**
  ```csharp
  if (!BassMix.MixerAddChannel(mixer, next.Stream, BassFlags.AutoFree)) { ... }
  Volatile.Write(ref _chained, next);
  ```
  ```csharp
  var chained = Interlocked.Exchange(ref _chained, null);
  if (chained is not null)
  {
      _current = chained;
  ```
- **Cómo reproducirlo:** (a) `PrepareNext(null)` hace su `Exchange(ref _chained, null)` entre el `MixerAddChannel`
  y el `Volatile.Write` de `OnMixedEnd`: ve `null`, y después `_chained` queda publicado con el archivo abierto
  aunque se pidió soltarlo; en Windows la escritura posterior falla. (b) `OnHeardEnd` toma `_chained` antes de que
  `Release` lo vacíe y asigna `_current` después de que `Release` lo puso a `null`: `_current` apunta a una fuente
  ya liberada. No verificable por lectura cuánto dura cada ventana; solo en Windows con la app abierta.
- **Fix sugerido:** un candado único para las transiciones de `_current`, `_chained` y `_next` (callbacks,
  `PrepareNext`, `Release`), y comprobar `_mixer` dentro de él.

### H3. Los JSON se renombran sin vaciar a disco

- **Archivo y línea:** `src/MusicPlayer.Infrastructure/AtomicJson.cs:49-52`;
  `src/MusicPlayer.Infrastructure/JsonLibraryRepository.cs:76-79`.
- **Criterio:** CLAUDE.md, «Escritura atómica»; enfoque B (pérdida de datos).
- **Severidad:** corto plazo (solo con corte de luz o cuelgue del sistema).
- **Evidencia:**
  ```csharp
  await using (var stream = File.Create(tempPath))
      await JsonSerializer.SerializeAsync(stream, value, Options, ct);

  File.Move(tempPath, path, overwrite: true);
  ```
- **Cómo reproducirlo:** el renombrado puede quedar en disco antes que los datos del temporal. Tras un corte de
  luz justo después de guardar, el archivo queda vacío o a medias; la siguiente carga da `JsonException`, se
  aparta como `.corrupt-<fecha>` y la biblioteca arranca vacía (los Id nuevos rompen las playlists). No verificable
  por lectura ni con una prueba automática fiable.
- **Fix sugerido:** `stream.Flush(flushToDisk: true)` antes de cerrar el temporal, en los dos sitios.

### H4. Si falla la limpieza de temporales, se pierde el escaneo

- **Archivo y línea:** `src/MusicPlayer.Core/Services/LibraryService.cs:94-96`;
  `src/MusicPlayer.Core/Services/LibraryScanner.cs:50`.
- **Criterio:** enfoque B.
- **Severidad:** corto plazo.
- **Evidencia:**
  ```csharp
  var cleaned = await scanner.CleanStaleTemporariesAsync(folder, ct);

  var saved = await CommitAsync(new LibrarySnapshot(TrackSorter.Sort(byPath.Values)), ct);
  ```
  ```csharp
  foreach (var path in Directory.EnumerateFiles(rootFolder, "~*", options))
  {
      ct.ThrowIfCancellationRequested();
      try
  ```
- **Cómo reproducirlo:** la enumeración está fuera del `try`. Si la carpeta deja de ser accesible entre el escaneo
  y la limpieza (unidad externa o de red que se desconecta), la excepción sale antes de `CommitAsync` y no se
  guarda nada de lo escaneado. Prueba en Core: un escáner cuya limpieza lanza, y comprobar que el escaneo se
  guarda igual.
- **Fix sugerido:** guardar primero y limpiar después, con la limpieza capturando E/S (devuelve 0 si falla).

### H5. El doble clic reproduce lo seleccionado, no la fila pulsada

- **Archivo y línea:** `src/MusicPlayer.Desktop/Views/MainWindow.axaml.cs:419-423`.
- **Criterio:** enfoque D.
- **Severidad:** corto plazo.
- **Evidencia:**
  ```csharp
  if (DataContext is MainViewModel vm && vm.SelectedTrack is { } track)
      vm.PlayTrackCommand.Execute(track);
  ```
- **Cómo reproducirlo:** con una pista seleccionada, doble clic en la barra de desplazamiento de la lista o en el
  hueco vacío bajo la última fila: se reproduce la pista seleccionada. Solo en la app abierta.
- **Fix sugerido:** tomar la fila de `e.Source`, como ya hace `OnTrackListContextRequested`. Se arregla en
  `settings-panel.md` (criterio del clic).

### H6. El grupo derecho de la barra de reproducción no cabe en su columna

- **Archivo y línea:** `src/MusicPlayer.Desktop/Views/MainWindow.axaml:501`, `636-688`.
- **Criterio:** fuera de especificación (feedback: «corta algunas opciones»).
- **Severidad:** corto plazo.
- **Evidencia:** `<Grid ColumnDefinitions="*,2*,*">` y, en la columna 2, cuatro botones `.icon` de 36 px, huecos
  de 4, un margen de 8 y `<Slider Width="100" ...>`.
- **Cómo reproducirlo:** la columna mide `(ancho − 32) / 4`: 232 px a 960 y 152 px a 640; el contenido, unos
  268. **Confirmado con una captura del autor (15 de noviembre de 2026):** con la ventana a un ancho
  cercano al de arranque, el deslizador de volumen queda cortado por el borde derecho.
- **Fix sugerido:** se arregla en `responsive-layout.md`.

### H7. El temporal de `SafeFileEdit` nace con fecha vieja, y su `finally` puede tapar el error

- **Archivo y línea:** `src/MusicPlayer.Infrastructure/SafeFileEdit.cs:20`, `24-27`.
- **Criterio:** CLAUDE.md, «Datos de la biblioteca» (temporales `~tags-*` de más de 24 horas se borran).
- **Severidad:** largo plazo.
- **Evidencia:**
  ```csharp
  File.Copy(filePath, temp);
  editCopy(temp);
  ```
  ```csharp
  finally
  {
      if (File.Exists(temp)) File.Delete(temp);
  }
  ```
- **Cómo reproducirlo:** `File.Copy` conserva la fecha de modificación del original. Entre la copia y el guardado
  de TagLib, el `~tags-*` de un archivo antiguo cumple `IsStaleTemporary`; un «Agregar carpeta» en marcha sobre
  esa carpeta puede borrarlo y la edición falla (el original queda intacto). Aparte, si `File.Delete` lanza (el
  antivirus retiene el temporal), su excepción sustituye a la de la edición. Prueba en Infrastructure: tras
  copiar, la fecha del temporal es reciente.
- **Fix sugerido:** `File.SetLastWriteTimeUtc(temp, DateTime.UtcNow)` tras copiar, y un borrado que no lance.

### H8. `finally` que solo capturan `IOException`, y `Kill` que puede lanzar

- **Archivo y línea:** `src/MusicPlayer.Infrastructure/AtomicJson.cs:56`;
  `src/MusicPlayer.Infrastructure/JsonLibraryRepository.cs:83`;
  `src/MusicPlayer.Infrastructure/FfmpegAudioConverter.cs:49-53`, `69`.
- **Criterio:** enfoque B.
- **Severidad:** largo plazo.
- **Evidencia:** `try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch (IOException) { }` y
  `try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }`.
- **Cómo reproducirlo:** un `UnauthorizedAccessException` al borrar el temporal sale del `finally` y sustituye al
  error original. `Kill` del árbol puede lanzar `Win32Exception` o `AggregateException`: la cancelación se anota
  como error de conversión. Las líneas de `FfmpegAudioConverter` vienen de la exploración: confirmarlas al
  implementar.
- **Fix sugerido:** capturar también `UnauthorizedAccessException` en esos borrados y las excepciones de `Kill`.

### H9. La traducción se elige antes de saber si sirve

- **Archivo y línea:** `src/MusicPlayer.Infrastructure/CompanionLyricsProvider.cs:35-40`.
- **Criterio:** `docs/specs/lyrics-layers.md`, criterio 10.
- **Severidad:** largo plazo.
- **Evidencia:**
  ```csharp
  var code = LyricsLanguage.Pick(...);
  var translation = code is null ? null : await LayerAsync(original.Kind, LyricsLayerKind.Translation, code, siblings, layers, ct);
  ```
- **Cómo reproducirlo:** sistema en `es`, original sincronizado, `cancion.es.lrc` sin marcas de tiempo y
  `cancion.en.lrc` válido: `Pick` elige `es`, `LayerAsync` devuelve `null` y no se prueba `en`. Prueba en
  Infrastructure con esos tres archivos.
- **Fix sugerido:** probar los códigos en el orden de preferencia hasta que uno dé una capa válida.

### H10. `ModalTracker.Begin()` deja el contador subido si un suscriptor lanza

- **Archivo y línea:** `src/MusicPlayer.Core/Services/ModalTracker.cs:17-22`.
- **Criterio:** CLAUDE.md, «Selectores y diálogos dentro de `ModalTracker.Begin()`».
- **Severidad:** largo plazo.
- **Evidencia:**
  ```csharp
  if (++_depth == 1)
      Changed?.Invoke(this, EventArgs.Empty);
  return new Scope(this);
  ```
- **Cómo reproducirlo:** si un manejador de `Changed` lanza, el `using` no recibe el `Scope`, `_depth` no baja e
  `IsOpen` queda en `true`: la isla no vuelve a mostrarse. Hoy el único suscriptor (`IslandController.Schedule`)
  no lanza. Prueba en Core con un suscriptor que lanza.
- **Fix sugerido:** crear el `Scope` antes de avisar y, si el aviso lanza, liberarlo y relanzar.

### H11. El aviso del tema al arrancar se pisa

- **Archivo y línea:** `src/MusicPlayer.Desktop/ViewModels/MainViewModel.cs:261`, `270`;
  `src/MusicPlayer.Desktop/App.axaml.cs:46`.
- **Criterio:** enfoque D (mensajes que no llegan a la línea de estado). Limitación ya documentada en el reporte
  («puede quedar tapado»): aquí se arregla junto con el punto 8 de `pendientes.md`.
- **Severidad:** largo plazo.
- **Evidencia:** `Themes.Notified += message => Status = message;` y, después, `Status = "Cargando…";`.
- **Cómo reproducirlo:** poner en `settings.json` un `ThemeId` que no exista y arrancar: «No se encontró el
  tema…» no llega a verse. Solo en la app abierta.
- **Fix sugerido:** guardar los avisos del arranque (tema y playlists) y escribirlos al final de
  `InitializeAsync`, como ya se hace con `settingsProblem`.

### H12. Temporizadores de la isla que no se paran

- **Archivo y línea:** `src/MusicPlayer.Desktop/Services/IslandController.cs:35`;
  `src/MusicPlayer.Desktop/Views/IslandWindow.axaml.cs:376-381`, `421-430`.
- **Criterio:** enfoque D (temporizadores). El de `HideFaded` es el punto 4 de `pendientes.md`.
- **Severidad:** largo plazo.
- **Evidencia:**
  ```csharp
  _settle = new DispatcherTimer(IslandMetrics.VisibilityDelay, DispatcherPriority.Normal, (_, _) => Apply());
  ```
  ```csharp
  protected override void OnClosed(EventArgs e)
  {
      if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelChanged;
      _waveTimer.Stop();
  ```
- **Cómo reproducirlo:** ese constructor de `DispatcherTimer` arranca el temporizador (el resto del código lo
  para justo después; aquí no): dispara una vez sin que nadie lo pida, también en Linux. `OnClosed` no para
  `_collapseDelay` ni `_collapseEnd`, y `HideFaded` sigue tras el `await` sin saber si la ventana se cerró. No
  verificable por lectura si Avalonia despacha esos tics tras cerrar.
- **Fix sugerido:** `_settle.Stop()` en el constructor; parar los dos temporizadores y marcar «cerrada» en
  `OnClosed`; comprobar esa marca tras el `await`.

### H13. CLAUDE.md no cita una excepción real a la regla de colores

- **Archivo y línea:** `CLAUDE.md` («Colores solo con recursos del tema») frente a
  `src/MusicPlayer.Desktop/Themes/ThemeBrushes.axaml:78-122`.
- **Criterio:** enfoque G (coherencia con la documentación).
- **Severidad:** largo plazo (solo documentación). Desactualizado: CLAUDE.md.
- **Evidencia:** CLAUDE.md nombra dos excepciones (grises de los botones de icono y bloque del inmersivo); los
  pinceles `ThemeIsland*` y `ThemeImmersiveMenu*` llevan literales fijos, comentados como excepción en el propio
  archivo. Las líneas vienen de la exploración: confirmarlas al implementar.
- **Fix sugerido:** añadir esas dos excepciones a la regla de CLAUDE.md.

## Candidatos por verificar (no se arreglan sin confirmarlos antes)

Salieron de la exploración y no los comprobé línea a línea:

- `LyricsEditor`: un error de E/S al leer una capa secundaria aborta toda la incrustación en lugar de saltarla.
- `TagLibLyricsLayerFields`: una capa incrustada con varios valores se une con `\0` y el parser la descarta.
- `JsonLibraryRepository`: un `library.json` válido con `"FilePath": null` hace fallar la carga con una excepción
  que no es `JsonException` (la app se niega a guardar, que es el comportamiento seguro).
- `JsonThemeRepository`: el tope de 50 archivos se aplica después de enumerar toda la carpeta.
- `PlayQueue.RemoveWhere` pone `_pendingNext = 0` y se pierde el orden de «Reproducir siguiente».
- `BassAudioPlayer.OnMixedEnd`: los avisos de la pista encadenada se registran desde el pool de hilos; con una
  pista más corta que el búfer podría no llegar ningún aviso de fin.
- `MainViewModel.ConfirmPruneAsync` puede quitar de la cola la pista que suena sin pararla.
- E/S síncrona en el hilo de la interfaz: `FfmpegAudioConverter.IsAvailable` desde un binding,
  `JsonThemeRepository.LoadAllAsync` al abrir el selector de tema y `LyricsSiblingFiles.Find`.
- `<Style Selector="Style Selector" />` en `MainWindow.axaml:47`: un estilo sin efecto.

## FEATURE: Cierre de los hallazgos H1 a H4 y H7 a H13

H5 y H6 se arreglan en `settings-panel.md` y `responsive-layout.md`.

### ACCEPTANCE CRITERIA

1. **H1.** Un aviso de fin de pista o de avance que llega después de que el usuario cargó otra pista, o de que la
   pista se descargó, no cambia la cola ni arranca nada. La regla («el aviso es de la generación actual») es una
   función pura de Core con pruebas: vigente, viejo por otra carga y viejo por descarga.
2. **H2.** Tras `PrepareNext(null)` el reproductor no tiene abierto ningún archivo distinto del actual, y tras
   `Unload()` ninguno, aunque un callback de BASS esté en curso. `_current` nunca queda apuntando a una fuente
   liberada.
3. **H3.** `AtomicJson.WriteAsync` y `JsonLibraryRepository.SaveAsync` vacían el temporal a disco antes de
   renombrarlo.
4. **H4.** Un fallo de E/S en la limpieza de temporales no impide guardar el escaneo: se guarda primero y la
   limpieza fallida cuenta como 0 borrados. Prueba en Core.
5. **H7.** El `~tags-*` recién copiado tiene fecha de modificación de ahora. Si su borrado final falla, la
   excepción que sale de `SafeFileEdit.Run` es la de la edición, no la del borrado. Pruebas en Infrastructure.
6. **H8.** Un `UnauthorizedAccessException` al borrar un temporal no sustituye al error original, y una
   cancelación de la conversión se anota como cancelada aunque `Kill` lance.
7. **H9.** Con la traducción preferida inválida y otra válida, se muestra la válida. Prueba en Infrastructure.
8. **H10.** Si un suscriptor de `Changed` lanza en `Begin()`, `IsOpen` vuelve a `false`. Prueba en Core.
9. **H11 y `pendientes.md` 8.** Los avisos del tema y de `playlists.json` del arranque se ven en la línea de
   estado cuando termina la carga.
10. **H12 y `pendientes.md` 4.** `IslandController` no dispara `Apply` sin un cambio previo; tras cerrar la isla
    no corre ningún temporizador suyo ni se llama a `Hide()` sobre la ventana cerrada.
11. **H13.** CLAUDE.md cita las cuatro excepciones a la regla de colores.
12. `dotnet build` sin advertencias nuevas (queda la CS8602 conocida) y `dotnet test` en verde, con las pruebas
    nuevas vistas fallar antes del cambio donde sea posible.

### OUT OF SCOPE

- Los candidatos por verificar: cada uno que se confirme pasa a `docs/pendientes.md` o a una spec.
- R6 y el congelamiento con unidades de red (spec propia, ya decidido).
- `PlaylistManager` sin serializar y los `Bitmap` sin `Dispose` (`pendientes.md` 2 y 3).
- Enfoques F, G y H de la auditoría.

### EDGE CASES

- **H1 y «repetir una»:** el aviso vigente sigue reiniciando la misma pista sin recargar letra ni carátula.
- **H1 y `ResumeInterrupted`:** la recarga tras escribir sube la generación; un aviso anterior a la escritura se
  descarta.
- **H2 y el coste:** el candado se toma en callbacks de BASS; dentro de él no se abre ningún archivo ni se llama
  a la interfaz (los eventos se disparan fuera).
- **H3 y la velocidad:** el vaciado a disco hace más lento cada guardado; el de los ajustes ya va con retardo de
  500 ms y el de la biblioteca solo ocurre al cambiarla.
- **H4 con cancelación:** cancelar durante la limpieza no deshace el guardado ya hecho.
- **H7 en un sistema de archivos que no deja cambiar la fecha:** el fallo se ignora; el temporal queda como hoy.

## Hitos

Cada hito va en su propio PR, para poder revertir los de reproducción (M2, los delicados) sin perder los de
archivos.

- **M1:** H3, H4, H7, H8 (archivos), con sus pruebas. **Hecho el 15 de noviembre de 2026** (rama
  `fix/audit-3-archivos`): compila con la CS8602 de siempre y pasan 962 pruebas (756 en Core, 206 en
  Infrastructure). Tres pruebas nuevas, vistas fallar antes del cambio: la limpieza con la carpeta inaccesible
  (H4) y las dos de `SafeFileEditTests` (H7). Sin prueba automática: el vaciado a disco (H3, no se puede
  comprobar sin cortar la corriente), el orden «guardar y luego limpiar» de `LibraryService` (H4; lo cubre que la
  limpieza ya no lanza), y los `catch` de H8 (no hay forma portable de provocar `UnauthorizedAccessException` ni
  un fallo de `Kill`). Las líneas de H8 en `FfmpegAudioConverter` se confirmaron al implementar.
- **M2:** H1 y H2 (reproducción). **Código hecho el 15 de noviembre de 2026** (rama
  `fix/audit-3-reproduccion`); **falta la verificación manual del autor en Windows**: elegir otra pista justo al
  final de una, incrustar letra en la pista que suena con «repetir una», y una pasada normal de empalmes sin
  silencio (varias pistas seguidas, «Reproducir siguiente», aleatorio y repetir). Compila con la CS8602 de siempre,
  pasan 966 pruebas (760 en Core, 206 en Infrastructure) y la app arranca y se cierra sin anotar nada en
  `errores.log`; no se probó el audio.
  - **H1:** `PlaybackEpoch` (Core, 4 pruebas) cuenta las órdenes de carga y descarga; `MainViewModel` la avanza
    justo después de `Load` y de `Unload`, y cada aviso de BASS anota la cuenta al salir y se descarta si cambió.
  - **H2:** un candado (`_sync`) en `BassAudioPlayer` para `_mixer`, `_current`, `_chained` y `_next`. El cambio de
    pista y su aviso salen juntos dentro del candado; las llamadas que liberan en BASS van siempre fuera (BASS
    espera al callback en curso, que podría estar esperando el candado). La pista encadenada se publica antes de
    añadirla al mezclador, así que `PrepareNext(null)` siempre la ve.
  - **Sin cubrir:** si la pista avanzó sola y su aviso aún no se atendió, `OnTagsSaving` compara con la pista
    anterior y no descarga la nueva: escribir en ella falla con el archivo abierto (el original queda intacto y el
    aviso lo dice). No hay pruebas automáticas de `BassAudioPlayer` (el proyecto no tiene pruebas de Audio).
- **M3:** H9 a H13 y los puntos 4 y 8 de `pendientes.md`. **Hecho el 15 de noviembre de 2026** (rama
  `fix/audit-3-resto`): compila con la CS8602 de siempre y pasan 969 pruebas (761 en Core, 208 en
  Infrastructure). Pruebas nuevas, vistas fallar antes del cambio: la traducción preferida inválida con otra
  válida (H9) y el suscriptor que lanza en `Begin()` (H10); una más cubre que sin ninguna traducción válida el
  original queda igual. H11 y H12 son código de vista, sin prueba automática: **falta comprobarlos a mano**
  (arrancar con un `ThemeId` inexistente y ver el aviso al terminar la carga; cerrar la app con la isla
  desvaneciéndose). Los avisos del arranque se muestran juntos, separados por « · ».
