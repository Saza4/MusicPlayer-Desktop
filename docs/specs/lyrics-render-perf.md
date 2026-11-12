# Spec: rendimiento del dibujo de la letra (derivada de la auditoría del 11 de noviembre de 2026)

Estado: **aprobada el 11 de noviembre de 2026**, reducida a «medir primero». **Medida hecha el mismo día
con `tools/medir-cpu.ps1`: el relleno por palabra no tiene un coste medible (−0,4 puntos), así que no se optimiza
nada** (ver «Medidas»). Queda sin medir el texto RTL.

## Context

Quinta spec de la auditoría. El plan la describía como «caché de `TextLayout` y `Typeface`, sin asignaciones por
fotograma, onda de la isla». Tras leer el código y las specs anteriores, **se redujo**: primero medir, y
optimizar solo si la medida lo pide.

**Por qué.** La auditoría señaló trabajo repetido en cada fotograma, y es cierto que existe:

| Id | Trabajo por fotograma (solo la fila que se rellena) | Evidencia |
| -- | --------------------------------------------------- | --------- |
| P1 | Un `TextLayout` nuevo, con su pincel de degradado y sus dos paradas, para el renglón a medio rellenar | `FillTextBlock.cs` `EdgeLayout`, llamado desde `Render` |
| P2 | Con letra enhanced, dos `HitTestTextPosition` por fotograma para situar el relleno en la palabra | `FillTextBlock.XAt` → `CumulativeX`, llamado desde `LineItem.SetFill` (`ProgressiveLyricsView.cs:508`) |
| P3 | Un iterador (`yield`) por cada `SetFill` | `ProgressiveLyricsView.cs:544` `Rows()` |
| P4 | Al medir una fila: un `Typeface` y dos `TextLayout` con el mismo texto (tenue y brillante) | `FillTextBlock.MeasureOverride` (no es por fotograma: solo al cambiar texto, tamaño o ancho) |
| P5 | Isla: un arreglo nuevo por fotograma de la onda (15 fps) y se anima también la onda que no se ve | `IslandWindow.axaml.cs` `OnWaveFrame` |

Pero el proyecto ya midió esto y decidió no recortar más:

- `lyrics-progressive.md`: el coste en Release quedó en **+3,4 puntos de CPU** frente a la lista clásica, dentro
  del tope de +5 (criterio 12), con P1, P3 y P4 ya presentes. La decisión registrada fue «no se recorta más».
  El `TextLayout` por fotograma de P1 es justo lo que sustituyó a `PushOpacityMask`, que era más caro.
- `dynamic-island.md`: la onda se midió en su M7.
- Lo único sin medir es **P2**, que llegó después con el relleno por palabra. Es el criterio 12 de
  `lrc-enhanced-fill.md` (M4, pendiente): como máximo **+5 puntos de CPU frente a la estimación uniforme**. Esa
  misma spec deja la «caché de posiciones por palabra» para cuando «la medida de M4 lo pida».

Optimizar P1, P3, P4 o P5 sin una medida que lo justifique iría contra una decisión ya tomada y tocaría el
código de dibujo que más costó afinar. Esta spec cierra la medida pendiente y deja escrito qué se haría, y en
qué orden, si no sale bien.

**Corrección al plan de la auditoría:** `MUSICPLAYER_LYRICS_STATS` no sirve para esto. Solo existe en Debug y
mide fotogramas por segundo y desvío del reloj, no CPU. La sonda de CPU que se usó en M0-M5 no está en el
repositorio.

## FEATURE: Medir el relleno por palabra y optimizar solo si hace falta

### OVERVIEW
Cerrar la medida de CPU en Release que quedó pendiente para las letras enhanced, con una sonda que se pueda
repetir, y aplicar optimizaciones solo si se supera el tope.

### ACCEPTANCE CRITERIA

1. **Sonda repetible.** Un script de PowerShell en el repositorio (`tools/medir-cpu.ps1`) que, dado el nombre o
   el Id de un proceso, espera 3 s, mide su tiempo de CPU durante 10 s y escribe el porcentaje de un núcleo, con
   el número de pasadas como parámetro. Es el método de M0-M5 de `lyrics-progressive.md`. No lanza la app, no
   toca `settings.json` ni la biblioteca y no usa la red.
2. **Medida de P2 en Release.** Con el modo inmersivo a pantalla completa, volumen 0, aleatorio e isla apagados
   (las condiciones de M0-M5), cinco pasadas de cada variante en la misma sesión:
   - una pista con `.lrc` normal (estimación uniforme), y
   - la misma pista con su `.lrc` enhanced.
   Se anotan en esta spec las pasadas, las medias y la diferencia.
3. **Casos del criterio 12 de `lrc-enhanced-fill.md`:** además del caso normal, una pasada con una línea de
   varios renglones y otra con alfabetos mezclados (latín y CJK), que es donde `EdgeLayout` crea un pincel por
   tirada. El texto RTL se mide si hay una letra a mano; si no, se anota como no medido.
