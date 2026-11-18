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

public sealed class CoverArtLoader(ICoverArtProvider provider, ICoverArtCache cache)
{
    private readonly LruCache<string, Bitmap> _thumbnails = new(400, StringComparer.OrdinalIgnoreCase);
    // Qué se está cargando, qué no tiene carátula y qué se invalidó a mitad (solo hilo de UI).
    private readonly ThumbnailRequests _requests = new(comparer: StringComparer.OrdinalIgnoreCase);

    // Tarjetas de la página de artista (docs/specs/redesign-mockups.md, tercera pasada): a 512 px en lugar de la
    // miniatura de 96, que a 156 en pantalla se veía pixelada. Unos 48 MB como mucho; se vacía al salir de la vista.
    private readonly LruCache<string, Bitmap> _cards = new(48, StringComparer.OrdinalIgnoreCase);
    private readonly ThumbnailRequests _cardRequests = new(comparer: StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _gate = new(4);

    /// <summary>
    /// Devuelve la miniatura si ya está en caché. Si no, la carga en segundo plano,
    /// devuelve null y llama a <paramref name="onLoaded"/> (en el hilo de UI) al terminar.
    /// </summary>
    public Bitmap? GetThumbnail(Track track, Action onLoaded) =>
        Get(track, _thumbnails, _requests, CoverSizes.ThumbnailWidth, onLoaded);

    /// <summary>Como <see cref="GetThumbnail"/>, pero a la resolución de la tarjeta de álbum.</summary>
    public Bitmap? GetCard(Track track, Action onLoaded) =>
        Get(track, _cards, _cardRequests, CoverSizes.CardDecodeWidth, onLoaded);

    /// <summary>
    /// Suelta las tarjetas al salir de la página de artista. Las imágenes que aún se muestran siguen vivas mientras
    /// algo las use; las cargas en curso terminan y no se guardan.
    /// </summary>
    public void ReleaseCards()
    {
        _cards.Clear();
        _cardRequests.Clear();
    }

    private Bitmap? Get(Track track, LruCache<string, Bitmap> bitmaps, ThumbnailRequests requests, int width,
        Action onLoaded)
    {
        if (bitmaps.TryGet(track.FilePath, out var bitmap))
            return bitmap;

        // Null si ya se está cargando o si ya se sabe que no tiene: no se vuelve a leer el disco al desplazar.
        if (requests.Begin(track.FilePath) is { } ticket)
            _ = LoadAsync(track, bitmaps, requests, width, ticket, onLoaded);

        return null;
    }

    public async Task<Bitmap?> LoadLargeAsync(Track track, CancellationToken ct = default)
    {
        var bytes = await ReadBytesAsync(track, ct);
        return bytes is null ? null : await DecodeAsync(bytes, CoverSizes.LargeWidth, ct);
    }

    private async Task LoadAsync(Track track, LruCache<string, Bitmap> bitmaps, ThumbnailRequests requests, int width,
        int ticket, Action onLoaded)
    {
        Bitmap? bitmap = null;
        await _gate.WaitAsync();
        try
        {
            var bytes = await ReadBytesAsync(track, CancellationToken.None);
            if (bytes is not null)
                bitmap = await DecodeAsync(bytes, width, CancellationToken.None);
        }
        catch (Exception)
        {
            // La carátula es decorativa: un fallo aquí nunca debe afectar a la lista.
        }
        finally
        {
            _gate.Release();
        }

        // Si la carátula se invalidó mientras se cargaba, este resultado es el viejo: ni se guarda ni se avisa
        // (docs/specs/ui-responsiveness.md, criterio 7). La fila nueva ya lanzó su propia carga.
        if (!requests.Complete(track.FilePath, ticket, found: bitmap is not null))
        {
            bitmap?.Dispose();
            return;
        }
        if (bitmap is null) return;

        bitmaps.Set(track.FilePath, bitmap);
        onLoaded();
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

    public void Invalidate(string filePath)
    {
        cache.Invalidate(filePath);
        _thumbnails.Remove(filePath);
        _requests.Invalidate(filePath);
        _cards.Remove(filePath);
        _cardRequests.Invalidate(filePath);
    }
}