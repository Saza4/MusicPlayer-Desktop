# Especificación: Dynamic Island («ahora suena» flotante)

Estado: **aprobada**. M0 (spike) completado, ver «Resultado del spike (M0)».

## Contexto

MusicPlayer-Desktop solo se controla desde `MainWindow`. Con la app minimizada o tapada no hay forma de ver ni controlar lo que suena, salvo la barra espaciadora con la ventana enfocada. Se añade una ventana flotante estilo Live Activity (referencia: Dynamic Island de Apple Music en Mac y `Referencias.png`): una pastilla negra arriba al centro, siempre encima, que se expande al pasar el cursor.

Fuentes: `docs/reporte-reproductor.md`, `README.md`, `docs/diseño.md` (principios: simplicidad, discreción —«no te metas»—, constancia de interacciones, accesibilidad, animaciones fluidas) y la exploración de `Desktop`.

## Ajustes aprobados tras M0

1. Las pruebas manuales pendientes de M0 pasan a **puertas de verificación** con la ventana definitiva (M4, M6, M8). Si alguna falla, se aplica el plan B (`SetForegroundWindow` tras el clic del usuario) y se avisa.
2. Decisión abierta 1 resuelta: la isla se ve **solo cuando `MainWindow` no está activa** (minimizada o detrás de otra ventana), con ~150 ms de retardo y fundido para evitar parpadeo.
3. Seek: reutiliza el mecanismo anti-reentrada de la barra principal (`_isUpdatingFromTimer`).
4. Ventana B híbrida: redimensionar con la pastilla bajo el cursor no debe causar saltos; el hover depende de la pastilla, no de la ventana; se verifican píxeles físicos frente a DIPs.

## Comprensión de los criterios de aceptación

1. `IslandWindow` propia: sin bordes, transparente, `Topmost`, fuera de la barra de tareas, centrada arriba en la pantalla principal con margen de 8 px. Usa el `MainViewModel` singleton como `DataContext`.
2. Solo se muestra con pista cargada (`CurrentTrack != null`) y con `MainWindow` no activa. En pausa sigue visible, colapsada.
3. Colapsada: ~36 px de alto, 160-200 px de ancho, miniatura de ~24 px y onda animada solo mientras suena.
4. Hover: expansión de 350-450 ms con `SplineEasing` a 130-160 × 320-360 px, esquinas de ~24 px y `BoxShadow`; colapso 200 ms después de salir. Todas las medidas en un solo archivo de constantes.
5. Expandida: carátula, título (blanco, negrita), artista (gris), onda, progreso con tiempo transcurrido y restante con signo menos, y controles aleatorio / anterior / play-pausa (mayor) / siguiente.
6. Clic en carátula o título trae `MainWindow` al frente (la restaura si está minimizada).
7. No roba el foco; las zonas transparentes no bloquean clics. El spike decide cómo.
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

1. **Visible solo cuando `MainWindow` no está activa** (minimizada o detrás de otra ventana). Con `MainWindow` activa, la isla duplica los controles y tapa el centro de su barra superior. La regla es una función pura de Core, `IslandVisibility.ShouldShow(hasTrack, showIsland, isImmersive, isMainWindowActive)`, con pruebas. Contra el parpadeo, `IslandController` espera ~150 ms (constante en `IslandMetrics`) a que el estado se estabilice antes de mostrar u ocultar, y lo hace con fundido de opacidad. Un cambio que se revierte dentro de ese plazo (por ejemplo, Alt+Tab rápido) no produce nada visible.
2. **Dónde va «Mostrar isla».** Recomiendo un botón `.icon` en el `StackPanel` derecho de la barra de reproducción (junto a Letra y Pantalla completa), con clase `.active` cuando está activo, `ToolTip` y `AutomationProperties.Name`. Es un `Button` con comando, no `ToggleButton`, para no pelear con el atajo global de Espacio (`OnPreviewKeyDown`). Se oculta si la plataforma no es Windows.
3. **Modo inmersivo:** recomiendo **ocultar** la isla mientras `IsImmersive` (ya es pantalla completa con los mismos controles y la isla tapa el contenido).

## Diseño

### Ventana y arquitectura

