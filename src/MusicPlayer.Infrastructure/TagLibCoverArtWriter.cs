using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

public sealed class TagLibCoverArtWriter : ICoverArtWriter
{
    public void Write(string filePath, byte[]? image)
    {
        var mime = image is null
            ? null
            : CoverImage.DetectMime(image) ?? throw new ArgumentException("La imagen debe ser JPG o PNG.");

        SafeFileEdit.Run(filePath, temp =>
        {
            Apply(temp, image, mime);
            Verify(temp, image);
        });
    }

    private static void Apply(string path, byte[]? image, string? mime)
    {
        using var file = TagLib.File.Create(path);

        file.Tag.Pictures = image is null
            ? Array.Empty<TagLib.IPicture>()
            : new TagLib.IPicture[]
            {
                new TagLib.Picture(new TagLib.ByteVector(image))
                {
                    Type = TagLib.PictureType.FrontCover,
                    MimeType = mime,
                    Description = ""
                }
            };

        file.Save();
    }

    private static void Verify(string path, byte[]? image)
    {
        using var file = TagLib.File.Create(path);
        var pictures = file.Tag.Pictures;

        var ok = image is null
            ? pictures.Length == 0
            : pictures.Length > 0 && pictures[0].Data.Count == image.Length;

        if (!ok)
            throw new IOException("La verificación tras escribir falló. El archivo original no se modificó.");
    }
}