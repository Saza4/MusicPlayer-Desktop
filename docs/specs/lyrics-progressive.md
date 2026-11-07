# Especificación: Letras progresivas (estilo Apple Music)

Estado: **decisiones confirmadas por el autor (7 de noviembre de 2026); sin código de la función escrito**. Alcance: **solo el modo inmersivo**. El panel lateral no cambia. Ver «Decisiones tomadas» y «Resultado del análisis de `Michael Jackson - Chicago.lrc`».

## Contexto

Referencia: `ProgressingStyleLyrics.swift` (Magesh Sridhar, SwiftUI, 2023), una recreación de la letra «progresiva» de Apple Music. Se quiere el mismo resultado en MusicPlayer-Desktop (C#, .NET 8, Avalonia 11.2.5), sin servicios externos y sobre los `.lrc` que la app ya lee.

Hoy la letra es un `ListBox` (`MainWindow.axaml`, clase `lyrics`) con la línea activa en negrita y opacidad 1 frente a 0,45 en las demás. `LyricsViewModel.UpdatePosition` se llama desde `MainViewModel.OnPositionSecondsChanged`, que a su vez recibe un temporizador de **250 ms**: suficiente para cambiar de línea, insuficiente para un relleno fluido.

## Qué hace el Swift (lectura del código)

| Efecto | Cómo lo hace el Swift | Problema del original |
|---|---|---|
| Relleno de la línea activa | `AnimatedMask`: un `Path` con `trim(from:0, to:phase)` hace de máscara; `phase` va de 0 a 1 con `animationLength[i]` | La duración de cada línea está escrita a mano |
| Desplazamiento | Cada línea desplaza su `offset` con `spring()`; la espera crece `0,1 s × (i − línea activa)` (efecto cascada) | Resta `33.6 × líneas + 20` a mano: no es la altura real y **no admite saltar atrás** |
| Línea activa | `timestamps: [String:Int]` con clave `"mm:ss"`, evaluada cuando cambia el texto del tiempo | Resolución de 1 s; si se salta en la barra, la línea no se recalcula bien |
| Inactivas | `blur(4)` y color secundario | — |
| Efectos puntuales | `glow` morado y `ShakeEffect` solo para las líneas 7, 10, 18 y 31 | Código escrito para una canción |
| Borde superior e inferior | `mask` con `LinearGradient` | — |

Se replica el **resultado visual**, no la implementación: en Avalonia el estado se **deriva de la posición de reproducción** en cada fotograma, así que saltar adelante o atrás, pausar o cambiar de pista siempre queda coherente.

## Viabilidad en Avalonia 11.2.5

| Pieza | Viable | Cómo |
|---|---|---|
| Relleno progresivo de izquierda a derecha | Sí | Control propio que dibuja el texto dos veces con `TextLayout`: tenue y brillante; el brillante, recortado por un rectángulo con borde suavizado (`OpacityMask` con `LinearGradientBrush`). Con texto en varias líneas, el relleno recorre cada renglón en orden |
| Fundido arriba y abajo | Sí | `OpacityMask` con `LinearGradientBrush` |
| Separar las líneas inactivas | Sí, **sin desenfoque** | Opacidad menor y reducción de escala según la distancia a la línea activa (`Opacity` y `ScaleTransform` por línea). Decisión del autor: no se usa `BlurEffect` |
| Desplazamiento con resorte | Sí | Avalonia no trae resorte; se calcula uno amortiguado en Core (función pura) y se aplica con `TranslateTransform` por línea |
| Cascada entre líneas | Sí | Retardo por distancia a la línea activa, igual que el original |
| Medir el alto real de cada línea | Sí | `Measure`/`Bounds` del propio panel, en lugar de 33,6 fijo |
| Brillo y temblor por línea | Técnicamente sí | Fuera de alcance: requieren datos por canción (ver más abajo) |

Dato clave: el `.lrc` solo da el **inicio** de cada línea, no cuándo termina cada palabra. El relleno usará la duración estimada de la línea (siguiente marca menos esta, con tope por longitud del texto) y avanzará de forma uniforme. No coincidirá palabra por palabra como en Apple Music; sí se verá progresivo y fluido. El formato `.lrc` «enhanced» (`<mm:ss.xx>` por palabra) queda para una fase posterior.

## Criterios de aceptación

1. **Relleno:** la línea activa se rellena de izquierda a derecha según la posición real; en pausa se detiene, y al saltar con la barra o al pulsar una línea el relleno salta al punto correcto.
2. **Estados de línea:** pasada (tenue), activa (brillante, opacidad 1, escala 1) y futura (tenue). Las inactivas bajan de opacidad y de escala con la distancia a la activa (curva y mínimos en constantes). Las ya cantadas no vuelven a rellenarse.
3. **Desplazamiento:** la línea activa se ancla a ~35 % de la altura del panel; al cambiar de línea el conjunto se desplaza con resorte sin rebote excesivo y con cascada entre líneas (retardo ≤ 100 ms por línea, tope configurable).
4. **Saltos:** adelantar, retroceder, cambiar de pista o reanudar tras pausa deja la letra en el estado correcto, sin acumular desfases (el estado no se acumula, se recalcula).
5. **Pausas instrumentales:** se **infieren por hueco**, no por líneas vacías. El parser descarta las líneas en blanco, así que un `.lrc` normal (como `Chicago.lrc`) no trae líneas «♪». Hay pausa cuando una línea termina (duración estimada) y la siguiente empieza más de `InstrumentalGap` después (valor inicial: 8 s, en constantes), y también antes de la primera línea y después de la última. Se muestra «♪» con un indicador discreto de progreso; no rellena texto. Una línea con marca y texto vacío (la forma explícita) se trata igual.
6. **Traducción y pronunciación:** se mantienen con las mismas opciones de hoy (`LyricsDisplayOptions`). El relleno aplica solo al renglón original; los demás se atenúan según el estado de la línea.
7. **Fundido** superior e inferior de la zona de letra.
8. **Interacción:** pulsar una línea salta a su marca (como hoy, `SeekToLine`). La rueda del ratón o un gesto pausa el auto-desplazamiento unos segundos y luego retoma.
9. **Colores solo con recursos del tema** (`ThemeXxxBrush`), sin literales `#RRGGBB` (convención de CLAUDE.md).
10. **Ajuste temporal `LyricsStyle`** (Clásico / Progresivo) en `AppSettings` (parámetro nuevo al final, por defecto Clásico), con un interruptor en el modo inmersivo para alternar mientras se prueba. Es un mecanismo explícito, no código comentado. Al aprobar el nuevo estilo, se retira el clásico **solo del modo inmersivo** (y con él el ajuste). El `ListBox` clásico permanece: lo usa el panel lateral y las letras `Plain`.
11. **Accesibilidad:** el ajuste «reducir movimiento» del sistema, o una opción propia, desactiva resorte y cascada y deja solo el cambio de opacidad.
12. **Rendimiento:** con el panel de letra visible y sonando, CPU adicional ≤ ~+5 puntos de un núcleo frente a hoy (medida como en M7 de la isla). Sin letra visible o en pausa, **0 fotogramas extra**.
13. **Pruebas:** la lógica (relleno, línea activa, resorte, estimación de duración) vive en Core con pruebas; `dotnet build` sin warnings nuevos y todas las pruebas actuales (403) en verde.
14. **Linux:** compila y no depende de Win32.

## Requisitos no funcionales

- **RNF-1 Reloj de fotogramas.** El relleno necesita más de los 250 ms actuales. **Decidido en M0:** un `DispatcherTimer` propio a 30 fps pedidos (~24 reales), **solo** con el modo inmersivo abierto, la pista sonando y el estilo progresivo. No `RequestAnimationFrame` (ver «Resultado de M0»). Cada fotograma lee `IAudioPlayer.CurrentTime` directamente: **no hace falta interpolar**, porque la posición de BASS avanza en cada lectura igual que el reloj de pared.
- **RNF-2 Sin estado acumulado.** Todo (línea activa, relleno, desplazamiento objetivo) es función de `posición` y `alturas medidas`. No se imita el `offset = offset − …` del Swift.
- **RNF-3 Sin desenfoque.** No se usa `BlurEffect`. La profundidad sale de opacidad y escala por distancia. Solo se animan transformaciones y opacidad, que son baratas.
- **RNF-4 Un solo lugar de uso.** El control vive solo en el modo inmersivo (letra a media pantalla sobre la carátula desenfocada, lo más parecido al vídeo de referencia). El panel lateral no se toca y no hay segunda fase en esta spec.
- **RNF-5 Preparado para palabras, sin implementarlas.** (a) `FillTextBlock` recibe el progreso como **posición horizontal** (DIPs desde el borde izquierdo del texto), no solo como fracción 0–1; el control que lo usa convierte fracción en posición hoy y podrá convertir tiempo por palabra mañana. (b) `LyricLine` gana un campo opcional `Words` (lista de palabras con inicio y fin), `null` por ahora, **al final** del registro para no romper llamadas posicionales. Ningún código lo rellena ni lo lee en esta spec.

## Diseño propuesto

**Core** (sin dependencias, con pruebas):
- `LyricsTiming`: duración estimada de cada línea y fracción de relleno `Fill(posición, línea)` en [0, 1]. Duración = `min(siguiente − inicio, f(longitud del texto))`, con mínimos y máximos en constantes. Última línea: duración estimada por texto.
- `SpringSolver`: resorte amortiguado semi-implícito (posición, velocidad, objetivo, rigidez, amortiguación). Función pura y determinista.
- `LyricsScroll`: dado alturas, índice activo y ancla, devuelve el desplazamiento objetivo y el retardo en cascada de cada línea.
- Extensión de `LyricBlock`: `GetLineEnd(index)` (y reutiliza `GetActiveLineIndex`).
- `LyricsTiming.GetInstrumentalGaps`: huecos (inicio, fin) a partir de las duraciones estimadas y `InstrumentalGap`.
- `LyricLine.Words` opcional (RNF-5), sin uso por ahora.

**Desktop:**
- `Controls/ProgressiveLyricsView.cs`: control propio (panel con medición, resorte por línea, reloj de fotogramas, máscara de fundido, opacidad y escala por distancia).
- `Controls/FillTextBlock.cs`: dibuja texto tenue y texto brillante recortado hasta una **posición horizontal** (`FillX`), con borde suave.
- `LyricsViewModel`: expone la posición fina (`Position`) y el estilo elegido; `MainViewModel` solo le entrega el reloj. Registro en `CompositionRoot.cs` si hace falta un servicio de reloj.
- `MainWindow.axaml`: solo el bloque del modo inmersivo (columna derecha, hoy un `ListBox` con la plantilla `LyricLineTemplate`). Ahí se añade el control nuevo y el interruptor temporal Clásico/Progresivo; el panel lateral queda intacto.
- `Themes/ThemeBrushes.axaml`: pinceles para texto activo, tenue y máscara; sin colores en línea.

## Fuera de alcance

- **El panel lateral** (se queda con el `ListBox` actual) y cualquier fase posterior para él.
- **Desenfoque** de líneas inactivas.
- Brillo morado y temblor por línea (`glow`, `ShakeEffect`) del original: dependen de números de línea de una canción concreta.
- **Sincronía palabra por palabra** (`.lrc` enhanced, TTML) y generación automática de tiempos (Whisper u otras): segunda spec. Aquí solo quedan las dos precauciones de RNF-5.
- Descarga de letras o servicios externos (la app es 100 % local).
- Edición de tiempos dentro de la app.
- Letras no sincronizadas (`Plain`): conservan el diseño actual.
- Rebote tipo «pelota» (alguien lo echó de menos en el hilo).

## Casos límite

- Canción sin letra o con `Plain`: sin cambios.
- Dos líneas con la misma marca (`[00:12.00]` repetidas): se tratan como ya hace el parser (orden estable); la duración de la primera sería 0, así que se aplica un mínimo y se rellena al instante.
- Hueco largo entre dos líneas sin línea vacía: el tope por longitud evita un relleno de 20 s para tres palabras.
- Línea muy larga que ocupa varios renglones: el relleno recorre renglón a renglón; el ancla usa la altura total.
- Línea de un solo carácter o solo símbolos.
- Cambio de tamaño de ventana o de fuente: se vuelve a medir y el desplazamiento se reajusta sin saltos.
- Cambio de pista mientras se anima: se cancela todo y la nueva letra empieza desde su estado inicial.
- Pausa a mitad de línea: el relleno queda congelado; reanudar continúa desde ahí.
- Seek hacia atrás: las líneas posteriores vuelven a estado «futura» y el desplazamiento retrocede (el Swift no lo hacía).
- Letra muy larga (más de ~300 líneas): virtualizar el dibujo (solo las líneas visibles ± margen).
- Tema claro/oscuro/personalizado en vivo (vista previa del selector): los pinceles se actualizan sin reiniciar.
- Modo inmersivo siempre oscuro: los pinceles del control son los del modo inmersivo.
- «Reducir movimiento» activado: ver criterio 11.
- `[offset:]` del `.lrc`: **el parser lo descarta** como metadato (comprobado) y no se aplica a ninguna letra, tampoco hoy. Queda fuera de alcance; si un `.lrc` lo usa, la letra irá desfasada igual que en el estilo clásico.
- Cabecera con finales de línea mezclados (`[id: bccjinbd]` con espacio y sin `\r`, el resto con `\r\n`): el parser lo tolera (`Trim` quita el `\r`; `[a-zA-Z]+:` admite el espacio tras los dos puntos). Lo cubre una prueba (hito M1).
- Líneas en blanco entre estrofas: el parser las descarta; no producen «♪». Ver criterio 5.

## Resultado de M0

**Qué se construyó (rama `feature/letras-progresivas`):**
- `Controls/FillTextBlock.cs`: texto tenue y, encima, brillante recortado hasta `FillX` (posición horizontal acumulada sobre los renglones, RNF-5) con borde suave de 24 DIPs (`PushOpacityMask` con degradado). El borde recorre `ancho + suavizado` para que no haya salto al completar el renglón.
- `Controls/ProgressiveLyricsView.cs`: línea activa rellenándose y la siguiente tenue; antes de la primera línea, «♪». El estado se recalcula en cada fotograma a partir de la posición. Con el reloj parado (pausa), un salto o un cambio de pista llegan por `PropertyChanged` de `LyricsViewModel`. La duración de la línea es **provisional** (`min(siguiente − inicio, 0,08 s × caracteres, entre 1 y 8 s)`) y M1 la lleva a Core.
- `LyricsViewModel`: `CurrentPosition`, `ActiveIndexAt`, `ShowProgressiveImmersive` y `ShowClassicImmersive`, más `UseProgressiveSpike`, que solo existe en Debug y se activa con `MUSICPLAYER_LYRICS_SPIKE=1`. En Release vale `false` siempre, y M4 lo sustituye por `LyricsStyle`.
- `MainWindow.axaml`: el control entra en la columna de letra del modo inmersivo; el `ListBox` sigue igual para el estilo clásico y las letras `Plain`. El panel lateral no se tocó.
- Variables de Debug para medir: `MUSICPLAYER_LYRICS_CLOCK=<fps>|raf` y `MUSICPLAYER_LYRICS_STATS=<ruta>` (fps, % de fotogramas con posición nueva, desvío entre el avance de la posición y el del reloj).

**Archivo de prueba:** `Chicago.mp3` ya trae la letra incrustada (`lyrics-spa`), con las mismas 77 líneas con marca que `Michael Jackson - Chicago.lrc`, así que no se copió nada a la carpeta de música.

**Sonda** (Windows 11, pantalla 1920×1080 a ~120 Hz, compilación Debug como en M7, volumen 0, aleatorio apagado, isla apagada, modo inmersivo a pantalla completa; `settings.json` copiado y restaurado idéntico por hash; `library.json` sin cambios). CPU del proceso en 10 s, en % de un núcleo, tras 3 s de estabilización:

| Variante | CPU (pasadas) | Frente al clásico | fps reales |
|---|---|---|---|
| Clásico (`ListBox`) | 7,6 / 9,8 / 6,7 / 7,3 / 9,1 / 7,3 / 9,4 | — | reloj parado |
| `RequestAnimationFrame` | 26,2 / 31,6 | **≈ +20** | 96-122 |
| Temporizador a 60 fps | 19,2 / 20,3 | ≈ +11 | 43 |
| **Temporizador a 30 fps (elegido)** | 14,8 / 17,0 / 13,3 / 18,0 / 16,9 / 16,4 / 16,2 | **≈ +7,5** | 24 |
| 30 fps, reloj sin redibujar | 7,8 / 8,3 / 10,0 / 8,9 / 8,4 | ≈ +0,5 | 24 |
| 30 fps, redibujar solo el texto tenue | 13,7 / 14,4 / 10,9 | ≈ +4 | 24 |
| 30 fps, recorte duro sin máscara | 12,5 / 15,3 / 25,8 | ≈ +5 (una pasada atípica) | 24 |
| 30 fps, sin el fondo `blur(60)` | 11,9 / 11,7 | ≈ +4 | 24 |

- **Fluidez de la posición:** el 100 % de los fotogramas trae una posición nueva, y el desvío medio frente al reloj de pared es de 0,0 ms (máximo 2,7 ms). El paso máximo entre fotogramas es de ~55 ms a 24 fps y de ~35 ms a 120 fps.
- **Relleno:** una ráfaga de 14 capturas cada 250 ms sobre la primera línea («Ah, I met her on my way to Chicago») muestra el avance de izquierda a derecha con el borde suave, y «♪» con la siguiente línea antes de que empiece.
- **De dónde sale el coste** (*corregido en M2: la ventana no se redibuja entera; ver «Resultado de M2»*): casi todo es redibujar la ventana a pantalla completa en cada fotograma (≈ +4 aunque solo se pinte el texto tenue). La máscara del borde suave suma ~2-3 puntos y el fondo desenfocado, ~2-3. El reloj en sí no cuesta. El coste crece con los fps, y por eso `RequestAnimationFrame` (sigue los ~120 Hz de la pantalla) se descarta.
- **Pausa y clásico:** con el reloj parado no hay fotogramas extra (0,3 «fps» en las estadísticas: solo los cambios de línea por `PropertyChanged`).
- **Criterio 12 (≤ +5):** **no se cumple todavía** (+7,5). Medidas para M2 y M3, por orden de esperado ahorro:
  1. Parar el reloj cuando la línea activa está completa y programar el siguiente arranque para la marca de la línea siguiente. Ahorra en proporción al tiempo con la línea completa: poco en `Chicago.lrc` (líneas seguidas), mucho en canciones con pausas.
  2. Sustituir `PushOpacityMask` (una capa por fotograma) por un pincel de degradado como primer plano del renglón parcial, sin capa.
  3. Bajar a 20 fps si a la vista sigue siendo fluido (el borde avanza ~5-7 px por fotograma con un suavizado de 24 px).
- Los experimentos («sin relleno», «solo tenue», «sin máscara», «sin fondo») se quitaron del código tras medir.

**A mano (autor): pasa.** A ~24 fps y a 20 fps el relleno se ve fluido.

**Decisiones tras M0 (autor, 7 de noviembre de 2026):**
1. Se aceptan los +7,5 puntos por ahora; se sigue con M1 sin esperar.
2. Antes de recortar nada, **medir en Release** (M0 midió en Debug). Si en Release el coste baja de +5, no se recorta más. Hace falta poder activar el estilo progresivo en Release: se adelanta el ajuste `LyricsStyle` (M4) o se habilita la variable del spike en Release solo para la medida. Se decide en M2.
3. **Investigar por qué se redibuja la ventana completa** en cada fotograma. Si se puede invalidar solo la zona de la letra, el ahorro sería mayor que el de las tres mejoras juntas.
4. Mejoras en M2/M3, en este orden: (1) degradado directo en el texto en lugar de `PushOpacityMask`; (2) parar el reloj con la línea completa y arrancarlo con la siguiente; (3) bajar a 20 fps solo si tras 1 y 2 sigue por encima del tope (a 20 fps se ve fluido).

## Resultado de M1

Lógica en Core, sin dependencias, con 74 pruebas nuevas (más las 2 del parser de antes): 421 en Core y 58 en Infrastructure, todas en verde. `dotnet build -c Release` solo con la CS8602 conocida.

- **`LyricLine.Words`** (`IReadOnlyList<LyricWord>?`, al final del registro) y **`LyricWord(Text, Start, End)`**. Siempre `null`; las llamadas posicionales y los `with` existentes no cambian (RNF-5).
- **`LyricBlock.GetLineEnd(index)`**: la marca de la línea siguiente; `null` para la última, para letras no sincronizadas o para un índice inválido.
- **`LyricsTiming`**:
  - `FillDuration`: 80 ms por carácter (sin espacios de los extremos), entre 1 y 8 s, y nunca más que el hueco hasta la siguiente línea. Cero si comparte marca con la siguiente o si es una línea vacía. La última línea usa solo la estimación.
  - `Fill(block, index, posición)` en [0, 1] y `FillEnd`. Se calculan desde la posición: tras un salto atrás no queda nada del estado anterior (prueba propia).
  - `GetInstrumentalGaps(block, duraciónPista?)` y `GapAt`. Hay pausa con ≥ 8 s entre el fin estimado de una línea y la siguiente, antes de la primera y después de la última (con la duración de la pista), y siempre desde una línea vacía explícita hasta la siguiente. Las pausas contiguas se unen. `InstrumentalGap.Progress` da el avance para el indicador «♪».
  - Con las marcas y longitudes reales de `Chicago.lrc` (sin su texto), la única pausa es **02:03.46 → 02:13.00**. La entrada de 2,59 s y el final de ~3,9 s (pista de 4:05.53) no llegan al umbral.
- **`SpringSolver`**: `Spring(rigidez, amortiguación)`, `Spring.FromResponse(response, dampingFraction)` y `Spring.Default` = el `spring()` de SwiftUI (0,55 s; 0,825; ~1 % de rebote). Euler semi-implícito con subpasos de ≤ 4 ms, estable con fotogramas de 200 ms. Una pausa de ≥ 1 s coloca directamente en el objetivo. Cambiar el objetivo a mitad del movimiento conserva la inercia, sin saltos. Trocear el mismo tiempo en fotogramas distintos cambia el resultado menos de medio píxel (con dobles el número de subpasos variaba; se cuenta en ticks).
- **`LyricsScroll`**: `LineTops` con las alturas medidas, `TargetOffset` (ancla al 35 %; índice -1 o fuera de rango ajustado a la primera o la última línea) y `CascadeDelay`: 100 ms por línea con tope de 400 ms. Al avanzar se retrasan las líneas de debajo y al retroceder las de encima; un salto de más de una línea mueve todo a la vez.
- El spike ya usa `LyricsTiming.Fill` mediante `LyricsViewModel.FillAt`; la estimación provisional de M0 desapareció.

## Ajuste `LyricsStyle` adelantado (inicio de M2)

Se adelantó de M4 **solo el ajuste** para poder medir en Release (decisión del autor). El resto de M4 sigue en M4.

- `Core/Models/LyricsStyle.cs` (`Classic`, `Progressive`) y `AppSettings.LyricsStyle`, **al final** y por defecto `Classic`.
- `LyricsViewModel.Style` (restaurado en `Restore` y guardado al cambiar) e `IsProgressiveStyle` para el interruptor. `ShowProgressiveImmersive` = estilo progresivo y letra sincronizada.
- Interruptor: `ToggleButton` «Letra progresiva», arriba a la derecha del modo inmersivo, visible solo con letra sincronizada. Con el foco en él, la barra espaciadora sigue siendo reproducir/pausa (el atajo global solo cede a los `CheckBox`).
- Se quitó `UseProgressiveSpike` y la variable `MUSICPLAYER_LYRICS_SPIKE`. Quedan, solo en Debug, `MUSICPLAYER_LYRICS_CLOCK` y `MUSICPLAYER_LYRICS_STATS`.
- Pruebas (Infrastructure): JSON parcial → `Classic`; el `settings.json` completo de la v1.3.0 (sin la clave) → `Classic` y el resto se conserva; ida y vuelta con `Progressive`.

## Notas para M3

- **Reiniciar la referencia de tiempo del resorte al reiniciar el reloj.** Si no, el primer fotograma tras una pausa ve un intervalo de 1 s o más y `SpringSolver.Step` coloca directamente en el objetivo, sin animar.
- **El reloj solo se para cuando el relleno está completo y todos los resortes están en reposo** (`SpringSolver.IsSettled`), no solo cuando termina la línea.

## Resultado de M2

Sonda como en M0 (Windows 11, 1920×1080 a ~120 Hz, volumen 0, aleatorio e isla apagados, modo inmersivo con Chicago; `settings.json` copiado y restaurado idéntico por hash en cada ejecución; `library.json` sin cambios). El estilo se elige con `LyricsStyle` en `settings.json`, así que ya se mide en Release. CPU en % de un núcleo, 10 s tras 3 s de estabilización.

**1. Release frente a Debug** (misma sesión, 3 pasadas, todavía con la máscara):

| | Clásico | Progresivo | Diferencia |
|---|---|---|---|
| Release | 10,7 | 16,1 | **+5,4** |
| Debug | 8,8 | 15,5 | +6,7 |

En Release seguía por encima del tope, así que había que recortar.

**2. ¿Se redibuja la ventana entera? No.** Con `MUSICPLAYER_RENDER_OVERLAY=DirtyRects` (nuevo, solo Debug), la zona sucia de cada fotograma es **solo el rectángulo de la línea que se rellena**. La conclusión de M0 («redibujar la ventana a pantalla completa») era una deducción y estaba mal. Lo que pesa es un coste fijo por fotograma: el overlay `RenderTimeGraph` daba ~3,25 ms de render de media. Dentro del rectángulo sucio se vuelve a componer todo lo que hay debajo, y eso se midió por separado:

| Experimento (Debug, 3-4 pasadas) | Clásico | Progresivo |
|---|---|---|
| Normal | 8,0 / 8,6 | 15,4 / 15,1 |
| Ventana principal oculta bajo el inmersivo | 7,5 | 15,3 (sin efecto) |
| Sin el fondo `blur(60)` | 7,9 | 12,8 (≈ −2) |

- Ocultar la ventana principal, que el inmersivo tapa pero sigue en el árbol, no ahorra nada. Sí explica una franja que se repinta cada 250 ms abajo (la barra de progreso tapada), en los dos estilos.
- El fondo desenfocado cuesta ~2 puntos con el relleno en marcha: se recompone bajo la línea en cada fotograma. Si algún día hiciera falta, se podría desenfocar la carátula una vez en un mapa de bits y pintarla sin `Effect`. No hace falta (ver 3).
- Invalidar solo la zona de la letra ya es lo que ocurre, así que ese ahorro no existe.

**3. Degradado en el texto en lugar de `PushOpacityMask`** (mejora 1). El renglón a medio rellenar se dibuja con un `TextLayout` cuyo pincel es un `ImmutableLinearGradientBrush` (color pleno → transparente en `SoftEdge` DIPs), recortado a su renglón con `PushClip`, que no abre capa. Release, misma sesión, 6 pasadas (la versión con máscara es una copia del Release anterior):

| Release | Pasadas | Media | Frente al clásico |
|---|---|---|---|
| Clásico | 10,3 / 9,4 / 9,5 / 8,3 / 8,7 / 9,2 | 9,2 | — |
| Con máscara | 13,6 / 16,5 / 15,9 / 14,0 / 15,6 / 14,4 | 15,0 | +5,8 |
| **Con degradado** | 13,7 / 11,2 / 12,8 / 13,4 / 11,9 / 14,7 | **13,0** | **+3,8** |

En una sesión anterior de 4 pasadas el degradado dio 12,5 / 12,6 / 13,3 / 22,2 (una atípica). **Criterio 12 (≤ +5) cumplido en Release.** Según la decisión 2 tras M0, no se recorta más: la mejora 3 (20 fps) no se aplica. La mejora 2 (parar el reloj) queda en M3 con las condiciones de «Notas para M3», porque M3 necesita esa regla de todos modos para los resortes.

**4. Aspecto.** Ráfagas de capturas en Release:
- Primera línea (un renglón): igual que con la máscara. El borde suave cae donde corresponde: las coordenadas del degradado son las del control.
- Línea de 77 caracteres de las 3:15.04 (dos renglones), alcanzada moviendo el `Slider` del inmersivo por UI Automation: el primer renglón se rellena y el relleno continúa en el segundo («Heav|en just for me)»), con el borde suave en su sitio.
- Temas Negro puro, Claro y Medianoche: idénticos. El modo inmersivo fuerza la variante oscura, así que el texto es blanco y el tenue gris con cualquier tema (la spec pedía Oscuro; se usó Negro puro, el tema del autor, que también es oscuro).
- No probado: un renglón con varias tiradas de texto (mezcla de alfabetos con otra fuente). Ahí el degradado podría no ir en coordenadas del control.