- `Views/IslandWindow.axaml(.cs)`: `SystemDecorations="None"`, `TransparencyLevelHint="Transparent"`, `Background="Transparent"`, `Topmost`, `ShowInTaskbar="False"`, `ShowActivated="False"`, `CanResize="False"`. Contiene un `ThemeVariantScope RequestedThemeVariant="Dark"` (precedente del modo inmersivo) para que el texto y los controles Fluent salgan claros.
- `Services/IslandController.cs` (singleton, registrado en el bloque «Interfaz» de `CompositionRoot.cs`): recibe `MainViewModel`, crea `IslandWindow` de forma perezosa, escucha `PropertyChanged` (`CurrentTrack`, `ShowIsland`, `IsImmersive`) y `MainWindow.Activated`/`Deactivated`/`WindowState`, y llama a `Show()`/`Hide()` según `IslandVisibility` (con el retardo y el fundido de la decisión 1). En Linux no hace nada.
- `App.axaml.cs`: tras crear `MainWindow`, arranca el controlador; `desktop.ShutdownMode = OnMainWindowClose`; la isla se cierra en `MainWindow.Closed` y en `desktop.Exit`.
- `Views/IslandMetrics.cs` (o diccionario de recursos): **todas** las constantes (tamaños, radios, duraciones, retardo de colapso, margen superior, sombra).
- Posición: `Screens.Primary.WorkingArea` (respeta una barra de tareas superior), centrada, margen superior `8 * RenderScaling`, calculada en píxeles físicos. Se recalcula al mostrar, en `Screens.Changed` y al cambiar el escalado.
- **Unidades:** `Width`/`Height` de la ventana y las medidas de `IslandMetrics` están en DIPs; `Position` y `WorkingArea` en píxeles físicos. La conversión es siempre `físicos = (int)Math.Round(dips * escala)`, con la escala de la pantalla de destino (`Screen.Scaling`), no la de la ventana antes de moverla. El cálculo vive en un solo método. En M4 y M9 se comprueba con `GetWindowRect` a 100 % y a 125/150 %.

### Ventana B híbrida sin saltos

- La pastilla se ancla **arriba al centro** de la ventana, y la ventana se recentra en el mismo paso en que cambia de tamaño (`Position` y tamaño juntos). Así el borde superior y el eje central de la pastilla no se mueven en pantalla: el cursor sigue sobre ella y no hay salto.
- El **hover depende de la pastilla** (`PointerEntered`/`PointerExited` del `Border`), no de la ventana. El margen de sombra no tiene fondo (`Background=null`), así que tampoco cuenta como hover.
- Un `PointerExited` provocado por el propio redimensionado no colapsa nada: el colapso espera 200 ms y se cancela si el cursor sigue o vuelve dentro de la pastilla.
- Al encoger la ventana al final del colapso se comprueba antes que el cursor no esté sobre la pastilla; si lo está, se vuelve a expandir en lugar de encoger.

### Colapso/expansión

- Un `Border` raíz anclado arriba al centro. `Transitions` de `Width`, `Height` y `CornerRadius` con `SplineEasing` (curva a ajustar con capturas). Contenido colapsado y expandido en dos capas con fundido de opacidad.
- `PointerEntered` cancela el temporizador de colapso y expande; `PointerExited` arranca un `DispatcherTimer` de 200 ms que colapsa. Reentrar cancela, así que no hay saltos.
- Sombra con `BoxShadow` (sin `blur()` de efecto, que es costoso).

### Contenido

- Colapsada: miniatura de 24 px (`NowPlayingCover`; sin carátula, marcador neutro con icono de nota) y onda a la derecha.
- Expandida: carátula, `Title` (blanco, negrita) y `Artist` (gris) con `TextTrimming="CharacterEllipsis"` y ancho fijo, onda, barra de progreso con `PositionText` a la izquierda y `RemainingText` (`−1:31`) a la derecha, y la fila de controles. Los botones llevan `ToolTip` y `AutomationProperties.Name`.
- Onda: barras (`Border`) con animación por clase `.playing` (animación de `ScaleY` desfasada), quitada al pausar (queda estática). No hay temporizador propio; se medirá en el spike.
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
- Transparencia: se prueban en el spike (A) ventana fija del tamaño expandido + margen de sombra, con fondo `null` fuera de la pastilla; (B) ventana redimensionada al tamaño de la pastilla en cada estado, recentrada; (C) región de recorte por interop. Se elige la más simple que no bloquee clics. **Resultado: A descartada, se usa B híbrida (ver «Resultado del spike (M0)»).**
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

