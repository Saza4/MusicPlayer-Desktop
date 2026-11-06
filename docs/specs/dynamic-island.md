# Especificación: Dynamic Island («ahora suena» flotante)

Estado: **terminada** (5 de noviembre de 2026). M0-M10 hechos en la rama `feature/isla` (ver «Resultado del spike (M0)» y «Resultado de M4» a «de M9»). Puertas de M4, M6 y M8 y la escala de M9 superadas a mano por el autor. Pendiente: publicar (fusionar en `main`).

## Contexto

MusicPlayer-Desktop solo se controla desde `MainWindow`. Con la app minimizada o tapada no hay forma de ver ni controlar lo que suena, salvo la barra espaciadora con la ventana enfocada. Se añade una ventana flotante estilo Live Activity (referencia: Dynamic Island de Apple Music en Mac y `Referencias.png`): una pastilla negra arriba al centro, siempre encima, que se expande al pasar el cursor.

Fuentes: `docs/reporte-reproductor.md`, `README.md`, `docs/diseño.md` (principios: simplicidad, discreción —«no te metas»—, constancia de interacciones, accesibilidad, animaciones fluidas) y la exploración de `Desktop`.

## Ajustes aprobados tras M0

1. Las pruebas manuales pendientes de M0 pasan a **puertas de verificación** con la ventana definitiva (M4, M6, M8). Si alguna falla, se aplica el plan B (`SetForegroundWindow` tras el clic del usuario) y se avisa.
2. Decisión abierta 1 resuelta: la isla se ve **solo cuando `MainWindow` no está activa** (minimizada o detrás de otra ventana), con ~150 ms de retardo y fundido para evitar parpadeo.
3. Seek: reutiliza el mecanismo anti-reentrada de la barra principal (`_isUpdatingFromTimer`).
4. Ventana B híbrida: redimensionar con la pastilla bajo el cursor no debe causar saltos; el hover depende de la pastilla, no de la ventana; se verifican píxeles físicos frente a DIPs.

## Ajustes aprobados para M5

1. La pastilla colapsada no tiene sombra, así que en reposo el margen de sombra es 0: solo la pastilla bloquea clics, y el margen aparece al expandir. (Tras la prueba a mano, la pastilla más 2 DIPs, para que el borde salga suavizado; ver «Ventana fija con región de clics».)
2. Modo solo de depuración que fuerza la isla visible aunque `MainWindow` esté activa, para probar el hover a mano y tomar capturas. No existe en Release.
3. Caso límite: volver a entrar con el cursor mientras la isla se encoge tras colapsar. Debe cancelar el encogimiento sin saltos ni parpadeo.
4. El hover real lo prueba el autor a mano.
5. Los avisos del CI (Ubuntu 26, Node 20 obsoleto) van en un commit aparte, fuera de `feature/isla`.

## Comprensión de los criterios de aceptación

1. `IslandWindow` propia: sin bordes, transparente, `Topmost`, fuera de la barra de tareas, centrada arriba en la pantalla principal con margen de 8 px. Usa el `MainViewModel` singleton como `DataContext`.
2. Solo se muestra con pista cargada (`CurrentTrack != null`) y con `MainWindow` no activa. En pausa sigue visible, colapsada.
3. Colapsada: ~36 px de alto, 160-200 px de ancho, miniatura de ~24 px y onda animada solo mientras suena. Sin sombra.
4. Hover: expansión de 350-450 ms con `SplineEasing` a 130-160 × 320-360 px, esquinas de ~24 px y `BoxShadow`; colapso 200 ms después de salir. Todas las medidas en un solo archivo de constantes.
5. Expandida: carátula, título (blanco, negrita), artista (gris), onda, progreso con tiempo transcurrido y restante con signo menos, y controles aleatorio / anterior / play-pausa (mayor) / siguiente.
6. Clic en carátula o título trae `MainWindow` al frente (la restaura si está minimizada).
7. No roba el foco; las zonas transparentes no bloquean clics. En reposo solo la pastilla (más 2 DIPs) recibe clics (región de la ventana, decidido en M5).
8. Fondo negro fijo con pincel nombrado en `ThemeBrushes.axaml`, texto blanco; play/pausa e iconos activos toman el tema; se reutilizan `PathIcon` y `.icon/.skip/.play`.
9. Ajuste persistente «Mostrar isla» (activado por defecto) con control en la interfaz principal. Cerrar `MainWindow` cierra isla y app.
10. Onda y progreso solo trabajan con isla visible y pista sonando; sin blur.
11. Cambios en `Desktop`; excepciones inevitables en Core (ver «Archivos»). `dotnet build` sin warnings nuevos y 341 pruebas (más las nuevas) en verde.
12. Checklist manual en Windows (al final de este documento).

Fuera de alcance: Spotlight/Siri, expansión automática al cambiar de pista, arrastrar, forma de notch, SMTC/MPRIS, favoritos, resortes pesados. Linux: la isla no se muestra y no rompe el arranque.

## Lo que ya existe (nombres reales, no inventados)

`MainViewModel` ya expone: `CurrentTrack` (`Title`, `Artist`), `NowPlayingCover` (`Bitmap?`), `IsPlaying`, `PositionSeconds` / `DurationSeconds`, `PositionText` / `DurationText`, `IsShuffleOn`, `IsImmersive`, `TogglePlayPauseCommand`, `NextCommand`, `PreviousCommand`.

