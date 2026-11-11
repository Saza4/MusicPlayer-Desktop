# Especificación: relleno por palabra (`.lrc` enhanced, Fase 2)

Estado: **aprobada (10 de noviembre de 2026); M1 y M2 hechos. M2b (naturalidad del relleno, 10 de noviembre) añadido tras la prueba a mano de M2.** Continúa `docs/specs/lrc-enhanced.md` (Fase 1, lectura), de donde sale el modelo `LyricWord`. Va en un PR distinto, en una rama nueva (`feature/lrc-enhanced-relleno`) que se crea cuando el PR de la Fase 1 esté fusionado.

## Contexto

Hoy el relleno de la línea activa avanza de forma uniforme: `LyricsTiming.FillDuration` estima `80 ms × caracteres` (entre 1 y 8 s, con el hueco hasta la siguiente línea como tope). Un `.lrc` enhanced ya trae cuándo empieza cada palabra, y desde la Fase 1 `LyricLine.Words` los guarda (`LyricWord(Text, Start, End, Offset)`, con `Offset` sobre el `Text` limpio). Esta fase hace que el relleno los use.

Lo que ya está preparado: `FillTextBlock` recibe el progreso como **posición horizontal** (`FillX`, acumulada entre renglones; RNF-5 de `lyrics-progressive.md`), así que el dibujo no se rehace. Lo que falta es calcular esa posición a partir de las palabras.

Puntos del código que esta fase toca:

- `LyricsTiming.cs`: `FillDuration`, `Fill`, `FillEnd` (de ahí salen también `GetInstrumentalGaps` y `LyricsTimeline.FillAt`).
- `ProgressiveLyricsView.cs`, `Tick()` (`timeline.FillAt` → `SetFill`) y `LineItem.SetFill` (`fraction * row.FillLength`).
- `FillTextBlock.cs`: nuevo método para convertir una posición de carácter en x.

## Diseño

**Core** (sin dependencias nuevas, con pruebas):

- `FillPoint(int Index, int Length, double Fraction)`: «carácter de `LyricLine.Text` donde empieza el tramo activo, su longitud y fracción recorrida». La posición continua en el texto es `Index + Fraction × Length` (propiedad `CharPosition`). `Length` está en el registro porque la vista no conoce las palabras (se añadió en M1). Desde M2b el tramo de una palabra llega hasta el `Offset` de la siguiente (incluye el espacio) mientras no haya una pausa real.
- `LyricsTiming.FillPoint(block, index, position)` devuelve `FillPoint?`: `null` si la línea no tiene `Words` (la vista usa la fracción de siempre).
- **Tiempos efectivos.** Inicio efectivo de la palabra *i*: `max(inicio_0 … inicio_i)` (máximo acumulado). Fin efectivo: `max(End ?? inicio efectivo de la siguiente, inicio efectivo propio)`. En la última palabra sin `End` propio se usa `LyricLine.End` y, si no existe, `inicio + 80 ms × longitud de la palabra` (sin los topes de 1 y 8 s, que son de línea entera).
- **Tramo continuo (M2b).** Si la pausa entre el fin efectivo de la palabra *i* y el inicio efectivo de la *i+1* es ≤ `LyricsTiming.WordPauseHold` (250 ms), el tiempo de la *i* se estira hasta el inicio de la *i+1* y su texto hasta el `Offset` de la *i+1*: el relleno no se detiene ni salta el espacio. Con una pausa mayor (por ejemplo un `End` explícito antes de un silencio), el punto queda al final del texto de la *i* hasta que empieza la *i+1*. Antes de la primera palabra efectiva: relleno 0.
- **Fin de línea:** `line.End ?? fin efectivo de la última palabra`, con el mismo tope de hueco hasta la siguiente línea que hoy (`room`; 0 si comparte marca). `FillDuration` = ese fin − `Timestamp`; `Fill` y `FillEnd` salen de ahí, así que `GetInstrumentalGaps` y `LyricsTimeline` lo heredan.
- `LyricsTimeline.FillPointAt(entryIndex, position)`: delega en `LyricsTiming`; `null` para pausas y líneas vacías.

