# Spec: arreglos de la segunda auditoría (11 de noviembre de 2026)

Estado: implementada el 11 de noviembre de 2026, a petición del autor («encárgate de todo esto»), sin ronda de
aprobación previa: las decisiones tomadas están en «Decisiones» por si quiere cambiarlas. Compila con la
advertencia CS8602 de siempre y pasan las 958 pruebas (754 en Core, 204 en Infrastructure). Las pruebas de los
criterios 4, 5 y 6 se vieron fallar antes del cambio; las de `PlayQueue.Replace` nacieron con el método.
**Falta la verificación manual** de los criterios 1, 2 y 3 (lista de «Verificación»): viven en `MainViewModel`,
sin pruebas automáticas, y nada de esto se ha visto con la app abierta.

## Context

Tras las specs 1 a 5 se hizo otra auditoría de solo lectura (enfoques A, B y C de la skill `auditor`: convenciones,
archivos y entradas no confiables, audio y concurrencia). No encontró nada crítico ni incumplimientos de
CLAUDE.md. Dejó dos hallazgos de corto plazo, cuatro de largo plazo y documentación desactualizada.

| Id | Hallazgo | Evidencia (antes del cambio) |
| -- | -------- | ---------------------------- |
| A1 | Al reanudar en pausa una pista recién escrita, el reproductor vuelve a su punto pero la barra, el tiempo y la letra quedan en 0:00, y ← → saltan desde el principio | `MainViewModel.ResumePlayback`: `ShowTrack` pone `PositionSeconds = 0`, el `Seek` no lo actualiza y `OnTick` no escribe en pausa |
| A2 | La cola conserva las instancias de `TrackItemViewModel` anteriores a cada `ApplySnapshot`: «ahora suena» muestra el título viejo de una pista editada, y una pista reubicada se marca como no disponible por su ruta vieja | `MainViewModel.ApplySnapshot` crea todas las instancias de nuevo; `Next`, `OnTrackEnded` y `OnAdvancedToNext` usan las de la cola |
| A3 | Escribir en una pista que no es la que suena («Incrustar letra», «Carátula del álbum…», o un guardado de etiquetas que falla) deja sin preparar la siguiente: esa transición tiene un hueco | `OnTagsSaving` llama a `PrepareNext(null)` y solo `OnTagsSaved` y `ResumePlayback` volvían a `UpdateGapless()` |
| A4 | `JsonThemeRepository` lanza `InvalidOperationException` con un texto que lleva un sustituto UTF-16 suelto escapado (`"\uD800"`): un archivo así dejaba sin cargar todos los temas de usuario | El filtro del `catch` solo cubría `IOException`, `UnauthorizedAccessException` y `JsonException`. Reproducido con tres pruebas (nombre, color y clave) |
| A5 | Si `settings.json` no se puede leer por E/S, el siguiente cambio guarda los valores por defecto encima | `SettingsService.LoadAsync` sin la regla de «carga fallida» que sí tienen `LibraryService` y `PlaylistManager` |
| A6 | Las carátulas se leen sin tope de tamaño: un archivo enorme llamado `cover.jpg` se lee entero por cada pista de la carpeta | `FolderCoverArtProvider` (`File.ReadAllBytesAsync`) y `EmbeddedCoverArtProvider`; el tope de 10 MB solo se aplicaba al elegir una imagen |
| A7 | El reporte seguía en el 8 de noviembre (v1.4.0 «como borrador», 813 pruebas, piezas nuevas sin listar) y dos specs daban por pendiente lo ya fusionado | `docs/reporte-reproductor.md`, `lrc-enhanced.md`, `lrc-enhanced-fill.md` |

## FEATURE: Reanudación, cola y lecturas con sus bordes cerrados

### ACCEPTANCE CRITERIA

1. **A1.** Tras reanudar una pista en una posición (`ResumePlayback`), `PositionSeconds` vale esa posición, sonando
   o en pausa: la barra, el tiempo, la letra y los saltos de ← → parten de ahí. Asignarla no provoca otro `Seek`.
2. **A2.** Después de cada `ApplySnapshot`, la cola tiene las mismas pistas, en el mismo orden y en la misma
   posición, pero con las instancias nuevas (por Id). Las pistas que ya no están en la biblioteca se quedan como
   estaban. La lógica va en Core (`PlayQueue.Replace`) con pruebas: orden normal, aleatorio, pistas pendientes de
   «Reproducir siguiente» y cola vacía.
