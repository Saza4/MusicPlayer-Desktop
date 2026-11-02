using System.Diagnostics;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

/// <summary>Convierte con el ffmpeg instalado en el equipo. No se redistribuye ni usa la red.</summary>
public sealed class FfmpegAudioConverter : IAudioConverter
{
    public bool IsAvailable => Locate() is not null;

    public async Task ConvertAsync(string input, string output, ConversionOptions options, CancellationToken ct = default)
    {
        var ffmpeg = Locate() ?? throw new InvalidOperationException("No se encontró ffmpeg.");

        // Se escribe a un temporal y se renombra al final: cancelar o fallar no deja archivos a medias.
        var directory = Path.GetDirectoryName(Path.GetFullPath(output))!;
        var temp = Path.Combine(directory, $"~conv-{Guid.NewGuid():N}{Path.GetExtension(output)}");

        try
        {
            var psi = new ProcessStartInfo(ffmpeg)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            // ArgumentList: cada ruta viaja como un solo argumento, sin pasar por ningún intérprete de comandos.
            foreach (var argument in BuildArguments(input, temp, options))
                psi.ArgumentList.Add(argument);

            using var process = Process.Start(psi) ?? throw new IOException("No se pudo iniciar ffmpeg.");
            var stderr = process.StandardError.ReadToEndAsync();
            var stdout = process.StandardOutput.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                process.WaitForExit(2000);
                throw;
            }

            await Task.WhenAll(stderr, stdout);
            if (process.ExitCode != 0)
                throw new IOException(LastLine(await stderr));

            File.Move(temp, output); // lanza si ya existe: nunca sobrescribe
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
        }
    }

    /// <summary>Argumentos de ffmpeg. Público para poder probarlos sin ejecutar nada.</summary>
    public static IReadOnlyList<string> BuildArguments(string input, string output, ConversionOptions options)
    {
        var args = new List<string>
        {
            "-hide_banner", "-nostdin", "-loglevel", "error", "-n",
            "-i", input,
            "-map", "0:a:0",        // el primer flujo de audio
            "-map_metadata", "0"    // título, artista, álbum...
        };

        if (options.Format == AudioFormat.Wav)
        {
            args.AddRange(new[] { "-vn", "-c:a", "pcm_s16le" }); // WAV no lleva carátula
        }
        else
        {
            // La carátula (si hay) se copia sin recodificar. El "?" la hace opcional.
            args.AddRange(new[] { "-map", "0:v:0?", "-c:v", "copy", "-disposition:v:0", "attached_pic" });

            var bitrate = $"{options.BitrateKbps}k";
            switch (options.Format)
            {
                case AudioFormat.Mp3:
                    args.AddRange(new[] { "-c:a", "libmp3lame", "-b:a", bitrate, "-id3v2_version", "3" });
                    break;
                case AudioFormat.M4a:
                    args.AddRange(new[] { "-c:a", "aac", "-b:a", bitrate });
                    break;
                default:
                    args.AddRange(new[] { "-c:a", "flac" });
                    break;
            }
        }

        args.Add(output);
        return args;
    }

    private static string LastLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? "ffmpeg falló sin dar detalles";

    // Primero junto al ejecutable (por si lo copias ahí) y luego en el PATH.
    private static string? Locate()
    {
        var name = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        var local = Path.Combine(AppContext.BaseDirectory, name);
        if (File.Exists(local)) return local;

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), name);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { } // entrada del PATH con caracteres inválidos
        }
        return null;
    }
}