**Desktop:**

- `FillTextBlock.XAt(index, length, fraction)` (M2b; en M2 era `XAt(charPosition)`): calcula la distancia acumulada del carácter `index` y la de `index + length` (`HitTestTextPosition` sobre `_bright`, sumando el ancho de los renglones anteriores) e interpola entre ellas por `fraction`: **velocidad uniforme en x** dentro del tramo, no por carácter (una «m» ancha y una «i» estrecha no cambian el ritmo). Vale para palabras partidas entre renglones, porque la distancia acumulada es continua, y para sílabas pegadas.
- `LineItem.SetFill(fraction, point)`: si hay `point` y la fila rellenada es la original, `FillX = XAt(...)`; en otro caso, `fraction × FillLength` como hoy. `MeasureOverride` repite la última llamada, así que debe guardar también el `point`.
- **Suavizado (M2b).** En la ruta por palabra, `LineItem` guarda la x objetivo y mueve el `FillX` mostrado con un resorte críticamente amortiguado (`SpringSolver.Step`, `ProgressiveLyricsMetrics.FillSpring`: respuesta 0,18 s, sin rebote), de modo que la velocidad es continua al cambiar de ritmo entre palabras. El objetivo se calcula con la posición adelantada `ProgressiveLyricsMetrics.FillLookahead` (60 ms) para compensar el retraso del resorte (para una rampa, ≈ 2/ω ≈ 57 ms). Salto directo, sin suavizar, cuando el objetivo retrocede (> 1 DIP) o se aleja más de `FillSnapDistance` (120 DIPs): buscar con la barra, pulsar una línea, redimensionar. Al dejar de ser la activa, la línea vuelve a tenue sin animar.
- Regla del reloj: sin cambios salvo que el resorte del relleno sin asentar también cuenta como «algo que mover» (`moving`) para no quedarse a medias en pausa. La fracción temporal de la línea sigue llegando a 1 en `FillEnd`.

## Criterios de aceptación

1. **Línea con `Words`:** el relleno de la fila original avanza según los tiempos de palabra. **Sin `Words`:** igual que hoy (80 ms por carácter), sin cambios en las pruebas actuales.
2. **Máximo acumulado:** el relleno nunca retrocede con tiempos desordenados. `[00:48.29]So <00:48.07>beat <00:49.00>it`: «beat» entra con «So» (inicio efectivo 48.29), sin ir hacia atrás.
3. **Core devuelve `FillPoint`, no x.** La vista lo convierte con `XAt`. Core no conoce fuentes ni anchos.
4. **Entre palabras:** el relleno no se detiene ni salta el espacio cuando la pausa es ≤ `WordPauseHold`; con una pausa real se queda al final del texto de la palabra hasta que empieza la siguiente (M2b; antes se quedaba siempre quieto, y se sentía rígido).
5. **Sílabas pegadas** (`Hel<t>lo`) **y palabras con espacio:** el relleno pasa de una a otra sin hueco visual ni salto.
6. **Varios renglones:** `XAt` suma el ancho de los renglones anteriores; una palabra partida entre dos renglones se rellena de forma continua.
7. **Fin de línea:** `End` (o la última palabra más su estimación, con el tope del hueco hasta la siguiente línea) alimenta `FillDuration`, `FillEnd`, `GetInstrumentalGaps` y `LyricsTimeline`. Con letras sin `Words`, las pausas «♪» no cambian.
8. **Líneas mixtas** (enhanced y normales en el mismo archivo): cada una usa su camino.
9. **Capas:** si la fila rellenada es la pronunciación o la traducción, el relleno avanza por la fracción temporal de la línea (de `Timestamp` a `FillEnd`) y no usa `Words` (sus `Offset` valen solo sobre el texto original).
10. **Saltos, pausa y cambio de pista:** el relleno cae en el punto correcto (salto directo, criterio 14). El objetivo sigue siendo función de la posición; el único estado es el del resorte de la vista, que no viaja entre pistas ni líneas.
11. **RTL:** el relleno sigue yendo de izquierda a derecha aunque el texto sea de derecha a izquierda (limitación documentada, ver «Fuera de alcance»). Una línea RTL con `Words` usa igualmente la ruta nueva.
12. **Medidas en Release** con el método de M0-M5 (criterio 12 de `lyrics-progressive.md`): como máximo **+5 puntos de CPU** frente a la estimación uniforme, con alfabetos mezclados, texto RTL y líneas de varios renglones.
14. **Naturalidad (M2b):** la velocidad del relleno es continua al cambiar de palabra (sin escalones), uniforme en x dentro de cada palabra, y el relleno no queda visiblemente por detrás del audio. Se juzga a mano; las constantes `WordPauseHold`, `FillSpring` y `FillLookahead` son ajustables.
15. **Calidad:** lógica en Core con pruebas con letra sintética; `dotnet build -c Release` sin warnings nuevos (hoy 1 CS8602 conocido); todas las pruebas en verde; CI en Windows y Ubuntu. Sin ajustes nuevos: un `settings.json` anterior carga igual.

