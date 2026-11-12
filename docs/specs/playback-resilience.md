# Spec: reproducción que no se cae ni se corta (derivada de la auditoría del 11 de noviembre de 2026)

Estado: aprobada el 11 de noviembre de 2026 con las dos decisiones propuestas (seguir abierta tras un error
inesperado; reanudar siempre). Implementados R1 a R5; compila y pasan las 886 pruebas (704 en Core, 182 en
Infrastructure). **Verificación manual hecha por el autor el 11 de noviembre de 2026, en Windows: todo correcto** (no se anotó el resultado paso a paso). Antes de esa pasada, los criterios 1, 2, 4,
5, 6, 7 y 8 solo estaban comprobados por lectura. R6 no se ha intentado reproducir y sigue abierto. *Nota posterior:* la segunda auditoría encontró que, al reanudar en pausa (criterios 2 y 5), la barra quedaba en 0:00; corregido en `audit-followup.md`. Pruebas:
`PlayQueueTests` (seis nuevas) y `ErrorLogTests`. Las de la cola no se vieron fallar antes del cambio: la lógica
anterior estaba en `MainViewModel`, sin pruebas, y los métodos nuevos no existían.

## Context

La auditoría de solo lectura encontró un camino que cierra la app y varios que cortan la música o dejan la cola
desalineada. Todos pasan por `BassAudioPlayer`, `MainViewModel` y `PlayQueue`. Esta spec es la primera de siete
(el orden estaba en el plan de la auditoría, que no se guardó en el repositorio); las demás cubren entradas no confiables, datos de la biblioteca,
fluidez de la interfaz, rendimiento de la letra, mejoras de uso y documentación.

Cada hallazgo se comprobó leyendo el código que se cita. Ninguno se ha reproducido con la app abierta.

| Id | Hallazgo | Evidencia |
| -- | -------- | --------- |
| R1 | Reproducir con el archivo de la pista terminada ya ausente lanza en el hilo de la interfaz y cierra la app | `BassAudioPlayer.cs:150-154` (`Play` llama a `Load`, que lanza en la 93); `MainViewModel.cs:722-726` y `:788-789` sin `try`; `Program.cs` y `App.axaml.cs` sin manejador global |
| R2 | `PrepareNext(null)` no suelta la pista que ya entró al mezclador | `BassAudioPlayer.cs:129` sale antes del `Discard` de la 135 |
| R3 | Guardar etiquetas, carátula o letra de la pista que suena la descarga y no la vuelve a cargar | `MainViewModel.cs:798-803` y `:823-828`; solo `EmbedLayerInImmersiveAsync` (`:656-671`) reanuda |
| R4 | Los selectores de carpeta e imagen se esperan fuera del `try` de su comando | `MainViewModel.cs:302`, `TagEditorViewModel.cs:101` |
| R5 | «Anterior» o «Siguiente» sin pista disponible en esa dirección dejan la cola en una pista ausente | `MainViewModel.cs:440-451` avanza `_position` aunque devuelva null; `:744-748` |
| R6 | Un `TrackEnded` o `AdvancedToNext` que llega después de que el usuario cambió de pista avanza la cola otra vez | `MainViewModel.cs:227-230` (`Post` sin identificar la carga que lo originó) |

## FEATURE: Reproducción resistente a archivos ausentes y a escrituras

### OVERVIEW
Que ninguna acción de reproducción cierre la app, que el reproductor suelte de verdad los archivos que se van a
escribir o borrar, que editar la pista que suena no deje al usuario en silencio, y que la cola siga apuntando a
lo que suena cuando hay pistas ausentes.

### ACCEPTANCE CRITERIA

1. **R1a. Reproducir sin archivo no cierra la app.** Con una pista terminada (o descargada) cuyo archivo ya no
   existe o no se puede abrir, pulsar Reproducir o Espacio deja la app abierta, marca la pista como no disponible
   y escribe en la línea de estado el mismo aviso que `StartPlayback` («… no está disponible: el archivo se movió
   o se borró», o el mensaje del error si el archivo existe pero no abre). Lo mismo cuando «Repetir una» reinicia
   la pista al terminar.
2. **R1b. No se pierde la posición en pausa.** Si la pista se recargó en pausa en un punto (`ResumePlayback` con
   `play: false`), Reproducir sigue desde ese punto: el arreglo de R1a no puede consistir en recargar siempre
   desde cero.
