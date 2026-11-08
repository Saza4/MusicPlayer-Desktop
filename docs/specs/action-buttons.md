# Especificación: botones «action» (barra superior y «+» de COLECCIONES)

Estado: **terminada** (8 de noviembre de 2026), PR #6. Ver «Resultados por hito».

## Contexto

«Agregar carpeta», «Limpiar etiquetas», «Convertir lo que veo…», «Tema» y el «+» de COLECCIONES son hoy `Button` Fluent genéricos (caja con borde, relleno por defecto, `Content="+"`). La barra multimedia ya tiene un lenguaje propio (`.icon`: sin caja, esquinas redondeadas, velo al pasar el cursor, `PathIcon`). Se quiere que estos cinco botones tengan un estilo reconocible y coherente con él. Es un cambio **solo visual**: no cambian comandos, bindings ni comportamiento.

**`docs/diseño.md` no define botones.** Contiene principios generales (Propósito, Agencia, Responsabilidad, Familiaridad, Flexibilidad, Simplicidad, Oficios, Delicia), sin forma, relleno, borde, tipografía ni estados. Por decisión del autor, las reglas concretas de abajo se **derivan de esos principios y del lenguaje `.icon`** de `PlayerButtonStyles.axaml`; cada una es una decisión de diseño de esta spec, no una cita de `diseño.md`. Estilo elegido: **tonal con icono**.

Fuentes: `docs/diseño.md`, `README.md`, `docs/reporte-reproductor.md` (secciones 3, 4.2, 4.3.2 y 9), `Views/MainWindow.axaml`, `App.axaml`, `Themes/*.axaml`.

## Principios aplicados → regla

| Principio de `diseño.md` | Regla (decisión de esta spec) |
|---|---|
| Familiaridad; constancia de imágenes e interacciones | Una sola clase y los mismos estados en los 5 botones. Vocabulario de `.icon`: píldora (`CornerRadius=100`), `Cursor=Hand`, icono que pasa de opacidad 0,7 a 1 en 0,12 s. |
| Simplicidad; jerarquía | Una capa: relleno tonal sin borde visible ni sombra. Misma tipografía que hoy. Icono de 16 px a la izquierda del texto, separación 6. |
| Retroalimentación clara | Reposo, hover, pressed, foco y deshabilitado se distinguen entre sí. |
| Flexibilidad; accesibilidad | `ToolTip.Tip` y `AutomationProperties.Name` en los 5; foco con teclado visible. |
| Oficios | Transiciones cortas; el foco no mueve el diseño. |

## Reglas del estilo `Button.action`

- Tamaño: `MinHeight=32` (como el cuadro de búsqueda), relleno horizontal 14, píldora. Variante `Button.action.only` (solo icono): 28×28, círculo.
- Reposo `ThemeOverlayHoverBrush`; hover `ThemeOverlaySelectedHoverBrush`; pressed `ThemeOverlaySelectedBrush` y `scale(0.97)` (como `.play`); deshabilitado: reposo con opacidad 0,5.
- Se aplica a `/template/ ContentPresenter#PART_ContentPresenter`, como `.icon`. No se tocan los `ButtonBackground*` globales de `FluentOverrides.axaml`.
- Foco: borde de 2 px siempre presente (transparente en reposo) con el relleno compensado, para que no haya salto de diseño. En `:focus-visible` el borde toma `ThemeFocusRingBrush`.
- Contenido: `StackPanel` horizontal con `PathIcon` (Material, 24 px de lienzo, `Data` en línea como el resto) y `TextBlock.label`.

## Decisiones sobre choques con las restricciones

1. **Foco y contraste 3:1.** `ThemeFactory` solo garantiza 3:1 del texto contra `background` y `surface`; `accent` y `primary` solo contra el velo negro del inmersivo. Un anillo de `ThemeAccentBrush` podría quedar por debajo de 3:1 en Claro o con un tema de usuario. El anillo usa el **color de texto de Fluent** (`SystemBaseHighColor`) mediante el pincel nuevo `ThemeFocusRingBrush`, que no cambia los nueve colores ni el JSON de temas.
2. **Relleno tonal sin borde.** El relleno (≈8 % de velo) no llega a 3:1 contra la superficie. El botón se identifica por su texto e icono (3:1 con el color de texto) y sigue siendo una etiqueta, no un control mudo.
3. **Ventana mínima 640×400.** Hoy la barra ya se queda corta a 640 px (se mide en M0). Con iconos crece. Por debajo de un ancho umbral (fijado en M0) la barra superior recibe la clase `compact`, que oculta `TextBlock.label` y deja solo iconos; por eso `ToolTip` y `AutomationProperties.Name` son obligatorios. La clase se conmuta desde `MainWindow.axaml.cs` al cambiar el ancho (solo visual).

## Hitos