## Fuera de alcance

- **Relleno de derecha a izquierda en texto RTL** (árabe, hebreo). Con tiempos por palabra la limitación se nota más (el relleno salta entre palabras en orden visual equivocado), pero resolverlo implica el orden visual de las tiradas y el texto bidireccional mixto; es un cambio aparte en `FillTextBlock`. Se documenta en README y reporte.
- Cambiar el borde suave del relleno (se mantiene en M2b; si tras el suavizado sigue sin verse coherente, el siguiente paso sería escalarlo con la velocidad o con la anchura de la palabra). Hoy `FillTextBlock.Render` calcula `edge = x0 + avance/ancho × (ancho + soft)`, y la parte sólida queda hasta `soft` DIPs por detrás de la x real; con el relleno quieto al final de una palabra, sus últimas letras pueden verse a medio rellenar. Se acepta en esta fase y se juzga a mano en M2; si no gusta, será un hito posterior que toque `Render`.
- Sincronía más fina que la del archivo, corregir o reordenar tiempos al leer (el parser no lo hace) y `[offset:]`.
- Caché de posiciones por palabra (solo si la medida de M4 lo pide) y parar el reloj mientras el relleno espera entre palabras (con el suavizado, el resorte tampoco lo permite).
- Generar o editar tiempos por palabra en la app.

## Casos límite

- Palabra de duración 0 (inicio = fin): el relleno salta a su final al llegar.
- Pausa de exactamente `WordPauseHold`: cuenta como continua (≤).
- Objetivo que retrocede (buscar hacia atrás dentro de la línea): salto directo, sin deslizarse hacia atrás.
- Pausa de la reproducción con el resorte sin asentar: se piden fotogramas hasta que llega, luego el reloj se para.
- Redimensionar con la línea activa a medias: la x objetivo cambia y, si se aleja más de `FillSnapDistance`, salta directo.
- `End` de palabra anterior a su `Start`: se trata como duración 0 (fin efectivo = inicio efectivo).
- Palabras desordenadas o anteriores a la marca de línea: máximo acumulado (criterio 2); la línea no se activa antes de su marca.
- Línea que comparte marca con la siguiente: duración 0, como hoy.
- Última línea sin `End`: fin = última palabra + estimación.
- Línea sin texto con `End` (pausa explícita): sin `Words`, sin `FillPoint`; sigue la ruta de pausas.
- Redimensionar la ventana: la x se recalcula al medir (`MeasureOverride`), con el último `FillPoint`.
- Palabra con emoji o par sustituto: el índice es UTF-16; no se parte un par (se redondea al límite del par).
- `Offset + Text.Length` fuera de `Text`: se recorta, no lanza.
- Traducción o pronunciación visibles sin el original: criterio 9.
- Línea con una sola palabra: el relleno recorre la palabra de su inicio a su fin.

