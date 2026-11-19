# Spec: título desplazable y botones de ocultar y salir en la isla

Estado: **M1 a M3 implementados el 15 de noviembre de 2026** (rama `feat/island-title-and-close`): título
desplazable y «Ocultar isla». Probado a mano por el autor. M4 («Salir del reproductor») se hizo con `background-playback.md`: el botón aparece junto a «Ocultar isla» solo con la ventana oculta en la bandeja
(ver «Resultado»). Compila con la CS8602 de siempre y pasan 1021 pruebas (811 en Core, 210 en Infrastructure). Depende de `settings-panel.md` (la casilla vive en
Configuración) y, para el botón «Salir», de `background-playback.md`.

## Context

Feedback de uso (noviembre de 2026): «podría haber una opción donde se vea el nombre de la canción desplazándose
en la isla» y «la isla dinámica no tiene un botón para cerrarla directamente, así que se debe cerrar la aplicación
para detener todo y quitarla».

Hoy la isla colapsada (180×36) solo muestra la miniatura y la onda. El título y el artista aparecen al expandir,
recortados con «…». No hay ningún texto desplazable en la app. Para ocultar la isla hay que ir al botón de la
barra de reproducción de la ventana principal. Especificación original: `docs/specs/dynamic-island.md`.

## FEATURE: título en la isla colapsada, «Ocultar isla» y «Salir»

### ACCEPTANCE CRITERIA

**Título desplazable**

1. Ajuste nuevo `IslandShowTitle = false`, **al final** de `AppSettings`, con la casilla «Mostrar el título en la
   isla» en la sección «Isla flotante» de Configuración. Un `settings.json` anterior lo carga desactivado.
2. Con el ajuste activado, la isla colapsada muestra «título — artista» entre la miniatura y la onda. Con el
   ajuste desactivado, la isla colapsada se ve exactamente como hoy.
3. Si el texto cabe, se queda quieto. Si no cabe y la pista suena, se desplaza de derecha a izquierda en bucle:
   pausa al principio, recorrido a velocidad constante, un hueco, y vuelta a empezar. Si no cabe y la pista está
   en pausa, se queda quieto al principio y recortado con «…».
4. Al cambiar de pista, el texto nuevo empieza por el principio.
5. El desplazamiento es una función pura de Core (`Marquee.Offset(elapsed, textWidth, viewportWidth, ...)`) con
   pruebas: texto que cabe (siempre 0), pausa inicial, mitad del recorrido, hueco y vuelta, y anchos de 0.
6. El dibujo es un control propio (`Controls/MarqueeText`) que no crea objetos de texto en cada fotograma: el
   texto se compone una vez por cambio de texto, de fuente o de ancho.
7. El movimiento usa el temporizador de la onda: solo corre con la isla visible y la pista sonando. Con la isla
   oculta o en pausa no se pide ningún fotograma. Mientras el título se desplaza, ese temporizador va a 30 fps
   (`IslandMetrics.MarqueeFrameInterval`) y la onda sigue a 15 (un fotograma de cada dos); sin título que
   desplazar, va a los 15 fps de siempre. *(Cambiado el 15 de noviembre de 2026: el autor lo probó a 15 fps y se
   veía a saltos.)*
8. Coste: medido en Release con `tools/medir-cpu.ps1`, la isla colapsada con el título desplazándose no gasta más
   de 1 punto de un núcleo por encima de la isla con solo la onda. Las cifras se anotan aquí.
9. El ancho de la pastilla colapsada no cambia (`IslandMetrics.CollapsedWidth`), así que la región de clics y la
   colocación no se tocan. Las medidas nuevas (hueco, velocidad, pausa) van en `IslandMetrics`.
10. El texto usa `ThemeIslandTextBrush`; no hay literales de color nuevos.

**Ocultar y salir**

11. La isla expandida tiene un botón «Ocultar isla» (icono de cerrar) con `ToolTip.Tip` y
    `AutomationProperties.Name`. Al pulsarlo, `ShowIsland` pasa a `false` (el mismo ajuste que el botón de la
    barra de reproducción y la casilla de Configuración), la isla se desvanece y la música sigue.
