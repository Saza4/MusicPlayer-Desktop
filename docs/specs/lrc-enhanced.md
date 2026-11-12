# Especificación: `.lrc` enhanced (marcas `<mm:ss.xx>` por palabra)

Estado: **aprobada (10 de noviembre de 2026). Fases 1 y 2 hechas y fusionadas en `main` el 10 de noviembre de 2026 (`3ae9ff9`, una fusión local, sin PR).** La Fase 2 está en `lrc-enhanced-fill.md`. Fase 1 (lectura) y Fase 2 (relleno por palabra) van en PR distintos. Fase 3 son comprobaciones de coherencia.

## Contexto

Hoy `LrcLyricParser` solo reconoce las marcas de línea `[mm:ss.xx]` al inicio. En un `.lrc` enhanced, las marcas `<mm:ss.xx>` por palabra quedan dentro de `LyricLine.Text` (se deduce de la expresión regular; ninguna prueba lo fija). Consecuencias:

- Se ven `<00:13.55>` en el panel lateral y en el modo inmersivo.
- `LyricsTiming.FillDuration` estima la duración con `Text.Trim().Length`, inflada por las etiquetas.
- Una traducción o pronunciación enhanced (hermana o incrustada) también se ve sucia, porque `LyricsMerger` copia `.Text`.
- La incrustación no necesita cambios: guarda el archivo tal cual y al leer vuelve a pasar por el mismo parser.

LrcStudio (otro proyecto del autor) produce estos archivos: lee, edita y escribe enhanced (sus specs 01, 09 y 10). Esta spec fija lo que MusicPlayer debe entender de ese formato. **No se copia su parser**: tiene defectos que aquí no se quieren heredar (ver «Diferencias con LrcStudio»).

Lo que ya está preparado: `FillTextBlock` recibe el progreso como posición horizontal (RNF-5 de `lyrics-progressive.md`). `LyricLine.Words` y `LyricWord` se quitaron el 8 de noviembre de 2026 por no tener uso; ahora lo tienen y se reintroducen con otra forma.

## Formato que se entiende

```
[00:13.15]<00:13.15>Oh <00:13.55>it <00:13.80>was <00:14.20>the <00:14.50>night
[00:41.65]Don't <00:41.85>wanna <00:42.26>see <00:42.50>your face
[00:48.29]So <00:48.07>beat <00:49.00>it
[00:52.00]<00:52.00>Hel<00:52.30>lo <00:52.60>
[00:10.00][00:30.00]<00:10.00>Estribillo <00:10.50>largo
```

(Letra de ejemplo inventada en las pruebas; estas líneas solo muestran la forma.)

- La marca de línea `[mm:ss.xx]` es el inicio de la línea. Cada `<mm:ss.xx>` es el instante en que empieza el texto que le sigue.
- El texto anterior a la primera `<>` es la primera palabra y empieza a la hora de la línea (segunda línea del ejemplo).
- Una `<>` final sin texto marca el **fin** de la línea (cuarta línea).
- Las sílabas pegadas (`Hel<00:52.30>lo`) forman palabras distintas que se muestran como `Hello`.

## Criterios de aceptación

**Fase 1: lectura**

1. **Etiqueta válida:** solo es etiqueta la secuencia `<` + 1 a 3 dígitos ASCII `[0-9]` + `:` + 2 dígitos + (`.` o `:` + 1 a 3 dígitos, opcional) + `>`, con **segundos < 60**. Cualquier otra cosa entre `<` y `>` es texto literal (`<00:99.00>`, `<3>`, `<hola>`, `<١٢:٣٠>`).
2. **Solo en líneas con marca de línea.** En una línea sin marca (letra `Plain`, o línea sin marca dentro de un archivo sincronizado, que ya se descarta) las `<>` no se interpretan y el texto queda intacto.
3. **`LyricLine.Text` queda limpio:** sin etiquetas y con el espaciado original entre tramos (no se normaliza). Se recorta solo en los extremos. Toda la UI actual (lista clásica, panel lateral, inmersivo, `LyricsMerger`, traducciones) sigue usando `Text` sin cambios.
4. **Modelo:** `LyricLine` gana, **al final** del registro y opcionales (`null` por defecto, para no romper llamadas posicionales ni `with`):
   - `Words`: `IReadOnlyList<LyricWord>?`.
   - `End`: `TimeSpan?`, la `<>` final sin texto.
   `LyricWord(string Text, TimeSpan Start, TimeSpan? End, int Offset)` en `Core/Models`, donde `Offset` es el índice de inicio de la palabra dentro de `LyricLine.Text` y `Text` el trozo (`Text.Substring(Offset, Text.Length)`). Sin etiquetas en la línea, `Words` es `null` y nada cambia.
