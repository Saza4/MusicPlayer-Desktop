using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
    private TimeSpan? _trackDuration;
    private CancellationTokenSource? _cts;
    private bool _restoring;

    [ObservableProperty] private ObservableCollection<LyricLineViewModel> _lines = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProgressiveImmersive), nameof(ShowClassicImmersive), nameof(ShowLayersButton))]
    private bool _isSynced;

    [ObservableProperty] private int _activeIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPanel), nameof(ShowClassicImmersive))]
    private bool _isAvailable;

    // Modo inmersivo (docs/specs/lyrics-progressive.md): la letra sincronizada va siempre con el estilo
    // progresivo; la letra sin tiempos (Plain) sigue en la lista clásica. El panel lateral es siempre la lista.

    /// <summary>Modo inmersivo: letra progresiva (letras sincronizadas).</summary>
    public bool ShowProgressiveImmersive => IsSynced;

    /// <summary>Modo inmersivo: lista clásica (letras sin sincronizar).</summary>
    public bool ShowClassicImmersive => IsAvailable && !IsSynced;

    /// <summary>Posición actual del reproductor, para el relleno fotograma a fotograma.</summary>
    public TimeSpan CurrentPosition => player.CurrentTime;

    /// <summary>
    /// Líneas y pausas instrumentales de la letra progresiva. Se reconstruye antes de cambiar <see cref="Lines"/>,
    /// así que al recibir el cambio de <see cref="Lines"/> ya es la de la letra nueva.
    /// </summary>
    public LyricsTimeline Timeline { get; private set; } = LyricsTimeline.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPanel))]
    private bool _isOpen;

    // Lo que la canción actual tiene disponible.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExtras), nameof(ShowLayersButton))]
    private bool _hasTranslation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExtras), nameof(ShowLayersButton))]
    private bool _hasPronunciation;

    /// <summary>Código de la traducción mostrada («en»), para la etiqueta del menú de capas.</summary>
    [ObservableProperty] private string? _translationLanguage;

    // Preferencias del usuario persistidas mediante SettingsService.
    [ObservableProperty] private bool _showTranslation;
    [ObservableProperty] private bool _showPronunciation;
    [ObservableProperty] private bool _hideOriginal;
    [ObservableProperty] private bool _emphasizePronunciation;

    /// <summary>El panel solo se muestra si el usuario lo abrió y hay letra.</summary>
    public bool ShowPanel => IsOpen && IsAvailable;

    public bool HasExtras => HasTranslation || HasPronunciation;

    /// <summary>Botón de capas del modo inmersivo (docs/specs/lyrics-layers.md, criterio 12).</summary>
    public bool ShowLayersButton => LyricsLayerMenu.IsButtonVisible(IsSynced, HasTranslation, HasPronunciation);

    /// <summary>Opciones del menú de capas con el estado de ahora (criterio 13). Se recalculan cada vez.</summary>
    public IReadOnlyList<LyricsLayerMenuItem> LayerMenu() =>
        LyricsLayerMenu.Build(CurrentOptions(), HasTranslation, HasPronunciation,
            TranslationLanguage, CultureInfo.CurrentUICulture.Name);

    /// <summary>
    /// Aplica una opción del menú de capas, recalculada con el estado de ahora: si la capa ya no existe (cambió la
    /// pista con el menú abierto) o la opción dejaría todo oculto, no hace nada.
    /// </summary>
    public void ApplyLayerOption(LyricsRow row)
    {
        if (LayerMenu().FirstOrDefault(i => i.Row == row) is not { IsEnabled: true } item) return;

        // Las mismas preferencias que el panel lateral (criterio 14): cada cambio se guarda como allí.
        ShowTranslation = item.Result.ShowTranslation;
        ShowPronunciation = item.Result.ShowPronunciation;
        HideOriginal = item.Result.HideOriginal;
    }

    private LyricsDisplayOptions CurrentOptions() =>
        new(ShowTranslation, ShowPronunciation, HideOriginal, EmphasizePronunciation);

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

        _trackDuration = track.Duration > TimeSpan.Zero ? track.Duration : null;
        SetBlock(LyricBlock.Empty);

        try
        {
            var block = await provider.GetLyricsAsync(track, cts.Token);
#if DEBUG
            // Pruebas de la letra progresiva con un .lrc sintético, sin letra real ni tocar la biblioteca.
            // Si junto a él hay «.es.lrc» o «.rom.lrc», se añaden como traducción y pronunciación.
            if (Environment.GetEnvironmentVariable("MUSICPLAYER_LYRICS_OVERRIDE") is { Length: > 0 } overridePath)
            {
                var parser = new LrcLyricParser();
                async Task<LyricBlock?> Companion(string suffix)
                {
                    var path = System.IO.Path.ChangeExtension(overridePath, suffix);
                    return System.IO.File.Exists(path)
                        ? parser.Parse(await System.IO.File.ReadAllTextAsync(path, cts.Token))
                        : null;
                }
                block = parser.Parse(await System.IO.File.ReadAllTextAsync(overridePath, cts.Token));
                var translation = await Companion(".es.lrc");
                var pronunciation = await Companion(".rom.lrc");
                if (translation is not null || pronunciation is not null)
                    block = LyricsMerger.Merge(block, translation, pronunciation) with
                    {
                        TranslationLanguage = translation is null ? null : "es"
                    };
            }
#endif
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
        SeekTo(line.Line.Timestamp);
    }

    /// <summary>Salta a <paramref name="position"/> (una línea o una pausa de la letra progresiva).</summary>
    public void SeekTo(TimeSpan position)
    {
        if (!IsSynced) return;
        player.Seek(position);
        UpdatePosition(position); // También funciona con la reproducción en pausa
    }

    private void SetBlock(LyricBlock block)
    {
        _block = block;
        Timeline = LyricsTimeline.Build(block, _trackDuration);
        Lines = new ObservableCollection<LyricLineViewModel>(
            block.Lines.Select(l => new LyricLineViewModel(l, block.Kind == LyricsKind.Synced)));
        IsSynced = block.Kind == LyricsKind.Synced;
        IsAvailable = block.IsAvailable;
        HasTranslation = block.Lines.Any(l => !string.IsNullOrWhiteSpace(l.Translation));
        HasPronunciation = block.Lines.Any(l => !string.IsNullOrWhiteSpace(l.Romanization));
        TranslationLanguage = HasTranslation ? block.TranslationLanguage : null;
        ActiveIndex = -1;
        ApplyDisplay();
    }

    private void ApplyDisplay()
    {
        var layout = CurrentOptions().Resolve(HasTranslation, HasPronunciation);

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