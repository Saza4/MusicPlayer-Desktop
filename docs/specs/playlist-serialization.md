# Spec: las operaciones de `PlaylistManager`, una a la vez

Estado: implementada el 16 de noviembre de 2026, a petición del autor (tercera mejora de la auditoría de ese día).
Compila con 0 errores y la advertencia CS8602 de siempre, y pasan las 1041 pruebas (830 en Core, 211 en
Infrastructure). De las cinco pruebas nuevas, cuatro se vieron fallar antes del arreglo; la del guardado fallido
nació en verde: fija que un fallo no deja bloqueadas las operaciones siguientes.

## Context

Era el punto 2 de `docs/pendientes.md`, anotado antes en `library-data-safety-2.md` (D3).

Cada operación de `PlaylistManager` parte de `_playlists`, guarda una lista nueva y, si el guardado sale bien, la
asigna. Con dos operaciones a la vez, las dos parten de la misma lista y la segunda en guardar descarta el cambio
de la primera, en el disco y en la memoria. `LibraryService` ya lo resuelve con `ExclusiveAsync`.

Camino real: «Nueva playlist…» desde el menú de una canción hace `CreateAsync` y después `AddTracksAsync`; si
entre los dos llega otro cambio (por ejemplo, la limpieza de playlists al quitar pistas de la biblioteca), se
pierde uno. La comprobación del nombre repetido tenía el mismo hueco: dos altas simultáneas con el mismo nombre
pasaban las dos.

## FEATURE: Una operación a la vez sobre `_playlists`

### ACCEPTANCE CRITERIA

1. Dos operaciones de escritura simultáneas (`CreateAsync`, `RenameAsync`, `DeleteAsync`, `AddTracksAsync`,
   `RemoveTrackAsync`, `RemoveTracksEverywhereAsync`) conservan los dos cambios, en la memoria y en lo guardado.
2. La validación del nombre se hace dentro del turno: de dos altas simultáneas con el mismo nombre, la segunda
   falla con «Ya existe una playlist con ese nombre.».
3. Un guardado que falla no bloquea las operaciones siguientes, y la memoria sigue siendo lo último guardado.
4. Una operación cancelada mientras espera su turno no hace nada y no bloquea a las demás.
5. Con una sola operación a la vez, todo se comporta como antes: las pruebas que ya había siguen en verde,
   incluidas las de «carga fallida» y la de no guardar cuando no hay nada que cambiar.

### OUT OF SCOPE

- `LoadAsync`: se llama una vez al arrancar, antes de cualquier operación; queda fuera del turno, como en
  `LibraryService`.
- Agrupar `CreateAsync` y `AddTracksAsync` en una sola operación («crear con pistas»): con el turno ya no se
  pierde nada, y si la segunda falla queda una playlist vacía con su aviso, como hoy.
- `PlaylistViewModel` y el resto de Desktop: no cambian.
- `JsonLibraryRepository` sobre `AtomicJson` y la sesión de escritura de los editores: otras mejoras de la
  auditoría.

### EDGE CASES

- **Cancelar justo cuando llega el turno:** la operación puede llegar a ejecutarse (así se comporta
  `SemaphoreSlim`). No se pierde nada: o se hace entera o no se hace.
- **Operación que no cambia nada** (añadir pistas que ya están, quitar una que no está): espera su turno y no
  guarda, como antes.
- **Carga fallida:** la negativa a guardar sigue en `CommitAsync`; una operación que no llega a guardar no
  lanza, como antes.
- **Playlist borrada mientras otra operación esperaba:** la que esperaba falla con «La playlist no existe.», en
  lugar de resucitarla.

## Resultado

- `src/MusicPlayer.Core/Services/PlaylistManager.cs`: `SemaphoreSlim` y `ExclusiveAsync`; las seis operaciones de
  escritura pasan por ahí. La firma pública no cambia.
- Pruebas nuevas (5) en `PlaylistManagerTests`, con un repositorio que retiene los guardados (`HeldRepository`):
  `TwoAddsAtOnce_KeepBothTracks`, `CreateAndAddToAnotherAtOnce_KeepBothChanges`,
  `TwoCreatesWithTheSameNameAtOnce_OnlyTheFirstSucceeds`, `AfterAFailedSave_TheNextOperationStillRuns` y
  `CancelledWhileWaitingItsTurn_DoesNothing_AndDoesNotBlockTheRest`.
- No se probó en la app: el cambio es lógica pura de Core.
