# Especificación: release v1.6.0

Estado: **preparada** (19 de noviembre de 2026) y revisada con una auditoría previa de solo lectura el mismo día (ver
al final). Notas escritas y zip construido y comprobado. **Falta** fusionar el PR de esta documentación, crear el
borrador en GitHub sobre el commit de `main` resultante y que el autor lo publique.

## Contexto

La última release publicada es v1.5.0 (12 de noviembre de 2026). Desde entonces `main` acumula 42 commits y 119
pruebas más (de 959 a 1078). Todo está probado a mano por el autor:

- **Tercera auditoría** (`audit-3.md`, PR #13 a #15): avisos de fin de pista con sello (`PlaybackEpoch`), candado
  común con BASS, JSON vaciados a disco antes de renombrar, escaneo que se guarda aunque falle la limpieza de
  temporales, temporizadores de la isla y avisos del arranque.
- **Interfaz:** diseño adaptable (`responsive-layout.md`, #16), vista «Configuración» y reproducir con un clic
  (`settings-panel.md`, #17) y el rediseño según los mockups (`redesign-mockups.md`, #29).
- **Isla y segundo plano:** título desplazable, ocultar, salir y barra arrastrable (`island-title-and-close.md`,
  #19); bandeja del sistema e instancia única (`background-playback.md`, #20).
- **Arreglos del 16 y 19 de noviembre:** `ascii-digits.md` (#22), `single-seek-path.md` (#23),
  `playlist-serialization.md` (#24), `write-after-advance.md` (#25), `write-sessions.md` (#26),
  `remove-and-advance.md` y `keep-browse-scope.md` (#27).
- **Descartado:** traducir la letra en línea (`lyrics-online-translation.md`, #21).

## Decisiones

1. **Versión v1.6.0.** Funciones nuevas y arreglos, nada incompatible: un `settings.json` anterior carga igual. Los
   tres ajustes nuevos (`PlayOnSingleClick`, `IslandShowTitle`, `KeepRunningOnClose`) van al final de
   `AppSettings` y empiezan desactivados, así que nada cambia hasta que el usuario los encienda. Salto menor según
   SemVer. Release normal (**Latest**).
2. **Las notas dicen lo que hace el código** y qué se probó y qué no, como las de v1.5.0. La prueba a mano del autor
   (15, 16 y 19 de noviembre de 2026) se dio por buena entera, sin anotar el resultado paso a paso.
3. **El zip incluye las DLL de BASS**, como los anteriores.
4. **Borrador en GitHub** (`gh release create --draft`) sobre el commit de `main` que fusiona esta documentación.
   El tag no existe hasta que el autor publica.
5. **Sin `<Version>` en el `.csproj`:** la versión vive en el tag, el título y el nombre del zip.
6. **Integridad:** SHA-256 del zip en las notas; el zip no está firmado y las notas lo dicen.
7. **Rutas neutras en los binarios** (decidido el 19 de noviembre de 2026, `pendientes.md`): se publica con
   `-p:ContinuousIntegrationBuild=true`. Solo eso no basta: el compilador de XAML de Avalonia reescribe
   `MusicPlayer.Desktop.dll` y deja en su cabecera de depuración la ruta absoluta del `.pdb` en `obj/`. Por eso se
   publica desde una copia limpia del commit (`git worktree`) montada como unidad virtual (`subst X:`): esa ruta
   pasa a empezar por `X:\src\…`, sin la carpeta de usuario. Los `.pdb` se conservan: dan los números de línea de
   `errores.log`.

## Criterios de aceptación

1. `main` compila con `dotnet build -c Release` (solo la CS8602 conocida) y pasa 1078 pruebas (867 Core + 211
   Infrastructure). El CI de los PR pasó en Windows y Ubuntu.
2. `docs/notas-de-version/v1.6.0.md` está en español, no afirma nada que el código no haga e incluye novedades,
   arreglos, «Al actualizar», requisitos, qué se probó y qué no, cómo verificar la descarga (SHA-256) y los
   contribuyentes. Los textos de la interfaz que cita son los del código.
3. El zip `MusicPlayer-v1.6.0-win-x64.zip` se construye con
   `dotnet publish -c Release -r win-x64 --self-contained false -p:ContinuousIntegrationBuild=true` desde `X:\`, con
   la misma estructura que el de v1.5.0 (49 archivos, carpeta raíz `win-x64/`, rutas con `/`), las cuatro DLL de BASS
   y sin el código de depuración.
4. **Ninguna DLL, `.pdb` ni el `.exe` del zip contiene la carpeta de usuario** del equipo que compila.
5. La release es un **borrador** titulado «MusicPlayer v1.6.0», con ese zip adjunto.
6. Docs al día: reporte (cabecera, 4.3.9 y 4.3.10, sección 5 con las notas y 10) y `pendientes.md`.

## Fuera de alcance

Firmar el ejecutable, un instalador, la versión en el `.csproj`, un workflow de release automático, un build para
Linux y cualquier cambio de código de la app.

## Casos límite

- **El zip se reconstruye:** su SHA-256 cambia; hay que cambiarlo en las notas y en el reporte antes del borrador.
  El SHA-256 de abajo corresponde al commit `fe96d79`: el commit de esta documentación no toca código, así que el
  zip vale igual para él.
- **La letra `X:` ya está en uso:** se elige otra libre. Al terminar, `subst X: /d`.
- **Zip creado con Windows PowerShell 5.1:** `Compress-Archive` escribe rutas con `\`. Se usa PowerShell 7.
- **Usuario con «Seguir sonando al cerrar la ventana» activado:** en la prueba de arranque, cerrar la ventana la
  oculta a la bandeja y el proceso sigue vivo. Se termina el proceso a mano; es lo esperado, no un fallo de cierre.
- **Promesas no probadas:** Linux, pantalla táctil, el foco con `Tab` real en los botones nuevos y el relleno por
  palabra con texto de derecha a izquierda. Las notas lo dicen.
- **Pendientes conocidos:** el congelamiento con unidades de red (R6) y la memoria de los `Bitmap`
  (`pendientes.md`). Las notas los citan sin prometer arreglo.

## Resultado

Zip construido el 19 de noviembre de 2026 con el código del commit `fe96d79` (`main` tras el PR #29 y el commit de
`docs/diseño.md` y la captura).

| Comprobación | Resultado |
|---|---|
| `dotnet build -c Release` | 0 errores, solo la CS8602 conocida |
| `dotnet test -c Release` | 1078 en verde (867 Core + 211 Infrastructure) |
| `dotnet publish … -p:ContinuousIntegrationBuild=true` desde `X:\` | 49 archivos, **el mismo conjunto** que el zip de v1.5.0 (comparado con el zip publicado) |
| Zip `MusicPlayer-v1.6.0-win-x64.zip` | 11 848 043 bytes, carpeta raíz `win-x64/`, sin rutas con barra invertida (PowerShell 7.6) |
| SHA-256 | `098164E66D1500ECF090880ED26BB0545B2A805237B13656817B25445EC71536` |
| DLL de BASS en el zip | `bass.dll`, `bass_aac.dll`, `bassflac.dll` y `bassmix.dll` |
| Código de depuración | Ninguna variable `MUSICPLAYER_*` ni `ReadOnlySettingsRepository` en `MusicPlayer.Desktop.dll` |
| Rutas locales | Ninguna DLL, `.pdb` ni `.exe` contiene la carpeta de usuario. Con solo `ContinuousIntegrationBuild`, sin `subst`, quedaba una: la del `.pdb` en la cabecera de `MusicPlayer.Desktop.dll` |
| Arranque del exe publicado | Arranca (ventana «Music Player»). Con «Seguir sonando al cerrar» activado, cerrar la ventana la oculta a la bandeja; se terminó el proceso |
| Datos del usuario | `settings.json` y `library.json` idénticos antes y después (mismo hash); no se creó `errores.log` |

## Auditoría previa (19 de noviembre de 2026)

Skill `auditor`, enfoques G (coherencia de documentación, pruebas y notas) y H (higiene del repositorio), solo
lectura. Hallazgos y qué se hizo con ellos:

| Comprobación | Resultado |
|---|---|
| Críticos (pérdida de datos, borrados, reglas de escritura) | Ninguno |
| Cifras de pruebas (CLAUDE.md, README, reporte) | Coinciden con las reales: 867 + 211 |
| «No hay pruebas de Desktop ni de Audio» | Cierto: los proyectos de prueba solo referencian Core e Infrastructure |
| Cifras contrastables | Coinciden: 5 `.corrupt` (`CorruptBackup.Keep`), 256 KB de `errores.log`, 1 MB de letra, 10 MB de carátula, 24 h de temporales, 64 KB y 50 temas |
| Texto «se detendrá» en los paneles de edición | Ya no existe: dicen «se notará un corte breve» |
| Audio en `tests/`, BASS, `bin/`/`obj/`, rutas personales, `.env` | Solo `Fixtures/silencio.m4a`; `native/` en `.gitignore`; nada más versionado |
| Reporte 3.1, «Navegación» | Hablaba de pestañas Canciones, Álbumes y Artistas: desde el rediseño, «Tu biblioteca» con Canciones y Artistas. Corregido |
| `island-title-and-close.md`, «Sin verificar» | Seguía listando lo que el autor ya probó. Corregido |
| `action-buttons.md` | Cita los principios de `diseño.md` con los nombres anteriores («Oficios», «Delicia»); `diseño.md` usa ahora «Artesanía» y «Encanto». Anotado en la spec |
| `docs/diseño.md` | Es una traducción de los principios de las Human Interface Guidelines de Apple. Queda a decisión del autor si conviene tenerla en un repositorio público (el README declara MIT para el código) |
| Publicación efectiva | No verificable por lectura: se comprueba en GitHub al publicar |
