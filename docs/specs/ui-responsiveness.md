# Spec: interfaz que responde y no pierde el sitio (derivada de la auditoría del 11 de noviembre de 2026)

Estado: aprobada e implementada el 11 de noviembre de 2026 (U1 a U8). El congelamiento con unidades de red se
decidió llevar a una spec propia, junto con R6. Compila y pasan las 945 pruebas (748 en Core, 197 en
Infrastructure). Las 16 pruebas nuevas (`ThumbnailRequestsTests`, `SeekScrubTests`) nacieron con sus clases: la
lógica anterior estaba en Desktop, sin pruebas, así que no se vieron fallar antes. **Falta toda la verificación
manual** (lista de «Verificación»): nada de esta spec se ha visto con la app abierta, y el arrastre (U4) depende
de cómo entrega Avalonia los eventos de puntero del `Slider`.

## Context

Cuarta spec de la auditoría. Reúne los hallazgos de la ventana principal: la lista, el buscador, la barra de
progreso, las miniaturas y la carátula de «ahora suena».

U1 está reproducido con un programa mínimo; el resto se comprobó leyendo el código que se cita. Ninguno se ha
visto con la app abierta.

| Id | Hallazgo | Evidencia |
| -- | -------- | --------- |
| U1 | El temporizador del buscador arranca solo al construirse: 200 ms después de abrir la app se ejecuta un `ApplyFilter()` que nadie pidió. Sustituye la lista y pisa la línea de estado («Agrega una carpeta para empezar» mientras aún carga, o encima del aviso de ajustes ilegibles) | `MainViewModel.cs:236-237`. Comprobado: en Avalonia 11.2.5, `new DispatcherTimer(intervalo, prioridad, callback)` queda con `IsEnabled = True`. `IslandWindow.axaml.cs:60-61` ya lo sabe y llama a `Stop()` |
| U2 | Cada `ApplyFilter` sustituye `Tracks` por una colección nueva y la selección se pierde: tras buscar y borrar la búsqueda, o al cambiar una playlist, Reproducir arranca la primera pista de la lista y no la que estaba seleccionada | `MainViewModel.cs:370` (`Tracks = new ObservableCollection…`) sin restaurar `SelectedTrack`; `TogglePlayPause` usa `SelectedTrack ?? Tracks.FirstOrDefault()` |
| U3 | Cada `ApplyFilter` agrupa por artista y por álbum toda la biblioteca aunque se esté viendo «Canciones» | `MainViewModel.cs:361-367`; `LibraryGrouper.GroupArtists` llama a `GroupAlbums` por artista y este ordena cada grupo |
| U4 | Arrastrar la barra de progreso hace un `Seek` por cada valor intermedio, y cada 250 ms el temporizador devuelve el pulgar a la posición real | `MainWindow.axaml:621-625` (`Value="{Binding PositionSeconds}"`); `MainViewModel.cs:924-944` |
| U5 | Las filas sin carátula vuelven a leer el disco cada vez que reaparecen al desplazar la lista | `CoverArtLoader.cs:49-52` (`if (bytes is null) return;` sin recordar el resultado). La caché de Core sí guarda los «sin carátula», pero solo 32 entradas |
| U6 | Si se invalida una carátula mientras su miniatura se está cargando, la carga en curso guarda la imagen vieja y la fila nueva se queda sin miniatura hasta que vuelva a mostrarse | `CoverArtLoader.cs:54` (`_thumbnails.Set` tras `Invalidate`) y `:30-31` (`_loading.Add` falso para la fila nueva) |
| U7 | Si leer la carátula de «ahora suena» lanza algo que no sea una cancelación, se queda la carátula de la pista anterior y la excepción queda sin observar | `MainViewModel.cs` `LoadNowPlayingCoverAsync`: solo `catch (OperationCanceledException)`. Un FLAC dañado hace lanzar a TagLib `ArgumentOutOfRangeException` (ver `untrusted-input-limits.md`), que `EmbeddedCoverArtProvider` no captura |
| U8 | Mantener pulsada la barra espaciadora alterna reproducir y pausar en cada repetición del teclado | `MainWindow.axaml.cs:100-103`. Para la M ya hay guarda (`_muteKeyDown`). Hoy figura como limitación conocida en la sección 9 del reporte |