## Hitos (un commit cada uno)

| Hito | Contenido | Verificación |
|---|---|---|
| M0 | Esta spec, aprobada | Revisión del autor |
| M1 | Core: `FillPoint`, máximo acumulado, `LyricsTiming.FillPoint` y `FillDuration` con `Words` | Pruebas Core (`LyricsTimingTests`) |
| M2 | Desktop: `FillTextBlock.XAt`, `LineItem.SetFill(fraction, point)`, `LyricsTimeline.FillPointAt` | A mano (Debug, Windows) con `MUSICPLAYER_LYRICS_OVERRIDE` y un `.lrc` enhanced sintético; juzgar el borde del relleno |
| M2b | Naturalidad: tramo continuo en Core (`WordPauseHold`), `XAt(index, length, fraction)` por ancho, resorte y adelanto del relleno en la vista | Pruebas Core; a mano con el mismo `.lrc` sintético: sin tirones entre palabras ni en los espacios |
| M3 | Pausas «♪» y línea de tiempo con letras enhanced: pruebas y casos límite | Pruebas Core (`LyricsTimelineTests`, `LyricsTimingTests`) |
| M4 | Medidas en Release; docs (README sin «sin sincronía por palabra» y con la limitación RTL, `reporte-reproductor.md`, RNF-5 de `lyrics-progressive.md`, `CLAUDE.md` con el nº de pruebas); PR y CI verde | CI en Windows y Ubuntu |

Pruebas Core que se escriben desde el principio (letra inventada, sin letra real):

1. Fracción por palabra: al inicio, a mitad y al final.
2. Máximo acumulado con palabras desordenadas y con una anterior a la marca de línea.
3. Quieto entre dos palabras solo con pausa real (> `WordPauseHold`); continuo, sin saltos de `CharPosition`, con pausa ≤.
4. Sílabas pegadas: el final de la primera es el inicio de la segunda.
5. Línea sin `Words`: mismos resultados que hoy.
6. Línea mixta en el mismo bloque.
7. `End` presente y ausente; tope del hueco hasta la siguiente línea; marca compartida.
8. Palabra de duración 0 y `End` anterior a `Start`.
9. `FillEnd` y pausa «♪» (`GetInstrumentalGaps`) con letra enhanced.
10. `LyricsTimeline.FillAt` y `FillPointAt` con letra enhanced; `null` en pausas.

## Verificación

- Automática: `dotnet build -c Release`, `dotnet test -c Release` y `dotnet test tests/MusicPlayer.Core.Tests --filter "FullyQualifiedName~LyricsTiming"`.
- A mano (Debug, Windows): `MUSICPLAYER_LYRICS_OVERRIDE=<ruta a un .lrc enhanced sintético>` con líneas cortas, de varios renglones, con sílabas pegadas, desordenadas y mezcladas con líneas normales; con y sin traducción visible. Comprobar el borde del relleno entre palabras.
- Medidas: `MUSICPLAYER_LYRICS_STATS` y CPU en Release con el método de M0-M5.
- No se suben letras reales ni audio nuevo (reglas de `CLAUDE.md`).

## Decisiones tomadas (autor, 10 de noviembre de 2026)

1. **RTL:** se deja igual y se documenta.
2. **Borde del relleno:** se mantiene el actual y se juzga a mano en M2.
3. **Prueba a mano de M2:** funciona, pero «los cambios bruscos de velocidad» lo hacen rígido y el barrido «no da tiempo a que el efecto termine». Enfoque elegido: tramo continuo en Core más suavizado en la vista (M2b).
