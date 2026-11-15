# Spec: área de Configuración y reproducir con un clic

Estado: **implementada el 15 de noviembre de 2026** (rama `feat/settings-panel`, desde `main` con los PR #12 a #16
ya fusionados), a falta de la prueba a mano del autor (ver «Resultado»). Compila con la CS8602 de siempre y pasan
996 pruebas (787 en Core, 209 en Infrastructure).

## Context

Feedback de uso (noviembre de 2026): «no hay un área dedicada a configuración» y «el hacer doble clic para
reproducir es molesto y es una de las opciones que deben estar en la configuración».

Hoy los diez ajustes de `AppSettings` se cambian desde sitios distintos: el tema en la barra superior, la isla en
la barra de reproducción, las capas de letra en un flyout del panel lateral. No hay ninguna vista de ajustes. La
app no crea ventanas de diálogo: todo lo que parece un diálogo es un panel dentro de `MainWindow` (Convertir,
Editar etiquetas, Limpiar etiquetas).

El doble clic se atiende en `MainWindow.axaml.cs`, `OnTrackDoubleTapped`, que además reproduce
`vm.SelectedTrack` sin mirar la fila pulsada (hallazgo H5 de `audit-3.md`).

Esta spec crea el sitio donde vivirán los ajustes de las demás specs de v1.6.0
(`island-title-and-close.md`, `background-playback.md`, `lyrics-online-translation.md`): cada una añade su
sección.

## FEATURE: vista «Configuración» y ajuste de clic

### ACCEPTANCE CRITERIA

1. La barra superior tiene un botón «Configuración» con icono de engranaje, a la derecha junto a «Tema», con
   `Classes="action"`, `ToolTip.Tip`, `AutomationProperties.Name` y el texto en un `TextBlock.label` (se oculta
   en modo `compact`).
2. El botón abre y cierra una vista «Configuración» que es una capa dentro de `MainWindow` (no una ventana): cubre
   la zona central y deja a la vista la barra superior y la de reproducción. `Esc` y un botón «Cerrar» la cierran.
   La música no se interrumpe y la isla no cambia de estado.
3. La vista está dentro de un `ScrollViewer`: con la ventana a 640×400 se llega a todas las opciones.
4. Secciones, en este orden: **Apariencia**, **Reproducción**, **Isla flotante**, **Segundo plano**, **Letras**
   y **Datos**. Una sección sin opciones todavía no se muestra. «Isla flotante» y «Segundo plano» solo aparecen
   en Windows.
5. **Apariencia:** una lista desplegable con los mismos temas que el botón «Tema»; elegir uno lo aplica y lo
   guarda (`ThemeViewModel.Commit`). El botón «Tema» de la barra superior sigue como está.
6. **Isla flotante:** una casilla «Mostrar la isla flotante» enlazada al mismo `ShowIsland` que el botón de la
   barra de reproducción (los dos se mantienen sincronizados).
7. **Datos:** la ruta de la carpeta de datos y un botón «Abrir carpeta» (el mismo mecanismo que «Carpeta de
   temas»). Si no se puede abrir, la ruta aparece en la línea de estado.
8. **Reproducción:** una casilla «Reproducir con un solo clic», desactivada por defecto. Ajuste nuevo
   `PlayOnSingleClick = false`, **al final** de `AppSettings`.
9. Con el ajuste desactivado, un clic selecciona y el doble clic reproduce, como hoy. Con el ajuste activado, un
   clic con el botón izquierdo y sin Ctrl ni Mayús sobre una fila de canción la reproduce; con Ctrl o Mayús solo
   selecciona; el doble clic no la reinicia.
10. En los dos modos se reproduce **la fila pulsada**. Un clic o doble clic fuera de una fila (barra de
    desplazamiento, hueco vacío) no reproduce nada.
11. La regla del criterio 9 es una función pura de Core (`TrackClick.ShouldPlay`) con pruebas: ajuste apagado y un
    clic, ajuste apagado y doble clic, ajuste encendido y un clic, con Ctrl, con Mayús, botón derecho, y segundo
    clic del doble clic con el ajuste encendido.
12. Cada opción se guarda al cambiarla con `SettingsService.Update` (sin botón «Guardar»). Un `settings.json`
    anterior sin las claves nuevas carga con sus valores por defecto: prueba en `JsonSettingsRepositoryTests`.
13. `SettingsViewModel` es nuevo y se registra en `CompositionRoot.cs`. La vista usa solo `CheckBox`,
    `ComboBox` y `Button`, así que `OwnsKeyboard` no cambia.
14. Sin literales de color en el XAML nuevo: solo pinceles `ThemeXxxBrush`.
15. `dotnet build` sin advertencias nuevas y `dotnet test` en verde.

### OUT OF SCOPE

- Mover o quitar los controles actuales (botón «Tema», botón de la isla, flyout de capas de letra).
- Ajustes nuevos que nadie ha pedido (carpetas vigiladas, atajos configurables, idioma de la interfaz).
- Un rediseño visual de la app: «la interfaz sigue estando fea» necesita referencias del autor y spec propia.
- Reproducir con un clic en Álbumes y Artistas: ahí un clic ya abre el álbum o el artista.
- Enter para reproducir la fila seleccionada.

### EDGE CASES

- **Configuración abierta y se abre el modo inmersivo** (clic en la carátula): la vista se cierra.
- **Configuración abierta y se abre un panel de edición** (Convertir, Editar etiquetas): el panel se ve encima de
  la zona central, como hoy, y la vista de Configuración se cierra.
