<#
.SYNOPSIS
    Mide la CPU de un proceso en porcentaje de un núcleo, en varias pasadas.

.DESCRIPTION
    Es la sonda de las medidas de docs/specs/lyrics-progressive.md (M0-M5): en cada pasada
    espera unos segundos de estabilización y luego compara el tiempo de CPU del proceso al
    principio y al final de la ventana de medida.

    Solo lee contadores del proceso. No lanza la app, no toca settings.json ni la biblioteca
    y no usa la red.

    Si el proceso no existe, hay varios con ese nombre o termina durante la medida, lo dice,
    no escribe ninguna cifra y sale con código 1.

.EXAMPLE
    .\tools\medir-cpu.ps1
    Cinco pasadas de MusicPlayer.Desktop (3 s de espera y 10 s de medida cada una).

.EXAMPLE
    .\tools\medir-cpu.ps1 -Etiqueta 'enhanced' -Pasadas 5
    Igual, y la fila para la tabla de la spec sale con esa etiqueta.

.EXAMPLE
    .\tools\medir-cpu.ps1 -Id 12345 -Pasadas 1
    Una pasada del proceso con ese Id.
#>
[CmdletBinding()]
param(
    # Nombre del proceso, sin «.exe». Se ignora si se da -Id.
    [string]$Proceso = 'MusicPlayer.Desktop',

    # Id del proceso. Hace falta si hay varios con el mismo nombre.
    [int]$Id = 0,

    [ValidateRange(1, 100)]
    [int]$Pasadas = 5,

    # Segundos de estabilización antes de cada pasada.
    [ValidateRange(0, 600)]
    [double]$Espera = 3,

    # Segundos que dura cada medida.
    [ValidateRange(1, 600)]
    [double]$Duracion = 10,

    # Cuenta atrás antes de la primera pasada, para dar tiempo a volver a la app.
    [ValidateRange(0, 600)]
    [double]$Inicio = 0,

    # Texto de la primera columna en la fila para la tabla de la spec.
    [string]$Etiqueta = ''
)

$ErrorActionPreference = 'Stop'
$cultura = [System.Globalization.CultureInfo]::GetCultureInfo('es-ES')

function Salir([string]$mensaje) {
    [Console]::Error.WriteLine($mensaje)
    exit 1
}

function Formato([double]$valor) {
    return $valor.ToString('0.0', $cultura)
}

# --- Buscar el proceso ---
if ($Id -gt 0) {
    $encontrados = @(Get-Process -Id $Id -ErrorAction SilentlyContinue)
    if ($encontrados.Count -eq 0) { Salir "No hay ningún proceso con Id $Id. No se ha medido nada." }
}
else {
    $nombre = $Proceso -replace '\.exe$', ''
    $encontrados = @(Get-Process -Name $nombre -ErrorAction SilentlyContinue)
    if ($encontrados.Count -eq 0) { Salir "No hay ningún proceso llamado «$nombre». No se ha medido nada." }
    if ($encontrados.Count -gt 1) {
        $ids = ($encontrados | ForEach-Object { $_.Id }) -join ', '
        Salir "Hay $($encontrados.Count) procesos llamados «$nombre» (Id: $ids). Elige uno con -Id. No se ha medido nada."
    }
}
$proc = $encontrados[0]
Write-Host "Proceso: $($proc.ProcessName) (Id $($proc.Id)). $Pasadas pasada(s) de $Duracion s tras $Espera s de espera."

if ($Inicio -gt 0) {
    Write-Host "Empieza en $Inicio s..."
    Start-Sleep -Milliseconds ([int]($Inicio * 1000))
}

# --- Medir ---
$resultados = New-Object System.Collections.Generic.List[double]
for ($i = 1; $i -le $Pasadas; $i++) {
    Start-Sleep -Milliseconds ([int]($Espera * 1000))

    $proc.Refresh()
    if ($proc.HasExited) { Salir "El proceso terminó antes de la pasada $i. No se escribe ninguna cifra." }
    try { $cpuAntes = $proc.TotalProcessorTime }
    catch { Salir "No se pudo leer el tiempo de CPU del proceso (¿terminó o falta permiso?). No se escribe ninguna cifra." }
    $reloj = [System.Diagnostics.Stopwatch]::StartNew()

    Start-Sleep -Milliseconds ([int]($Duracion * 1000))

    $proc.Refresh()
    if ($proc.HasExited) { Salir "El proceso terminó durante la pasada $i. No se escribe ninguna cifra." }
    try { $cpuDespues = $proc.TotalProcessorTime }
    catch { Salir "No se pudo leer el tiempo de CPU del proceso (¿terminó o falta permiso?). No se escribe ninguna cifra." }
    $reloj.Stop()

    # Porcentaje de un núcleo: 100 es un núcleo entero ocupado durante toda la ventana.
    $porcentaje = ($cpuDespues - $cpuAntes).TotalMilliseconds / $reloj.Elapsed.TotalMilliseconds * 100
    $resultados.Add($porcentaje)
    Write-Host ("  Pasada {0}: {1} %" -f $i, (Formato $porcentaje))
}

# --- Resumen ---
$media = ($resultados | Measure-Object -Average).Average
Write-Host ("Media: {0} % de un núcleo" -f (Formato $media))

# Una pasada atípica (más del doble de la media de las demás) se repite y se anota.
if ($resultados.Count -ge 3) {
    for ($i = 0; $i -lt $resultados.Count; $i++) {
        $resto = @(for ($j = 0; $j -lt $resultados.Count; $j++) { if ($j -ne $i) { $resultados[$j] } })
        $mediaResto = ($resto | Measure-Object -Average).Average
        if ($mediaResto -gt 0 -and $resultados[$i] -gt 2 * $mediaResto) {
            Write-Warning ("La pasada {0} ({1} %) es atípica: más del doble de la media de las demás ({2} %). Repite la medida y anótalo." -f `
                ($i + 1), (Formato $resultados[$i]), (Formato $mediaResto))
        }
    }
}

# Fila lista para pegar en la tabla de la spec. Es lo único que sale por la salida estándar.
$lista = ($resultados | ForEach-Object { Formato $_ }) -join ' / '
"| $Etiqueta | $lista | $(Formato $media) |"
