# Spec: límites para entradas que no controla la app (derivada de la auditoría del 11 de noviembre de 2026)

Estado: aprobada e implementada el 11 de noviembre de 2026 (E1 a E5). Compila y pasan las 903 pruebas (712 en
Core, 191 en Infrastructure). Las 12 pruebas nuevas de comportamiento se vieron fallar antes del cambio. E3 no tiene
prueba automática. **Verificación manual hecha por el autor el 11 de noviembre de 2026, en Windows: todo correcto** (no se anotó el resultado paso a paso).

**Resultado del criterio 9:** E5 sí se reproduce. Dañando bytes de los audios de prueba (12 000 variantes de MP3,
FLAC y M4A con etiquetas), TagLib lanzó `ArgumentOutOfRangeException` con FLAC y M4A y `ArgumentException` con
M4A; con MP3 no apareció ninguna fuera de las previstas. El caso mínimo quedó como prueba fija
(`TagLibTrackMetadataReaderTests`: longitud del «vendor» del bloque de comentarios de un FLAC a 0xFFFFFFFF). La
exploración tardaba unos 3 minutos y no se subió. Esto cierra también el S1 de `library-data-safety.md`.

## Context

Segunda spec de la auditoría. Cubre dos entradas externas: los `.lrc` (y la letra incrustada) y las etiquetas
de los audios al escanear. Los temas de usuario ya tienen sus límites (50 archivos, 64 KB) y `JsonThemeRepository`
no lanza: se revisó y queda fuera.

Cada hallazgo se comprobó leyendo el código que se cita. Ninguno se ha reproducido con la app abierta.

| Id | Hallazgo | Evidencia |
| -- | -------- | --------- |
| E1 | Los `.lrc` se leen enteros sin mirar el tamaño. El tope de 1 MB solo se aplica al incrustar | `LrcFileLyricsProvider.cs:17`, `CompanionLyricsProvider.cs:70`; el tope está en `LyricsEditor.cs:10` y `:131` |
| E2 | Una línea con N marcas iniciales y M marcas por palabra crea N×M palabras | `LrcLyricParser.cs:47-54` (`Shift(words, delta)` por cada marca) |
| E3 | El `.lrc` se analiza en el hilo de la interfaz | `LrcFileLyricsProvider.cs:17` y `CompanionLyricsProvider.cs:60,70`: `Parse` corre en la continuación del `await`. `EmbeddedLyricsProvider` sí usa `Task.Run` |
| E4 | Un archivo cualquiera renombrado a `.lrc` se muestra como letra | `LrcLyricParser.Parse`: todo lo que no lleva marca es una línea de texto |
| E5 | Si el lector de etiquetas lanza una excepción no prevista, el escaneo entero falla y no se añade nada | `LibraryScanner.cs:44` y `:56` sin `try`; `TagLibTrackMetadataReader.cs:32-35` captura cuatro tipos |

**Sobre E5.** Es el mismo punto que `library-data-safety.md` cerró como «no reproducible» (S1), porque no se
encontró un archivo que hiciera lanzar a TagLib otra excepción. Aquí se propone protegerlo en `LibraryScanner`
(Core), donde sí se puede probar con un lector falso que lanza. Si prefieres mantener la regla anterior
(sin archivo que lo provoque, no se toca), E5 se quita de esta spec.

## FEATURE: Letras y etiquetas con límites

### OVERVIEW
Que un `.lrc` enorme, binario o construido a mala fe no congele ni agote la memoria de la app, y que un solo
audio con etiquetas raras no impida agregar el resto de la carpeta.

### ACCEPTANCE CRITERIA

1. **E1a. Tope al leer.** Un `.lrc` principal de más de 1 MB (`LyricsEditor.MaxBytes`, el mismo tope que al
   incrustar) no se lee: `LrcFileLyricsProvider` devuelve «sin letra» y el proveedor compuesto pasa a la letra
   incrustada, como si el archivo no existiera. El tamaño se comprueba antes de abrir el archivo.
2. **E1b. Hermanos.** Un `cancion.<idioma>.lrc` o `cancion.rom.lrc` de más de 1 MB se trata como un hermano que
   no se pudo leer: cede su sitio a la capa incrustada del mismo tipo y código, y la letra principal se muestra
   igual.
3. **E1c. Incrustada.** Una letra o capa incrustada cuyo texto pase de 1 MB (medido en UTF-8) se trata como
   ausente. La app nunca escribe una así; otra herramienta sí puede.
4. **E2. Tope de marcas por línea.** `LrcLyricParser` usa como mucho las primeras 32 marcas `[mm:ss.xx]`
   iniciales de una línea; las demás se descartan (no pasan al texto). Con 32 o menos, el resultado es idéntico
   al de hoy.
5. **E3. Fuera del hilo de la interfaz.** `LrcFileLyricsProvider` y `CompanionLyricsProvider` analizan el texto
   en un hilo de fondo (como ya hace `EmbeddedLyricsProvider`), también las capas incrustadas.