- **Aleatorio:** no hay comando; se enlaza `IsChecked="{Binding IsShuffleOn}"` a un `ToggleButton`.
- **Seek (criterio 5):** sí está expuesto. Asignar `PositionSeconds` hace `_player.Seek` (`OnPositionSecondsChanged`, `MainViewModel.cs:796`), salvo si `_isUpdatingFromTimer` está activo. Ese es el mecanismo anti-reentrada de la barra principal: `OnTick`, `ShowTrack` y `ClearCurrentTrack` asignan con la bandera puesta, y el usuario (slider, `SeekBy`) asigna sin ella. La barra de la isla **es interactiva** y usa el mismo camino: su progreso se enlaza en un solo sentido (`Mode=OneWay`, nunca escribe en `PositionSeconds` al refrescarse) y el clic llama a un método nuevo `MainViewModel.SeekToFraction(double)`, que asigna `PositionSeconds` una sola vez, fuera del temporizador, igual que `SeekBy`. Así no hay bucle temporizador → barra → `Seek`.
- **Pista limpiada:** no hay evento; se observa `CurrentTrack == null` por `PropertyChanged`.
- **Tiempo restante:** no existe. `TimeFormatter.Format` (Desktop) solo da `m:ss`/`h:mm:ss`.
- **Estilos `.icon`, `.skip`, `.play`, `.pp`:** viven en `MainWindow.Window.Styles`, no en `App.axaml`; otra ventana no los ve.
- **Ajustes:** `AppSettings` (Core) es el DTO JSON. Un parámetro nuevo **al final** con valor por defecto mantiene compatibles los JSON antiguos y las llamadas posicionales.
- **Cierre:** no hay `ShutdownMode` explícito (por defecto `OnLastWindowClose`).
- **DI:** solo en `CompositionRoot.cs`. `MainWindow` no está registrada; se crea en `App.axaml.cs`.

## Decisiones (resueltas)

1. **Visible solo cuando `MainWindow` no está activa** (minimizada o detrás de otra ventana). Con `MainWindow` activa, la isla duplica los controles y tapa el centro de su barra superior. La regla es una función pura de Core, `IslandVisibility.ShouldShow(hasTrack, showIsland, isImmersive, isMainWindowActive, isModalOpen)` (el último parámetro, desde M4; ver decisión 4), con pruebas. Contra el parpadeo, `IslandController` espera ~150 ms (constante en `IslandMetrics`) a que el estado se estabilice antes de mostrar u ocultar, y lo hace con fundido de opacidad. Un cambio que se revierte dentro de ese plazo (por ejemplo, Alt+Tab rápido) no produce nada visible.
2. **Dónde va «Mostrar isla».** Recomiendo un botón `.icon` en el `StackPanel` derecho de la barra de reproducción (junto a Letra y Pantalla completa), con clase `.active` cuando está activo, `ToolTip` y `AutomationProperties.Name`. Es un `Button` con comando, no `ToggleButton`, para no pelear con el atajo global de Espacio (`OnPreviewKeyDown`). Se oculta si la plataforma no es Windows.
3. **Modo inmersivo:** recomiendo **ocultar** la isla mientras `IsImmersive` (ya es pantalla completa con los mismos controles y la isla tapa el contenido).
4. **Selectores y diálogos de la app.** Los selectores nativos (carpeta de música en «Agregar carpeta», imagen en «Carátula del álbum…» y en el editor de etiquetas, letra en «Incrustar letra (.lrc)…») son modales de `MainWindow`: al abrirse la desactivan, y sin más la isla aparecería encima del selector. **La isla no se muestra mientras haya un selector o un diálogo propio de la app abierto.**
   - **Detección:** `Services/ModalTracker.cs` (singleton en `CompositionRoot`), un contador con `IDisposable Begin()`, `bool IsOpen` y evento `Changed`. Los tres servicios de selector (`AvaloniaFolderPickerService`, `AvaloniaImagePickerService`, `AvaloniaLyricsPickerService`) reciben el `ModalTracker` y envuelven su `await` en `using (_modals.Begin())`. El contador admite anidamiento y se libera aunque el selector lance una excepción.
   - **Regla:** `IslandVisibility.ShouldShow` recibe un quinto parámetro `isModalOpen`; con `true`, la isla no se muestra. `IslandController` reevalúa en `ModalTracker.Changed`. Al cerrar el selector, `MainWindow` vuelve a activarse, así que la isla sigue oculta sin parpadear: el retardo de 150 ms absorbe el hueco entre «se cierra el selector» y «se activa `MainWindow`».
   - **Convención (se añade a CLAUDE.md en M10):** todo selector o ventana de diálogo nuevo se abre dentro de `ModalTracker.Begin()`.
   - **Hoy no hay diálogos propios:** la app no crea otras ventanas (el editor de etiquetas, conversión, autocompletar y confirmaciones son paneles dentro de `MainWindow`), y los menús contextuales, el flyout de «Tema» y los tooltips son popups que no desactivan `MainWindow`.
   - **Alternativa descartada:** comprobar por Win32 si la ventana en primer plano pertenece a nuestro proceso. Es más genérica, pero depende del orden de los eventos de activación y solo vale en Windows; la explícita es predecible y comprobable.
   - **No es un modal:** «Carpeta de temas» abre el Explorador, que es otra app. Ahí la isla sí aparece, porque el usuario salió de la app.

## Diseño

### Ventana y arquitectura

