# Spec: seguir sonando al cerrar la ventana (bandeja del sistema)

Estado: **aprobada por el autor el 15 de noviembre de 2026**. Sin código todavía. Depende de `settings-panel.md`.

## Context

Feedback de uso (noviembre de 2026): «el reproductor no puede estar en segundo plano, así que debe estar abierto
siempre, por lo que pierde todo el sentido tener una isla dinámica si siempre está abajo».

Hoy cerrar la ventana principal termina la app (`ShutdownMode.OnMainWindowClose`, `App.axaml.cs:51`). La isla
solo aparece con la ventana minimizada o detrás de otra, y la ventana sigue en la barra de tareas. No hay icono
de bandeja ni instancia única: abrir el ejecutable dos veces crea dos procesos que escriben los mismos
`settings.json`, `library.json` y `playlists.json`.

Decisión del autor (15 de noviembre de 2026): cerrar oculta la ventana a la bandeja del sistema, con un ajuste;
el icono de la bandeja permite abrir o salir.

## FEATURE: cerrar oculta a la bandeja, e instancia única

### ACCEPTANCE CRITERIA

**Cerrar a la bandeja**

1. Ajuste nuevo `KeepRunningOnClose = false`, **al final** de `AppSettings`, con la casilla «Seguir sonando al
   cerrar la ventana» en la sección «Segundo plano» de Configuración. Solo en Windows: en Linux la sección no
   aparece y cerrar termina la app como hoy.
2. Con el ajuste desactivado, cerrar la ventana termina la app, como hoy.
3. Con el ajuste activado, cerrar la ventana (la X, Alt+F4) la oculta: desaparece de la barra de tareas, la música
   sigue, y la isla se muestra si su regla lo permite (la ventana oculta cuenta como no activa).
4. Mientras la ventana está oculta hay un icono en la bandeja del sistema con el icono de la app y el texto
   «Music Player». Un clic en él abre la ventana. Su menú tiene: «Abrir», «Reproducir» o «Pausar», «Siguiente»,
   «Salir».
5. Con la ventana visible, el icono de la bandeja no está.
6. «Salir» (bandeja, o el botón de la isla de `island-title-and-close.md`) termina la app por el camino de hoy:
   `FlushAsync` de los ajustes y `services.Dispose()`.
7. La decisión de ocultar o salir es una función pura de Core (`CloseBehavior.Decide(keepRunning, supported,
   exitRequested)`) con pruebas de todas las combinaciones.
8. Abrir la ventana desde la bandeja o desde la isla la deja en el estado que tenía (normal o maximizada) y con
   el foco, usando el `BringMainToFront` que ya existe.
9. La primera vez que la ventana se oculta, una notificación de la bandeja (o, si no se puede, la línea de estado
   al volver) dice que la app sigue abierta y cómo salir. Solo una vez por sesión.

**Instancia única** (no la pidió el feedback; se añade porque, con la ventana oculta, abrir la app otra vez dejaría
dos procesos escribiendo los mismos JSON)

10. En Windows, al abrir la app con otra instancia ya en marcha, la segunda no carga nada, avisa a la primera y
    termina. La primera muestra su ventana y la trae al frente.
11. El código nativo y los objetos con nombre van detrás de `OperatingSystem.IsWindows()` con
    `[SupportedOSPlatform("windows")]`. En Linux no hay instancia única (como hoy).

**General**

12. La isla no aparece mientras un selector de archivos está abierto (regla actual de `ModalTracker`), también
    con la ventana oculta.
13. `dotnet build` sin advertencias nuevas, `dotnet test` en verde y el CI de Ubuntu compila.

### OUT OF SCOPE

- Arrancar con Windows o arrancar minimizada.
- Minimizar a la bandeja (minimizar sigue dejando la ventana en la barra de tareas).
- SMTC, MPRIS y teclas multimedia (hoja de ruta).
- Bandeja en Linux.
- Pasar argumentos de la segunda instancia a la primera (abrir un archivo desde el Explorador).

### EDGE CASES

- **Cerrar con el modo inmersivo abierto:** se sale del inmersivo y se oculta; al volver, la ventana está en su
  estado anterior al inmersivo.
- **Cerrar con un editor escribiendo en un archivo:** ocultar no interrumpe la escritura. «Salir» durante una
  escritura se comporta como hoy cerrar la app en ese momento (no se cambia aquí).
- **Cerrar con un panel o Configuración abiertos:** se quedan como estaban al volver.
- **Isla desactivada (`ShowIsland = false`) y ventana oculta:** queda solo el icono de la bandeja.
- **Apagado o cierre de sesión de Windows:** la app termina aunque el ajuste esté activado; no cancela el apagado.
- **El icono de la bandeja no se puede crear:** el ajuste no surte efecto (cerrar termina la app) y la línea de
  estado lo dice. Nunca se oculta la ventana sin una forma de volver.
- **Fallo de arranque con la ventana oculta en la bandeja** (la primera instancia no llega a mostrar ventana o
  falla al cargar): la segunda instancia no se queda esperando; pasado el tiempo corto, avisa y termina. Caso
  probado a mano en M5.
- **Segunda instancia mientras la primera arranca:** la segunda espera un tiempo corto a que la primera atienda y,
  si no hay respuesta, termina sin hacer nada.
- **La primera instancia está colgada:** la segunda termina igual; no abre otra ventana sobre los mismos datos.
- **Ajuste cambiado con la ventana ya oculta:** no aplica (la casilla solo se alcanza con la ventana visible).

## Diseño

- `App.axaml.cs`: `ShutdownMode.OnExplicitShutdown`; `MainWindow.Closing` consulta `CloseBehavior.Decide` y, si
  toca ocultar, `e.Cancel = true` y `Hide()`; si toca salir, `desktop.Shutdown()`. El cierre por apagado del
  sistema (`WindowCloseReason`) siempre sale.
- `App.axaml`: `TrayIcon` con `NativeMenu`, con `IsVisible` enlazado al estado «ventana oculta».
- `IslandController`: observa también `Visual.IsVisibleProperty` de la ventana principal y la cuenta como no
  activa cuando está oculta. `BringMainToFront` ya hace `Show()` y `Activate()`.
- `MainViewModel`: `IsMainWindowHidden` (para el botón «Salir» de la isla) y `ExitRequested` (evento que
  `App.axaml.cs` conecta a `Shutdown`).
- `Services/SingleInstance.cs` (Desktop, Windows): `Mutex` con nombre por usuario y un `EventWaitHandle` con
  nombre que la primera instancia escucha en un hilo de fondo; al recibirlo, `Dispatcher.UIThread.Post` de
  `BringMainToFront`. Se comprueba en `Program.Main`, antes de arrancar Avalonia.

## Hitos

- **M0 (spike):** comprobar en Avalonia 11.2.5 que `TrayIcon` se muestra y se oculta en caliente en Windows 11,
  que `Hide()` de la ventana principal no dispara `Closed`, y qué `WindowCloseReason` llega en el apagado.
  Si algo falla, se trae al autor antes de seguir.
- **M1:** `CloseBehavior`, ajuste, casilla y pruebas.
- **M2:** ocultar al cerrar, bandeja y salida explícita.
- **M3:** isla con la ventana oculta.
- **M4:** instancia única.
- **M5:** prueba a mano del autor en Windows: cerrar con el ajuste encendido y apagado, abrir desde la bandeja y
  desde la isla, salir desde los dos sitios, segunda instancia, y comprobar que `settings.json` guarda lo último.
