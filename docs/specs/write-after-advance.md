# Spec: escribir o borrar justo después de un avance automático

Estado: implementada el 16 de noviembre de 2026, a petición del autor (arreglo decidido el 15 de noviembre de
2026 en `docs/pendientes.md`). Compila con 0 errores y la advertencia CS8602 de siempre, y pasan las 1041 pruebas
(830 en Core, 211 en Infrastructure), las mismas que antes: el cambio vive en `MainViewModel` y
`BassAudioPlayer`, sin pruebas automáticas. **El caso no se puede provocar a mano** (la ventana dura lo que
tarda la interfaz en atender un aviso): está comprobado solo por lectura. **Pasada manual de regresión hecha por
el autor el 16 de noviembre de 2026, en Windows: todo correcto** (no se anotó el resultado paso a paso).

## Context

Es el hueco «sin cubrir» de `audit-3.md` (M2).

Cuando una pista termina y la siguiente ya estaba encadenada, `BassAudioPlayer` cambia de pista en su hilo y
encola el aviso `AdvancedToNext` para la interfaz. Hasta que ese aviso se atiende, el reproductor ya tiene abierto
el archivo de la pista nueva, pero `CurrentTrack` sigue siendo la anterior. Quien decide en ese momento si un
archivo está abierto comparando con `CurrentTrack` se equivoca en los dos sentidos:

| Se escribe o borra… | Qué pasaba |
| ------------------- | ---------- |
| La pista nueva (la que ya suena) | No se descargaba: la escritura fallaba con el archivo abierto (el original quedaba intacto y el aviso lo decía) |
| La pista anterior (la que aún se ve) | Se descargaba la nueva creyendo que era la anterior, y al terminar se reanudaba la anterior en la posición de la nueva |

Afectaba a `OnTagsSaving` (etiquetas, carátula, carátula del álbum, letra), `OnAutoFixSaving` («Limpiar
etiquetas») y `RemoveTrackAsync` (quitar o eliminar del disco).

## FEATURE: Antes de soltar un archivo, la interfaz se pone al día con el reproductor

### ACCEPTANCE CRITERIA

1. `IAudioPlayer.CurrentFilePath` devuelve la ruta del archivo abierto como pista en curso, o null sin pista.
   Tras un avance automático ya es la de la pista nueva, aunque el aviso no se haya atendido.
2. `OnTagsSaving`, `OnAutoFixSaving` y `RemoveTrackAsync` llaman a `CatchUpWithPlayer` antes de comparar con
   `CurrentTrack`: si el reproductor ya está en la pista que la cola da por siguiente, se atiende el avance en ese
   momento (cola, título, letra, carátula) y el aviso en cola se descarta para que la cola no avance dos veces.
3. Después de eso, la decisión de descargar es la de siempre (por Id contra `CurrentTrack`), ya con la pista
   correcta: escribir en la que suena la descarga y la reanuda en su punto; escribir en la anterior no toca la
   reproducción.
4. Sin avance pendiente, nada cambia: `CatchUpWithPlayer` no hace nada.

### OUT OF SCOPE

- La sesión de escritura con contador (escrituras solapadas de dos herramientas): mejora aparte de la auditoría.
- El hueco de `RemoveTrackAsync` cuando el guardado falla (punto 9 de `docs/pendientes.md`).
- Pruebas de Desktop o de Audio: el proyecto no las tiene.

### EDGE CASES

- **«Repetir una»:** la pista avanza a sí misma y la ruta no cambia, así que no hay nada que poner al día; la
  comparación por Id ya acierta y el aviso en cola se descarta al descargar (`PlaybackEpoch`).
- **La ruta difiere por otro motivo** (una pista reubicada mientras suena): solo se pone al día si la ruta del
  reproductor es la de la pista que la cola da por siguiente (`PeekNextAfterEnd`). Si no, se queda como antes.
- **La cola cambió justo antes del salto** y lo que suena no es lo que la cola da por siguiente: no se pone al
  día; el aviso en cola lo resuelve después, como hasta ahora. Queda sin cubrir: una escritura en ese instante
  exacto puede fallar con el archivo abierto, sin dañar el original.
- **Orden:** `CatchUpWithPlayer` va antes de `PrepareNext(null)`, porque ponerse al día vuelve a preparar la
  pista siguiente y eso reabriría un archivo que se acaba de soltar.

## Resultado

- `src/MusicPlayer.Core/Interfaces/IAudioPlayer.cs`: propiedad `CurrentFilePath`.
- `src/MusicPlayer.Audio/BassAudioPlayer.cs`: la implementa con `_current?.FilePath`.
- `src/MusicPlayer.Desktop/ViewModels/MainViewModel.cs`: `CatchUpWithPlayer`, llamado desde los tres sitios.
- Sin pruebas nuevas: no hay lógica nueva en Core.

## Pasada manual de regresión (hecha por el autor el 16 de noviembre de 2026, Windows)

El caso en sí no se puede provocar a mano; esto comprueba que lo de siempre sigue igual.

1. Editar las etiquetas de la pista que suena: se nota un corte breve y sigue en el mismo punto.
2. Editar las etiquetas de otra pista mientras suena una: la música no se corta, y el empalme con la siguiente
   sigue sin silencio.
3. «Limpiar etiquetas» con la pista que suena entre las elegidas: vuelve al mismo punto al terminar.
4. Eliminar del disco la pista que suena: se detiene y el archivo se borra. Eliminar otra: la música sigue.
5. Dejar que varias pistas se encadenen solas y, nada más empezar una, incrustarle la letra: se escribe y la
   pista vuelve a su punto.
6. Con «repetir una», incrustar la letra en la pista que suena: igual que antes.
