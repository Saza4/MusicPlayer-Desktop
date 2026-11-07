using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using MusicPlayer.Desktop.ViewModels;

namespace MusicPlayer.Desktop.Controls;

/// <summary>
/// Spike M0 (docs/specs/lyrics-progressive.md): muestra la línea activa rellenándose según la posición real
/// y la siguiente, tenue. El estado no se acumula: cada fotograma se recalcula a partir de la posición, así que
/// saltar, pausar o cambiar de pista siempre queda bien. M3 lo convierte en la lista completa con resorte.
/// </summary>
public sealed class ProgressiveLyricsView : StackPanel
{
    public static readonly StyledProperty<LyricsViewModel?> SourceProperty =
        AvaloniaProperty.Register<ProgressiveLyricsView, LyricsViewModel?>(nameof(Source));

    /// <summary>Con true (letra a la vista y pista sonando), corre el reloj de fotogramas.</summary>
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<ProgressiveLyricsView, bool>(nameof(IsRunning));

    public static readonly StyledProperty<IBrush?> DimForegroundProperty =
        FillTextBlock.DimForegroundProperty.AddOwner<ProgressiveLyricsView>();

    private const double LineFontSize = 30;

    private readonly FillTextBlock _current;
    private readonly FillTextBlock _next;
    private readonly FrameClock _clock;
    private int _shownIndex = int.MinValue;
    private object? _shownLines;
    private LyricsViewModel? _subscribed;

    public ProgressiveLyricsView()
    {
        Spacing = 20;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

        _current = new FillTextBlock { FontSize = LineFontSize, FontWeight = FontWeight.Bold };
        _next = new FillTextBlock { FontSize = LineFontSize, FontWeight = FontWeight.Bold };
        Children.Add(_current);
        Children.Add(_next);

        _clock = new FrameClock(this, Tick);
    }

    public LyricsViewModel? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public bool IsRunning { get => GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }
    public IBrush? DimForeground { get => GetValue(DimForegroundProperty); set => SetValue(DimForegroundProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            if (_subscribed is not null) _subscribed.PropertyChanged -= OnSourceChanged;
            _subscribed = Source;
            if (_subscribed is not null) _subscribed.PropertyChanged += OnSourceChanged;
            Tick();
        }
        else if (change.Property == DimForegroundProperty)
        {
            _current.DimForeground = DimForeground;
            _next.DimForeground = DimForeground;
        }

        if (change.Property == SourceProperty || change.Property == IsRunningProperty)
            UpdateClock();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateClock();
        Tick();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock.Stop();
    }