3. **R1c. Red de seguridad.** Una excepción no controlada en el hilo de la interfaz no cierra la app: se muestra
   «Error inesperado: …» en la línea de estado y se añade a `errores.log` en la carpeta de datos (`AppPaths`),
   con fecha, tipo, mensaje y traza. El archivo no pasa de 256 KB (al superarlos se conserva la mitad más
   reciente). Escribir el registro nunca lanza. Las excepciones de tareas sin observar
   (`TaskScheduler.UnobservedTaskException`) solo se registran. El registro vive en Infrastructure, se registra
   en `CompositionRoot.cs` y tiene pruebas (añade, recorta, no lanza con la carpeta de solo lectura).
4. **R2. `PrepareNext(null)` suelta todo lo preparado.** Tras llamarlo, el reproductor no tiene abierto ningún
   archivo salvo el de la pista que se oye: ni la preparada (`_next`) ni la ya añadida al mezclador (`_chained`),
   que se quita del mezclador y se libera. Cuando la actual termina, se dispara `TrackEnded` (no
   `AdvancedToNext`) y la cola sigue por el camino normal, con el pequeño silencio que eso implica.
   `PrepareNext(ruta)` con una pista ya encadenada sigue sin hacer nada, como hoy.
5. **R3a. Editar la pista que suena la reanuda.** Si una escritura descargó la pista en curso (editor de
   etiquetas, carátula de la pista, carátula de álbum, autocompletar y su deshacer, «Incrustar letra (.lrc)…»),
   al terminar la escritura, haya salido bien o mal, la pista se vuelve a cargar en la misma posición y sigue
   sonando si sonaba o queda en pausa si estaba en pausa. Se ve con sus datos nuevos (título, artista, carátula).
6. **R3b. El modo inmersivo vuelve solo si estaba abierto.** `ResumePlayback` hoy lo abre siempre; con R3a debe
   restaurar el estado que había antes de escribir. El flujo existente del menú de capas no cambia de
   comportamiento.
7. **R3c. La cola no se mueve.** Tras reanudar, «Siguiente» y «Anterior» llevan a las mismas pistas que antes de
   guardar.
8. **R4. Selectores dentro del `try`.** Si `PickFolderAsync` o `PickImageAsync` lanzan, el comando termina con un
   aviso en la línea de estado (o en `Error` del editor de etiquetas) y la app sigue abierta.
9. **R5. La cola no se queda en una pista ausente.** Si al avanzar o retroceder no hay ninguna pista disponible
   en esa dirección, la cola queda exactamente como estaba: misma pista en curso, mismo orden (también con
   Aleatorio y «Repetir todas», donde `MoveNext` baraja una vuelta nueva) y mismas pistas pendientes de
   «Reproducir siguiente». La lógica va en Core (`PlayQueue`) con pruebas en `PlayQueueTests`:
   - `[A ausente, B]` sonando B: «Anterior» reinicia B y después «Siguiente» no vuelve a arrancar B.
   - `[A, B ausente]` sonando A con «Repetir una»: «Siguiente» no encuentra nada y, al terminar A, se repite A.
   - Con todo ausente salvo la actual y «Repetir todas»: la búsqueda termina (tope de una vuelta) y la cola no
     cambia.
10. **R6 (condicional). Eventos tardíos.** Solo si se consigue reproducir el salto de una pista (por ejemplo con
    un retraso temporal en el `Post`, que no se sube): los eventos `TrackEnded` y `AdvancedToNext` que
    correspondan a una carga anterior a la actual se descartan. Si no se reproduce, se cierra como «no
    reproducible», igual que S1 en `library-data-safety.md`.
11. **Pruebas.** Los criterios 3 y 9 tienen pruebas que fallan antes del cambio. Los criterios 1, 2, 4, 5, 6, 7 y
    8 no se pueden probar de forma automática (no hay pruebas de Desktop ni de Audio): se verifican a mano en
    Windows con la lista de «Verificación» y se anota el resultado en esta spec.
12. **Cierre.** `dotnet build` sin advertencias nuevas (solo la CS8602 conocida) y `dotnet test` en verde. Se
    actualizan el recuento de pruebas (README, CLAUDE.md, reporte) y la sección 9 del reporte: la limitación «Si
    suena una pista del álbum al aplicarle una carátula, la reproducción se detiene…» deja de ser cierta.

### OUT OF SCOPE
- `File.Exists` y `Load` síncronos en el hilo de la interfaz, y el bloqueo de `ThemeViewModel.Initialize` al
  arrancar: van en `ui-responsiveness.md`.