5. **Tiempos de palabra:** absolutos, como la marca de línea. Primer tramo sin etiqueta: `Start` = hora de la línea. `End` de una palabra = `Start` de la siguiente; la última toma `LyricLine.End` si existe y, si no, queda `null`.
6. **El parser no corrige tiempos.** Una palabra anterior al inicio de la línea (`[00:48.29]So <00:48.07>beat`) o que retrocede se conserva tal cual: los archivos reales lo traen. La lectura no lanza ni avisa.
7. **Varias marcas de línea** (`[00:10.00][00:30.00]…`): se crea una `LyricLine` por marca, como hoy. Los tiempos de las palabras están escritos respecto a la **primera marca del archivo** y en cada copia se desplazan por `marca − primera marca` (misma convención que LrcStudio, `LrcParser.cs:52-62`). Una `End` también se desplaza.
8. **Línea sin texto con etiquetas** (solo `<>`): se trata como línea vacía con marca (ya existe), sin palabras. Si es solo una `<>` final, es su `End`.
9. **Dos `<>` seguidas sin texto entre ellas:** la primera se descarta, la segunda manda. Sin error ni aviso (la lectura no tiene dónde avisar).
10. **Líneas enhanced y normales mezcladas** en el mismo archivo: cada una se interpreta por separado.
11. **Metadatos y `[offset:]`:** sin cambios (se descartan; el offset no se aplica, ver «Fuera de alcance»).
12. **Capas:** una traducción o pronunciación enhanced se lee con las mismas reglas y se une por `Text` limpio. `LyricsMerger` no cambia. Las `Words` de una capa no se usan (solo se usa su `Text`).
13. **Incrustación:** una letra enhanced incrustada (USLT, `LYRICS`, `©lyr` y las capas) se lee igual que la del archivo: ida y vuelta en MP3, FLAC y M4A, con prueba. `LyricsEditor` no cambia.
14. **Hito medible:** con cualquier `.lrc` enhanced que cumpla este formato, ninguna pantalla muestra `<...>`. Aún no hay relleno por palabra.
15. **Calidad:** lógica en Core con pruebas con letra sintética; `dotnet build -c Release` sin warnings nuevos (hoy 1 CS8602 conocido); todas las pruebas en verde; CI en Windows y Ubuntu. Un `settings.json` anterior carga igual (no hay ajustes nuevos).

**Fase 2: relleno por palabra (spec propia: `docs/specs/lrc-enhanced-fill.md`; aquí solo la dirección)**

16. `LyricsTiming` calcula el relleno con `Words` cuando la línea los tiene, y con la estimación de 80 ms por carácter cuando no. Aplica un **máximo acumulado** a los tiempos de palabra para que el relleno no retroceda con tiempos desordenados.
17. Core devuelve la posición de relleno como **índice de carácter de `Text` más fracción dentro de la palabra**, no como x. La vista la convierte con `TextLayout.HitTestTextPosition`, sumando el ancho de los renglones anteriores (el `FillX` de `FillTextBlock` es acumulado). Punto de enganche actual: `ProgressiveLyricsView.cs`, donde hoy se calcula `fraction * row.FillLength`.
18. Entre palabras, el relleno se queda al final de la anterior hasta que empieza la siguiente. El fin de la línea (`End`, o última palabra más su estimación) también alimenta `FillEnd`, `GetInstrumentalGaps` y `LyricsTimeline`.
19. Medidas en Release con el método de M0-M5 (criterio 12 de `lyrics-progressive.md`: ≤ +5 puntos de CPU), con alfabetos mezclados, texto de derecha a izquierda y líneas de varios renglones.

## Diferencias con LrcStudio (decididas)

| Tema | LrcStudio hoy | MusicPlayer en esta spec |
|---|---|---|
| Espaciado entre palabras | `LrcWords.Join` une con un espacio: `<t>Hel<t>lo` → `Hel lo` | Se conserva el espaciado original: `Hello` |
| `<>` final sin texto | Se ignora | Se guarda como `End` |
| Segundos ≥ 60 en `<>` | Palabra sin tiempo y etiqueta borrada, sin aviso | Texto literal |
| `<12:30>` en letra sin marca de línea | Se interpreta como marca | Texto intacto; con marca de línea sigue siendo ambigua (limitación) |
| Espacio inicial tras la marca | Se conserva sin palabras, se recorta con palabras | Siempre se recorta en los extremos |
| `[offset:]` | Se aplica y se elimina al guardar | No se aplica (fuera de alcance) |

## Fuera de alcance