- `Views/IslandWindow.axaml(.cs)`: `SystemDecorations="None"`, `TransparencyLevelHint="Transparent"`, `Background="Transparent"`, `Topmost`, `ShowInTaskbar="False"`, `ShowActivated="False"`, `CanResize="False"`. Contiene un `ThemeVariantScope RequestedThemeVariant="Dark"` (precedente del modo inmersivo) para que el texto y los controles Fluent salgan claros.
- `Services/IslandController.cs` (singleton, registrado en el bloque «Interfaz» de `CompositionRoot.cs`): recibe `MainViewModel`, crea `IslandWindow` de forma perezosa, escucha `PropertyChanged` (`CurrentTrack`, `ShowIsland`, `IsImmersive`) y `MainWindow.Activated`/`Deactivated`/`WindowState`, y llama a `Show()`/`Hide()` según `IslandVisibility` (con el retardo y el fundido de la decisión 1). En Linux no hace nada.
- `App.axaml.cs`: tras crear `MainWindow`, arranca el controlador; `desktop.ShutdownMode = OnMainWindowClose`; la isla se cierra en `MainWindow.Closed` y en `desktop.Exit`.
- `Views/IslandMetrics.cs` (o diccionario de recursos): **todas** las constantes (tamaños, radios, duraciones, retardo de colapso, margen superior, sombra).
- Posición: `Screens.Primary.WorkingArea` (respeta una barra de tareas superior), centrada, margen superior `8 * RenderScaling`, calculada en píxeles físicos. Se recalcula al mostrar, en `Screens.Changed` y al cambiar el escalado, junto con la región de clics.
- **Unidades:** `Width`/`Height` de la ventana y las medidas de `IslandMetrics` están en DIPs; `Position` y `WorkingArea` en píxeles físicos. La conversión es siempre `físicos = (int)Math.Round(dips * escala)`, con la escala de la pantalla de destino (`Screen.Scaling`), no la de la ventana antes de moverla. El cálculo vive en un solo método. En M4, M5 y M9 se comprueba con `GetWindowRect` y `WindowFromPoint` a 100 % y a 125/150 %.

### Ventana fija con región de clics (de B a C, decidido en M5)

La B híbrida (cambiar tamaño y posición de la ventana al expandir) **se descartó en M5**. Aunque posición y tamaño cambien en una sola llamada (`SetWindowPos`), Avalonia pinta el contenido nuevo un fotograma después. En ese fotograma la pastilla salta 96 px a la izquierda al crecer, o se ve cortada al encoger (medido, ver «Resultado de M5»). Se aplica la alternativa prevista en M0, la **C**:

- La ventana mide siempre lo mismo (pastilla expandida + margen de sombra, `IslandMetrics.WindowSize`) y no se mueve al expandir ni al colapsar. La pastilla va arriba al centro.
- Lo que cambia es la **región** de la ventana (`SetWindowRgn`, en `Services/WindowNative.cs`): fuera de ella los clics pasan a lo que hay debajo. En reposo es la pastilla colapsada con sus esquinas redondeadas, agrandada `IslandMetrics.CollapsedHitMargin` (2 DIPs) por los lados y por debajo. Expandida, la ventana entera.
- **Por qué 2 DIPs y no 0:** la región también recorta lo que se pinta, y sin suavizar. Con margen 0 el borde de la pastilla colapsada salía a escalones (lo vio el autor con la Lupa y lo confirmó la sonda: de blanco 255 a negro 2 sin grises). Con 2 DIPs el corte cae en la zona transparente y el borde suavizado de Avalonia queda entero (255 → 215 → 145 → 88 → 45 → 16 → 2). El coste es un anillo invisible de 2 DIPs que no deja pasar clics.
- **Alternativa descartada: `WM_NCHITTEST` con `HTTRANSPARENT`.** Windows solo pasa el clic a ventanas del mismo hilo; a las de otras apps no les llega, así que no da clics a través.
- Al expandir, primero crece la región (instantáneo y no se ve) y luego anima la pastilla. Al colapsar, anima la pastilla y, al terminar, se encoge la región.
- El **hover depende de la pastilla** (`PointerEntered`/`PointerExited` del `Border`), no de la ventana. El margen de sombra no tiene fondo (`Background=null`), así que tampoco cuenta como hover.
- Un `PointerExited` espurio no colapsa nada: el colapso espera 200 ms y entonces mira el cursor real (`GetCursorPos`). Si sigue sobre la pastilla, no colapsa.
- **Volver a entrar mientras se colapsa** (caso límite de M5): la pastilla vuelve a crecer desde su tamaño de ese momento y se cancela el encogimiento de la región. Si el cursor vuelve sin que llegue `PointerEntered`, al terminar la animación se mira el cursor real y se vuelve a expandir en lugar de encoger.
- Los pasos son una máquina de estados pura en Core (`IslandHover`: colapsada, expandida, espera, colapsando), con pruebas de cada transición y de los dos casos límite. La ventana solo ejecuta las acciones que devuelve.

### Colapso/expansión

- Un `Border` raíz anclado arriba al centro. `Transitions` de `Width`, `Height` y `CornerRadius` con `SplineEasing`: 400 ms al expandir y 300 ms al colapsar. El contenido colapsado y el expandido van en dos capas de tamaño fijo con fundido de opacidad; la expandida aparece con 150 ms de retraso, cuando ya hay sitio. Un `Border` interior recorta el contenido a las esquinas sin cortar la sombra.
- `PointerEntered` cancela el temporizador de colapso y expande; `PointerExited` arranca un `DispatcherTimer` de 200 ms que colapsa. Reentrar cancela, así que no hay saltos.
- Sombra con `BoxShadow` (sin `blur()` de efecto, que es costoso), **solo expandida**. Va en una capa aparte (`PillShadow`), detrás de la pastilla, con su tamaño enlazado al de la pastilla, y aparece o desaparece con una transición de **opacidad** al ritmo de la pastilla. Una transición de `BoxShadow` no interpola en Avalonia 11.2: la sombra saltaba de golpe al final (a ~520 ms medido; el autor la vio aparecer al cabo de 1-1,5 s).

