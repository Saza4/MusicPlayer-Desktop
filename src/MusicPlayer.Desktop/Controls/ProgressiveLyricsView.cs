using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
/// Un salto de más de una pantalla se coloca sin animar (M5, <see cref="LyricsScroll.IsLongJump"/>).
/// Interacción (M4): pulsar una línea salta a su marca; la rueda o arrastrar con el dedo desplazan a mano, y tras
/// <see cref="ProgressiveLyricsMetrics.ManualScrollHold"/> sin tocar nada vuelve el desplazamiento automático.
/// </remarks>
public sealed class ProgressiveLyricsView : Panel
{
    public static readonly StyledProperty<LyricsViewModel?> SourceProperty =
        AvaloniaProperty.Register<ProgressiveLyricsView, LyricsViewModel?>(nameof(Source));

    /// <summary>La letra está a la vista (modo inmersivo con letra sincronizada).</summary>
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
    private readonly DispatcherTimer _resume;   // fin de la pausa del auto-desplazamiento (criterio 8)
    private readonly Cursor _hand = new(StandardCursorType.Hand);
    private LyricsViewModel? _subscribed;

    private double[] _tops = [];
    private double[] _heights = [];
    private double _viewport;
    private int _active = int.MinValue;   // entrada activa mostrada; MinValue = ninguna todavía
    private double _target;
    private bool _snap = true;            // al reconstruir o recolocar: sin animación
    private bool _running;
    private TimeSpan _lastFrame;

    private double? _manual;              // desplazamiento manual en curso; null = automático
    private IPointer? _drag;              // dedo o lápiz que arrastra
    private Point _dragStart;
    private double _dragBase;
    private bool _dragMoved;              // ya no es un toque: no salta a la línea