    // Con el reloj parado (pausa), un salto o un cambio de pista llegan por aquí.
    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LyricsViewModel.ActiveIndex) or nameof(LyricsViewModel.Lines))
            Tick();
    }

    private void UpdateClock()
    {
        if (IsRunning && Source is not null && VisualRoot is not null)
            _clock.Start();
        else
            _clock.Stop();
    }

    private void Tick()
    {
        var source = Source;
        if (source is null)
        {
            Show(-1, null);
            return;
        }

        var position = source.CurrentPosition;
        var lines = source.Lines;
        var index = source.ActiveIndexAt(position);

        if (index != _shownIndex || !ReferenceEquals(lines, _shownLines))
            Show(index, lines);

        if (index < 0 || index >= lines.Count)
        {
            _current.FillX = 0;
            return;
        }

        var fraction = source.FillAt(index, position);

#if DEBUG
        _clock.Stats?.Record(position);
#endif
        _current.FillX = fraction * _current.FillLength;
    }

    private void Show(int index, System.Collections.Generic.IReadOnlyList<LyricLineViewModel>? lines)
    {
        _shownIndex = index;
        _shownLines = lines;

        if (lines is null || lines.Count == 0)
        {
            _current.Text = null;
            _next.Text = null;
            return;
        }

        // Antes de la primera línea: nota musical y, debajo, lo que viene.
        _current.Text = index >= 0 && index < lines.Count ? lines[index].Text : "♪";
        _next.Text = index + 1 < lines.Count ? lines[index + 1].Text : null;
        _current.FillX = 0;
    }

    /// <summary>
    /// Reloj de fotogramas: un DispatcherTimer a <see cref="FramesPerSecond"/> (decidido en M0: RequestAnimationFrame
    /// sigue el refresco de la pantalla, ~120 fps aquí, y cuadruplica el coste). En Debug,
    /// MUSICPLAYER_LYRICS_CLOCK=&lt;fps&gt; cambia los fps y =raf usa RequestAnimationFrame, para comparar.
    /// </summary>
    private sealed class FrameClock
    {
        // Pedido 30; el temporizador de Windows redondea y da ~24 fps reales (medido en M0).
        private const double FramesPerSecond = 30;

        private readonly Visual _owner;
        private readonly Action _tick;
        private readonly DispatcherTimer? _timer;
        private bool _running;
        private bool _frameQueued;

        public FrameClock(Visual owner, Action tick)
        {
            _owner = owner;
            _tick = tick;

            var fps = FramesPerSecond;
            var useAnimationFrame = false;
#if DEBUG
            var setting = Environment.GetEnvironmentVariable("MUSICPLAYER_LYRICS_CLOCK");
            useAnimationFrame = setting == "raf";
            if (double.TryParse(setting, System.Globalization.CultureInfo.InvariantCulture, out var custom) && custom > 0)
                fps = custom;
            Stats = ClockStats.FromEnvironment();
#endif
            if (!useAnimationFrame)
                _timer = new DispatcherTimer(TimeSpan.FromSeconds(1.0 / fps), DispatcherPriority.Render, (_, _) => _tick());
        }

#if DEBUG
        public ClockStats? Stats { get; }
#endif

        public void Start()
        {
            if (_running) return;
            _running = true;
            if (_timer is not null) _timer.Start();
            else Queue();
        }

        public void Stop()
        {
            _running = false;
            _timer?.Stop();
        }

        private void Queue()
        {
            if (_frameQueued) return; // nunca dos cadenas de fotogramas a la vez
            if (TopLevel.GetTopLevel(_owner) is not { } top) return;
            _frameQueued = true;
            top.RequestAnimationFrame(OnFrame);
        }

        private void OnFrame(TimeSpan _)
        {
            _frameQueued = false;
            if (!_running) return;
            _tick();
            Queue();
        }
    }

#if DEBUG
    /// <summary>
    /// Medidas del spike: con MUSICPLAYER_LYRICS_STATS=ruta, cada 5 s escribe fotogramas por segundo, cuántos
    /// traen una posición nueva y cuánto se aparta el avance de la posición del avance del reloj de pared.
    /// </summary>
    private sealed class ClockStats
    {
        private readonly string _path;
        private readonly Stopwatch _wall = Stopwatch.StartNew();
        private TimeSpan _lastWall, _lastPos, _windowStart;
        private bool _hasLast;
        private int _frames, _changed;
        private double _jitterSum, _jitterMax, _stepMax;

        private ClockStats(string path) => _path = path;

        public static ClockStats? FromEnvironment() =>
            Environment.GetEnvironmentVariable("MUSICPLAYER_LYRICS_STATS") is { Length: > 0 } path
                ? new ClockStats(path)
                : null;

        public void Record(TimeSpan position)
        {
            var now = _wall.Elapsed;
            if (_hasLast)
            {
                _frames++;
                var step = (position - _lastPos).TotalMilliseconds;
                if (step != 0)
                {
                    _changed++;
                    _stepMax = Math.Max(_stepMax, step);
                }
                var jitter = Math.Abs(step - (now - _lastWall).TotalMilliseconds);
                if (jitter < 500) // un salto del usuario no es fluctuación
                {
                    _jitterSum += jitter;
                    _jitterMax = Math.Max(_jitterMax, jitter);
                }
            }
            else _windowStart = now;

            _hasLast = true;
            _lastWall = now;
            _lastPos = position;

            var window = now - _windowStart;
            if (window < TimeSpan.FromSeconds(5) || _frames == 0) return;

            System.IO.File.AppendAllText(_path, string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"fps={_frames / window.TotalSeconds:F1} cambian={100.0 * _changed / _frames:F0}% " +
                $"pasoMax={_stepMax:F1}ms desvioMedio={_jitterSum / _frames:F1}ms desvioMax={_jitterMax:F1}ms\n"));

            _windowStart = now;
            _frames = _changed = 0;
            _jitterSum = _jitterMax = _stepMax = 0;
        }
    }
#endif
}