- La carrera entre `Release` y `OnMixedEnd` (`BassAudioPlayer.cs:199-208` y `:260-276`): ventana de
  microsegundos, sin forma de provocarla. Queda anotada, sin arreglo.
- Reanudar tras «Eliminar del disco» o «Quitar de la biblioteca» de la pista que suena: ahí detenerse es lo
  correcto.
- Escribir etiquetas sin descargar la pista (evitar el corte): BASS mantiene el archivo abierto.
- Diálogo o ventana de errores, envío de informes, telemetría. El registro es un archivo local.
- Crossfade, cambios en el empalme sin silencio o en el formato de la cola.

### EDGE CASES
- **Archivo que existe pero no abre** (bloqueado, dañado): R1a muestra el mensaje del error; la pista no se
  marca como no disponible, porque el archivo está.
- **Sin pista en curso y sin selección:** Reproducir sigue arrancando la primera de la lista, como hoy.
- **Carrera en R2:** si `OnHeardEnd` ya promovió la pista encadenada a actual pero la interfaz aún no recibió
  `AdvancedToNext`, `OnTagsSaving` no la reconoce como la que suena y el archivo sigue abierto. La escritura
  falla con el aviso de siempre y `SafeFileEdit` deja el original intacto. Se acepta.
- **R3 con la escritura fallida:** la pista se reanuda igual, con sus datos anteriores, y el aviso de error del
  editor no se pisa.
- **R3 por lotes** (carátula de álbum, autocompletar): se reanuda una sola vez, al terminar todo el lote, no por
  cada archivo.
- **R3 y el archivo desaparece durante la escritura:** aviso de «no disponible» y nada en reproducción, como
  hace hoy `ResumePlayback`.
- **R3 y el usuario elige otra pista mientras se escribe:** no se reanuda la anterior (gana lo último que pidió
  el usuario).
- **R3 con la pista terminada o parada antes de guardar:** se recarga parada al principio; no arranca sola.
- **R1c durante el arranque:** si el error ocurre antes de que exista la línea de estado, basta con el registro.
- **R1c en bucle:** un error que se repite en cada fotograma (dibujo de la letra) no debe llenar el disco: el
  tope de 256 KB lo cubre, y el mismo mensaje consecutivo se registra una vez con un contador.

## Decisiones que confirmar antes de implementar
1. **R1c: seguir abierta tras un error inesperado.** Alternativa: registrar y cerrar. Se propone seguir, porque
   para un reproductor es peor perder la sesión que mostrar un aviso; el riesgo es continuar con un estado
   incoherente.
2. **R3: reanudar siempre.** Alternativa: mantener el corte actual y solo avisar. Se propone reanudar, que es lo
   que ya hace el menú de capas del inmersivo.

## Verificación
- `dotnet test tests/MusicPlayer.Core.Tests --filter "FullyQualifiedName~PlayQueueTests"` y la clase nueva del
  registro de errores en Infrastructure.
- A mano en Windows, con música de prueba copiada a una carpeta temporal:
  1. Dejar que termine la última pista de la cola, renombrar su archivo en el Explorador y pulsar Espacio: la
     app sigue abierta y avisa (R1a).
  2. Con «Repetir una», repetir el paso anterior justo antes del final (R1a).
  3. Incrustar una capa desde el inmersivo estando en pausa a mitad de pista y pulsar Reproducir: sigue desde
     ahí (R1b).
  4. Editar las etiquetas de la pista que suena y guardar: sigue sonando en el mismo punto, con el título nuevo
     (R3a); repetir con el inmersivo abierto y cerrado (R3b) y comprobar «Siguiente» (R3c).
  5. Aplicar una carátula de álbum con una pista de ese álbum sonando (R3a por lotes).
  6. Cola de dos pistas con la primera renombrada: «Anterior» y luego «Siguiente» (R5).
  7. R2: con dos pistas cortas seguidas, pedir «Eliminar del disco…» de la segunda y confirmar durante el último
     medio segundo de la primera. Si no se acierta la ventana a mano, se deja constancia de que solo se verificó
     por lectura.
- `Dispatcher.UIThread.UnhandledException` debe existir en Avalonia 11.2.5; se confirma al implementar R1c.

## Orden de trabajo
1. Pruebas rojas de los criterios 3 y 9.
2. R1a, R1b y R4 (los que cierran la app), luego R5, R2 y R3.
3. R1c.
4. R6 solo si se reproduce.
5. Verificación manual, documentación y recuento de pruebas.