- **Ajuste encendido y clic derecho:** abre el menú contextual, no reproduce.
- **Ajuste encendido y pista no disponible:** el clic hace lo mismo que hoy el doble clic (aviso en la línea de
  estado, la fila queda marcada).
- **Ajuste encendido y la selección que la app mueve sola** (`SyncSelectionWith` al cambiar de pista): no cuenta
  como clic; no reproduce nada.
- **Ajuste encendido y flechas del teclado:** mueven la selección sin reproducir.
- **Carga de ajustes fallida** (`SettingsService` no guarda): las casillas funcionan en memoria durante la sesión.
- **Espacio con una casilla enfocada:** marca la casilla (excepción que ya existe en `OnPreviewKeyDown`). `M`, `←`
  y `→` siguen actuando como atajos, como en las demás casillas de la app.

## Diseño

- `ViewModels/SettingsViewModel.cs`: `IsOpen`, `ToggleOpenCommand`, `CloseCommand`, `PlayOnSingleClick` y
  `Restore(AppSettings)` (misma forma que `LyricsViewModel.Restore`, con la guarda `_restoring`). `MainViewModel`
  lo recibe por constructor y lo expone como `Settings`.
- `MainWindow.axaml`: la vista es un `Border` sobre la zona central (`Grid` de la línea 693), con
  `IsVisible="{Binding Settings.IsOpen}"`. Cada sección es un `StackPanel` con un título.
- `MainWindow.axaml.cs`: `OnKeyDown` cierra la vista con `Esc` (después del caso del inmersivo). El manejador de
  la lista pasa a `Tapped` + `DoubleTapped`, toma la fila de `e.Source` (como `OnTrackListContextRequested`) y
  pregunta a `TrackClick.ShouldPlay`.
- `Core/Services/TrackClick.cs`: función pura, sin tipos de Avalonia (recibe `bool` y un entero de clics).

## Resultado

**Orden seguido:** primero el ajuste de un clic (lo más visible del feedback) y después la vista.

**Decisiones tomadas al implementar (revisables):**

1. **`TrackClick.ShouldPlay` recibe también si la fila pulsada es la pista que ya suena.** Con el ajuste activado,
   el primer clic la pone a sonar; llegue el segundo clic como «doble clic» o como otro clic (depende de la
   plataforma), no la reinicia. De paso, un clic suelto en la pista que suena tampoco la reinicia. Con el ajuste
   desactivado, el doble clic en la pista que suena la reinicia, como siempre.
2. **Criterio 5, el tema:** `ThemeViewModel.CommittedChoice` (el tema confirmado, sin vista previa) y
   `ReloadAsync` (relee los temas de usuario sin marcar el selector como abierto, para que los atajos de teclado
   sigan funcionando). Configuración los relee al abrirse.
3. **Cierre de la vista:** con `Esc`, con «Cerrar», con el propio engranaje, al entrar en el modo inmersivo y al
   abrir un panel de edición (Convertir, Limpiar etiquetas, Editar etiquetas). El `Esc` del inmersivo va antes; un
   cuadro de texto o una lista desplegable que usan `Esc` lo marcan como atendido y no cierran la vista.
4. **Secciones de esta entrega:** Apariencia, Reproducción, Isla flotante (solo Windows) y Datos. «Segundo plano»
   y «Letras» llegan con sus specs.
5. **`pendientes.md`, punto 5:** los paneles «Editar etiquetas» y «Limpiar etiquetas» ya dicen «se notará un corte
   breve» en lugar de «se detendrá».

**Verificado:** 9 pruebas nuevas en Core (`TrackClickTests`) y 1 en Infrastructure (guardar y leer el ajuste; un
`settings.json` de la v1.3.0 lo carga desactivado). Capturas en Windows (Oscuro, escala 100 %, `settings.json`
intacto por hash), abriendo la vista por UI Automation: a 640×400 y 960×600 se ven el título, «Cerrar», la lista
de temas con el tema guardado, la casilla del clic y, a 960, la de la isla; la vista se desplaza cuando no cabe.
A 640 px el engranaje cabe en la barra superior compacta junto a «Tema», sin recortar ningún botón.

**Sin verificar (falta la prueba a mano del autor):**

- Los dos modos de clic con el ratón: ajuste apagado (un clic selecciona, doble clic reproduce), ajuste encendido
  (un clic reproduce; Ctrl o Mayús solo seleccionan; el doble clic no reinicia), y que el doble clic en la barra
  de desplazamiento o en el hueco vacío ya no reproduce nada (H5).
- `Esc` cerrando la vista, y que no interfiere con el inmersivo ni con el cuadro del nombre de una playlist.
- Cambiar de tema desde la lista de Configuración y que el botón «Tema» y la lista quedan de acuerdo.
- «Abrir carpeta» y la casilla de la isla sincronizada con el botón de la barra de reproducción.
- Los temas Claro y de usuario (no se cambió el tema para no tocar el `settings.json` real).

## Hitos

- **M1:** `AppSettings`, `TrackClick` y pruebas (Core e Infrastructure).
- **M2:** `SettingsViewModel`, registro en DI y vista con las secciones Apariencia, Reproducción, Isla y Datos.
- **M3:** manejador de clic de la lista y arreglo de H5.
- **M4:** verificación con capturas en Windows (procedimiento de CLAUDE.md) a 640×400 y 960×600, en Oscuro,
  Claro y un tema de usuario; prueba a mano de los dos modos de clic.
