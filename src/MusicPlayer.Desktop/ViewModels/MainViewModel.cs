using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.Interfaces;

namespace MusicPlayer.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "MusicPlayer – listo para desarrollo";

    private readonly IAudioPlayer _player;

    [ObservableProperty] private string _filePath = "";
    [ObservableProperty] private string _status = "Detenido";

    public MainViewModel(IAudioPlayer player) => _player = player;

    [RelayCommand]
    private void LoadAndPlay()
    {
        try
        {
            _player.Load(FilePath.Trim('"'));
            _player.Play();
            Status = $"Reproduciendo ({_player.Duration:mm\\:ss})";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand] private void Pause() { _player.Pause(); Status = "En pausa"; }
    [RelayCommand] private void Stop() { _player.Stop(); Status = "Detenido"; }

}