### Modo de depuración

Solo en compilación Debug (`#if DEBUG`: el código no existe en Release). Con la variable de entorno `MUSICPLAYER_ISLAND_FORCE=1`, la isla se muestra aunque `MainWindow` esté activa. El resto de la regla (pista cargada, «Mostrar isla», inmersivo, selectores) se respeta. Como la isla no se activa al pulsarla, `MainWindow` sigue activa mientras se prueba el hover.

```powershell
$env:MUSICPLAYER_ISLAND_FORCE = '1'; dotnet run --project src/MusicPlayer.Desktop
```

### Contenido

- Colapsada: miniatura de 24 px (`NowPlayingCover`; sin carátula, marcador neutro con icono de nota) y onda a la derecha.
- Expandida: carátula, `Title` (blanco, negrita) y `Artist` (gris) con `TextTrimming="CharacterEllipsis"` y ancho fijo, onda, barra de progreso con `PositionText` a la izquierda y `RemainingText` (`−1:31`) a la derecha, y la fila de controles. Los botones llevan `ToolTip` y `AutomationProperties.Name`.
- Onda: cinco barras (`Border`) con su propio `ScaleTransform`. **Decidido en M7 con medidas:** un `DispatcherTimer` propio a 15 fps (`IslandMetrics.WaveFrameInterval`) calcula la altura de cada barra con una senoide de periodo distinto (`WaveBarPeriods`, entre `WaveMinScale` y la altura completa). Solo corre con la isla visible y la pista sonando; al pausar, ocultar o cerrar se para y las barras vuelven a su altura fija (onda estática). La animación de Avalonia, que era el plan inicial, se descartó: pide un fotograma en cada refresco de pantalla (ver «Resultado de M7»).
- Clic en carátula/título: `MainWindow.WindowState = Normal` si estaba minimizada, `Show()` y `Activate()`. Los controles marcan el evento como manejado para no disparar esto.
- Pincel de play/pausa y de iconos activos: `ThemePrimaryBrush` / `ThemeAccentBrush` (ya garantizan 3:1 sobre negro por `ThemeFactory`).

### Colores (`Themes/ThemeBrushes.axaml`)

Pinceles fijos nuevos, sin colores inline en el XAML de la isla: `ThemeIslandBackgroundBrush` (negro), `ThemeIslandTextBrush` (blanco), `ThemeIslandSecondaryTextBrush` (gris), `ThemeIslandTrackBrush` (blanco translúcido para la barra) y `ThemeIslandPlaceholderBrush` (blanco translúcido para la carátula ausente).

### Estilos compartidos

Mover `.icon`, `.skip`, `.play`, `.pp` de `MainWindow.Window.Styles` a `Themes/PlayerButtonStyles.axaml`, incluido desde `App.axaml`. Es un cambio en `MainWindow`, así que va en su propio hito con captura antes/después para comprobar que la barra principal queda idéntica.

### Ajuste «Mostrar isla»

`AppSettings` gana `bool ShowIsland = true` **al final**. En `MainViewModel`: `[ObservableProperty] ShowIsland`, restauración dentro del bloque `_restoring` de `InitializeAsync` y guardado con `partial void OnShowIslandChanged` → `_settings.Update(s => s with { ShowIsland = value })`, más un `ToggleIslandCommand` y `IsIslandSupported` (`OperatingSystem.IsWindows()`).

### Tiempo restante

`RemainingText` en `MainViewModel`, calculado donde ya se actualiza `PositionText`. El formateo (`-m:ss`, `-h:mm:ss`, redondeo y límites) va en **Core** (`TimeText.Remaining`) con pruebas, porque `TimeFormatter` está en Desktop y no cubre el signo.

### Foco y clics a través (resultado del spike, con alternativas)

- Base: `ShowActivated=false`. Si no basta, `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` por `GetWindowLongPtr`/`SetWindowLongPtr` (DllImport, sin `unsafe`) tras obtener el HWND con `TryGetPlatformHandle()`, todo tras `OperatingSystem.IsWindows()` para que Ubuntu compile.
- Transparencia: se prueban en el spike (A) ventana fija del tamaño expandido + margen de sombra, con fondo `null` fuera de la pastilla; (B) ventana redimensionada al tamaño de la pastilla en cada estado, recentrada; (C) región de recorte por interop. Se elige la más simple que no bloquee clics. **Resultado: A descartada en M0; B híbrida descartada en M5 por saltos de un fotograma; se usa C (ver «Ventana fija con región de clics»).**
- Riesgo conocido: con `NOACTIVATE`, `Activate()` sobre `MainWindow` puede ser rechazado por Windows (bloqueo de primer plano). El spike lo verifica; plan B: `SetForegroundWindow` tras el clic del usuario.

## Resultado del spike (M0)

Probado en Windows 11 con una ventana mínima (360×180, `SystemDecorations=None`, `Background=Transparent`, `TransparencyLevelHint=Transparent`, `Topmost`, `ShowInTaskbar=False`, `ShowActivated=False`) y una pastilla negra de 180×36 arriba al centro. Medido con `GetWindowRect`, `GetWindowLongPtr`, `WindowFromPoint` y `GetForegroundWindow`.

