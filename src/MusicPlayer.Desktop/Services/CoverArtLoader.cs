using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.Services;

public sealed class CoverArtLoader(ICoverArtProvider provider)
{
    private const int ThumbnailWidth = 96;
    private const int LargeWidth = 640;

    private readonly LruCache<string, Bitmap> _thumbnails = new(400, StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loading = new(StringComparer.OrdinalIgnoreCase); // solo hilo de UI
    private readonly SemaphoreSlim _gate = new(4);

    /// <summary>
    /// Devuelve la miniatura si ya está en caché. Si no, la carga en segundo plano,
    /// devuelve null y llama a <paramref name="onLoaded"/> (en el hilo de UI) al terminar.
    /// </summary>
    public Bitmap? GetThumbnail(Track track, Action onLoaded)
    {
        if (_thumbnails.TryGet(track.FilePath, out var bitmap))
            return bitmap;

        if (_loading.Add(track.FilePath))
            _ = LoadThumbnailAsync(track, onLoaded);

        return null;
    }

    public async Task<Bitmap?> LoadLargeAsync(Track track, CancellationToken ct = default)
    {
        var bytes = await ReadBytesAsync(track, ct);
        return bytes is null ? null : await DecodeAsync(bytes, LargeWidth, ct);
    }

    private async Task LoadThumbnailAsync(Track track, Action onLoaded)
    {
        await _gate.WaitAsync();
        try
        {
            var bytes = await ReadBytesAsync(track, CancellationToken.None);
            if (bytes is null) return;

            var bitmap = await DecodeAsync(bytes, ThumbnailWidth, CancellationToken.None);
            if (bitmap is null) return;

            _thumbnails.Set(track.FilePath, bitmap);
            onLoaded();
        }
        catch (Exception)
        {
            // La carátula es decorativa: un fallo aquí nunca debe afectar a la lista.
        }
        finally
        {
            _gate.Release();
            _loading.Remove(track.FilePath);
        }
    }

    // Task.Run saca toda la cadena (disco, TagLib) del hilo de UI.
    private Task<byte[]?> ReadBytesAsync(Track track, CancellationToken ct) =>
        Task.Run(() => provider.GetCoverAsync(track, ct), ct);

    private static Task<Bitmap?> DecodeAsync(byte[] bytes, int width, CancellationToken ct) =>
        Task.Run<Bitmap?>(() =>
        {
            try
            {
                using var stream = new MemoryStream(bytes);
                return Bitmap.DecodeToWidth(stream, width);
            }
            catch (Exception)
            {
                return null; // imagen corrupta o formato no soportado
            }
        }, ct);
}