6. **E4. Binarios.** Un texto que contiene el carácter nulo (`\0`) no es una letra: `Parse` devuelve
   `LyricBlock.Empty`.
7. **E5. El escaneo sobrevive a un archivo.** Si `ITrackMetadataReader.Read` lanza cualquier excepción que no sea
   de cancelación, `ScanAsync` y `ReadFilesAsync` omiten ese archivo y siguen con los demás. La cancelación se
   sigue propagando. Además `TagLibTrackMetadataReader.Read` devuelve null ante cualquier excepción.
8. **Pruebas que fallan antes del cambio:**
   - `LrcFileLyricsProviderTests`: `.lrc` de 1 MB + 1 byte → sin letra; de 1 MB justo → se lee.
   - `CompanionLyricsProviderTests`: hermano de más de 1 MB → se usa la capa incrustada; la principal no cambia.
   - `LrcLyricParserTests`: línea con 40 marcas → 32 líneas; línea con 2 000 marcas iniciales y 2 000 marcas por
     palabra → termina y da 32 líneas; texto con `\0` → vacío.
   - `LibraryScannerTests`: lector falso que lanza `ArgumentOutOfRangeException` en un archivo de tres → dos
     pistas; lo mismo en `ReadFilesAsync`; con el token cancelado se lanza `OperationCanceledException`.
   - Los archivos de prueba se generan en el directorio temporal. Sin audio ni letras reales en el repositorio.
9. **Búsqueda de un caso real para E5.** Antes de implementar el criterio 7 se intenta provocar la excepción con
   archivos generados en código (`TestAudio`) a los que se les dañan bytes de la cabecera y de las etiquetas. El
   resultado se anota en esta spec, haya o no caso. Esa prueba exploratoria no se sube si no encuentra nada.
10. **Cierre.** `dotnet build` sin advertencias nuevas y `dotnet test` en verde. Se actualiza el recuento de
    pruebas (README, CLAUDE.md) y la limitación de letras del README y del reporte: «hasta 1 MB» pasa a valer
    también al leer.

### OUT OF SCOPE
- Número de líneas de una letra: 1 MB de líneas cortas son decenas de miles de filas en la lista. No se limita
  aquí; si la lista se resiente, va en `lyrics-render-perf.md`.
- Los otros tres lectores con el mismo filtro de cuatro excepciones (`EmbeddedLyricsProvider`,
  `TagLibLyricsLayerReader`, `EmbeddedCoverArtProvider`): `LyricsViewModel.LoadAsync` ya captura todo, y la
  carátula de «ahora suena» va en `ui-responsiveness.md`.
- Detectar la codificación de los `.lrc` (siguen siendo solo UTF-8; los bytes no válidos se ven como «�»).
- Aplicar `[offset:]`, escapar `<...>` literales.
- Temas de usuario y salida de ffmpeg: revisados en la auditoría sin hallazgos.
- Avisar al usuario de qué archivos se omitieron al escanear (hoy tampoco se avisa de los que devuelven null).

### EDGE CASES
- **`.lrc` de 0 bytes o solo espacios:** sin letra, como hoy.
- **`.lrc` que crece entre la comprobación y la lectura:** se acepta; el tope evita el caso normal, no una
  carrera deliberada.
- **`.lrc` inaccesible al pedir su tamaño** (`IOException`, `UnauthorizedAccessException`): sin letra, como hoy
  al leerlo.
- **Línea con exactamente 32 marcas:** sin cambios. Con 33, la última se pierde en silencio.
- **Marcas repetidas legítimas** (un estribillo con 10 o 15 marcas): no les afecta el tope.
- **`\0` en una letra incrustada:** misma regla, porque la aplica el parser.
- **Lector que lanza en todos los archivos:** el escaneo termina con cero pistas y sin error.
- **Modo de depuración `MUSICPLAYER_LYRICS_OVERRIDE`:** no se le aplica el tope de tamaño; es una ayuda de
  desarrollo (`#if DEBUG`).

## Verificación
- `dotnet test tests/MusicPlayer.Core.Tests --filter "FullyQualifiedName~LrcLyricParserTests|FullyQualifiedName~LibraryScannerTests"`
  y `tests/MusicPlayer.Infrastructure.Tests --filter "FullyQualifiedName~LyricsProviderTests"`.
- A mano en Windows: copiar un archivo de vídeo de varios cientos de MB junto a una pista de prueba con el
  nombre `pista.lrc` y reproducirla: la interfaz no se congela y la pista aparece sin letra.
- E3 no tiene prueba automática (depende del hilo): se verifica por lectura.

## Orden de trabajo
1. Pruebas rojas del criterio 8 y búsqueda del criterio 9.
2. E1, E2 y E4 (parser y proveedores), E3, E5.
3. Documentación y recuento de pruebas.