- **`[offset:]`**: es un tema distinto que afecta también a la lista clásica; merece su propia iteración.
- Dialectos distintos de `<mm:ss.xx>`: `[mm:ss.xx]` por palabra, `{}` de Apple, formatos A2 y TTML.
- Escapar `<...>` literales dentro de la letra. Un `<12:30>` escrito a propósito en una línea con marca se interpretará como etiqueta (limitación, la misma de LrcStudio).
- Generar tiempos por palabra automáticamente, y editarlos en la app.
- Relleno por palabra (Fase 2) y todo lo visual de esta fase.
- Sincronía por sílaba más fina que la que dé el archivo.
- Corregir o reordenar los tiempos al leer.

## Casos límite

- **Etiqueta pegada a otra** (`<00:12.00><00:13.00>hola`): la primera se descarta (criterio 9).
- **Etiqueta inválida a mitad de línea:** queda como texto, el resto de la línea se interpreta (`Oh <00:99.00> it <00:13.55>was` → texto «Oh <00:99.00> it was»).
- **`<>` al principio, igual que la marca de línea** (`[00:13.15]<00:13.15>Oh`): el primer tramo está vacío, la primera palabra empieza ahí.
- **Palabras sin etiqueta en medio de un tramo** (`<t>Hola mundo`): es un solo tramo; hasta la siguiente etiqueta es una sola palabra con ese texto.
- **Línea enhanced con un solo carácter o solo símbolos:** igual que una línea normal.
- **Marca de línea con texto vacío y etiquetas** (`[00:10.00]<00:11.00>`): línea vacía con `End` = 00:11, sin palabras.
- **Línea muy larga:** sin límite nuevo.
- **Archivo con CRLF y `[id: x]` mezclados:** igual que hoy.
- **`.lrc` enhanced en una letra `Plain`:** las `<>` quedan como texto (criterio 2).
- **Pronunciación sincronizada con otras etiquetas que la original:** `LyricsMerger` empareja por marca de línea (±300 ms), no por palabra.
- **Archivos con tiempos de palabra desordenados:** se leen sin cambios; en Fase 1 no afecta a nada visible.

## Hitos (un commit cada uno)

| Hito | Contenido | Verificación |
|---|---|---|
| M0 | Esta spec, aprobada | Revisión del autor |
| M1 | Core: `LyricWord`, `LyricLine.Words` y `End`, y `LrcLyricParser` con los criterios 1 a 12 | Pruebas Core (casos de abajo) |
| M2 | Pruebas de capas y de incrustación en MP3, FLAC y M4A (criterios 12 y 13) | Pruebas Infrastructure |
| M3 | Docs: README (limitación de «sin sincronía por palabra» solo hasta Fase 2), `reporte-reproductor.md`, `lyrics-progressive.md` (RNF-5), `CLAUDE.md` (nº de pruebas); PR y CI verde | CI en Windows y Ubuntu |

Pruebas que se escriben desde el principio (letra inventada, sin letra real):

1. Línea enhanced simple, con fin de palabra y sin él.
2. Sílabas sin espacio conservadas (`<t>Hel<t>lo` → `Hello`).
3. `<12:30>` en una letra sin marca de línea: texto intacto.
4. Etiqueta inválida a mitad de línea: no rompe la línea.
5. Mezcla de líneas enhanced y normales en el mismo archivo.
6. Texto antes de la primera `<>`.
7. Varias marcas de línea con enhanced (desplazamiento de las palabras y de `End`).
8. Palabra anterior a la línea y palabras desordenadas: se conservan.
9. Traducción enhanced unida con `LyricsMerger`.
10. Ida y vuelta de incrustación en MP3, FLAC y M4A.

## Verificación

- Automática: `dotnet build -c Release`, `dotnet test -c Release`, y `dotnet test tests/MusicPlayer.Core.Tests --filter "FullyQualifiedName~LrcLyricParser"`.
- A mano (Debug, Windows): `MUSICPLAYER_LYRICS_OVERRIDE=<ruta a un .lrc enhanced sintético>` y comprobar que el panel lateral y el modo inmersivo no muestran `<...>`.
- No se suben letras reales ni audio nuevo (reglas de `CLAUDE.md`).

## Decisiones que quedan al autor

1. **`End` desde la `<>` final** (propuesto: sí, se guarda; en Fase 1 no cambia nada visible).
2. **Segundos ≥ 60 en `<>`** (propuesto: texto literal, estricto, sin tocar las marcas de línea de hoy, que aceptan `[00:75.00]`).
3. **`<>` en letra `Plain`** (propuesto: texto intacto).
4. **Convención de varias marcas** (propuesto: copiar la de LrcStudio; es la única compatible con lo que escribe).