**Correcciones al informe de la auditoría.** Dos puntos se retiran tras leer el código con calma:
- «Las flechas se roban a los Slider» no es un fallo: `MainWindow.axaml.cs:131` lo hace a propósito («el botón,
  el slider o la fila enfocados no reciben la tecla»). ← y → saltan 5 s en toda la ventana.
- «Bloqueo al arrancar» (`ThemeViewModel.Initialize`): también es deliberado, para que la ventana no parpadee en
  Oscuro antes de aplicar el tema. Lee un archivo de ajustes y como mucho 50 temas de 64 KB.

## FEATURE: Lista, buscador, progreso y carátulas sin tropiezos

### OVERVIEW
Que la app no rehaga la lista sin motivo, conserve la canción seleccionada, deje arrastrar la barra de progreso
sin pelearse con ella y no repita lecturas de disco al desplazar la lista.

### ACCEPTANCE CRITERIA

1. **U1. El buscador no filtra solo.** Tras construir `MainViewModel`, `ApplyFilter` no se ejecuta hasta que
   algo lo pida (escribir en el buscador, cargar la biblioteca, cambiar de playlist). Al abrir la app, la línea
   de estado pasa de «Cargando…» al resumen de la biblioteca sin mensajes intermedios, y el aviso de ajustes
   ilegibles no se pisa.
2. **U2. La selección sobrevive al filtro.** Después de cualquier `ApplyFilter`, si la pista seleccionada sigue
   en la lista visible, sigue seleccionada (se busca por Id, como hace `SyncSelectionWith`). Si ya no está, no
   queda nada seleccionado, como hoy.
3. **U3. Solo se agrupa lo que se ve.** `ApplyFilter` calcula los artistas solo cuando se muestra la vista de
   artistas y los álbumes solo cuando se muestra la de álbumes. Al cambiar de vista se calcula lo que falte. El
   resultado visible en cada vista es el mismo que hoy.
4. **U4a. Arrastrar no salta en cada paso.** Mientras el puntero está pulsado sobre la barra de progreso, el
   reproductor no recibe ningún `Seek` y el temporizador no mueve el pulgar; el tiempo de la izquierda sí sigue
   al pulgar. Al soltar se hace un único `Seek` al punto elegido.
5. **U4b. Lo demás no cambia.** Un clic en la barra salta a ese punto, ← y → saltan 5 s, y el clic en la barra
   de la isla sigue funcionando. Si la pista termina o cambia durante el arrastre, el arrastre se abandona sin
   saltar en la pista nueva.
6. **U5. «Sin carátula» se recuerda.** Una pista para la que no se encontró carátula (o cuya imagen no se pudo
   decodificar) no vuelve a leer el disco al reaparecer su fila. Se recuerdan hasta 2 000 rutas; invalidar la
   carátula de una pista la olvida.
7. **U6. Invalidar gana a una carga en curso.** Si se invalida una ruta mientras su miniatura se carga, ese
   resultado se descarta (no entra en la caché ni avisa a la fila vieja) y la siguiente petición de esa ruta
   lanza una carga nueva.
8. **U7. La carátula grande no se queda vieja por un error.** Si la carga de la carátula de «ahora suena» lanza
   cualquier excepción que no sea de cancelación, la carátula queda vacía. Mientras la nueva se carga se sigue
   viendo la anterior (sin parpadeo entre pistas del mismo álbum).
9. **U8. Espacio mantenido cuenta una vez.** Mantener pulsada la barra espaciadora alterna reproducir/pausar una
   sola vez; hay que soltarla y volver a pulsar para alternar de nuevo. Las repeticiones no llegan al control
   enfocado.
10. **Lógica en Core con pruebas.** La contabilidad de las miniaturas (qué se está cargando, qué no tiene
    carátula, qué se invalidó a mitad) sale de `CoverArtLoader` a una clase de Core sin Avalonia, con pruebas que
    fallan antes del cambio para los criterios 6 y 7. Lo mismo para el arrastre (criterios 4 y 5): una clase
    pequeña que decide cuándo se hace el `Seek` y cuándo puede escribir el temporizador.
11. **Verificación manual.** Los criterios 1, 2, 3, 8 y 9 viven en Desktop y no tienen prueba automática: se
    comprueban en Windows con la lista de «Verificación» y se anota el resultado en esta spec.
12. **Cierre.** `dotnet build` sin advertencias nuevas y `dotnet test` en verde. Se actualiza el recuento de
    pruebas y se quita de la sección 9 del reporte «Mantener pulsada la barra repite el cambio de estado».

