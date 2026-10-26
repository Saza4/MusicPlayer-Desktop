using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Desktop.ViewModels;

public partial class LyricsViewModel(ILyricsProvider provider, IAudioPlayer player) : ObservableObject
{
    private LyricBlock _block = LyricBlock.Empty;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private ObservableCollection<LyricLineViewModel> _lines = new();
    [ObservableProperty] private bool _isSynced;
    [ObservableProperty] private int _activeIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPanel))]
    private bool _isAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPanel))]
    private bool _isOpen;

    /// <summary>El panel solo se muestra si el usuario lo abrió y hay letra.</summary>
    public bool ShowPanel => IsOpen && IsAvailable;

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
        UpdatePosition(line.Line.Timestamp); // también funciona con la reproducción en pausa
    }

    private void SetBlock(LyricBlock block)
    {
        _block = block;
        Lines = new ObservableCollection<LyricLineViewModel>(
            block.Lines.Select(l => new LyricLineViewModel(l, block.Kind == LyricsKind.Synced)));
        IsSynced = block.Kind == LyricsKind.Synced;
        IsAvailable = block.IsAvailable;
        ActiveIndex = -1;
    }
}