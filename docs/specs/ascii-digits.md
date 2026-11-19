# Spec: dígitos que no son `0`-`9` en los parsers

Estado: aprobada e implementada el 16 de noviembre de 2026. Compila con 0 errores y la advertencia CS8602 de
siempre, y pasan las 1036 pruebas (825 en Core, 211 en Infrastructure). Cinco de las seis pruebas nuevas se vieron
fallar con `FormatException` antes del arreglo; la sexta (`10.０００ Reasons`) nació en verde: fija un
comportamiento que ya existía y que el arreglo no debía cambiar.

## Context

Sale de la auditoría de solo lectura previa a la v1.5.0 (era el punto 7 de `docs/pendientes.md`) y se confirmó en
la auditoría del 16 de noviembre de 2026.

En .NET, `\d` acepta cualquier dígito Unicode (`０１` de ancho completo, `١٢` árabes), pero `int.Parse` y
`uint.Parse` solo aceptan `0`-`9`. Dos parsers de Core reconocían un número con `\d` y después no lo sabían
convertir:

| Dónde | Qué pasaba | Efecto para el usuario |
| ----- | ---------- | ---------------------- |
| `FileNameParser` (`TagText.cs`): `^(?<n>\d{1,3})…` y `uint.Parse` | `FormatException` con un archivo llamado `０１ - título.mp3` | «Limpiar etiquetas» fallaba para toda la lista visible («No se pudo revisar: …») |
| `LrcLyricParser`: `LeadingTimeTags`, `SingleTimeTag` y `ToTimeSpan` | `FormatException` con `[０１:２３.００]` | La pista se quedaba sin letra e «Incrustar letra (.lrc)…» fallaba con un mensaje en inglés |

La marca por palabra de los `.lrc` enhanced (`WordTag`) ya usaba `[0-9]`. No hay más `\d` en `src`.

## FEATURE: Los parsers solo toman por número los dígitos `0`-`9`

### ACCEPTANCE CRITERIA

1. `FileNameParser.Parse` no lanza con ningún nombre. Un prefijo escrito con dígitos que no son `0`-`9` no es un
   número de pista: queda como texto.
2. `LrcLyricParser.Parse` no lanza con ninguna marca. Una marca `[mm:ss.xx]` escrita con dígitos que no son
   `0`-`9` no es una marca de tiempo: la línea queda como texto.
3. Los nombres y las marcas con `0`-`9` se comportan igual que antes: las pruebas que ya había siguen en verde.
4. Una prueba en Core por cada parser, con dígitos de ancho completo y árabes.

### OUT OF SCOPE

- Convertir esos dígitos a su valor (`char.GetNumericValue`): nadie lo ha pedido y abriría casos nuevos (mezclas
  de alfabetos en un mismo número).
- `[offset:]` del `.lrc` y cualquier otro cambio de los parsers.
- Los fallos vecinos de `docs/pendientes.md` (saltos en pausa, `PlaylistManager`, el hueco en el empalme).

### EDGE CASES

- **`０１ - título.mp3`:** ya no hay número de pista y el nombre se parte por « - »: se propone artista «０１» y
  título «título». Se ve en la vista previa de «Limpiar etiquetas» y se puede desmarcar.
- **`10.０００ Reasons`:** sigue sin número de pista, como `10.000 Reasons`. Lo que va después del separador se
  sigue comprobando con `\D` a propósito: cualquier dígito, del tipo que sea, descarta el número. Aquí el plan
  decía cambiarlo a `[^0-9]`; se dejó como estaba porque el cambio habría empezado a tomar ese «10» por número
  de pista.
- **Un `.lrc` solo con marcas de ese tipo:** se muestra como letra sin tiempos, con las marcas a la vista.
- **Marcas normales y de ese tipo mezcladas:** la letra es sincronizada y las líneas con marcas que no valen se
  descartan, como cualquier línea sin marca en una letra sincronizada.

## Resultado

- `src/MusicPlayer.Core/Services/TagText.cs`: el número de pista pasa de `\d{1,3}` a `[0-9]{1,3}`.
- `src/MusicPlayer.Core/Services/LrcLyricParser.cs`: `LeadingTimeTags` y `SingleTimeTag` pasan de `\d` a `[0-9]`.
- Pruebas nuevas (6): tres casos en `LrcLyricParserTests` (`Parse_NonAsciiDigitsInTimeTag_DoesNotThrowAndIsPlainText`
  con dos entradas y `Parse_NonAsciiTimeTagAmongNormalOnes_KeepsTheSyncedLines`) y tres en `TagSuggesterTests`
  (`FileName_WithNonAsciiLeadingDigits_DoesNotThrowAndHasNoNumber` con dos entradas y
  `FileName_NumberFollowedByNonAsciiDigit_IsNotATrackNumber`).
- No se probó en la app: el cambio es lógica pura de Core.
