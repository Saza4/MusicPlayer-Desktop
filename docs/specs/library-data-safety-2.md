# Spec: datos de la biblioteca, segunda parte (derivada de la auditoría del 11 de noviembre de 2026)

Estado: aprobada e implementada el 11 de noviembre de 2026 (D1 a D7; en D6 se decidió borrar). Compila y pasan
las 929 pruebas (732 en Core, 197 en Infrastructure). `JsonLibraryConcurrentSaveTests` pasó 50 veces seguidas en
Windows. Diez pruebas (las de `LibraryServiceSaveFailureTests` y cuatro de `AtomicJsonTests`) se vieron fallar
antes del cambio; las del escáner, las de rutas y la de dos respaldos del repositorio nacieron con su código.
**Verificación manual hecha por el autor el 11 de noviembre de 2026, en Windows: todo correcto** (no se anotó el resultado paso a paso). D5 no tiene prueba automática: depende solo de esa pasada.

## Context

Tercera spec de la auditoría. Continúa `library-data-safety.md`, que dejó fuera S3 (ffmpeg huérfano al cerrar) y
los hallazgos de largo plazo. Cubre los guardados de `library.json`, `playlists.json` y `settings.json`, y los
temporales `~conv-*` y `~tags-*` que la app deja junto a la música.

D2 está reproducido; el resto se comprobó leyendo el código que se cita.

| Id | Hallazgo | Evidencia |
| -- | -------- | --------- |
| D1 | Si guardar falla, la biblioteca en memoria ya cambió y la interfaz no se entera. Con «Eliminar del disco», el archivo ya está borrado y la pista sigue en la lista como si nada | `LibraryService.cs`: `_current = …` y después `SaveAsync` en las cinco operaciones (`ScanFolderCoreAsync`, `RemoveTracksCoreAsync`, `ApplyTagsCoreAsync`, `ApplyTagsBatchCoreAsync`, `AddFilesCoreAsync`); `MainViewModel.RemoveTrackAsync` cae al `catch` sin tocar la cola ni la lista |
| D2 | Dos guardados simultáneos de `library.json` fallan a veces en Windows | `JsonLibraryRepository.cs:74` (`File.Move` con sobrescritura). `JsonLibraryConcurrentSaveTests` falló 15 de 40 ejecuciones aisladas con `UnauthorizedAccessException: Access to the path is denied` |
| D3 | `AtomicJson` usa siempre el mismo `.tmp` y no lo borra si falla la escritura | `AtomicJson.cs`: `path + ".tmp"`, sin `try/finally`. `PlaylistManager` no serializa sus operaciones |
| D4 | Un segundo JSON dañado pisa el respaldo del primero | `File.Move(…, ".corrupt", overwrite: true)` en `JsonLibraryRepository.cs:51` y en `AtomicJson.ReadAsync` |
| D5 | Cerrar la app durante una conversión deja ffmpeg vivo y, al terminar, un `~conv-*` completo junto a la música | `FfmpegAudioConverter.cs`: el proceso solo se mata al cancelar; `App.axaml.cs` (Exit) no cancela nada |
| D6 | Los `~conv-*` y `~tags-*` que quedan tras un cierre brusco no se limpian nunca | Solo se borran en el `finally` de `FfmpegAudioConverter` y `SafeFileEdit`; `LibraryScanner.IsTemporary` solo los ignora |
| D7 | En Linux, `A.mp3` y `a.mp3` de la misma carpeta se funden en una sola pista | `LibraryService.cs:61-62` y `:188-189` (`StringComparer.OrdinalIgnoreCase`) |

En la app, D2 queda tapado porque `LibraryService` ya serializa sus operaciones (`ExclusiveAsync`); el
repositorio promete algo que no cumple y su prueba falla de forma intermitente, también en CI.

## FEATURE: Guardados coherentes y temporales que no se acumulan

### OVERVIEW
Que la memoria y el disco digan lo mismo aunque un guardado falle, que los repositorios JSON aguanten escrituras
simultáneas y conserven todos los respaldos, y que los temporales de la app no se queden para siempre junto a la
música.

### ACCEPTANCE CRITERIA

