using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class ConversionService(IAudioConverter converter)
{
    public bool IsAvailable => converter.IsAvailable;

    /// <summary>Convierte en orden. Un fallo no detiene el resto; al cancelar se conserva lo ya convertido.</summary>
    public async Task<ConversionBatchResult> ConvertAsync(
        IReadOnlyList<Track> tracks, ConversionOptions options,
        IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var created = new List<string>();
        var errors = new List<string>();
        var skipped = 0;
        var done = 0;

        foreach (var track in tracks)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                if (ConversionPlanner.IsAlreadyFormat(track.FilePath, options.Format))
                    skipped++;
                else if (!File.Exists(track.FilePath))
                    errors.Add($"{Path.GetFileName(track.FilePath)}: el archivo no existe");
                else
                {
                    var output = ConversionPlanner.OutputPath(track.FilePath, options.Format, File.Exists);
                    await converter.ConvertAsync(track.FilePath, output, options, ct);
                    created.Add(output);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) // el conversor es una abstracción: cualquier fallo es "no se pudo convertir"
            {
                errors.Add($"{Path.GetFileName(track.FilePath)}: {ex.Message}");
            }

            progress?.Report(++done);
        }

        return new ConversionBatchResult(created, skipped, errors, ct.IsCancellationRequested);
    }
}