4. **Si la diferencia es de +5 puntos o menos:** no se cambia el código de dibujo. Se marca M4 como hecho en
   `lrc-enhanced-fill.md`, se actualiza la frase del README («Las medidas de CPU en Release de esta función
   están pendientes») y se cierra esta spec.
5. **Si pasa de +5 puntos:** se aplican las mejoras de esta lista, una cada vez y midiendo después de cada una,
   hasta volver al tope. No se aplica ninguna más de las necesarias.
   1. **Caché de posiciones por palabra (P2):** la distancia acumulada del inicio y del final de cada palabra se
      calcula una vez por línea y por medida de la fila, y se invalida en `MeasureOverride`. `XAt` pasa a ser una
      interpolación entre dos números.
   2. **Borde cuantizado (P1):** reutilizar el `TextLayout` del borde mientras el relleno no avance más de
      1 DIP, o mover solo el pincel si Avalonia lo permite sin rehacer el texto.
   3. **`Rows()` sin iterador (P3):** un arreglo fijo por línea.
6. **Sin regresión visual.** Si se toca el dibujo (criterio 5), se comparan capturas antes y después con la
   misma letra sintética: borde suave del relleno, líneas de varios renglones y alfabetos mezclados. El método de
   capturas es el de CLAUDE.md (copia de seguridad de `settings.json` y comprobación del hash al restaurar).
7. **Cierre.** `dotnet build -c Release` sin advertencias nuevas y `dotnet test` en verde. Las cifras medidas
   quedan en esta spec, tanto si se optimiza como si no.

### OUT OF SCOPE
- P4 y P5: no son por fotograma (P4) o ya se midieron y cuestan poco (P5: un arreglo de dos elementos 15 veces
  por segundo). Solo se tocarían con una medida propia que lo pidiera.
- Bajar los fotogramas por segundo (`ProgressiveLyricsMetrics.FramesPerSecond`): descartado en M4 de
  `lyrics-progressive.md`.
- Liberar (`Dispose`) los `Bitmap` grandes y las suscripciones a eventos sin cancelar que señaló la auditoría:
  no son del dibujo de la letra. Quedan anotados para una spec de memoria si se observa crecimiento.
- Virtualizar letras muy largas: medido en M5 de `lyrics-progressive.md`, no hace falta.
- Relleno de derecha a izquierda en texto RTL.
- Medir en Linux.

### EDGE CASES
- **Ruido entre sesiones:** las specs anteriores lo estiman en ±1,5 puntos. Las dos variantes se miden en la
  misma sesión y se comparan medias de cinco pasadas; una pasada atípica (más del doble de la media de las
  demás; el script avisa) se repite y se anota.
- **La pista no tiene `.lrc` normal y enhanced a la vez:** se usa una copia de la pista en una carpeta temporal
  con uno u otro archivo al lado. No se sube ninguna letra real al repositorio.
- **`MUSICPLAYER_LYRICS_OVERRIDE` no existe en Release:** la letra tiene que estar junto al audio o incrustada.
- **El proceso no existe o termina durante la medida:** el script lo dice y no escribe una cifra.
- **Diferencia cerca del tope** (entre +4 y +6): se repite la sesión completa antes de decidir.

## Decisiones (confirmadas por el autor el 11 de noviembre de 2026)
1. **La spec se reduce a «medir primero».** No se optimiza P1 a P5 sin una medida que lo pida.
2. **Mide el autor, con el script.** Hace falta su biblioteca, una pista con las dos variantes de `.lrc`, la
   pantalla completa y un entorno quieto durante varios minutos; así no se toca `settings.json` desde fuera.
   **Cambiado después por el autor:** pidió que la midiera el agente (ver «Medidas»).
3. **Carpeta `tools/` en el repositorio** para la sonda, para que la medida se pueda repetir (la de M0-M5 se
   perdió).

## Procedimiento de medida

Preparación (una vez):

1. Elegir una pista con `.lrc` enhanced junto al audio. Guardar a mano, fuera de la carpeta de música, dos
   versiones de esa letra: la enhanced y una normal (la misma sin las etiquetas `<mm:ss.xx>`). La letra que está
   junto al audio tiene prioridad sobre la incrustada y se vuelve a leer cada vez que se carga la pista, así que
   para cambiar de variante basta con sustituir el `.lrc`, pasar a otra pista y volver.
2. Para los casos del criterio 3, tener a mano una pista cuya letra enhanced tenga una línea de varios renglones
   y otra con latín y CJK mezclados (pueden ser letras sintéticas puestas junto a cualquier pista; no se suben).
3. Compilar y abrir la app en Release, sin depurador:
   `dotnet build -c Release` y `dotnet run -c Release --no-build --project src/MusicPlayer.Desktop`.
4. Condiciones de M0-M5: volumen 0, aleatorio apagado, isla apagada, estilo de letra progresivo, modo inmersivo
   a pantalla completa. Cerrar lo que pueda meter ruido (navegador, compilaciones, actualizaciones).

Cada medida:

1. Poner la pista al principio de un tramo con letra continua (el mismo tramo en las dos variantes) y entrar en
   el modo inmersivo.
