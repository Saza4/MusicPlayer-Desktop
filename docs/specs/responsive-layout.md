# Spec: diseño adaptable de la ventana principal

Estado: **implementada el 15 de noviembre de 2026** (rama `feat/responsive-layout`), a falta de la prueba a mano
del autor de lo que no se pudo capturar (ver «Resultado»). Compila con la CS8602 de siempre y pasan 986 pruebas
(778 en Core, 208 en Infrastructure).

## Context

Feedback de uso (noviembre de 2026): «la aplicación corta algunas opciones cuando la haces pequeña y no está en
pantalla completa».

La ventana mide 960×600 al abrirse, con un mínimo de 640×400. Solo la barra superior se adapta (clase `compact`
por debajo de 1100 px, `UpdateTopBarCompact`). Lo demás tiene medidas fijas. Calculado sobre el XAML. Una captura del autor
(15 de noviembre de 2026, ancho cercano al de arranque) confirma la primera fila: el deslizador de volumen queda
cortado por el borde derecho. Las otras dos siguen sin captura:

| Zona | Medida fija | Qué pasa |
| --- | --- | --- |
| Barra de reproducción (`MainWindow.axaml:501`) | `ColumnDefinitions="*,2*,*"`; el grupo derecho mide unos 268 px (4 botones de 36, huecos, volumen de 100) | Su columna mide `(ancho − 32) / 4`: 232 px a 960 y 152 a 640. El volumen se sale por la derecha. |
| Zona central (`:693`) | `ColumnDefinitions="220,*,Auto"`, panel de letra `Width="380"` (`:914`) | Con la letra abierta, a 640 la lista queda en unos 16 px. |
| Paneles de edición (`:181-492`) | Sin `ScrollViewer` | Con poca altura, los botones Guardar/Cancelar o Aplicar/Cancelar quedan fuera. |

## FEATURE: nada se recorta entre 640×400 y pantalla completa

### ACCEPTANCE CRITERIA

1. **M0 primero:** capturas de la app a 640, 800, 960 y 1100 px de ancho (alto 600) y a 640×400, con una pista
   cargada, con y sin el panel de letra, y con «Editar etiquetas» abierto. La tabla de arriba se corrige con lo
   que muestren antes de cambiar nada.
2. A cualquier tamaño entre 640×400 y pantalla completa, todos los botones de la barra de reproducción se ven
   enteros y se pueden pulsar: aleatorio, anterior, reproducir, siguiente, repetir, letra, pantalla completa,
   isla y silencio.
3. Por debajo de un umbral de ancho, el deslizador de volumen deja la barra y pasa a un flyout del botón de
   silencio (clic derecho o un botón de flecha junto a él; se decide en M0 con la captura). Por encima, se ve
   como hoy. `M` y `Ctrl+↑` `Ctrl+↓` funcionan igual en los dos casos.
4. El título de «ahora suena» de la izquierda se recorta con «…» antes de que se recorte cualquier botón.
5. Por debajo de un umbral de ancho, la barra lateral COLECCIONES se puede plegar con un botón (con `ToolTip.Tip`
   y `AutomationProperties.Name`) y arranca plegada; por encima, se ve como hoy.
6. Con el panel de letra abierto, la lista de canciones conserva al menos 240 px de ancho. Si no caben los dos,
   el panel de letra ocupa el sitio de la lista y un botón vuelve a ella.
7. Los paneles Convertir, Limpiar etiquetas y Editar etiquetas tienen desplazamiento vertical cuando no caben:
   sus botones de acción siempre se alcanzan a 640×400.
8. Los umbrales viven en una función pura de Core (`LayoutBreakpoints.For(width, height)`, que devuelve qué está
   compacto) con pruebas en los bordes; `MainWindow.axaml.cs` solo pone clases. El umbral de 1100 de la barra
   superior pasa a esa función sin cambiar de valor.
9. A 960×600 y más, la ventana se ve igual que antes, salvo lo que el M0 demuestre que ya estaba recortado.
10. Sin literales de color nuevos; los botones nuevos siguen las reglas de CLAUDE.md.
11. `dotnet build` sin advertencias nuevas y `dotnet test` en verde.

### OUT OF SCOPE

- Bajar el tamaño mínimo de 640×400.
- Medir el texto para decidir los umbrales (siguen siendo constantes; limitación ya documentada).
- El modo inmersivo y la isla.
- Rediseñar la lista de canciones o sus columnas.
- Escalas de pantalla o tamaños de fuente distintos de los de la máquina del autor.

### EDGE CASES

- **Título de «ahora suena» muy largo:** se recorta con «…» y nunca se solapa con el tiempo transcurrido ni con
  los controles (en la captura del autor, «Paparazzi — Lady Gaga» ya queda pegado al «0:19»).