1. **D1a. La memoria solo cambia si el guardado sale bien.** En las cinco operaciones de `LibraryService`, si
   `SaveAsync` lanza, `Current` sigue siendo la instantánea anterior y la excepción llega a quien llamó. Una
   operación posterior correcta no arrastra el cambio fallido.
2. **D1b. Borrado con guardado fallido.** Si «Eliminar del disco» borra archivos y luego el guardado falla, la
   interfaz lo dice («Se borró el archivo, pero no se pudo guardar la biblioteca: …») y vuelve a comprobar la
   disponibilidad, de modo que esas pistas quedan marcadas como no disponibles y se pueden limpiar con «Quitar
   de la biblioteca…».
3. **D2. Guardados simultáneos.** Las escrituras de un mismo archivo JSON se serializan dentro de su
   repositorio (`JsonLibraryRepository`, y `AtomicJson` para playlists y ajustes). `JsonLibraryConcurrentSaveTests`
   pasa 50 veces seguidas en Windows.
4. **D3. Temporal único y limpio.** `AtomicJson.WriteAsync` escribe en un `.tmp` con nombre único y lo borra si
   la serialización o el `File.Move` fallan. Tras un fallo no queda ningún `*.tmp` y el archivo original no
   cambia.
5. **D4. Respaldos que no se pisan.** Un JSON dañado se guarda como `<nombre>.corrupt-<aaaaMMdd-HHmmss>` (con un
   sufijo numérico si ya existe). Se conservan los cinco más recientes por archivo; los más antiguos se borran.
   Un `.corrupt` de versiones anteriores no se toca.
6. **D5. ffmpeg muere con la app.** `FfmpegAudioConverter` lleva la cuenta de los procesos que lanzó y es
   `IDisposable`: al cerrar la app (el contenedor ya se libera en `Exit`) mata el árbol de cada uno y borra su
   `~conv-*`. No queda ningún proceso ffmpeg ni temporal de esa conversión.
7. **D6. Limpieza de temporales antiguos.** Al escanear una carpeta («Agregar carpeta»), los archivos que
   cumplen las tres condiciones se borran: nombre exacto `~conv-<32 hex>.<ext>` o `~tags-<32 hex>.<ext>`,
   extensión de audio admitida y última modificación hace más de 24 horas. La línea de estado dice cuántos
   («3 temporales antiguos eliminados»). Si uno no se puede borrar, se deja y el escaneo sigue. La regla va en
   Core (`LibraryScanner`) con pruebas.
8. **D7. Rutas según el sistema.** La comparación de rutas de `LibraryService` distingue mayúsculas en Linux y
   no las distingue en Windows ni en macOS. La regla es un único punto en Core.
9. **Pruebas que fallan antes del cambio:**
   - `LibraryServiceTests` (o `LibraryDataSafetyTests`): repositorio falso cuyo `SaveAsync` lanza, para las
     cinco operaciones → `Current` no cambia.
   - `AtomicJson` (prueba directa nueva): un valor que no se puede serializar → no queda `.tmp` y el archivo
     anterior sigue igual; dos lecturas de JSON dañado → dos respaldos distintos; siete → quedan cinco.
   - `JsonLibraryRepositoryTests`: dos cargas de JSON dañado → dos respaldos.
   - `LibraryScannerTests`: temporal de 25 horas → borrado y contado; de 1 hora → se queda; `~tags-notas.mp3` y
     `~conv-<32 hex>.txt` → se quedan.
   - Comparador de rutas: una prueba por rama (sensible y no sensible), sin depender del sistema donde corre.
   - Sin reloj real: la antigüedad se fija con `File.SetLastWriteTimeUtc` y la hora del respaldo se inyecta.
10. **Cierre.** `dotnet build` sin advertencias nuevas y `dotnet test` en verde. Se actualizan el recuento de
    pruebas y CLAUDE.md: la frase sobre `~tags-*`/`~conv-*` (ahora los antiguos se borran al escanear) y la de
    `AtomicJson` (respaldo con fecha en lugar de `.corrupt`).

