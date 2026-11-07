using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using MusicPlayer.Core.Services;
using MusicPlayer.Desktop.ViewModels;
using M = MusicPlayer.Desktop.Controls.ProgressiveLyricsMetrics;

namespace MusicPlayer.Desktop.Controls;

/// <summary>
/// Letra progresiva del modo inmersivo (docs/specs/lyrics-progressive.md, M3): todas las líneas y pausas, la activa
/// anclada y rellenándose, desplazamiento con resorte y cascada, y opacidad y escala por distancia. El fundido de
/// los bordes es opacidad por línea (sin máscara). Nada se acumula: posición → entrada activa → objetivo.
/// </summary>
/// <remarks>
/// El reloj de fotogramas solo corre mientras hace falta: relleno a medias con la pista sonando, o algún resorte en
/// movimiento. Si no, se para y se despierta con el inicio de la siguiente entrada (o con un cambio del modelo).
/// </remarks>
public sealed class ProgressiveLyricsView : Panel
{
    public static readonly StyledProperty<LyricsViewModel?> SourceProperty =
        AvaloniaProperty.Register<ProgressiveLyricsView, LyricsViewModel?>(nameof(Source));

    /// <summary>La letra está a la vista (modo inmersivo con el estilo progresivo).</summary>
    public static readonly StyledProperty<bool> IsShownProperty =
        AvaloniaProperty.Register<ProgressiveLyricsView, bool>(nameof(IsShown));

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<ProgressiveLyricsView, bool>(nameof(IsPlaying));

    public static readonly StyledProperty<IBrush?> DimForegroundProperty =
        FillTextBlock.DimForegroundProperty.AddOwner<ProgressiveLyricsView>();

    private readonly List<LineItem> _items = new();
    private readonly Stopwatch _time = Stopwatch.StartNew();
    private readonly DispatcherTimer _frames;
    private readonly DispatcherTimer _wake;
    private LyricsViewModel? _subscribed;

    private double[] _tops = [];
    private double[] _heights = [];
    private double _viewport;
    private int _active = int.MinValue;   // entrada activa mostrada; MinValue = ninguna todavía
    private double _target;
    private bool _snap = true;            // al reconstruir o recolocar: sin animación
    private bool _running;
    private TimeSpan _lastFrame;

    public ProgressiveLyricsView()
    {
        ClipToBounds = true;

        var fps = M.FramesPerSecond;
#if DEBUG
        if (double.TryParse(Environment.GetEnvironmentVariable("MUSICPLAYER_LYRICS_CLOCK"),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var custom) && custom > 0)
            fps = custom;
        _stats = ClockStats.FromEnvironment();
#endif
        _frames = new DispatcherTimer(TimeSpan.FromSeconds(1.0 / fps), DispatcherPriority.Render, (_, _) => Tick());
        _wake = new DispatcherTimer(DispatcherPriority.Normal);
        _wake.Tick += (_, _) => { _wake.Stop(); Tick(); };
    }

    public LyricsViewModel? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public bool IsShown { get => GetValue(IsShownProperty); set => SetValue(IsShownProperty, value); }
    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public IBrush? DimForeground { get => GetValue(DimForegroundProperty); set => SetValue(DimForegroundProperty, value); }

