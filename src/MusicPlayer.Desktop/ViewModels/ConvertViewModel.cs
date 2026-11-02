using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.ViewModels;

public partial class ConvertViewModel(ConversionService conversion, LibraryService library) : ObservableObject
{
    private IReadOnlyList<Track> _scope = Array.Empty<Track>();
    private CancellationTokenSource? _cts;

    /// <summary>Se agregaron archivos nuevos: la biblioteca ya actualizada.</summary>
    public event Action<LibrarySnapshot>? Added;

    public event Action<string>? Notified;

    public IReadOnlyList<AudioFormat> Formats { get; } = Enum.GetValues<AudioFormat>();
    public IReadOnlyList<int> Bitrates { get; } = new[] { 128, 192, 256, 320 };

    public bool HasConverter => conversion.IsAvailable;
    public bool UsesBitrate => Format is AudioFormat.Mp3 or AudioFormat.M4a;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _heading = "";
    [ObservableProperty] private bool _addToLibrary = true;
    [ObservableProperty] private int _bitrate = 192;
    [ObservableProperty] private string _progressText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesBitrate))]
    private AudioFormat _format = AudioFormat.Mp3;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _isBusy;

    public void Begin(IReadOnlyList<Track> scope, string label)
    {
        if (IsBusy) return;

        if (scope.Count == 0)
        {
            Notified?.Invoke("No hay canciones disponibles para convertir");
            return;
        }

        _scope = scope;
        Heading = scope.Count == 1
            ? $"Convertir \"{label}\""
            : $"Convertir {Plural.Of(scope.Count, "canción", "canciones")}";
        ProgressText = "";

        OnPropertyChanged(nameof(HasConverter)); // por si se instaló ffmpeg desde la última vez
        StartCommand.NotifyCanExecuteChanged();
        IsOpen = true;
    }

    private bool CanStart() => !IsBusy && HasConverter;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        IsBusy = true;
        _cts = new CancellationTokenSource();
        var total = _scope.Count;

        try
        {
            ProgressText = $"Convirtiendo… 0 de {total}";
            var progress = new Progress<int>(n => ProgressText = $"Convirtiendo… {n} de {total}");

            var result = await conversion.ConvertAsync(
                _scope, new ConversionOptions(Format, Bitrate), progress, _cts.Token);

            if (AddToLibrary && result.Created.Count > 0)
                Added?.Invoke(await library.AddFilesAsync(result.Created));

            IsOpen = false;
            Notified?.Invoke(Summarize(result)); // después de Added: ApplySnapshot escribe el estado general
        }
        catch (Exception ex)
        {
            Notified?.Invoke($"No se pudo convertir: {ex.Message}");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
        }
    }

    // Durante la conversión cancela el proceso; si no, solo cierra el panel.
    [RelayCommand]
    private void Cancel()
    {
        if (_cts is not null) _cts.Cancel();
        else IsOpen = false;
    }

    private static string Summarize(ConversionBatchResult r)
    {
        var parts = new List<string>
        {
            Plural.Of(r.Created.Count, "canción convertida", "canciones convertidas")
        };
        if (r.Skipped > 0) parts.Add($"{r.Skipped} ya estaban en ese formato");
        if (r.Errors.Count > 0) parts.Add($"{Plural.Of(r.Errors.Count, "error", "errores")}: {r.Errors[0]}");
        if (r.Cancelled) parts.Add("cancelado");
        return string.Join("; ", parts);
    }
}