| Comprobación | Resultado |
|---|---|
| Transparencia | Funciona (estilo `WS_EX_NOREDIRECTIONBITMAP`, composición de DWM). |
| `Topmost` | Activo (`WS_EX_TOPMOST`). |
| Posición | Arriba al centro del `WorkingArea`: `L=780`, `T=8` a escala 100 % (1920 px de ancho). |
| `ShowActivated=false` | El primer plano sigue siendo «Music Player» tras abrir la isla. |
| `WS_EX_NOACTIVATE \| WS_EX_TOOLWINDOW` por `GetWindowLongPtr`/`SetWindowLongPtr` | Se aplican sin error (`ExStyle=0x8200088`) y el primer plano no cambia. |
| **Clics a través de zonas transparentes (opción A, ventana fija grande)** | **FALLA.** `WindowFromPoint` en una zona transparente devuelve la isla: la ventana captura clics en todo su rectángulo. |
| Clic real en un botón de la isla con otra app enfocada | Pasa a puerta de verificación en **M6**. |
| `Activate()` sobre `MainWindow` desde la isla | Pasa a puerta de verificación en **M8**. |
| Aparecer sin robar el foco | Pasa a puerta de verificación en **M4**. |
| DPI y varios monitores | Pendiente (M9). |

**Decisión:** se descarta la opción A. Se adopta la **B híbrida**: la ventana mide siempre lo justo.

- Colapsada: ventana = pastilla + margen mínimo de sombra.
- Al expandir: primero se agranda la ventana (instantáneo, recentrada) y luego anima la pastilla por dentro.
- Al colapsar: anima la pastilla y, al terminar, se encoge la ventana.
- Solo se bloquean clics bajo el rectángulo de la ventana en ese momento. En reposo es casi la pastilla.

`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` se aplica siempre (con `OperatingSystem.IsWindows()`), por seguridad. La opción C (región de recorte) queda como alternativa si B muestra parpadeo en M5. **Lo mostró: desde M5 se usa C.**

## Resultado de M4

Sonda automática en Windows 11 al 100 % (pantalla 1920×1032 de área de trabajo). La app reprodujo una pista con volumen 0 y los datos del usuario se restauraron al terminar. Una ventana propia de la sonda («Sonda») hizo de «otra app», sin tocar las del usuario ni mover el ratón.

| Control | Resultado |
|---|---|
| `MainWindow` activa con pista | Sin isla |
| Otra app activa | La isla aparece; **puerta M4: el foco sigue en «Sonda»** |
| Tamaño | 212×52 px = (180 + 2·16) × (36 + 16) DIPs × 1,0. **Cambia en M5:** la ventana mide 372×160 px = (340 + 2·16) × (144 + 16) DIPs y no varía; la zona de clics en reposo es la pastilla más 2 DIPs, 184×38 px (ver «Resultado de M5») |
| Posición | `L=854`, `T=8`: centrada y a 8 px del área de trabajo |
| Estilos | `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW` y `WS_EX_TOPMOST` |
| Volver a `MainWindow` / minimizar / restaurar | Oculta / visible / oculta |
| Selector de carpetas abierto y cancelado | La isla no aparece en ningún momento (muestreo cada 100 ms) |
| Cerrar `MainWindow` | El proceso termina |

Pendiente: 125/150 % y varios monitores (M9). En M4 el margen de sombra (16 DIPs a los lados y debajo) capturaba clics en reposo. Desde M5 ya no: en reposo la región de clics es la pastilla.

Ubicación final: `ModalTracker` e `IslandPlacement` (DIPs → píxeles físicos y centrado) están en **Core** con pruebas, porque no dependen de la interfaz.

## Resultado de M5

Sonda automática en Windows 11 al 100 %, con el modo de depuración, volumen 0 y los ajustes del usuario restaurados al terminar. Una ventana blanca propia («Sonda») hizo de fondo y de «otra app». El hover se simuló con `WM_MOUSEMOVE` enviado a la isla, sin mover el ratón. Como el cursor real estaba fuera, Windows envía enseguida `WM_MOUSELEAVE`, así que cada ciclo es entrar, esperar 200 ms y colapsar. Se muestreó la fila central de la pastilla cada ~4 ms (unas 320 muestras por ciclo) para medir sus bordes en pantalla.

| Control | Resultado |
|---|---|
| B híbrida (ventana redimensionada con un solo `SetWindowPos`) | **Falla.** Al crecer, 2-3 muestras con la pastilla colapsada pegada al borde izquierdo de la ventana nueva (774-954 en lugar de 870-1050). Al encoger, 2-3 muestras con la pastilla cortada (870-954 o 966-1050). Además, en reposo Windows impone 39 px de alto mínimo (180×39 en lugar de 180×36) |
| C (ventana fija + región) | **0 saltos** en 8 ciclos (~2 500 muestras): el centro de la pastilla siempre entre x = 959,5 y 960, y la ventana siempre en `L=774 T=8 372×160` |
| Clics en reposo (`WindowFromPoint`) | Centro de la pastilla y justo dentro de su borde: isla. Junto a la pastilla, debajo, en la esquina redondeada y en el margen de sombra: lo de debajo |
| Clics expandida | Ventana entera (incluido el margen de sombra): isla |
| Clics tras colapsar | Vuelven a pasar fuera de la pastilla |
| Reentrar durante el colapso (a 330, 380, 450 y 520 ms) | La pastilla vuelve a crecer desde su ancho de ese momento, sin saltos |
| Foco | El primer plano sigue en «Sonda» toda la prueba |
| Modo de depuración | La cadena `MUSICPLAYER_ISLAND_FORCE` está en el binario Debug y no en el Release |

**Prueba a mano del autor:** la expansión se ve muy bien y no hay que ajustar ninguna constante. Dos fallos, corregidos:

| Fallo | Medida antes | Corrección | Medida después |
|---|---|---|---|
| Bordes de la pastilla colapsada a escalones (Lupa) | Fila superior del borde: 255 → 2, sin grises | Región agrandada 2 DIPs (`CollapsedHitMargin`) | 255 → 215 → 145 → 88 → 45 → 16 → 2, igual que sin región |
| La sombra tarda en aparecer | Aparece de golpe a ~520 ms y sin animarse | Capa `PillShadow` con fundido de opacidad | Crece entre ~130 y ~250 ms y queda completa con la pastilla |

Tras la corrección se repitió la sonda de saltos: 0 saltos en 4 ciclos (~1 300 muestras), región de clics correcta en reposo, expandida y tras colapsar, y foco en «Sonda».

## Resultado de M6

Contenido expandido (340×144 DIPs, relleno 16): cabecera con carátula de 48 px, título en negrita y artista en gris (`CharacterEllipsis`, columna de ancho fijo) y onda; fila de progreso con transcurrido, barra y restante con signo menos (tiempos de ancho fijo para que la barra no cambie); y controles aleatorio, anterior, reproducir/pausa (36 px, mayor) y siguiente (32 px), con los estilos compartidos `.icon`, `.skip`, `.play` y `.pp`. Todas las medidas en `IslandMetrics`. Los controles solo reciben clics con la pastilla expandida.

- **Seek:** `MainViewModel.SeekToFraction` y la regla pura `PlaybackShortcuts.TargetAtFraction` (Core, 8 pruebas: fracciones, clic fuera de la barra, sin duración, barra sin medir). La barra (`ProgressBar`) solo lee `PositionSeconds` (`OneWay`) y un clic asigna `PositionSeconds` una sola vez, fuera del temporizador: un clic, un `Seek`.
- **Accesibilidad:** los cuatro botones tienen `ToolTip` y `AutomationProperties.Name` (el de play/pausa cambia entre «Reproducir» y «Pausa»), y la barra se expone como `ProgressBar` «Progreso» con su valor.

Sonda automática (modo de depuración, clics enviados a la isla con `WM_LBUTTONDOWN`/`UP`, botones localizados por UIA, ajustes restaurados al terminar):

| Control | Resultado |
|---|---|
| Reproducir/pausa | «Pausa» → «Reproducir» → «Pausa» |
| Aleatorio | Encendido → apagado (y vuelta) |
| Siguiente / anterior | Cambian de pista; título, artista y tiempos se actualizan |
| Clic en la barra al 50 / 25 / 90 % (pista de 4:55) | 2:27 / 1:14 / 4:26, con el restante a juego |
| Ancho del título | 213 px con títulos cortos y largos |
| UIA | `ProgressBar` «Progreso», valor 0,2 de 295,2 |
| Foco | El primer plano sigue en «Sonda» |

**Puerta de M6 (a mano, el autor): pasa.** Los clics de la sonda no pasan por la activación de Windows, así que el foco se probó con el ratón real: con el Bloc de notas enfocado, pulsar los botones y la barra de la isla deja el foco en el Bloc de notas y lo escrito sigue entrando ahí.

## Resultado de M7

Sonda de CPU (compilación Debug, Windows 11, volumen 0, ajustes restaurados): CPU del proceso en 10 s, en % de un núcleo, tras 3 s de estabilización. Para saber si la onda se mueve se comparan dos capturas de la onda colapsada separadas 120 ms (píxeles distintos).

| Versión | Isla oculta, sonando | Isla visible, sonando | Visible, en pausa | La onda se mueve |
|---|---|---|---|---|
| M6 (onda estática, base) | 5,5 % | 8,0 % | 4,2 % | No (0 px) |
| Animación de Avalonia (`ScaleY`, descartada) | 6,4 % | **27,0 %** | 4,1 % | Sí (74 px) / pausa 0 px |
| Temporizador propio a 30 fps | — | 10,6 % | — | Sí (78 px) |
| **Temporizador propio a 15 fps (elegido), dos pasadas** | 7,2 / 5,8 % | 6,4 / 7,2 % | 2,5 / 3,9 % | Sí (68-82 px) / pausa 0 px |

- Con 15 fps la isla visible no consume más que en M6: la diferencia queda dentro de la variación entre medidas (±1,5 puntos). Con la isla oculta el temporizador está parado.
- La animación de Avalonia pide un fotograma en cada refresco de pantalla (60 Hz o más) y triplicaba la CPU.
- Desde código, una animación de `RenderTransform` con `TransformOperations` lanza `No animator registered for the property RenderTransform` dentro de `Show()` y la isla no aparece. Se detectó en M7 y quedó resuelto al no usar animaciones de Avalonia para la onda.
- Si la onda se ve entrecortada, `WaveFrameInterval` es el ajuste: 30 fps cuesta unos 2,5 puntos más de CPU con la isla visible.

## Resultado de M8

- **Clic en la carátula o el título** (pastilla expandida): la isla emite `OpenMainRequested` y `IslandController` trae `MainWindow`. Si está minimizada la restaura con `ShowWindow(SW_RESTORE)`, que la devuelve a su estado anterior; `WindowState = Normal` la desmaximizaría. Después llama a `Show()` y `Activate()` y, si la ventana no queda en primer plano, aplica el plan B (`SetForegroundWindow`), que Windows permite porque el proceso acaba de recibir el clic del usuario. Los controles no disparan esto: el clic solo se atiende en la carátula y en los textos.
- **Botón en la barra de reproducción**, entre «Pantalla completa» y el volumen: `Button` `.icon` con `ToggleIslandCommand`, acento (`.active`) con la isla activada, oculto fuera de Windows (`IsIslandSupported`). Su `ToolTip` y su nombre accesible dicen lo que hará el clic: «Ocultar isla flotante» o «Mostrar isla flotante» (`IslandToggleText`), como el botón de silencio.