**5. Limpieza.** Se quitaron los experimentos («ventana principal oculta», «sin fondo»). Quedan solo en Debug `MUSICPLAYER_RENDER_OVERLAY`, `MUSICPLAYER_LYRICS_CLOCK` y `MUSICPLAYER_LYRICS_STATS`.

**Nota sobre `settings.json`:** cada sonda lo restaura idéntico al estado con que empezó. Entre M0 y M2 su `LastTrackId` pasó a Chicago fuera de las sondas, probablemente por la prueba a mano de M0 (la app guarda la última canción).

## Hitos (cada uno compila, pasa pruebas y se puede comprobar solo)

| # | Hito | Verificación |
|---|------|--------------|
| M0 | **Spike** con `Michael Jackson - Chicago.lrc`: control mínimo en el modo inmersivo, relleno por posición, reloj de fotogramas; medir CPU y suavidad (**hecho**, ver «Resultado de M0»; pendiente la prueba a mano) | Captura/vídeo y CPU con la letra visible. Decide `RequestAnimationFrame` frente a temporizador |
| M1 | Core: `LyricsTiming` (con huecos instrumentales), `LyricBlock.GetLineEnd`, `SpringSolver`, `LyricsScroll`, `LyricLine.Words` (vacío). Pruebas del parser con el formato de `Chicago.lrc` (**hecho**, ver «Resultado de M1») | Pruebas: duraciones, empates, última línea, saltos atrás, convergencia del resorte, huecos |
| M2 | `FillTextBlock` con `FillX`, borde suave **con degradado en el texto (sin `PushOpacityMask`)**, texto en varios renglones y temas. Medida en **Release** e investigación del redibujo de la ventana completa (**hecho**, ver «Resultado de M2») | Capturas en Oscuro, Claro y Medianoche; relleno al 0, 50 y 100 %. CPU en Release frente al clásico |
| M3 | `ProgressiveLyricsView`: desplazamiento con resorte, cascada, fundido, opacidad y escala por distancia. Reloj parado con la línea completa; 20 fps solo si aún supera el tope (ver «Notas para M3») | Medida de CPU en Release frente al `ListBox` clásico (criterio 12) |
| M4 | Integración en el modo inmersivo; panel lateral intacto. (El ajuste `LyricsStyle` y su interruptor se adelantaron a M2, ver «Ajuste `LyricsStyle` adelantado».) | Pulsar línea salta; `Plain` sigue en el `ListBox` |
| M5 | «Reducir movimiento» y revisión a mano | A mano (autor): sensación frente al vídeo de referencia |
| M6 | Tras la aprobación: retirar el estilo clásico **solo del modo inmersivo** y el ajuste `LyricsStyle`. Docs (`README`, `reporte-reproductor.md`, esta spec) y `CLAUDE.md` | `dotnet build -c Release` sin warnings nuevos y pruebas en verde; CI en Windows y Ubuntu |