3. **A3.** Cuando una escritura termina (`Finished`) y no hay nada que reanudar, se vuelve a preparar la pista
   siguiente.
4. **A4.** Un tema con un sustituto suelto escapado en el nombre, en un color o en una clave es un error de ese
   archivo («JSON mal formado») y los demás temas se cargan.
5. **A5.** Si la carga de los ajustes lanza (salvo cancelación), `SettingsService` no guarda nada hasta una carga
   correcta. Los cambios de la sesión sí valen en memoria. La línea de estado lo dice al arrancar.
6. **A6.** Una imagen de más de 10 MB (`CoverImage.MaxBytes`) se trata como ausente: en la carpeta se pasa a la
   siguiente candidata (el tamaño se mira antes de abrir) y la incrustada da «sin carátula». Con 10 MB justos se
   lee.
7. **A7.** El reporte dice la release publicada, el recuento de pruebas commit a commit y las piezas nuevas; las
   specs de los `.lrc` enhanced dicen que están fusionadas.

### OUT OF SCOPE
- El plan de la auditoría y `ux-quick-wins.md` (specs 6 y 7): no están en el repositorio y hay que escribirlos.
- R6 y el congelamiento con unidades de red: spec propia, como ya estaba decidido.
- `PlaylistManager` no serializa sus operaciones (anotado en `library-data-safety-2.md`).
- Los enfoques D, E, F y H de la auditoría (Avalonia, isla, rendimiento, higiene): no se revisaron.

### EDGE CASES
- **A1 con la posición en 0 o más allá del final:** no se salta y la barra queda en 0:00, como antes.
- **A2 y la pista que suena:** `CurrentTrack` no se cambia; si se escribió en ella, `ResumeInterrupted` ya la
  recarga con la instancia nueva.
- **A3 tras guardar etiquetas con éxito:** `OnTagsSaved` ya había preparado la siguiente; la segunda llamada no
  hace nada porque la ruta es la misma.
- **A5 y las dos cargas del arranque** (`ThemeViewModel.Initialize` y `MainViewModel.InitializeAsync`): cuenta la
  última. Si la segunda sale bien, se vuelve a guardar.
- **A5 con el JSON dañado:** no es un fallo de carga; queda como `.corrupt-<fecha>` y se empieza de cero, como
  antes.
- **A6 y una portada legítima de más de 10 MB incrustada por otra herramienta:** deja de verse. Se acepta: es el
  mismo tope que la app impone al escribirla.

## Decisiones (tomadas al implementar; revisables)
1. **A5: bloquear el guardado**, igual que la biblioteca y las playlists. La alternativa era dejarlo como estaba y
   escribir en CLAUDE.md que los ajustes quedan fuera a propósito.
2. **A6: el tope es el de escritura, 10 MB.** No se añadió un límite propio para lectura.
3. **A4: el mensaje es «JSON mal formado»**, el mismo que para un JSON que no se puede analizar.

## No verificable por lectura (para la pasada manual)
- **D5 con un lote** (`library-data-safety-2.md`): al cerrar, `Dispose` mata el `ffmpeg` en marcha, pero
  `ConversionService` trata ese fallo como un error más y pasa a la pista siguiente. Que llegue a lanzar otro
  proceso depende de si Avalonia sigue despachando continuaciones tras `Exit`. El paso 1 de esa spec debe hacerse
  con varias pistas, mirando el Administrador de tareas unos segundos después de cerrar.
- **U4** (`ui-responsiveness.md`): depende de cómo entrega Avalonia los eventos de puntero del `Slider`.
- **R6** sigue abierto.

## Verificación
- `dotnet test` (958). Clases tocadas: `PlayQueueTests`, `SettingsServiceTests`, `JsonThemeRepositoryTests`,
  `FolderCoverArtProviderTests` y la nueva `EmbeddedCoverArtProviderTests`.
- A mano en Windows, con música de prueba en una carpeta temporal:
  1. Pausar a mitad de pista, editar sus etiquetas y guardar: la barra y el tiempo siguen en ese punto; → salta
     5 s desde ahí y Reproducir continúa (A1). Es el paso 3 de `playback-resilience.md`.
  2. Con dos pistas en cola y la primera sonando, cambiar el título de la segunda: al llegar a ella, «ahora suena»
     y la isla muestran el título nuevo (A2).
  3. Con dos pistas en cola y la primera sonando, «Incrustar letra (.lrc)…» en una tercera: el paso a la segunda
     sigue sin hueco (A3).