- **M0** Medir la barra a 640 px con el estado actual y fijar el umbral de `compact`; captura «antes».
- **M1** `ActionButtonStyles.axaml` y `ThemeFocusRingBrush`, probados en un botón.
- **M2** Aplicar a los cuatro botones de la barra superior.
- **M3** «+» como icono (`only`) en COLECCIONES.
- **M4** Modo `compact` y comprobación a 640×400.
- **M5** Verificación visual (Oscuro, Claro, tema de usuario de ejemplo, teclado) y capturas.
- **M6** Documentación: `docs/reporte-reproductor.md` y, si procede, `README.md`.

## Criterios de aceptación

1. Los 5 botones comparten la clase `action` y se ven coherentes entre sí y con el cuadro de búsqueda.
2. Estados normal, hover, pressed, foco (con teclado) y deshabilitado distinguibles; el foco no mueve el diseño.
3. Oscuro, Claro y un tema de usuario de ejemplo: texto legible y foco con 3:1 como mínimo contra la superficie.
4. Ningún otro botón cambia (Guardar, Cancelar, Renombrar, Eliminar, Convertir, «Carpeta de temas», inmersivo, isla). Los `ButtonBackground*` siguen igual.
5. Ningún literal `#RRGGBB` nuevo en el XAML de la interfaz.
6. `Command`, `DockPanel.Dock`, `IsVisible` y los bindings quedan idénticos.
7. Los 5 tienen `ToolTip.Tip` y `AutomationProperties.Name`; el «+» es solo icono.
8. A 640×400 la barra no corta ni desborda ningún botón.
9. `dotnet build` sin errores (mismo aviso CS8602) y `dotnet test` en verde.

## Fuera de alcance

Modo inmersivo, isla flotante, barra de reproducción, paneles de edición, conversión y autocompletar, menús contextuales, los demás botones de texto, los nueve colores y el JSON de temas, y las capas Core, Audio e Infrastructure.

## Casos límite

- Tema de usuario con `overlay` casi igual al fondo: el relleno apenas se ve; texto e icono siguen legibles.
- Botón deshabilitado: opacidad 0,5 y sin efecto al pasar el cursor.
- Flyout de «Tema» abierto: sin cambios en el flyout ni en `ThemeButton`.
- Escalado 125/150 %: icono de 16 px y alto de 32 sin recortes.
- Linux (CI de Ubuntu): solo XAML, compila igual.

## Resultados por hito

Estado: **terminada** (8 de noviembre de 2026). Implementada y verificada; PR #6 con el CI en verde (Windows y Ubuntu), fusionado en `main`.

- **M0 (medidas).** Antes del cambio, a 640 px «Convertir lo que veo…» ya pisaba el cuadro de búsqueda; a 960 px el estado se veía como «32 canciones en la…». Con iconos la fila crece ≈100 px: a 960 px el texto de estado desaparecía por completo con las etiquetas visibles. **Umbral de `compact`: 1100 px** (en vez de los ≈900 estimados), para que a 960 px (ancho por defecto de la ventana) siga viéndose el estado. Consecuencia: **con el tamaño por defecto la ventana abre en modo solo iconos**; el texto de los botones se ve a partir de 1100 px. Alternativa si se prefiere ver el texto por defecto: subir `Width` de la ventana, acortar el cuadro de búsqueda o aceptar que el estado quede sin sitio.
- **M1 (estilos).** `Themes/ActionButtonStyles.axaml` y `ThemeFocusRingBrush` (en `FluentOverrides.axaml`, que sí resuelve `SystemBaseHighColor` por variante). Hallazgo: Fluent dibuja su propio adorno de foco rectangular y de doble trazo; no seguía la píldora, así que `Button.action` lo anula (`FocusAdorner` nulo) y el foco lo dibuja el borde de 2 px.
- **M2 y M3.** Los cinco botones usan `Classes="action"` (el «+» con `only`); `Command`, `DockPanel.Dock`, `Margin`, `x:Name="ThemeButton"` y el flyout quedan igual. «Agregar carpeta», «Limpiar etiquetas» y «Convertir lo que veo…» ganan `ToolTip.Tip` y `AutomationProperties.Name`; el «+» gana `AutomationProperties.Name`. La barra superior recibe `x:Name="TopBar"`.
- **M4 (compacto).** `UpdateTopBarCompact` en `MainWindow.axaml.cs`, llamado al abrir y en `SizeChanged`. A 640×400 la barra no corta ni desborda ningún botón y el estado se ve parcialmente («32 canciones en la bibli…»).
- **M5 (verificación).** Capturas en Windows con Oscuro, Claro y el tema de usuario «rosa», a 640, 960 y 1200 px; hover (con tooltip) y foco con anillo de 2 px en píldora, en Oscuro y Claro. Nada desborda y los textos son legibles. **No verificado a mano:** el recorrido real con `Tab` (las teclas sintéticas no movían el foco; el foco se probó con un cambio temporal por código, ya revertido), el estado pressed y el deshabilitado (ningún botón de la barra queda deshabilitado en el uso normal), el escalado 125/150 % y Linux.
- **Criterios.** 1-8 cumplidos con esas salvedades; 9: `dotnet build` 0 errores y el mismo aviso CS8602, `dotnet test` 641 + 173 en verde.