## Decisiones tomadas (autor, 7 de noviembre de 2026)

1. **Dónde.** Solo el modo inmersivo. El panel lateral conserva el `ListBox` actual; no hay segunda fase para él en esta spec.
2. **Estilo por defecto.** Clásico, con el interruptor temporal `LyricsStyle` para alternar mientras se prueba. Al aprobar el nuevo, el clásico se retira solo del modo inmersivo. El `ListBox` sigue por el panel lateral y las letras `Plain`.
3. **Desenfoque.** No. Se separan las inactivas con opacidad menor y reducción de escala por distancia.
4. **Sincronía por palabra.** Fuera de esta spec (los `.lrc` del autor solo traen el inicio de cada línea). Dos precauciones de diseño: `FillTextBlock` recibe la posición horizontal y `LyricLine` lleva un campo opcional de palabras, vacío (RNF-5).

## Resultado del análisis de `Michael Jackson - Chicago.lrc`

Archivo de prueba para M0 (92 líneas, 4 141 bytes). Revisado byte a byte:

| Qué | Resultado |
|---|---|
| Cabecera | `[id: bccjinbd]` (con espacio y **sin** `\r`) y luego `[ti:]`, `[ar:]`, `[al:]`, `[au:]`, `[by:]`, `[la:]`, `[ve:]` con `\r\n`. Es la mezcla de finales de línea que se sospechaba |
| Parser | La tolera: `Split('\n')` + `Trim()` quita el `\r`, y `^\[[a-zA-Z]+:.*\]$` acepta `[id: bccjinbd]`. Ninguna cabecera se cuenta como letra |
| Líneas con marca | 77, todas con formato `[mm:ss.xx]` |
| Líneas en blanco | 8 sin marca (entre estrofas y tras la cabecera). El parser las **descarta** (`line.Length == 0 → continue`) |
| Líneas con marca y texto vacío | **Ninguna**: este archivo no trae «♪» explícitos |
| Hueco más largo | 11,2 s antes de `02:13.00`; es la única pausa de más de 8 s. Hay también una entrada de 2,59 s antes de la primera línea |
| Línea más larga | 87 caracteres (cabe en 2 renglones en el panel de media pantalla) |

Conclusiones: (1) el criterio 5 no puede esperar líneas vacías; usa huecos. (2) Las líneas con paréntesis (`(She was lovin' me)`) son coros: se tratan como texto normal. (3) La duración estimada por línea debe respetar el tope por longitud: sin él, la línea previa al hueco de 11,2 s se rellenaría durante todo el hueco.

Pruebas añadidas en `LrcLyricParserTests`: cabecera mixta (`[id: x]` sin `\r` y el resto con `\r\n`) y líneas en blanco entre estrofas. Documentan el comportamiento actual.

## Verificación

Prueba a mano del spike (Debug):

```powershell
dotnet run --project src/MusicPlayer.Desktop
# Modo inmersivo → interruptor «Letra progresiva». Solo Debug, para comparar fluidez:
# $env:MUSICPLAYER_LYRICS_CLOCK = '20' | '60' | 'raf'
```

- Automática: `dotnet build -c Release` y `dotnet test -c Release` (403 actuales más las nuevas).
- Manual en Windows: relleno fluido, salto atrás y adelante, pausa, cambio de pista a mitad de línea, tema en vivo, ventana redimensionada, letra con traducción y pronunciación, `.lrc` de 300+ líneas y comparación de CPU con el estilo clásico.