12. La línea de estado de la ventana principal dice dónde reactivarla («Isla oculta. Se activa en Configuración o
    en la barra de reproducción»).
13. Con la ventana principal oculta en la bandeja (`background-playback.md`), la isla expandida muestra además un
    botón «Salir del reproductor». Al pulsarlo, la app termina por el camino normal de salida (ajustes guardados,
    audio liberado). Con la ventana principal visible o minimizada, ese botón no aparece.
14. Los dos botones no activan la isla ni traen la ventana principal al frente (marcan el evento como atendido,
    como los demás controles).
15. `dotnet build` sin advertencias nuevas y `dotnet test` en verde.

### OUT OF SCOPE

- Desplazar también el título de la isla expandida, de la barra de reproducción o del modo inmersivo.
- Cambiar el tamaño de la isla, arrastrarla o moverla a otra pantalla.
- Manejo de la isla con el teclado (sigue como limitación documentada).
- Un botón de «detener» que descargue la pista: pausa ya existe en la isla.
- Texto de derecha a izquierda (árabe, hebreo): se desplaza igual, sin probar.

### EDGE CASES

- **Sin artista:** se muestra solo el título, sin el guion.
- **Título vacío:** el nombre que ya muestra la app para esa pista (el de `Track.Title`).
- **Hover durante el desplazamiento:** la capa colapsada se funde como hoy; al colapsar de nuevo, el texto sigue
  donde iba.
- **Cambio de escala o de pantalla principal:** el ancho disponible se recalcula; el texto vuelve a medirse.
- **«Ocultar isla» con la ventana principal oculta en la bandeja:** se permite; queda el icono de la bandeja para
  abrir o salir.
- **«Ocultar isla» durante el fundido de aparición:** gana el ocultado (`_fadeVersion`).
- **Pista que termina y no hay siguiente:** `IsPlaying` pasa a `false`; el texto se queda quieto.

## Diseño

- `Core/Services/Marquee.cs`: función pura y constantes de prueba.
- `Desktop/Controls/MarqueeText.cs`: `Control` con `Text`, `Offset` y dibujo con recorte, en la línea de
  `FillTextBlock`.
- `IslandWindow.axaml`: el control va en la columna central de `Collapsed` (`ColumnDefinitions="Auto,*,Auto"`,
  hoy vacía), visible con `IslandShowTitle`. Los botones nuevos van en la capa `Expanded`; su sitio exacto se
  decide con capturas en M2 (candidato: «Ocultar» arriba a la derecha, en lugar de la onda expandida, y «Salir»
  a su lado).
- `IslandWindow.axaml.cs`: `OnWaveFrame` actualiza también `Offset`; `UpdateWave` ya decide cuándo corre.
- `MainViewModel`: `HideIslandCommand` (pone `ShowIsland = false` y escribe la línea de estado) y la propiedad que
  dice si la ventana principal está oculta (la publica `background-playback.md`).

## Resultado (M1 a M3)

**Decisiones tomadas al implementar (revisables):**

1. **Medidas** (`IslandMetrics`): letra de 12, margen de 8 a cada lado, 30 DIPs por segundo (1 por fotograma a
   30 fps), pausa inicial de 2 s y hueco de 36. Con la pastilla de 180, al texto le quedan unos 93 DIPs.
2. **Bucle sin salto:** tras el hueco se dibuja una segunda copia del texto; cuando llega al principio, la vuelta
   empieza de nuevo con su pausa. `Marquee.Offset` devuelve como mucho `ancho del texto + hueco`.
3. **El texto lo pone la ventana** (`IslandWindow.UpdateTitle`, con `Marquee.Label`): «título — artista», solo el
   título sin artista. Un texto nuevo vuelve al principio. Al reanudar tras una pausa también empieza por el
   principio (el reloj de la onda se reinicia).
4. **El ajuste vive en `SettingsViewModel`** (`IslandShowTitle`), como el del clic. La casilla se desactiva si la
   isla está apagada.
5. **«Ocultar isla»** va arriba a la derecha de la cabecera expandida, **junto a** la onda (no en su lugar): un
   botón `.icon` de 24 con una ×. El título y el artista expandidos pierden 30 DIPs de ancho.