Sonda automática (sin modo de depuración, ajustes restaurados):

| Control | Resultado |
|---|---|
| Botón de la barra | Se encuentra por UIA como «Ocultar isla flotante»; al pulsarlo pasa a «Mostrar isla flotante» y `settings.json` guarda `"ShowIsland": false` |
| Isla apagada | Al minimizar no aparece |
| Volver a encenderla | `"ShowIsland": true` |
| Clic en el título con `MainWindow` maximizada y minimizada | Se restaura **maximizada**, queda en primer plano y la isla se oculta |

**Puerta de M8 (a mano, el autor): pasa.** La sonda no podía probarla: sus clics no dan derecho al primer plano y Windows ya contaba como primer plano la ventana minimizada. Con el ratón real y otra app delante, el clic en la carátula o el título trae el reproductor al frente con el foco del teclado (la barra espaciadora pausa), también desde minimizado y maximizado.

## Resultado de M9

Sonda automática en Windows 11 con dos monitores: principal de 1920×1080 a 100 % y secundario a la izquierda (x = −1920) a 125 %. Se usó el modo de depuración y volumen 0. `settings.json` y `library.json` se copiaron antes y se restauraron idénticos (mismo hash). Para la carátula ausente se generó con `ffmpeg` una copia temporal de 30 s sin etiquetas ni carátula, que se borró al terminar; ninguna pista de la biblioteca real carece de carátula.

| Caso | Resultado |
|---|---|
| Carátula ausente | Marcador con la nota en la pastilla colapsada (24 px) y en la expandida (48 px); título y artista normales |
| Modo inmersivo | Al entrar, la isla se oculta; al salir («Cerrar»), vuelve |
| Ventana principal en el monitor secundario (125 %) | La isla sigue en el principal, `L=774 T=8 372×160`, colapsada y expandida |
| Temas en vivo (vista previa del selector: Papel, Negro puro, Menta clara, Medianoche, Bosque, Atardecer, Claro, Oscuro y Sistema) | El fondo de la pastilla es `#000000` con los nueve, y el texto sigue blanco también con los temas claros. El círculo de play/pausa toma el `primary` de cada tema (`#3B5BDB` Papel, `#1B7F4B` Menta clara, `#7AA2F7` Medianoche, `#66BB6A` Bosque, `#FF8A65` Atardecer, `#1A73E8` Claro, `#FFFFFF` Oscuro); la onda y el aleatorio activo, el acento |
| Ocultar y volver a mostrar varias veces | Misma posición y tamaño |

Cambio de código en M9: las suscripciones a `Screens.Changed` y `ScalingChanged` pasan de `OnOpened` al constructor de `IslandWindow`. La isla se muestra y se oculta muchas veces, y suscribirse al abrirse podía acumular manejadores.

Encontrado en la sonda (no es un fallo de la app): un `library.json` con `null` en campos numéricos (`TrackNumber`, `DiscNumber`) se rechaza al cargar y queda como `.corrupt`. La persistencia segura funcionó como se espera. El `.corrupt` lo creó la sonda y se borró.

**A mano (el autor): pasa.** No se podía automatizar sin cambiar la configuración de pantalla del sistema:
- Pantalla principal a 125 % o 150 %: la isla, arriba al centro y a 8 px del borde, con la pastilla nítida y los bordes suavizados. Las cuentas de DIPs a píxeles tienen pruebas en Core (`IslandPlacementTests`).
- Cambiar la resolución o el escalado con la isla visible: se recoloca sola (`Screens.Changed` / `ScalingChanged`).
- Cambiar cuál es la pantalla principal: la isla pasa a la nueva.

## Archivos

Nuevos (Desktop): `Views/IslandWindow.axaml(.cs)`, `Views/IslandMetrics.cs`, `Services/IslandController.cs`, `Services/NonActivatingWindow.cs`, `Services/WindowNative.cs` (región de clics y cursor real), `Themes/PlayerButtonStyles.axaml`.
Modificados (Desktop): `App.axaml(.cs)`, `CompositionRoot.cs`, `MainViewModel.cs`, `MainWindow.axaml` (estilos movidos y botón «Mostrar isla»), `Themes/ThemeBrushes.axaml`, los tres servicios de selector.
Excepciones a «solo Desktop», necesarias por la convención del repositorio: Core (`AppSettings.ShowIsland`, `IslandVisibility`, `TimeText.Remaining`, `ModalTracker`, `IslandPlacement`, `IslandHover`, `PlaybackShortcuts.TargetAtFraction`), y sus pruebas en `tests/MusicPlayer.Core.Tests` y `tests/MusicPlayer.Infrastructure.Tests` (`JsonSettingsRepositoryTests`). Al terminar se actualizan `README.md` y `docs/reporte-reproductor.md`.

## Hitos (cada uno compila, pasa pruebas y se puede comprobar solo)