### OUT OF SCOPE
- Avisar al usuario de que `library.json` estaba dañado y se empezó de cero, y recuperar los Id de las pistas
  desde el respaldo para no perder las playlists. Es el daño mayor de D4, pero pide diseño propio.
- Migrar `JsonLibraryRepository` a `AtomicJson` (su implementación propia es una excepción documentada).
- Forzar la escritura a disco (`Flush(true)`) antes del `File.Move`: protege de un corte de luz, no se puede
  probar y tiene coste en cada guardado.
- Job Objects de Windows para que ffmpeg muera también si la app se cae: D5 cubre el cierre normal.
- Limpiar temporales al arrancar o en carpetas que no se están escaneando.
- Otras comparaciones de rutas sin distinguir mayúsculas (`BassAudioPlayer.cs:146`, `MainViewModel.cs:612`):
  solo deciden si se reutiliza una pista ya preparada; Linux sigue sin probarse.
- Las pruebas con esperas reales (`SettingsServiceTests`, `LibraryDataSafetyTests`): van con la spec de
  documentación y pruebas.

### EDGE CASES
- **D1a con el escaneo:** si el guardado falla tras escanear, las pistas nuevas no aparecen y el aviso es el de
  error; volver a «Agregar carpeta» las encuentra otra vez.
- **D1a con etiquetas:** el archivo de audio ya tiene las etiquetas nuevas y la biblioteca muestra las viejas
  hasta el siguiente escaneo. Se acepta: es mejor que una memoria que no coincide con el disco.
- **D1a y la carga fallida:** el bloqueo de guardados tras una carga fallida (`library-data-safety.md`) no cambia.
- **D4 con el respaldo imposible** (carpeta de solo lectura): la carga falla con esa excepción, como hoy, y los
  guardados quedan bloqueados por la regla de carga fallida.
- **D5 sin conversión en curso:** liberar el conversor no hace nada.
- **D5 y un proceso que ya terminó:** matar un proceso terminado no lanza ni deja de borrar el temporal.
- **D6 y una conversión en curso en otra instancia de la app:** su temporal tiene menos de 24 horas y no se toca.
- **D6 y un archivo del usuario llamado así:** hace falta el prefijo, exactamente 32 dígitos hexadecimales y
  extensión de audio. CLAUDE.md ya declara esos prefijos como temporales de la app.
- **D6 y la fecha del archivo en el futuro o sin leer:** no se borra.
- **D7 en Windows:** sin cambios de comportamiento.

## Decisión que confirmar antes de implementar
**D6: borrar automáticamente.** La app borraría archivos de la carpeta de música sin preguntar. La alternativa es
solo contarlos y avisar («Hay 3 temporales antiguos en esta carpeta») para que los borres tú. Se propone borrar,
porque el patrón es estrecho (prefijo, 32 hexadecimales, extensión de audio, más de 24 horas) y son copias
completas de audio que ocupan espacio y nadie más va a limpiar.

## Verificación
- `dotnet test`; además, en Windows, 50 ejecuciones seguidas de
  `--filter "FullyQualifiedName~JsonLibraryConcurrentSaveTests"`.
- A mano en Windows, con música de prueba en una carpeta temporal y ffmpeg instalado:
  1. Empezar «Convertir lo que veo…» con varias pistas y cerrar la ventana a mitad: en el Administrador de
     tareas no queda `ffmpeg.exe` y en la carpeta no queda ningún `~conv-*` (D5).
  2. Marcar `library.json` como de solo lectura, pedir «Eliminar del disco…» de una pista de prueba y comprobar
     el aviso y que la pista queda como no disponible (D1b). Quitar el atributo al terminar.
  3. Copiar un audio de prueba como `~conv-0123456789abcdef0123456789abcdef.mp3`, ponerle fecha de hace dos
     días y usar «Agregar carpeta»: desaparece y la línea de estado lo dice (D6).
- D7 no se puede verificar a mano en Windows: queda cubierto por sus pruebas y por el CI de Ubuntu.

## Orden de trabajo
1. Pruebas rojas del criterio 9.
2. D1 (servicio e interfaz), D2 a D4 (repositorios), D6 y D7 (Core), D5 (conversor).
3. Verificación manual, documentación y recuento de pruebas.