- **Volumen en el flyout:** se maneja también con el teclado (el flyout se abre con Enter en el botón y el
  deslizador responde a las flechas) y con la rueda del ratón sobre el botón de silencio.
- **Cambio de ancho con el flyout de volumen abierto:** al pasar el umbral hacia arriba, el flyout se cierra.
- **Barra lateral plegada y creación de playlist desde el menú contextual** («Nueva playlist…»): la barra se
  despliega para mostrar el cuadro del nombre.
- **Panel de letra en lugar de la lista y cambio de pista:** se queda en la letra.
- **Ventana maximizada y luego restaurada a un tamaño pequeño:** las clases se recalculan en `SizeChanged`, como
  hoy la de la barra superior.
- **Fuera de Windows:** el botón de la isla no existe; el grupo derecho mide menos y los umbrales valen igual.

## Resultado

**M0 (capturas de antes, tema Oscuro, escala 100 %):** a 640 el deslizador de volumen no se ve; a 960 se ve
cortado por el borde derecho; a 1100 cabe entero. Coincide con el cálculo y con la captura del autor.

**Decisiones tomadas al implementar (revisables):**

1. **Dos umbrales**, en `LayoutBreakpoints`: por debajo de **1100** la barra superior queda solo con iconos (como
   antes) y la barra de reproducción da a su grupo derecho el ancho que necesita (la tercera columna pasa de `*` a
   `Auto`); por debajo de **800** el volumen pasa al desplegable y la barra lateral se pliega. A 1100 o más, la
   barra de reproducción es la de siempre (`*,2*,*`, controles centrados en la ventana).
2. **Criterio 3, el volumen estrecho:** un botón «Volumen» cuyo clic abre un desplegable con el botón de silencio
   y el deslizador. Se eligió el clic (y no el clic derecho) porque es lo único que se descubre solo. Al abrirse,
   el foco va al deslizador y las flechas cambian el volumen. La rueda sobre el botón de volumen (en los dos
   modos) lo sube o lo baja un paso sin abrir nada. `M` y `Ctrl+↑` `Ctrl+↓` no cambian.
3. **Criterio 5:** el botón de la barra lateral va en la barra superior, a la izquierda, solo con la ventana
   estrecha. Lo que el usuario elija se conserva mientras la ventana siga estrecha; al ensancharla, la barra
   lateral vuelve a verse siempre.
4. **Criterio 6:** el botón que vuelve a la lista es el de «Letra» de la barra de reproducción, que ya cierra el
   panel.
5. **Criterio 7:** el alto máximo de los tres paneles es el alto de la ventana menos 224 (las dos barras y un
   resto de lista), con un mínimo de 120.
6. **Criterio 8:** `LayoutBreakpoints.For` recibe solo el ancho; el alto se usa aparte en `EditPanelMaxHeight`.

**Verificado con capturas en Windows** (Oscuro, escala 100 %, `settings.json` intacto por hash): a 640×400,
640×600, 800×600, 960×600 y 1100×600 se ven enteros todos los botones de la barra de reproducción; a 960 el
deslizador de volumen ya no se corta; a 640 y 800 el volumen es un botón y la barra lateral está plegada; a 1100
todo es igual que antes. Por UI Automation, a 640×480: el botón «Colecciones» despliega la barra lateral y el
botón «Volumen» abre el desplegable con el foco en el deslizador.

**Sin verificar (falta la prueba a mano del autor):**

- El panel de letra ocupando el sitio de la lista (criterio 6) y el desplazamiento de «Convertir», «Limpiar
  etiquetas» y «Editar etiquetas» a 640×400 (criterio 7): hacen falta una pista con letra y el menú contextual.
- Las flechas y la rueda sobre el volumen con un teclado y un ratón reales (las teclas sintéticas no mueven el
  foco).
- Un título de «ahora suena» largo con una pista cargada (criterio 4): en las capturas, «Nada en reproducción» se
  recorta con «…» sin tocar los controles.
- Los temas Claro y de usuario (M5 pedía los tres): no se cambió el tema para no tocar el `settings.json` real;
  el cambio no añade colores.
- Escalas distintas de 100 % y Linux.

**Nota:** los umbrales comparan el ancho interior de la ventana, unos 16 px menor que el exterior en Windows 11.

## Hitos

- **M0:** capturas y tabla corregida. Decisión del criterio 3 con el autor.
- **M1:** `LayoutBreakpoints` y pruebas.
- **M2:** barra de reproducción (criterios 2 a 4).
- **M3:** zona central (criterios 5 y 6).
- **M4:** paneles de edición (criterio 7).
- **M5:** capturas de verificación a 640×400, 800, 960 y 1100, en Oscuro, Claro y un tema de usuario
  (procedimiento de CLAUDE.md: copia de `settings.json` y restauración comprobada por hash).
