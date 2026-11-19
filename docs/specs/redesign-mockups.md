# Especificación: rediseño según los mockups de Claude Design

Estado: **hecha** (17 de noviembre de 2026) y aplicada sobre `main` el 19 de noviembre (PR #29), **probada a mano
por el autor** ese día. Ver «Resultado».

## Contexto

El autor pidió llevar a la app el proyecto de Claude Design «MusicPlayer Desktop mockups»
(`MusicPlayer Mockups.dc.html`, pantallas 1a-1f), ya corregido antes para conservar lo que le gustaba: la carátula
junto a cada canción, el fondo desenfocado del inmersivo, la «X» para salir y la burbuja con «A» de las capas. Es un
cambio **visual**: comandos, atajos, bindings existentes y comportamiento no cambian. El criterio general es
`docs/diseño.md` (jerarquía, familiaridad, contraste AA, claro y oscuro).

## Qué cambia

1. **Paleta de los temas integrados** (`BuiltInThemes`, Core). Oscuro: fondo `#141416`, superficie `#0C0C0E`,
   primario y acento `#6B9CF4`, texto sobre primario `#0B0F1A`. Claro: fondo `#F4F4F2`, superficie `#FFFFFF`, primario
   y acento `#2F62C8` (más oscuro para llegar a 4,5:1 sobre blanco). El acento deja de ser el del sistema en los temas
   integrados, para que iconos activos, rellenos y Reproducir hablen un solo color. **Los temas de usuario no cambian:**
   sin `accent`, siguen usando el del sistema (README).
2. **Barra superior:** búsqueda a la izquierda (hasta 360 px, con lupa) y acciones a la derecha, sobre el fondo de la
   ventana. La línea de estado sale de aquí.
3. **Botones `action`:** rectángulo de 36 px con esquinas de 8 y borde fino del velo, icono en el primario. El texto
   **sigue en el color de texto**: `ThemeFactory` solo garantiza 3:1 del texto contra el fondo, y un tema de usuario
   podría traer un primario ilegible como texto. El foco pasa el borde de 1 a 2 px y baja 1 px el relleno, para no
   mover nada. Los botones `.icon` pasan a esquinas de 8.
4. **Barra lateral:** superficie de alto completo con línea a la derecha (232 px), «COLECCIONES» en versalitas y filas
   de 34 px.
5. **Lista de canciones:** cabecera grande (`ListHeading`: «Canciones», la playlist, el álbum…) con su resumen
   (`ListSummary`), columnas `# · TÍTULO · ARTISTA · ÁLBUM · DURACIÓN` y filas de 52 px con la carátula de 40 px junto
   al título. La que suena muestra tres barras en «#» y el título en el primario; una pista sin archivo, la etiqueta
   «No disponible» junto al título. Las pestañas Canciones/Álbumes/Artistas se quedan (el mockup no tiene vista de
   álbumes), pero en pequeño.
6. **Línea de estado:** pasa al pie de la zona central y solo aparece cuando dice algo distinto del resumen
   (`HasStatusMessage`): avisos, errores, progreso.
7. **Panel de letra:** 300 px, pegado al borde con una línea a la izquierda, cabecera «Letra» con un subtítulo
   (`PanelSubtitle`: «Sincronizada · traducción») y el botón de capas como icono (la misma burbuja con «A» del
   inmersivo). La línea que suena va en el primario.
8. **Barra de reproducción:** 88 px, carátula de 52, título y artista en dos líneas, tiempos pequeños a los lados de la
   barra y volumen de 92 px. El pulgar de las barras toma el color de texto para distinguirse del relleno.
9. **Inmersivo:** igual que antes (fondo desenfocado, velo, «X», burbuja con «A»); el subtítulo pasa a
   «Artista · Álbum». El velo se mantiene en 60 % → 90 %: el mockup usa una carátula de tono medio, y con una carátula
   clara un velo más suave dejaría la letra blanca por debajo de 4,5:1.
10. **Configuración:** barra con «Volver a la biblioteca» y «Esc cierra», columna centrada de 640 px, título grande y
    un grupo por tarjeta. Cada casilla lleva título y explicación dentro de su etiqueta. El botón «Tema» muestra el
    tema guardado (`ThemeViewModel.CommittedName`) con una flecha. Siguen siendo solo `CheckBox` y `Button`.

## Segunda pasada (17 de noviembre de 2026)

El autor dio por cerrados Configuración, el inmersivo y la isla, y pidió que la biblioteca y la página de artista
fueran iguales a 1a, 1b y 1c:

- **Barra lateral:** «Tu biblioteca» (Canciones con su número, Álbumes, Artistas) y «Playlists» con el «+». Sustituye a
  las pestañas; elegir una fila vuelve a la biblioteca desde una playlist (`LibraryNavIndex`). La entrada «Biblioteca»
  de `Playlists.Items` sigue existiendo, oculta (`IsUserPlaylist`). **Álbumes** se conservó al principio; después el
  autor pidió ocultarlo como en el mockup, comentado y no borrado: su `ListBoxItem` en `MainWindow.axaml` y su entrada
  en `MainViewModel.NavModes` (la tabla que traduce la fila elegida a la vista). La vista sigue en el código; para
  recuperarla basta con descomentar los dos.
- **Barra superior:** «Configuración» solo con icono (`Button.action.square`, 36 px) y el texto de las acciones en el
  primario, solo en esa barra. Con un tema de usuario cuyo primario no se lea sobre el fondo, ese texto perdería
  contraste.
- **Lista:** resumen «1.284 canciones · 3 no disponibles», la fila que suena con su velo (sin sumarlo al de la
  selección) y la línea de estado siempre visible («Biblioteca al día.» o el último aviso). La barra amarilla de no
  disponibles se conserva, porque tiene los botones para revisar y quitar.
- **Letra lateral:** plantilla propia con tamaños fijos (15 / 12,5), líneas no activas al 60 % y fundido al final (máscara
  de opacidad). «Pronunciación en texto grande» ya solo afecta al inmersivo.
- **Barra de reproducción:** letra, isla y pantalla completa en ese orden; «Letra» activa con fondo; pulgar de 12 px
  (recursos de Fluent redefinidos solo en esa barra).
- **Página de artista (1c):** lista «ARTISTAS DEL ÁLBUM» con su número de canciones, migas, nombre con resumen, carátulas
  de 156 px en una fila desplazable (la elegida con velo y título en el primario) y las pistas del álbum con su número
  de pista y separadores «DISCO n». Abre en el artista y el álbum de la canción que suena. Las listas de artistas y
  álbumes solo se rehacen si cambia la biblioteca, la búsqueda o el artista. (La lista de artistas iba aquí junto a la
  barra lateral; la tercera pasada la puso en su lugar.)
- Verificado con capturas en Oscuro y Claro a 1280×800 y a 640 px (Canciones, letra, Artistas al abrir y al cambiar de
  álbum, Álbumes). `settings.json` restaurado por hash al estado que el autor había dejado.

## Tercera pasada (17 de noviembre de 2026)

**La lista de artistas ocupa el sitio de la barra lateral (como en 1c).**
- En Artistas, la columna 0 de `CentralGrid` muestra `ArtistSidebar` («ARTISTAS DEL ÁLBUM», con su número de canciones)
  en lugar de `Sidebar` («Tu biblioteca» + «Playlists»): mismo ancho (232), fondo y margen. No hay columna extra y
  `LayoutBreakpoints` no cambia.
- Un botón ← sobre la lista («Volver a la biblioteca», con `ToolTip` y `AutomationProperties.Name`) vuelve a mostrar
  «Tu biblioteca» **sin salir de la página de artista** (decisión del autor); pulsar «Artistas» trae la lista de
  nuevo. Esc con el foco en la lista hace lo mismo que ←. Si el foco estaba en el panel que se va, pasa a la lista del
  que llega.
- Al salir de Artistas por cualquier camino (otra vista, una playlist), la barra es la de siempre:
  `ShowArtistSidebar => ShowArtistPage && !_libraryNavRequested`.
- El panel que aparece entra con un deslizamiento corto (0,18 s, opacidad y 16 px). `ApplyLayout` decide cuál se ve
  y solo anima al cambiar, no al redimensionar.
- A 640 px la columna se pliega como antes y el ≡ despliega el panel que toque.

**Carátulas nítidas en las tarjetas.**
- Causa: la tarjeta usaba la miniatura de la lista (`GetThumbnail`, 96 px de ancho) a 156 en pantalla; el inmersivo
  usa `LoadLargeAsync` a 640.
- Ahora la tarjeta usa `AlbumItemViewModel.CardCover` → `CoverArtLoader.GetCard`, decodificada a 512 px (cubre el 300 %:
  156 × 3 = 468), con su propia caché de 48 (unos 48 MB como mucho) y la misma carga en segundo plano y el mismo tope
  de 4 lecturas que las miniaturas. Mientras carga se ve el ♪. Al salir de la página se suelta (`ReleaseCards`); editar
  la carátula la invalida como a las miniaturas. La imagen se escala con `BitmapInterpolationMode.HighQuality`.
- Las medidas viven en Core (`CoverSizes`, con prueba) y la tarjeta usa `CoverSizes.CardDisplaySize`. La lista de
  canciones y la de «Álbumes» siguen con la miniatura de 96.
- Avalonia no tiene `DecodePixelWidth` ni `SnapsToDevicePixels`; se decodifica con `Bitmap.DecodeToWidth` y el
  redondeo de diseño ya viene activado.

**Modo de prueba (solo Debug):** `MUSICPLAYER_SETTINGS_READONLY=1` lee los ajustes y nunca los guarda;
`MUSICPLAYER_THEME_OVERRIDE` cambia el tema leído. Sirve para revisar sin tocar el `settings.json` del usuario.

**Resultado:** 848 + 211 pruebas superadas (nuevas: `CoverSizes`, `LruCache.Clear`, `ThumbnailRequests.Clear`).
Capturas en Oscuro y Claro a 1280×800 y a 640 px: la lista en lugar de la barra, ←, volver con «Artistas», salir a
Canciones, abrir en la canción que suena y carátulas nítidas al 100 %. `settings.json` idéntico por hash antes y
después. **Sin probar a 150 % y 200 %:** `AVALONIA_GLOBAL_SCALE_FACTOR` no tiene efecto en Windows y cambiar la escala
del sistema es un ajuste del usuario; 512 px cubre esas escalas por cálculo (234 y 312 px físicos).

## Cuarta pasada: barra de reproducción centrada (17 de noviembre de 2026)

- **Centrado real:** antes la barra repartía `*,2*,*` y, por debajo de 1100 px, la columna derecha pasaba a `Auto`, con
  lo que los controles se desplazaban. Ahora la columna central tiene un ancho fijo entre dos laterales iguales (`*`):
  aleatorio, anterior, reproducir/pausa, siguiente y repetir quedan centrados en la ventana a cualquier ancho, con el
  progreso debajo. El ancho lo calcula Core (`LayoutBreakpoints.PlayerCenterWidth`: lo que dejan dos lados del ancho
  del grupo derecho, entre 230 y 560), con pruebas; `ApplyLayout` solo lo aplica. `WindowLayout.PlayerBarCompact`
  se quitó: ya no lo usaba nadie.
- **Más compactos, como en 1a:** iconos de 17-18 px, Reproducir/Pausa de 42 y 6 px entre botones, solo en esta barra
  (`StackPanel.transport`); el inmersivo y la isla conservan los suyos.
- Verificado con capturas a 1280, 960 y 640 px: Reproducir cae en el centro de la ventana en los tres. 857 + 211
  pruebas superadas. Con `MUSICPLAYER_SETTINGS_READONLY`, silenciar y reproducir no escribieron `settings.json` (su
  última escritura es anterior a la prueba y fue del autor, usando la app).

## Diseño adaptable

`LayoutBreakpoints`: `SidebarWidth` 232, `LyricsPanelWidth` 300 y márgenes centrales 32. La letra reemplaza a la lista
por debajo de 804 px con la barra lateral visible (antes 864); a 640 px, con la barra plegada, caben las dos.

## Fuera de alcance

- La isla flotante (1f) ya coincidía con el mockup.
- «Limpiar no disponibles» del texto de estado del mockup: no existe esa orden; la barra de aviso tiene «Quitar de la
  biblioteca…».

## Resultado

- `dotnet build`: 0 errores, la advertencia CS8602 de siempre. `dotnet test`: 843 + 211, todas superadas (pruebas
  nuevas de la paleta y del contraste del primario de los temas integrados; umbrales de `LayoutBreakpoints` y la
  herencia del acento de un tema de usuario actualizados).
- Capturas en Windows con los temas Oscuro, Claro y uno de usuario, a 1280×800 y 640×720: lista con carátulas y la
  fila que suena, panel de letra (con letra sintética de `MUSICPLAYER_LYRICS_OVERRIDE`), barra de reproducción,
  Configuración e inmersivo. `settings.json` restaurado y comprobado por hash.
- Sin probar: Linux, el foco con teclado real en los botones nuevos y los paneles de edición (convertir, limpiar
  etiquetas, editor de etiquetas) con la paleta nueva.
