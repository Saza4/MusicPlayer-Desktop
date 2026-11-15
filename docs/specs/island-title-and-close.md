# Spec: título desplazable y botones de ocultar y salir en la isla

Estado: **aprobada por el autor el 15 de noviembre de 2026**. Sin código todavía. Depende de `settings-panel.md` (la casilla vive en
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
7. El movimiento usa el temporizador de la onda (15 fps, `IslandMetrics.WaveFrameInterval`): solo corre con la
   isla visible y la pista sonando. Con la isla oculta o en pausa no se pide ningún fotograma.
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

## Hitos

- **M1:** `Marquee` y pruebas; ajuste y casilla.
- **M2:** `MarqueeText` en la isla colapsada; capturas con `MUSICPLAYER_ISLAND_FORCE=1`; medida de CPU.
- **M3:** botón «Ocultar isla».
- **M4:** botón «Salir» (después de `background-playback.md`).
- **M5:** prueba a mano del autor: título corto, largo, en pausa, cambio de pista, ocultar y salir.
