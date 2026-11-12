using System.Text.Json;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Infrastructure;

/// <summary>
/// Lee los temas de usuario (<c>*.json</c>) de una carpeta. No lanza excepciones: cualquier problema de un
/// archivo se informa como error de ese archivo y los demás siguen cargándose. Las reglas viven en <see cref="ThemeFactory"/>.
/// </summary>
public sealed class JsonThemeRepository(string folder) : IThemeRepository
{
    public const int MaxFiles = 50;
    public const long MaxFileBytes = 64 * 1024;

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public async Task<ThemeLoadReport> LoadAllAsync(CancellationToken ct = default)
    {
        var themes = new List<AppTheme>();
        var errors = new List<ThemeIssue>();
        var warnings = new List<ThemeIssue>();

        string[] files;
        try
        {
            if (!Directory.Exists(folder))
                return ThemeLoadReport.Empty;

            files = Directory.EnumerateFiles(folder, "*.json")
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ThemeLoadReport([], [new ThemeIssue(folder, $"no se pudo leer la carpeta ({ex.Message})")], []);
        }

        if (files.Length > MaxFiles)
            warnings.Add(new ThemeIssue(Path.GetFileName(folder), $"hay {files.Length} archivos; solo se leen los primeros {MaxFiles}"));

        foreach (var path in files.Take(MaxFiles))
        {
            ct.ThrowIfCancellationRequested();
            var file = Path.GetFileName(path);

            try
            {
                var result = await ReadOneAsync(path, ct);

                foreach (var error in result.Errors) errors.Add(new ThemeIssue(file, error));
                foreach (var warning in result.Warnings) warnings.Add(new ThemeIssue(file, warning));
                if (result.Theme is not null) themes.Add(result.Theme);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            // InvalidOperationException: un texto con un sustituto UTF-16 suelto escapado («\uD800») pasa el
            // análisis y lanza al leerlo (docs/specs/audit-followup.md, criterio 4).
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                          or InvalidOperationException)
            {
                errors.Add(new ThemeIssue(file, ex is IOException or UnauthorizedAccessException
                    ? $"no se pudo leer ({ex.Message})"
                    : "JSON mal formado"));
            }
        }

        return new ThemeLoadReport(themes, errors, warnings);
    }

    private static async Task<ThemeResult> ReadOneAsync(string path, CancellationToken ct)
    {
        var length = new FileInfo(path).Length;
        if (length > MaxFileBytes)
            return Fail($"supera el tamaño máximo ({MaxFileBytes / 1024} KB)");

        var bytes = await File.ReadAllBytesAsync(path, ct);
        var start = bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0; // BOM UTF-8

        if (bytes.Length == start)
            return Fail("el archivo está vacío");

        using var doc = JsonDocument.Parse(bytes.AsMemory(start), ParseOptions);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return Fail("la raíz debe ser un objeto JSON");

        int? version = null;
        string? name = null, baseName = null;
        Dictionary<string, string?>? colors = null;

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "formatversion":
                    version = property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var v) ? v : -1;
                    break;
                case "name":
                    name = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                    break;
                case "base":
                    // Un valor que no es texto no vale 'dark' por defecto: es un error de la base.
                    baseName = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : "?";
                    break;
                case "colors" when property.Value.ValueKind == JsonValueKind.Object:
                    colors = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var color in property.Value.EnumerateObject())
                        colors[color.Name] = color.Value.ValueKind == JsonValueKind.String ? color.Value.GetString() : null;
                    break;
            }
        }

        var id = Path.GetFileNameWithoutExtension(path);
        return ThemeFactory.Create(id, version, name, baseName, colors);
    }

    private static ThemeResult Fail(string message) => new(null, [message], []);
}
