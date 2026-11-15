# Spec: diseño adaptable de la ventana principal

Estado: **aprobada por el autor el 15 de noviembre de 2026**. Sin código todavía.

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

## Hitos

- **M0:** capturas y tabla corregida. Decisión del criterio 3 con el autor.
- **M1:** `LayoutBreakpoints` y pruebas.
- **M2:** barra de reproducción (criterios 2 a 4).
- **M3:** zona central (criterios 5 y 6).
- **M4:** paneles de edición (criterio 7).
- **M5:** capturas de verificación a 640×400, 800, 960 y 1100, en Oscuro, Claro y un tema de usuario
  (procedimiento de CLAUDE.md: copia de `settings.json` y restauración comprobada por hash).