    // ---- Ciclo de vida ----

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            if (_subscribed is not null) _subscribed.PropertyChanged -= OnSourceChanged;
            _subscribed = Source;
            if (_subscribed is not null) _subscribed.PropertyChanged += OnSourceChanged;
            Rebuild();
        }
        else if (change.Property == DimForegroundProperty)
        {
            foreach (var item in _items) item.SetDim(DimForeground);
        }
        else if (change.Property == IsShownProperty)
        {
            _snap = true; // al volver al inmersivo, la letra aparece ya en su sitio
            Tick();
        }
        else if (change.Property == IsPlayingProperty)
        {
            Tick();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Tick();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopClock();
        _wake.Stop();
    }

    // Con el reloj parado (pausa, línea completa), un salto o un cambio de pista llegan por aquí.
    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LyricsViewModel.Lines)) Rebuild();
        else if (e.PropertyName == nameof(LyricsViewModel.ActiveIndex)) Tick();
    }

    private void Rebuild()
    {
        foreach (var item in _items) item.Detach();
        _items.Clear();
        Children.Clear();

        if (Source is { } source)
        {
            foreach (var entry in source.Timeline.Entries)
            {
                var line = entry.Kind == TimelineEntryKind.Line && entry.LineIndex < source.Lines.Count
                    ? source.Lines[entry.LineIndex]
                    : null; // pausa deducida: «♪»
                var item = new LineItem(line, DimForeground);
                _items.Add(item);
                Children.Add(item);
            }
        }

        _tops = [];
        _heights = [];
        _active = int.MinValue;
        _snap = true;
        InvalidateMeasure();
        Tick();
    }

    // ---- Diseño: las líneas apiladas; el desplazamiento va en la transformación de cada una ----

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        double total = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(width, double.PositiveInfinity));
            total += child.DesiredSize.Height + M.LineSpacing;
        }
        return new Size(width, double.IsInfinity(availableSize.Height) ? total : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var heights = new double[_items.Count];
        for (int i = 0; i < _items.Count; i++) heights[i] = _items[i].DesiredSize.Height;
        var tops = LyricsScroll.LineTops(heights, M.LineSpacing);

        for (int i = 0; i < _items.Count; i++)
            _items[i].Arrange(new Rect(0, tops[i], finalSize.Width, heights[i]));

        _heights = heights;
        _tops = [.. tops];
        _viewport = finalSize.Height;
        _snap = true; // cambió el tamaño o el contenido: la activa vuelve al ancla sin animación
        Dispatcher.UIThread.Post(Tick, DispatcherPriority.Render); // fuera del paso de diseño
        return finalSize;
    }

    // ---- Fotograma ----

    private void Tick()
    {
        var source = Source;
        if (!IsShown || source is null || _items.Count == 0 || _tops.Length != _items.Count || _viewport <= 0)
        {
            StopClock();
            _wake.Stop();
            return;
        }

        var now = _time.Elapsed;
        var elapsed = _running ? now - _lastFrame : TimeSpan.Zero; // reloj parado: nada que integrar
        _lastFrame = now;

        var position = source.CurrentPosition;
        var timeline = source.Timeline;
        var active = timeline.ActiveEntryAt(position);
        if (active >= _items.Count) active = _items.Count - 1;

        if (active != _active) OnActiveChanged(active, now);
        _target = LyricsScroll.TargetOffset(_heights, M.LineSpacing, active, _viewport, M.Anchor);

        if (_snap)
        {
            foreach (var item in _items) item.SnapTo(_target);
            _snap = false;
        }

        double fill = 1;
        if (active >= 0)
        {
            fill = timeline.FillAt(active, position);
            _items[active].SetFill(fill);
        }

        var moving = false;
        foreach (var item in _items)
            moving |= item.Advance(_target, now, elapsed);

        ApplyVisuals();

#if DEBUG
        if (_running) _stats?.Record(position);
#endif

        // Regla del reloj (notas para M3): solo se para con el relleno completo y todos los resortes en reposo.
        var needsFrames = moving || (IsPlaying && active >= 0 && fill < 1);
        if (needsFrames)
        {
            StartClock();
            _wake.Stop();
        }
        else
        {
            StopClock();
            ScheduleWake(IsPlaying ? timeline.NextStartAfter(position) - position : null);
        }
    }

    private void OnActiveChanged(int active, TimeSpan now)
    {
        var previous = _active;
        if (previous >= 0 && previous < _items.Count) _items[previous].SetActive(false);
        if (active >= 0) _items[active].SetActive(true);

        // Cascada: al avanzar o retroceder una entrada, las más lejanas arrancan después; un salto, todas a la vez.
        var from = previous == int.MinValue ? active : previous;
        for (int i = 0; i < _items.Count; i++)
            _items[i].StartAt = now + LyricsScroll.CascadeDelay(i, active, from);

        _active = active;
    }

    private void ApplyVisuals()
    {
        var anchorY = _viewport * M.Anchor;
        var fade = _viewport * LyricsAppearance.EdgeFadeFraction;

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var top = _tops[i] + item.Offset;
            var distance = (top - anchorY) / M.ReferenceStep;
            var opacity = LyricsAppearance.LineOpacity(distance) *
                          LyricsAppearance.EdgeOpacity(top, _heights[i], _viewport, fade);
            item.Apply(LyricsAppearance.LineScale(distance), opacity);
        }
    }

    // ---- Reloj ----

    private void StartClock()
    {
        if (_running) return;
        _running = true;
        _lastFrame = _time.Elapsed; // nota para M3: sin esto, tras una pausa el resorte saltaría al objetivo
        _frames.Start();
    }

    private void StopClock()
    {
        _running = false;
        _frames.Stop();
    }

    private void ScheduleWake(TimeSpan? delay)
    {
        _wake.Stop();
        if (delay is not { } d) return;
        _wake.Interval = d > M.MinWake ? d : M.MinWake;
        _wake.Start();
    }

    // ---- Una línea (o pausa) ----

    private sealed class LineItem : StackPanel
    {
        private readonly LyricLineViewModel? _line;
        private readonly FillTextBlock _lead;
        private readonly TextBlock? _pronunciation;
        private readonly TextBlock? _translation;
        private readonly MatrixTransform _transform = new();
        private IBrush? _dim;
        private bool _isActive;
        private double _scale = 1;
        private double _fill;

        private SpringState _spring;
        private double _springTarget;   // a dónde va ahora (el objetivo anterior hasta que le toca arrancar)

        public LineItem(LyricLineViewModel? line, IBrush? dim)
        {
            _line = line;
            _dim = dim;
            Spacing = M.RowSpacing;
            RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative); // escala desde la izquierda
            RenderTransform = _transform;

            _lead = new FillTextBlock { FontWeight = FontWeight.Bold, SoftEdge = M.SoftEdge, DimForeground = dim };

            if (line is null)
            {
                _lead.Text = "♪";
                _lead.FontSize = 18 * M.FontScale;
                Children.Add(_lead);
                return;
            }

            _pronunciation = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold };
            _translation = new TextBlock { TextWrapping = TextWrapping.Wrap, FontStyle = FontStyle.Italic };
            Children.Add(_pronunciation);
            Children.Add(_lead);
            Children.Add(_translation);

            line.PropertyChanged += OnLineChanged;
            Update();
        }

        public TimeSpan StartAt { get; set; }
        public double Offset => _spring.Position;

        public void Detach()
        {
            if (_line is not null) _line.PropertyChanged -= OnLineChanged;
        }

        public void SetDim(IBrush? dim)
        {
            _dim = dim;
            _lead.DimForeground = dim;
            UpdateExtrasBrush();
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            if (!active) SetFill(0); // las ya cantadas vuelven a tenue (criterio 2)
            UpdateExtrasBrush();
        }

        public void SetFill(double fraction)
        {
            _fill = fraction;
            _lead.FillX = fraction * _lead.FillLength;
        }

        public void SnapTo(double target)
        {
            _spring = SpringState.At(target);
            _springTarget = target;
            StartAt = TimeSpan.Zero;
        }

        /// <summary>Avanza el resorte. Devuelve true si aún se mueve o espera su turno en la cascada.</summary>
        public bool Advance(double target, TimeSpan now, TimeSpan elapsed)
        {
            var waiting = now < StartAt;
            if (!waiting) _springTarget = target;

            _spring = SpringSolver.Step(_spring, _springTarget, elapsed, M.Spring);
            if (!waiting && SpringSolver.IsSettled(_spring, target))
            {
                _spring = SpringState.At(target); // sin restos de medio píxel
                return false;
            }
            return true;
        }

        public void Apply(double scale, double opacity)
        {
            var offset = _spring.Position;
            var current = _transform.Matrix;
            if (Math.Abs(current.M32 - offset) > M.PositionEpsilon || Math.Abs(_scale - scale) > M.ScaleEpsilon)
            {
                _scale = scale;
                _transform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(0, offset);
            }
            if (Math.Abs(Opacity - opacity) > M.OpacityEpsilon || (opacity == 0) != (Opacity == 0))
                Opacity = opacity;
        }

        private void OnLineChanged(object? sender, PropertyChangedEventArgs e) => Update();

        private void Update()
        {
            if (_line is null || _pronunciation is null || _translation is null) return;

            _lead.Text = _line.Text;
            _lead.IsVisible = _line.ShowOriginal;
            _lead.FontSize = _line.OriginalFontSize * M.FontScale;

            _pronunciation.Text = _line.Pronunciation;
            _pronunciation.IsVisible = _line.ShowPronunciation;
            _pronunciation.FontSize = _line.PronunciationFontSize * M.FontScale;

            _translation.Text = _line.Translation;
            _translation.IsVisible = _line.ShowTranslation;
            _translation.FontSize = _line.TranslationFontSize * M.FontScale;

            UpdateExtrasBrush();
            SetFill(_fill);
        }

        // Pronunciación y traducción no se rellenan (criterio 6): brillantes en la activa y tenues en las demás.
        private void UpdateExtrasBrush()
        {
            foreach (var row in new[] { _pronunciation, _translation })
            {
                if (row is null) continue;
                if (_isActive) row.ClearValue(TextElement.ForegroundProperty);
                else row.Foreground = _dim;
            }
        }
    }

#if DEBUG
    private readonly ClockStats? _stats;

    /// <summary>
    /// Medidas (solo Debug): con MUSICPLAYER_LYRICS_STATS=ruta, cada 5 s escribe fotogramas por segundo, cuántos
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
                if (jitter < 500)
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
