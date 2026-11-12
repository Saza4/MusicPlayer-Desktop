# Spec: pruebas sin esperas reales y documentación al día

Estado: aprobada e implementada el 11 de noviembre de 2026, con la función inyectada. Compila y pasan las 959
pruebas (755 en Core, 204 en Infrastructure). `SettingsServiceTests` pasó 100 veces seguidas. La prueba de la
ráfaga se vio fallar quitando la cancelación de `_pending` (esperaba 4 cancelaciones y hubo 0). La del criterio 3
nació en verde: cubre un comportamiento que ya existía y no tenía prueba.

## Context

Antes de esta spec, `dotnet test` tenía 958 pruebas y una dependía del reloj real: podía fallar sin que el
código estuviera mal.

| Prueba | Qué hace | Riesgo |
| ------ | -------- | ------ |
| `SettingsServiceTests.Update_Burst_SavesOnlyOnceAfterTheDelay` | Crea el servicio con un retraso de 250 ms, hace cinco cambios seguidos, espera 1,5 s con `Task.Delay` y exige un solo guardado | Si la máquina del CI se detiene más de 250 ms entre dos cambios, hay dos guardados y la prueba falla. Además añade 1,5 s fijos a cada ejecución |

La otra espera real, `LibraryDataSafetyTests.ConcurrentOperations_NeverSaveAtTheSameTime_AndKeepEveryChange`
(80 ms dentro de un repositorio falso), **no** tiene ese riesgo: el retraso solo ensancha la ventana en la que dos
guardados se solaparían, y lo que se comprueba (`MaxConcurrent == 1`) no depende de cuánto dure.

`SettingsService` espera con `Task.Delay(_delay, ct)` y no hay forma de sustituir el reloj desde una prueba.

## FEATURE: El guardado diferido de los ajustes se prueba sin reloj

### ACCEPTANCE CRITERIA

1. `SettingsService` recibe la espera como dependencia opcional: un parámetro
   `Func<TimeSpan, CancellationToken, Task>? delay = null` que por defecto es `Task.Delay`. El registro en
   `CompositionRoot.cs` no cambia y el comportamiento en la app es el mismo.
2. La prueba de la ráfaga usa una espera falsa que la propia prueba completa a mano. Comprueba tres cosas, sin
   ningún `Task.Delay` ni `Thread.Sleep`:
   - mientras la espera no termina, no hay ningún guardado;
   - cada cambio nuevo cancela la espera anterior (cinco cambios, cuatro cancelaciones);
   - al completar la última espera hay exactamente un guardado, con el último valor.
3. Una prueba nueva cubre lo que hoy no se prueba: si el guardado falla, el siguiente cambio vuelve a guardar
   (`_dirty` se repone).
4. En `tests/` no queda ningún `Task.Delay` ni `Thread.Sleep` fuera de `LibraryDataSafetyTests` (el caso
   justificado arriba, que se deja con un comentario que lo explique).
5. Documentación: el recuento de pruebas de `README.md`, `CLAUDE.md` y `docs/reporte-reproductor.md` coincide con
   el de `dotnet test`. La hoja de ruta del reporte (sección 10) enlaza `docs/pendientes.md`.

### OUT OF SCOPE
- `TimeProvider` y el paquete `Microsoft.Extensions.TimeProvider.Testing`: sirven para lo mismo, pero añaden un
  paquete a las pruebas para sustituir una sola llamada.
- Cualquier cambio en el retraso de 500 ms o en `FlushAsync`.
- Pruebas de Desktop o de Audio (CLAUDE.md: no las hay).
- Los puntos sin spec de `docs/pendientes.md`.

### EDGE CASES
- **La espera falsa se cancela:** debe terminar con `OperationCanceledException`, como `Task.Delay`, o
  `SaveAfterDelayAsync` seguiría hasta guardar.
- **`FlushAsync` con una espera pendiente:** la cancela y guarda una sola vez (ya lo cubre
  `Update_ThenFlush_SavesLatestValue`; debe seguir pasando sin tocarla).
- **Carga fallida** (`audit-followup.md`, criterio 5): las dos pruebas existentes no cambian.

## Decisión
- **Función inyectada, no `TimeProvider`** (elegida por el autor). La función es una línea en el constructor y no añade
  paquetes. `TimeProvider` es lo habitual en .NET 8, pero su reloj falso viene en un paquete aparte.

## Verificación
- `dotnet test -c Release`, y la clase `SettingsServiceTests` cien veces seguidas sin fallos.
- La prueba de la ráfaga, vista fallar antes: quitar a mano la cancelación de `_pending` en `Update` y comprobar
  que da más de un guardado.