6. **`MainViewModel.HideIslandCommand`** apaga `ShowIsland` y escribe la línea de estado del criterio 12.

**Verificado:**

- 24 pruebas nuevas en Core (`MarqueeTests`) y 1 en Infrastructure (guardar y leer el ajuste; un `settings.json`
  de la v1.3.0 lo carga desactivado).
- Capturas en Windows (escala 100 %, `settings.json` cambiado con copia previa y restaurado, intacto por hash;
  volumen a 0 durante la prueba): el texto se desplaza con la pista sonando (dos capturas con 1,5 s de diferencia
  muestran tramos distintos), en pausa queda «It Wasn't Me …», con el ajuste apagado la isla se ve como antes, el
  botón «Ocultar isla» aparece al expandir con su globo, un clic en él oculta la isla y la línea de estado dice
  «Isla oculta. Se activa en Configuración o en la barra de reproducción».
- **Coste (criterio 8)**, Release, `tools/medir-cpu.ps1`, 3 pasadas de 10 s, ventana principal minimizada, isla
  colapsada y pista sonando:

  | | Pasadas (% de un núcleo) | Media |
  | --- | --- | --- |
  | Solo la onda (primera medida) | 8,7 / 5,5 / 6,1 | 6,8 |
  | Onda y título a 15 fps (primera medida) | 7,8 / 5,5 / 5,5 | 6,2 |
  | Solo la onda (segunda medida) | 6,4 / 6,2 / 4,8 | 5,8 |
  | Onda a 15 fps y título a 30 fps (segunda medida) | 11,1 / 6,7 / 7,0 | 8,3 |

  A 15 fps el título no añadía coste medible. **A 30 fps (lo que pidió el autor tras probarlo) cuesta entre 1 y
  2,5 puntos de un núcleo más** que la onda sola (2,5 con la primera pasada, que salió alta; alrededor de 1,4 sin
  ella). **El criterio 8 (no más de 1 punto) queda rebasado por poco**, con medidas ruidosas y pocas pasadas: es
  el precio de la fluidez, aceptado al pedir 30 fps. Solo se paga con el ajuste activado, la isla a la vista, la
  pista sonando y un título que no cabe.

**Añadido a petición del autor (15 de noviembre de 2026), fuera de los criterios de arriba: la barra de progreso
de la isla se arrastra.** Mismo aspecto que antes (era solo clic). Usa `BeginScrub` y `EndScrub` de
`MainViewModel`, como la barra principal: durante el arrastre la barra y los tiempos siguen al cursor y el audio
no salta; al soltar hay un único salto. La isla no se colapsa mientras se arrastra, aunque el cursor salga de la
pastilla; al soltar fuera, se colapsa. Comprobado con un arrastre de ratón simulado y capturas (barra siguiendo
al cursor, isla expandida con el cursor fuera y el botón pulsado, colapso al soltar). Arrastrar hasta el final y
soltar termina la pista, igual que en la barra principal.

**Prueba a mano del autor (15 de noviembre de 2026):** título, casilla y «Ocultar isla» correctos; pidió 30 fps
(hecho) y el arrastre de la barra (hecho). **El 19 de noviembre de 2026 el autor probó a mano el resto**, incluido
el arrastre y lo que sigue, que estaba sin verificar:

- El aspecto del desplazamiento en movimiento (velocidad, pausa y hueco son cuestión de gusto: se cambian en
  `IslandMetrics`).
- Cambio de pista con la isla a la vista, y un título corto que cabe.
- La casilla de Configuración encendiendo y apagando el título con la isla visible.
- Hover durante el desplazamiento, y «Ocultar isla» durante el fundido de aparición.
- Escalas de pantalla distintas de 100 % y cambio de pantalla principal.

## Hitos

- **M1:** `Marquee` y pruebas; ajuste y casilla.
- **M2:** `MarqueeText` en la isla colapsada; capturas con `MUSICPLAYER_ISLAND_FORCE=1`; medida de CPU.
- **M3:** botón «Ocultar isla».
- **M4:** botón «Salir» (después de `background-playback.md`).
- **M5:** prueba a mano del autor: título corto, largo, en pausa, cambio de pista, ocultar y salir.