### OUT OF SCOPE
- **`File.Exists` y `Load` síncronos en el hilo de la interfaz** al cambiar de pista (`MainViewModel`,
  `StartPlayback` y los movimientos de la cola). Con una unidad de red caída la ventana se congela, pero
  arreglarlo exige que arrancar una pista sea asíncrono y cancelable, con el mismo «token de sesión» que quedó
  pendiente como R6 en `playback-resilience.md`. Se propone una spec propia para las dos cosas.
- Cancelar las cargas de miniaturas de filas que ya no se ven al desplazar muy rápido: hace falta saber desde
  el cargador qué filas siguen visibles.
- Conservar la posición del desplazamiento de la lista tras filtrar (solo se conserva la selección).
- Conservar el álbum o artista abierto tras guardar etiquetas: va en `ux-quick-wins.md` (spec 6, todavía sin escribir).
- Liberar (`Dispose`) los `Bitmap` grandes y las suscripciones sin cancelar: va con el rendimiento.
- Que ← y → muevan un `Slider` enfocado, y el arranque de `ThemeViewModel`: decisiones de diseño (ver arriba).

### EDGE CASES
- **U1 con la biblioteca vacía:** «Agrega una carpeta para empezar» aparece cuando termina la carga, no antes.
- **U2 con la pista seleccionada fuera del filtro y luego dentro otra vez:** no se recupera sola; la selección
  ya se perdió al salir del filtro.
- **U2 con la pista que suena:** `SyncSelectionWith` sigue llevando el resaltado a ella en cada cambio de pista.
- **U3 dentro de un artista o un álbum abiertos:** se calcula lo necesario para esa vista y para su línea de
  estado; no se muestran recuentos de una agrupación sin calcular.
- **U4 soltando fuera de la barra o perdiendo la captura del puntero** (cambio de ventana a mitad): cuenta como
  soltar, con un único `Seek` al último valor.
- **U4 en pausa:** el `Seek` al soltar se hace igual y la pista sigue en pausa.
- **U5 y una carátula añadida a mano en la carpeta** (`cover.jpg`) con la app abierta: no se ve hasta reiniciar
  o invalidar esa pista. Hoy tampoco se ve si su «sin carátula» sigue en la caché de Core.
- **U6 con dos invalidaciones seguidas:** solo cuenta la carga lanzada después de la última.
- **U8 y la ventana pierde el foco con la tecla pulsada:** ya se reinicia en `Deactivated`
  (`MainWindow.axaml.cs:46`); no cambia.

## Decisión que confirmar antes de implementar
**El congelamiento con unidades de red queda para otra spec.** Estaba previsto aquí (venía de la spec 1), pero
tocar cómo arranca una pista en una spec de «fluidez» mezclaría un cambio de riesgo alto con arreglos pequeños.
Si prefieres que entre aquí, la spec crece y la verificación manual también.

## Verificación
- `dotnet test`, con las clases nuevas de Core para miniaturas y arrastre.
- A mano en Windows:
  1. Abrir la app con la biblioteca cargada: la línea de estado no muestra «Agrega una carpeta para empezar» en
     ningún momento y la última canción queda seleccionada (U1).
  2. Seleccionar una canción, escribir en el buscador algo que la incluya, borrar la búsqueda y pulsar
     Reproducir: suena esa canción (U2).
  3. Con una biblioteca grande, escribir en el buscador en la vista «Canciones» y luego pasar a «Álbumes» y
     «Artistas»: los resultados coinciden con los de antes del cambio (U3).
  4. Arrastrar la barra de progreso de un lado a otro durante varios segundos: el pulgar no rebota y el sonido
     salta una sola vez, al soltar (U4a). Probar un clic suelto, ← y → y la barra de la isla (U4b).
  5. Desplazar una lista con muchas pistas sin carátula arriba y abajo: el uso de disco no se repite (U5).
  6. Aplicar «Carátula del álbum…» justo después de desplazar la lista: todas las filas del álbum muestran la
     carátula nueva (U6).
  7. Mantener pulsada la barra espaciadora dos segundos: la reproducción cambia una sola vez (U8).

## Orden de trabajo
1. Pruebas rojas del criterio 10.
2. U1 y U8 (una línea cada uno), U2 y U3 (`ApplyFilter`), U7, U5 y U6 (miniaturas), U4 (arrastre).
3. Verificación manual, documentación y recuento de pruebas.