    public ProgressiveLyricsView()
    {
        ClipToBounds = true;
        Background = Brushes.Transparent; // la rueda y los gestos cuentan en todo el panel, no solo sobre el texto
        AddHandler(Gestures.TappedEvent, OnTapped);

#if DEBUG
        _stats = ClockStats.FromEnvironment();
#endif
        // Este constructor arranca el temporizador: se para hasta que haga falta (StartClock).
        _frames = new DispatcherTimer(TimeSpan.FromSeconds(1.0 / M.FramesPerSecond), DispatcherPriority.Render, (_, _) => Tick());
        _frames.Stop();
        _wake = new DispatcherTimer(DispatcherPriority.Normal);
        _wake.Tick += (_, _) => { _wake.Stop(); Tick(); };
        _resume = new DispatcherTimer(M.ManualScrollHold, DispatcherPriority.Normal, (_, _) => ResumeAuto());
        _resume.Stop();
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
        _resume.Stop();
        _manual = null;
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
                var item = new LineItem(line, DimForeground) { Cursor = _hand };
                _items.Add(item);
                Children.Add(item);
            }
        }

        _tops = [];
        _heights = [];
        _active = int.MinValue;
        _snap = true;
        _manual = null; // otra letra: vuelve el automático
        _resume.Stop();
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

        var changed = active != _active;
        if (changed) OnActiveChanged(active, now);
        _target = _manual ?? LyricsScroll.TargetOffset(_heights, M.LineSpacing, active, _viewport, M.Anchor);

        // Salto de más de una pantalla (con la barra, por ejemplo): se coloca sin animar.
        if (changed && _manual is null && LyricsScroll.IsLongJump(_items[Math.Max(active, 0)].Offset, _target, _viewport))
            _snap = true;

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
        var fade = _viewport * LyricsAppearance.EdgeFadeFraction;

        // La distancia se mide a la línea activa (criterio 2), esté donde esté: también si se desplaza a mano.
        // Antes de la primera, al ancla.
        var reference = _active >= 0 && _active < _items.Count
            ? _tops[_active] + _items[_active].Offset
            : _viewport * M.Anchor;

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var top = _tops[i] + item.Offset;
            var distance = (top - reference) / M.ReferenceStep;
            var opacity = LyricsAppearance.LineOpacity(distance) *
                          LyricsAppearance.EdgeOpacity(top, _heights[i], _viewport, fade);
            item.Apply(LyricsAppearance.LineScale(distance), opacity);
        }
    }

    // ---- Interacción (M4) ----

    // Pulsar una línea (ratón, dedo o lápiz) salta a su marca; una pausa «♪», a su inicio.
    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (_dragMoved || Source is not { } source) return;

        var y = e.GetPosition(this).Y;
        var entries = source.Timeline.Entries;
        for (int i = 0; i < _items.Count && i < entries.Count && i < _tops.Length; i++)
        {
            var top = _tops[i] + _items[i].Offset;
            if (y < top || y > top + _heights[i] || _items[i].Opacity <= 0) continue;

            _manual = null; // al saltar vuelve el automático, sin esperar
            _resume.Stop();
            source.SeekTo(entries[i].Start);
            Tick();
            e.Handled = true;
            return;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_items.Count == 0 || e.Delta.Y == 0) return;

        // Rueda hacia arriba (delta > 0) = ver lo anterior = bajar el contenido.
        BeginManual((_manual ?? _target) + e.Delta.Y * M.WheelStep, followPointer: false);
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _dragMoved = false;
        if (e.Pointer.Type == PointerType.Mouse) return; // con ratón: rueda para desplazar, clic para saltar

        _drag = e.Pointer;
        _dragStart = e.GetPosition(this);
        _dragBase = _manual ?? _target;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!ReferenceEquals(e.Pointer, _drag)) return;

        var dy = e.GetPosition(this).Y - _dragStart.Y;
        if (!_dragMoved && Math.Abs(dy) < M.DragThreshold) return;
        _dragMoved = true;
        BeginManual(_dragBase + dy, followPointer: true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!ReferenceEquals(e.Pointer, _drag)) return;
        _drag = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (ReferenceEquals(e.Pointer, _drag)) _drag = null;
    }

    /// <summary>Desplazamiento a mano: el automático se pausa y se retoma tras un rato sin tocar nada.</summary>
    private void BeginManual(double offset, bool followPointer)
    {
        if (_heights.Length != _items.Count || _viewport <= 0) return;

        _manual = LyricsScroll.ClampManualOffset(_heights, M.LineSpacing, offset, _viewport, M.Anchor);
        foreach (var item in _items)
        {
            if (followPointer) item.SnapTo(_manual.Value); // el contenido va pegado al dedo
            else item.StartAt = TimeSpan.Zero;             // con la rueda, todas a la vez y con el resorte
        }

        _resume.Stop();
        _resume.Start();
        Tick();
    }

    private void ResumeAuto()
    {
        _resume.Stop();
        if (_drag is not null) { _resume.Start(); return; } // aún arrastrando: se espera a que suelte

        _manual = null;
        foreach (var item in _items) item.StartAt = TimeSpan.Zero; // vuelta al ancla, sin cascada
        Tick();
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
        private readonly FillTextBlock? _pronunciation;
        private readonly FillTextBlock? _translation;
        private FillTextBlock _filled;   // el renglón que se rellena (criterio 17 de lyrics-layers.md)
        private readonly MatrixTransform _transform = new();
        private bool _isActive;
        private double _scale = 1;
        private double _fill;

        private SpringState _spring;
        private double _springTarget;   // a dónde va ahora (el objetivo anterior hasta que le toca arrancar)

        public LineItem(LyricLineViewModel? line, IBrush? dim)
        {
            _line = line;
            Spacing = M.RowSpacing;
            RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative); // escala desde la izquierda
            RenderTransform = _transform;

            _lead = new FillTextBlock { FontWeight = FontWeight.Bold, SoftEdge = M.SoftEdge, DimForeground = dim };
            _filled = _lead;

            if (line is null)
            {
                _lead.Text = "♪";
                _lead.FontSize = 18 * M.FontScale;
                Children.Add(_lead);
                return;
            }

            _pronunciation = new FillTextBlock { FontWeight = FontWeight.SemiBold, SoftEdge = M.SoftEdge, DimForeground = dim };
            _translation = new FillTextBlock { FontStyle = FontStyle.Italic, SoftEdge = M.SoftEdge, DimForeground = dim };
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
            foreach (var row in Rows()) row.DimForeground = dim;
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            SetFill(active ? _fill : 0); // las ya cantadas vuelven a tenue (criterio 2)
        }

        /// <summary>
        /// Rellena el renglón elegido hasta <paramref name="fraction"/>. Los demás no se rellenan (criterio 6 de
        /// lyrics-progressive.md, enmendado): brillantes en la activa y tenues en las otras.
        /// </summary>
        public void SetFill(double fraction)
        {
            _fill = fraction;
            foreach (var row in Rows())
                row.FillX = ReferenceEquals(row, _filled) ? fraction * row.FillLength
                          : _isActive ? double.PositiveInfinity
                          : 0;
        }

        private IEnumerable<FillTextBlock> Rows()
        {
            yield return _lead;
            if (_pronunciation is not null) yield return _pronunciation;
            if (_translation is not null) yield return _translation;
        }

        // FillLength se conoce al medir: un renglón que acaba de aparecer (al ocultar el original con la pista en
        // pausa, por ejemplo) se rellena hasta su sitio sin esperar al siguiente fotograma.
        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            SetFill(_fill);
            return size;
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

            _filled = LyricsLayerMenu.FillRow(_line.ShowOriginal, _line.ShowPronunciation, _line.ShowTranslation) switch
            {
                LyricsRow.Pronunciation => _pronunciation,
                LyricsRow.Translation => _translation,
                _ => _lead
            };
            SetFill(_fill);
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
