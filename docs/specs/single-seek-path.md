# Spec: un solo camino de salto

Estado: implementada el 16 de noviembre de 2026, a petición del autor (segunda mejora de la auditoría de ese día).
Compila con 0 errores y la advertencia CS8602 de siempre, y pasan las 1036 pruebas (825 en Core, 211 en
Infrastructure), las mismas que antes: el cambio vive en `MainViewModel` y `LyricsViewModel`, sin pruebas
automáticas. **Verificación manual hecha por el autor el 16 de noviembre de 2026, en Windows: todo correcto** (no
se anotó el resultado paso a paso). Los criterios 1 a 3 dependen solo de esa pasada.

## Context

Era el punto 6 de `docs/pendientes.md` y la misma familia que A1 de `audit-followup.md`, que solo se arregló en
`ResumePlayback`.

El temporizador (`OnTick`) solo escribe `PositionSeconds` mientras la pista suena. Un salto que llama a
`_player.Seek` sin escribir `PositionSeconds` se oye bien, pero en pausa deja la interfaz donde estaba:

| Dónde | Qué pasaba en pausa |
| ----- | ------------------- |
| `MainViewModel.Previous` (pasados 3 s, o sin pista anterior) | El reproductor vuelve a 0:00; la barra, el tiempo y el punto de partida de ← → no se mueven hasta reanudar |
| `LyricsViewModel.SeekTo` (clic en una línea, o en una pausa de la letra progresiva) | El reproductor salta y la línea activa cambia, pero la barra y el tiempo no; ← → saltan desde la posición vieja |

Además, el bloque `_isUpdatingFromTimer = true; try { … } finally { … }` estaba escrito cinco veces en
`MainViewModel` (`ClearCurrentTrack`, `ShowTrack`, `OnAdvancedToNext`, `ResumePlayback`, `OnTick`).

## FEATURE: Todo salto que no nace de la barra pasa por `MainViewModel.SeekTo`

### ACCEPTANCE CRITERIA

1. En pausa, «Anterior» pasados 3 s (o sin pista anterior) deja la barra y el tiempo en 0:00, y ← → parten de ahí.
2. En pausa, pulsar una línea de la letra (lista clásica o letra progresiva) deja la barra y el tiempo en esa
   línea, la línea queda activa y ← → parten de ahí.
3. Sonando, los dos saltos se comportan como antes, y cada uno llama a `Seek` una sola vez (escribir
   `PositionSeconds` no provoca un segundo salto).
4. `_isUpdatingFromTimer` solo se toca en un método (`SetPositionSilently`).
5. `LyricsViewModel` no llama a `IAudioPlayer.Seek`: pide el salto con un evento (`SeekRequested`) que
   `MainViewModel` conecta a `SeekTo`.

### OUT OF SCOPE

- Los saltos que nacen de la barra (`SeekBy`, `SeekToFraction`, el arrastre y `EndScrub`): ya escriben
  `PositionSeconds` y siguen pasando por `OnPositionSecondsChanged` y `SeekScrub`.
- El `_player.Seek(TimeSpan.Zero)` de «repetir una» en `OnTrackEnded`: la pista ya terminó y el `Play` siguiente
  vuelve a abrir el archivo; no es este fallo.
- Dividir `MainViewModel` o mover la posición a un servicio aparte.
- Pruebas de Desktop: el proyecto no las tiene.

### EDGE CASES

- **Posición fuera de rango** (una línea de un `.lrc` de otra versión, más allá del final): `SeekTo` la acota a
  la duración, como hace el reproductor, para que la barra no salga de su rango y devuelva un segundo salto.
- **Sin pista cargada:** «Anterior» deja la posición en 0, donde ya estaba; `Seek` no hace nada.
- **Saltar al punto donde ya está la barra:** `PositionSeconds` no cambia y no hay aviso; el reproductor salta
  igual.
- **Letra sin tiempos:** `LyricsViewModel.SeekTo` no pide nada, como antes.

## Resultado

- `MainViewModel`: `SetPositionSilently(posición, duración opcional)` sustituye a los cinco bloques;
  `SeekTo(posición)` salta en el reproductor y pone al día lo que se ve. Lo usan `Previous`, `ResumePlayback` y
  la letra.
- `LyricsViewModel`: evento `SeekRequested`; `SeekTo` ya no toca el reproductor ni llama a `UpdatePosition` (lo
  hace `OnPositionSecondsChanged`).
- Sin pruebas nuevas: no hay lógica nueva en Core.

## Verificación manual (hecha por el autor el 16 de noviembre de 2026, Windows)

1. Reproducir una pista con letra sincronizada, esperar más de 3 s, pausar y pulsar «Anterior»: la barra y el
   tiempo pasan a 0:00. Pulsar → una vez: salta desde 0:00.
2. En pausa, pulsar una línea de la letra del panel: la barra y el tiempo van a esa línea y queda resaltada.
   Repetir en el modo inmersivo con la letra progresiva.
3. Reanudar: suena desde el punto que marca la barra.
4. Sonando, repetir «Anterior» y el clic en una línea: sin cortes dobles ni saltos de la barra hacia atrás.
5. Editar las etiquetas de la pista en pausa a mitad de canción: al guardar, la barra vuelve al mismo punto
   (criterio 1 de `audit-followup.md`, que ahora pasa por `SeekTo`).
6. Arrastrar la barra y usar la barra de la isla: igual que antes.
