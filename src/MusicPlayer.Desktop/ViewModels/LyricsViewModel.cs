using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.ViewModels;

public partial class LyricsViewModel(ILyricsProvider provider, IAudioPlayer player, SettingsService settings)
    : ObservableObject
{
    private LyricBlock _block = LyricBlock.Empty;
    private CancellationTokenSource? _cts;
    private bool _restoring;

    [ObservableProperty] private ObservableCollection<LyricLineViewModel> _lines = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProgressiveImmersive), nameof(ShowClassicImmersive))]
    private bool _isSynced;

    [ObservableProperty] private int _activeIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPanel), nameof(ShowClassicImmersive))]
    private bool _isAvailable;

    // Spike M0 de letras progresivas (docs/specs/lyrics-progressive.md). Solo en Debug y con
    // MUSICPLAYER_LYRICS_SPIKE=1; en M4 lo sustituye el ajuste LyricsStyle.
#if DEBUG
    public static bool UseProgressiveSpike { get; } =
        Environment.GetEnvironmentVariable("MUSICPLAYER_LYRICS_SPIKE") == "1";
#else
    public static bool UseProgressiveSpike => false;
#endif

    /// <summary>Modo inmersivo: letra progresiva (solo letras sincronizadas).</summary>
    public bool ShowProgressiveImmersive => UseProgressiveSpike && IsSynced;

    /// <summary>Modo inmersivo: lista clásica (letras sin sincronizar, o estilo clásico).</summary>
    public bool ShowClassicImmersive => IsAvailable && !ShowProgressiveImmersive;

    /// <summary>Posición actual del reproductor, para el relleno fotograma a fotograma.</summary>
    public TimeSpan CurrentPosition => player.CurrentTime;

    /// <summary>Índice de la línea que suena en <paramref name="position"/>; -1 antes de la primera.</summary>
    public int ActiveIndexAt(TimeSpan position) => _block.GetActiveLineIndex(position);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPanel))]
    private bool _isOpen;

    // Lo que la canción actual tiene disponible.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExtras))]
    private bool _hasTranslation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExtras))]
    private bool _hasPronunciation;

    // Preferencias del usuario persistidas mediante SettingsService.
    [ObservableProperty] private bool _showTranslation;
    [ObservableProperty] private bool _showPronunciation;
    [ObservableProperty] private bool _hideOriginal;
    [ObservableProperty] private bool _emphasizePronunciation;

    /// <summary>El panel solo se muestra si el usuario lo abrió y hay letra.</summary>
    public bool ShowPanel => IsOpen && IsAvailable;

    public bool HasExtras => HasTranslation || HasPronunciation;

    partial void OnShowTranslationChanged(bool value) => OnPreferenceChanged();
    partial void OnShowPronunciationChanged(bool value) => OnPreferenceChanged();
    partial void OnHideOriginalChanged(bool value) => OnPreferenceChanged();
    partial void OnEmphasizePronunciationChanged(bool value) => OnPreferenceChanged();

    private void OnPreferenceChanged()
    {
        ApplyDisplay();
        if (_restoring) return; // Al restaurar no hay nada nuevo que guardar
        settings.Update(s => s with
        {
            ShowTranslation = ShowTranslation,
            ShowPronunciation = ShowPronunciation,
            HideOriginal = HideOriginal,
            EmphasizePronunciation = EmphasizePronunciation
        });
    }

    public void Restore(AppSettings saved)
    {
        _restoring = true;
        try
        {
            ShowTranslation = saved.ShowTranslation;
            ShowPronunciation = saved.ShowPronunciation;
            HideOriginal = saved.HideOriginal;
            EmphasizePronunciation = saved.EmphasizePronunciation;
        }
        finally
        {
            _restoring = false;
        }
    }

    public async Task LoadAsync(Track track)
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();

        SetBlock(LyricBlock.Empty);

        try
        {
            var block = await provider.GetLyricsAsync(track, cts.Token);
            if (!cts.IsCancellationRequested)
                SetBlock(block);
        }
        catch (OperationCanceledException)
        {
            // Se cambió de canción antes de terminar: el resultado ya no importa.
        }
        catch (Exception)
        {
            // La letra es opcional: un fallo aquí nunca debe afectar la reproducción.
            if (!cts.IsCancellationRequested)
                SetBlock(LyricBlock.Empty);
        }
    }

    /// <summary>Marca la línea activa para la posición dada. Barato: solo actúa si cambia el índice.</summary>
    public void UpdatePosition(TimeSpan position)
    {
        if (!IsSynced) return;

        var index = _block.GetActiveLineIndex(position);
        if (index == ActiveIndex) return;

        if (ActiveIndex >= 0 && ActiveIndex < Lines.Count)
            Lines[ActiveIndex].IsActive = false;
        if (index >= 0 && index < Lines.Count)
            Lines[index].IsActive = true;

        ActiveIndex = index;
    }

    [RelayCommand]
    private void ToggleOpen() => IsOpen = !IsOpen;

    [RelayCommand]
    private void SeekToLine(LyricLineViewModel? line)
    {
        if (line is not { IsSynced: true }) return;

        player.Seek(line.Line.Timestamp);
        UpdatePosition(line.Line.Timestamp); // También funciona con la reproducción en pausa
    }

    private void SetBlock(LyricBlock block)
    {
        _block = block;
        Lines = new ObservableCollection<LyricLineViewModel>(
            block.Lines.Select(l => new LyricLineViewModel(l, block.Kind == LyricsKind.Synced)));
        IsSynced = block.Kind == LyricsKind.Synced;
        IsAvailable = block.IsAvailable;
        HasTranslation = block.Lines.Any(l => !string.IsNullOrWhiteSpace(l.Translation));
        HasPronunciation = block.Lines.Any(l => !string.IsNullOrWhiteSpace(l.Romanization));
        ActiveIndex = -1;
        ApplyDisplay();
    }

    private void ApplyDisplay()
    {
        var options = new LyricsDisplayOptions(
            ShowTranslation, ShowPronunciation, HideOriginal, EmphasizePronunciation);
        var layout = options.Resolve(HasTranslation, HasPronunciation);

        foreach (var line in Lines)
            line.ApplyLayout(layout);
    }

    /// <summary>Descarta la letra actual (por ejemplo, al detener la reproducción).</summary>
    public void Clear()
    {
        _cts?.Cancel(); // evita que una carga en curso reponga la letra
        SetBlock(LyricBlock.Empty);
    }
}