`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` se aplica siempre (con `OperatingSystem.IsWindows()`), por seguridad. La opción C (región de recorte) queda como alternativa si B muestra parpadeo en M5.

## Archivos

Nuevos (Desktop): `Views/IslandWindow.axaml(.cs)`, `Views/IslandMetrics.cs`, `Services/IslandController.cs`, `Themes/PlayerButtonStyles.axaml`.
Modificados (Desktop): `App.axaml(.cs)`, `CompositionRoot.cs`, `MainViewModel.cs`, `MainWindow.axaml` (estilos movidos y botón «Mostrar isla»), `Themes/ThemeBrushes.axaml`.
Excepciones a «solo Desktop», necesarias por la convención del repositorio: Core (`AppSettings.ShowIsland`, `IslandVisibility`, `TimeText.Remaining`), y sus pruebas en `tests/MusicPlayer.Core.Tests` y `tests/MusicPlayer.Infrastructure.Tests` (`JsonSettingsRepositoryTests`). Al terminar se actualizan `README.md` y `docs/reporte-reproductor.md`.

## Hitos (cada uno compila, pasa pruebas y se puede comprobar solo)

| # | Hito | Verificación |
|---|------|--------------|
| M0 | **Spike** (hecho): ventana mínima; se elige la B híbrida | Tabla de resultados en la spec |
| M1 | Ajuste `ShowIsland` (Core + VM) e `IslandVisibility.ShouldShow(hasTrack, showIsland, isImmersive, isMainWindowActive)` | Pruebas: JSON parcial → `true`, ida y vuelta con `false`, tabla de reglas de visibilidad |
| M2 | `TimeText.Remaining` y `RemainingText` | Pruebas (0, <1 s, minutos, horas, duración desconocida) |
| M3 | Pinceles `ThemeIsland*` y estilos movidos a `PlayerButtonStyles.axaml` | Captura de `MainWindow` idéntica a la anterior |
| M4 | `IslandWindow` colapsada, posición y `IslandController` (mostrar/ocultar con pista y `MainWindow` no activa, retardo de 150 ms y fundido, cierre con la app, Linux sin efecto) | Manual: aparece al minimizar o pasar a otra app, desaparece al volver a `MainWindow` o limpiar la pista, la app cierra. `GetWindowRect` coincide con DIPs × escala. **Puerta:** al aparecer la isla, el Bloc de notas sigue recibiendo lo que se escribe |
| M5 | Expansión con hover (B híbrida), animación y constantes en un solo archivo | Capturas; entrar/salir repetido sin saltos; redimensionar con el cursor encima no mueve la pastilla ni la colapsa |
| M6 | Contenido expandido: textos, progreso con seek (`SeekToFraction`, sin bucle), controles, accesibilidad | Botones ejecutan los comandos reales; ellipsis sin cambiar el ancho; un clic en la barra produce un solo `Seek`. **Puerta:** pulsar un botón de la isla con el Bloc de notas enfocado conserva su foco |
| M7 | Onda animada solo al sonar; medición de CPU | Pausa = estática; ventana oculta sin consumo notable |
| M8 | Clic en carátula/título trae `MainWindow`; botón «Mostrar isla» en la barra | Restaura desde minimizada; el ajuste persiste. **Puerta:** `Activate()` lleva `MainWindow` al primer plano; si Windows lo impide, plan B (`SetForegroundWindow`) y aviso |
| M9 | Casos límite: monitores/DPI, temas en vivo, inmersivo, carátula ausente | Checklist completo |
| M10 | Docs (`README`, reporte, spec final) | `dotnet build` sin warnings nuevos; todas las pruebas en verde |

## Verificación

- Automática: `dotnet build -c Release` (solo la CS8602 conocida) y `dotnet test -c Release` (341 + nuevas).
- Manual en Windows (checklist, criterio 12):
  1. Sin pista, no hay isla. Con pista y `MainWindow` activa, tampoco. Al minimizar o pasar a otra app, aparece arriba al centro con margen de 8 px, con fundido; al volver a `MainWindow`, desaparece. Un Alt+Tab rápido de ida y vuelta no la hace parpadear.
  2. Pausa: sigue visible, colapsada y con la onda estática.
  3. Pasar el cursor expande en ~0,4 s; salir colapsa a los ~200 ms; entrar y salir rápido no produce saltos.
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
