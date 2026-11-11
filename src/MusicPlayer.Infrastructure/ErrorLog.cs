using System.Text;

namespace MusicPlayer.Infrastructure;

/// <summary>
/// Registro local de errores inesperados (docs/specs/playback-resilience.md, criterio 3). Es texto para leer,
/// no datos del usuario: no usa <c>AtomicJson</c>. Escribir nunca lanza: un registro que falla no debe tumbar la app.
/// </summary>
public sealed class ErrorLog(string path, Func<DateTimeOffset>? clock = null)
{
    public const int MaxBytes = 256 * 1024;

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.Now);
    private readonly object _gate = new();
    private string? _lastKey;
    private int _repeats;

    /// <param name="source">De dónde viene (por ejemplo «interfaz» o «tarea»).</param>
    public void Write(string source, Exception error)
    {
        try
        {
            lock (_gate)
            {
                // El mismo error seguido (uno por fotograma, por ejemplo) se anota una vez y luego se cuenta.
                var key = $"{source}|{error.GetType().FullName}|{error.Message}";
                if (key == _lastKey)
                {
                    _repeats++;
                    return;
                }

                var text = new StringBuilder();
                if (_repeats > 0)
                    text.AppendLine($"  (el error anterior se repitió {_repeats} veces más)");
                text.AppendLine($"[{_clock():yyyy-MM-dd HH:mm:ss}] {source}: {error.GetType().FullName}: {error.Message}");
                text.AppendLine(error.ToString());
                text.AppendLine();

                _lastKey = key;
                _repeats = 0;

                if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
                    Directory.CreateDirectory(directory);
                File.AppendAllText(path, text.ToString(), Encoding.UTF8);
                Trim();
            }
        }
        catch (Exception)
        {
            // Sin registro, pero con la app en pie.
        }
    }

    // Al pasar del tope se conserva la mitad más reciente, desde el inicio de una línea.
    private void Trim()
    {
        if (new FileInfo(path).Length <= MaxBytes) return;

        var bytes = File.ReadAllBytes(path);
        var start = bytes.Length - MaxBytes / 2;
        var newline = Array.IndexOf(bytes, (byte)'\n', start);
        if (newline >= 0) start = newline + 1;
        File.WriteAllBytes(path, bytes.AsSpan(start).ToArray());
    }
}