2. En una terminal aparte, desde la raíz del repositorio (`-Inicio 10` da diez segundos para volver a la app y
   no tocar nada; cinco pasadas duran 65 s más la cuenta atrás):

   ```powershell
   ./tools/medir-cpu.ps1 -Etiqueta 'normal' -Inicio 10
   ./tools/medir-cpu.ps1 -Etiqueta 'enhanced' -Inicio 10
   ./tools/medir-cpu.ps1 -Etiqueta 'enhanced, varios renglones' -Pasadas 1 -Inicio 10
   ./tools/medir-cpu.ps1 -Etiqueta 'enhanced, latín y CJK' -Pasadas 1 -Inicio 10
   ```

   Si PowerShell no deja ejecutar scripts: `powershell -ExecutionPolicy Bypass -File ./tools/medir-cpu.ps1 …`.
3. El script termina con una fila lista para pegar en la tabla de abajo. Si avisa de una pasada atípica, se
   repite esa medida y se anotan las dos.

## Medidas (11 de noviembre de 2026)

Windows 11, pantalla 1920×1080, compilación Release de la rama `fix/playback-resilience` (commit `d238269` más
esta spec), volumen 0, aleatorio e isla apagados, modo inmersivo a pantalla completa, sin tocar ratón ni teclado.
CPU del proceso con `tools/medir-cpu.ps1` (10 s tras 3 s de espera, % de un núcleo). `settings.json` copiado y
restaurado idéntico por hash; `library.json` sin cambios; ningún archivo de audio tocado.

**Cómo se hizo, y en qué se aparta del procedimiento de arriba.** Lo ejecutó el agente, a petición del autor, con
una sonda automática que no se sube (abre la app, pulsa «Reproducir» y «Pantalla completa» por UI Automation,
hace una captura para comprobar lo que se ve y lanza el script):

- **Letra sintética**, no la de una pista real: palabras inventadas, una línea cada 3,4 s (unos tres cambios de
  línea por pasada), puesta como `.lrc` junto a una pista de la biblioteca (`Zafar.mp3`) y borrada al terminar.
  La variante normal es el mismo texto sin las etiquetas `<mm:ss.xx>`.
- **La app se reinicia en cada variante**, con 13 s de reproducción antes de la primera pasada; todas las pasadas
  de una fila empiezan en el mismo punto de la pista.
- Normal y enhanced se midieron en orden normal, enhanced, enhanced, normal, para repartir la deriva.
- Los casos de varios renglones y alfabetos mezclados llevan tres pasadas y su propia variante normal con el
  mismo texto, en lugar de una sola pasada.

| Variante | CPU (pasadas, % de un núcleo) | Media |
| -------- | ----------------------------- | ----- |
| `.lrc` normal (estimación uniforme), 7 palabras por línea | 14,8 / 11,5 / 11,4 / 13,1 / 10,0 y 10,9 / 15,0 / 10,5 / 11,1 / 10,5 | **11,9** |
| `.lrc` enhanced, mismo texto | 10,3 / 11,4 / 11,2 / 10,0 / 12,5 y 12,0 / 14,2 / 9,4 / 11,4 / 12,7 | **11,5** |
| Normal, líneas de dos y tres renglones (22 palabras) | 10,3 / 16,4 / 15,0 | 13,9 |
| Enhanced, líneas de dos y tres renglones | 13,1 / 13,0 / 12,0 | 12,7 |
| Normal, latín y CJK alternados | 10,9 / 13,3 / 12,0 | 12,1 |
| Enhanced, latín y CJK alternados | 11,5 / 14,2 / 10,5 | 12,1 |
| Enhanced, RTL | no medido | |

- **Diferencia (enhanced − normal):** −0,4 puntos con diez pasadas por variante; −1,2 con varios renglones y 0,0
  con alfabetos mezclados (tres pasadas). Todo queda dentro del ruido (las pasadas de una misma variante van de
  9,4 a 15,0): **el relleno por palabra no tiene un coste medible.** Tope: +5.
- Ninguna pasada atípica según el script.
- **Decisión: criterio 4.** No se toca el código de dibujo: ni la caché de posiciones por palabra, ni el borde
  cuantizado, ni `Rows()`. Se anota la medida en `lrc-enhanced-fill.md` y se actualiza el README.
- **Sin medir:** texto RTL, y una letra real (más irregular que la sintética).

## Verificación
- El script se prueba contra un proceso cualquiera (por ejemplo la propia sesión de PowerShell) y contra un Id
  que no existe. **Hecho** en PowerShell 7 y en Windows PowerShell 5.1: mide la propia sesión, y con un Id o un
  nombre que no existe, o con varios procesos del mismo nombre, lo dice, no escribe cifra y sale con código 1.
- Las medidas de los criterios 2 y 3, anotadas aquí.
- Si se optimiza: `dotnet test`, capturas del criterio 6 y una nueva medida.

## Orden de trabajo
1. Script de la sonda. **Hecho.**
2. Medidas (criterios 2 y 3) y decisión (criterio 4 o 5). **Hecho: criterio 4.**
3. Documentación: `lrc-enhanced-fill.md` (M4), README y esta spec. **Hecho.**
