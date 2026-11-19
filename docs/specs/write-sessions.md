# Spec: sesión de escritura con contador

Estado: implementada el 16 de noviembre de 2026, a petición del autor (cuarta mejora de la auditoría de ese día).
Compila con 0 errores y la advertencia CS8602 de siempre, y pasan las 1051 pruebas (840 en Core, 211 en
Infrastructure). Las diez pruebas nuevas son de `WriteSessions` y nacieron con la clase. El resto del cambio vive
en Desktop, sin pruebas automáticas: **pasada manual de las cinco herramientas hecha por el autor el 16 de
noviembre de 2026, en Windows: todo correcto** (no se anotó el resultado paso a paso).

## Context

Hasta ahora, cada herramienta que escribe en un archivo de audio seguía a mano un protocolo de dos eventos: `Saving`
(antes de escribir, para que `MainViewModel` suelte el archivo) y `Finished` (al terminar, bien o mal, para que lo
vuelva a cargar). `MainViewModel` guardaba lo que sonaba en un solo hueco (`_interrupted`) y lo reanudaba en el
primer `Finished` que llegara.

Problemas, vistos en la auditoría del 16 de noviembre de 2026:

| Qué | Efecto |
| --- | ------ |
| Dos escrituras solapadas: una herramienta lenta («Limpiar etiquetas» sobre muchas pistas, entre ellas la que suena) y otra rápida que termina antes («Incrustar letra» en otra pista) | El `Finished` de la rápida reanudaba la pista que sonaba mientras la lenta todavía la escribía: el archivo quedaba abierto y esa escritura fallaba (el original quedaba intacto) |
| Lo mismo, sin pista interrumpida | El `Finished` de la rápida volvía a preparar la pista siguiente (`UpdateGapless`), que la lenta podía estar a punto de escribir |
| El protocolo se cumplía a mano en cuatro ViewModel, con `try/finally` y una bandera `saving` en dos de ellos | Una herramienta nueva podía olvidar `Finished` y dejar la app en silencio |
| `OnTagsSaving` y `OnAutoFixSaving` eran el mismo manejador escrito dos veces | Duplicación |

## FEATURE: `WriteSessions` (Core) sustituye a los eventos `Saving` y `Finished`

### ACCEPTANCE CRITERIA

1. `WriteSessions.Begin(ids)` avisa con `Releasing(ids)` y devuelve un `IDisposable`. Mientras haya alguna sesión
   abierta, `IsOpen` es true. `AllFinished` se dispara una sola vez, al cerrarse la última. Con pruebas en Core.
2. Liberar una sesión dos veces no descuadra el contador. Si `Releasing` lanza, la sesión se cierra (y se avisa
   con `AllFinished` si era la última) y la excepción llega a quien llamó.
3. Las cuatro herramientas (`TagEditorViewModel`, `TagAutoFixViewModel`, `AlbumCoverViewModel`,
   `EmbedLyricsViewModel`, esta última con sus dos caminos) abren una sesión antes de escribir y la cierran en su
   `finally`. Ya no tienen los eventos `Saving` ni `Finished`.
4. `MainViewModel` conecta `Releasing` a un solo manejador (`OnWriting`), que suelta la pista preparada y descarga
   la que suena si está entre las afectadas, y `AllFinished` a `ResumeInterrupted`.
5. Con dos escrituras solapadas, la pista interrumpida no se vuelve a cargar hasta que terminan las dos.
6. Mientras hay una sesión abierta, `UpdateGapless` no prepara ninguna pista; al cerrarse la última se vuelve a
   preparar.
7. Con una sola escritura a la vez, todo se comporta como antes.

### OUT OF SCOPE

- Impedir que el usuario reproduzca a mano, durante una escritura larga, una pista que se está escribiendo: ya
  pasaba y sigue igual (la escritura de esa pista falla y el original queda intacto).
- Borrar pistas (`RemoveTrackAsync`): no usa sesión; suelta el archivo por su cuenta, como antes.
- La conversión con ffmpeg: no escribe en los originales.
- Dividir `MainViewModel`.

### EDGE CASES

- **Escritura larga y el usuario sigue oyendo otras pistas:** los empalmes de ese rato tienen un pequeño silencio,
  porque no se prepara la siguiente (criterio 6). Vuelven a ser sin silencio al terminar.
- **El usuario eligió otra pista mientras se escribía:** al cerrarse la última sesión gana lo último que pidió,
  como antes (`ResumeInterrupted`).
- **Dos sesiones interrumpen pistas distintas** (la primera descarga X, el usuario pone Z y la segunda escribe
  en Z): se reanuda la última interrumpida, Z.
- **Sesión sin pistas:** cuenta como sesión; no descarga nada.

## Resultado

- `src/MusicPlayer.Core/Services/WriteSessions.cs` (nuevo) y `tests/MusicPlayer.Core.Tests/WriteSessionsTests.cs`
  (10 pruebas).
- Los cuatro ViewModel de edición reciben `WriteSessions` y pierden `Saving` y `Finished`; desaparece la bandera
  `saving` de `AlbumCoverViewModel` y `EmbedLyricsViewModel`.
- `MainViewModel`: `OnWriting` sustituye a `OnTagsSaving` y `OnAutoFixSaving`; `UpdateGapless` no prepara nada con
  una sesión abierta. Recibe `WriteSessions` (decimoquinta dependencia del constructor).
- `CompositionRoot.cs`: `WriteSessions` registrado como singleton.

## Pasada manual (hecha por el autor el 16 de noviembre de 2026, Windows)

Con una pista sonando a mitad de canción, en cada paso debe notarse un corte breve y seguir en el mismo punto:

1. **Editar etiquetas** de la pista que suena y guardar. Repetir con un error forzado (un año no válido no llega
   a escribir; un archivo de solo lectura sí): la pista vuelve igual.
2. **Editar etiquetas con carátula nueva** de la pista que suena.
3. **«Limpiar etiquetas»** con la pista que suena entre las elegidas, y después **deshacer**.
4. **«Carátula del álbum…»** en el álbum de la pista que suena.
5. **«Incrustar letra (.lrc)…»** desde la lista y **«Incrustar traducción…»** desde el modo inmersivo: el
   inmersivo se reabre solo.

Además:

6. Repetir 1 y 5 con la pista **en pausa**: vuelve en pausa y en el mismo punto.
7. Escribir en **otra** pista mientras suena una: la música no se corta y el empalme con la siguiente sigue sin
   silencio.
8. **Solapadas:** lanzar «Limpiar etiquetas» sobre muchas pistas (entre ellas la que suena) y, mientras escribe,
   incrustar la letra en otra pista. La pista que sonaba no debe volver hasta que termine la limpieza, y ninguna
   de las dos debe dar error de archivo en uso.
9. Cancelar un selector (carátula del álbum, letra) no corta la música.
