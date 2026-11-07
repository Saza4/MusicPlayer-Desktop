namespace MusicPlayer.Core.Services;

/// <summary>Resorte amortiguado de masa 1: rigidez (k) y amortiguación (c).</summary>
public readonly record struct Spring(double Stiffness, double Damping)
{
    /// <summary>
    /// Resorte a partir de cómo se siente: <paramref name="response"/> es el periodo de oscilación en segundos y
    /// <paramref name="dampingFraction"/>, la amortiguación (1 = crítica, sin rebote; menos de 1, rebota).
    /// </summary>
    public static Spring FromResponse(double response, double dampingFraction)
    {
        if (response <= 0) throw new ArgumentOutOfRangeException(nameof(response));
        if (dampingFraction < 0) throw new ArgumentOutOfRangeException(nameof(dampingFraction));
        var omega = 2 * Math.PI / response;
        return new Spring(omega * omega, 2 * dampingFraction * omega);
    }

    /// <summary>El <c>spring()</c> por defecto de SwiftUI (response 0,55, amortiguación 0,825): un rebote apenas visible.</summary>
    public static Spring Default { get; } = FromResponse(0.55, 0.825);
}

/// <summary>Posición y velocidad (unidades por segundo) de lo que mueve el resorte.</summary>
public readonly record struct SpringState(double Position, double Velocity)
{
    /// <summary>En reposo en <paramref name="position"/>.</summary>
    public static SpringState At(double position) => new(position, 0);
}

/// <summary>
/// Integra un resorte hacia un objetivo. Función pura y determinista: la vista guarda el estado y llama a
/// <see cref="Step"/> en cada fotograma con el tiempo transcurrido.
/// </summary>
public static class SpringSolver
{
    /// <summary>Paso máximo de integración: con fotogramas lentos se subdivide para que no se vuelva inestable.</summary>
    public static readonly TimeSpan MaxSubstep = TimeSpan.FromMilliseconds(4);

    /// <summary>
    /// Con una pausa más larga que esto entre fotogramas (la app estuvo oculta, un tirón largo), el movimiento
    /// ya habría terminado: se coloca en el objetivo en vez de simularlo.
    /// </summary>
    public static readonly TimeSpan MaxElapsed = TimeSpan.FromSeconds(1);

    public static SpringState Step(SpringState state, double target, TimeSpan elapsed, Spring spring)
    {
        if (elapsed <= TimeSpan.Zero) return state;
        if (elapsed >= MaxElapsed) return SpringState.At(target);

        // En ticks (enteros) para que la cuenta de pasos no dependa del redondeo de los dobles. Trocear el mismo
        // tiempo en fotogramas distintos da pasos algo distintos: la diferencia es de centésimas de píxel.
        var steps = (int)((elapsed.Ticks + MaxSubstep.Ticks - 1) / MaxSubstep.Ticks);
        var dt = elapsed.TotalSeconds / steps;
        double x = state.Position, v = state.Velocity;

        // Euler semi-implícito: primero la velocidad y con ella la posición. Estable con pasos pequeños.
        for (int i = 0; i < steps; i++)
        {
            var acceleration = -spring.Stiffness * (x - target) - spring.Damping * v;
            v += acceleration * dt;
            x += v * dt;
        }

        return new SpringState(x, v);
    }

    /// <summary>Ha llegado: cerca del objetivo y casi quieto. La vista puede dejar de pedir fotogramas.</summary>
    public static bool IsSettled(SpringState state, double target,
        double positionTolerance = 0.5, double velocityTolerance = 2) =>
        Math.Abs(state.Position - target) <= positionTolerance && Math.Abs(state.Velocity) <= velocityTolerance;
}