| # | Hito | Verificación |
|---|------|--------------|
| M0 | **Spike** (hecho): ventana mínima; se elige la B híbrida (en M5 se pasó a la C) | Tabla de resultados en la spec |
| M1 | Ajuste `ShowIsland` (Core + VM) e `IslandVisibility.ShouldShow(hasTrack, showIsland, isImmersive, isMainWindowActive)` | Pruebas: JSON parcial → `true`, ida y vuelta con `false`, tabla de reglas de visibilidad |
| M2 | `TimeText.Remaining` y `RemainingText` | Pruebas (0, <1 s, minutos, horas, duración desconocida) |
| M3 | Pinceles `ThemeIsland*` y estilos movidos a `PlayerButtonStyles.axaml` (**hecho**) | 15 capturas idénticas píxel a píxel antes y después (Oscuro, Claro, Medianoche × reposo, hover en `.icon`, reproduciendo, aleatorio + repetir, inmersivo). Arnés con `RenderTargetBitmap`, hover simulado con `:pointerover` y pruebas de impacto desactivadas para que el cursor real no interfiera; dos pasadas sin cambios dieron 0 px de ruido |
| M4 | `IslandWindow` colapsada, posición, `ModalTracker` (con `isModalOpen` en `IslandVisibility`) e `IslandController` (mostrar/ocultar con pista y `MainWindow` no activa, retardo de 150 ms y fundido, cierre con la app, Linux sin efecto). Push de `feature/isla` para comprobar el CI en Ubuntu | Manual: aparece al minimizar o pasar a otra app, desaparece al volver a `MainWindow` o limpiar la pista, **no aparece con un selector abierto**, la app cierra. CI en verde en Windows y Ubuntu. `GetWindowRect` coincide con DIPs × escala. **Puerta:** al aparecer la isla, el Bloc de notas sigue recibiendo lo que se escribe |
| M5 | Expansión con hover (**hecho**: ventana fija con región de clics, ver «Resultado de M5»), animación, constantes en un solo archivo y modo de depuración | Sonda: 0 saltos y región de clics = pastilla (+2 DIPs) en reposo. Pruebas de `IslandHover`. **A mano (autor, hecho):** expansión sin saltos; bordes con la Lupa y sombra corregidos tras la prueba |
| M6 | Contenido expandido: textos, progreso con seek (`SeekToFraction`, sin bucle), controles, accesibilidad (**hecho**, ver «Resultado de M6»; puerta de foco superada a mano) | Botones ejecutan los comandos reales; ellipsis sin cambiar el ancho; un clic en la barra produce un solo `Seek`. **Puerta:** pulsar un botón de la isla con el Bloc de notas enfocado conserva su foco |
| M7 | Onda animada solo al sonar; medición de CPU (**hecho**, ver «Resultado de M7») | Pausa = estática; ventana oculta sin consumo notable. Sonda: la onda se mueve solo al sonar; CPU visible y sonando igual que en M6. A mano (autor): la onda a 15 fps se ve bien |
| M8 | Clic en carátula/título trae `MainWindow`; botón «Mostrar isla» en la barra (**hecho**, ver «Resultado de M8»; puerta superada a mano) | Restaura desde minimizada; el ajuste persiste. **Puerta:** `Activate()` lleva `MainWindow` al primer plano; si Windows lo impide, plan B (`SetForegroundWindow`) y aviso |
| M9 | Casos límite: monitores/DPI, temas en vivo, inmersivo, carátula ausente (**hecho**, ver «Resultado de M9»; escala de la pantalla principal superada a mano) | Checklist completo |
| M10 | Docs (`README`, reporte, spec final y convenciones en `CLAUDE.md`: `ModalTracker.Begin()` y Win32 tras `OperatingSystem.IsWindows()`) (**hecho**) | `dotnet build -c Release` solo con la CS8602 conocida; 403 pruebas en verde (345 Core + 58 Infrastructure) |

## Verificación

- Automática: `dotnet build -c Release` (solo la CS8602 conocida) y `dotnet test -c Release` (341 + nuevas; 395 tras M5, 403 tras M6 y al terminar).
- Manual en Windows (checklist, criterio 12):
  1. Sin pista, no hay isla. Con pista y `MainWindow` activa, tampoco. Al minimizar o pasar a otra app, aparece arriba al centro con margen de 8 px, con fundido; al volver a `MainWindow`, desaparece. Un Alt+Tab rápido de ida y vuelta no la hace parpadear.
  2. «Agregar carpeta», «Carátula del álbum…», imagen del editor de etiquetas e «Incrustar letra (.lrc)…»: con el selector abierto la isla no aparece, tampoco al aceptar o cancelar. «Carpeta de temas» (Explorador) sí la muestra.
  2b. Pausa: sigue visible, colapsada y con la onda estática.
  3. Pasar el cursor expande en ~0,4 s; salir colapsa a los ~200 ms; entrar y salir rápido no produce saltos.
  3b. Salir y volver a entrar mientras la pastilla se encoge: vuelve a crecer desde donde estaba, sin saltos ni parpadeo, y no se queda pequeña con el cursor encima.
  3c. En reposo, un clic justo al lado de la pastilla (o en su esquina redondeada) llega a la ventana de debajo.
  4. Escribir en otra app y pulsar los botones de la isla: el foco no cambia.
  5. Clic sobre lo que hay bajo las zonas transparentes funciona.
  6. Aleatorio, anterior, play/pausa y siguiente actúan; el clic en la barra hace seek; el tiempo restante baja.
  7. Título y artista largos se recortan con «…» sin cambiar el ancho; sin carátula se ve el marcador.
  8. Clic en carátula/título restaura `MainWindow` minimizada.
  9. Cambiar de tema en vivo (Claro, Oscuro, de usuario): el fondo sigue negro; play/pausa e iconos activos cambian.
  10. Modo inmersivo: la isla se oculta y vuelve al salir.
  11. «Mostrar isla» apagado: se oculta; el estado persiste al reiniciar. JSON antiguo sin la clave: activado.
  12. Dos monitores y DPI distintos: aparece en la pantalla principal y se recoloca al cambiar resolución.
  13. Cerrar `MainWindow` cierra la isla y el proceso.
  14. Uso de CPU en reproducción y en pausa sin aumento apreciable.
  15. CI en Ubuntu compila (guardas de plataforma).
