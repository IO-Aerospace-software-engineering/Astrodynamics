// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// Fixed-step RK7(8) integration of a state with its variational equations, outside any propagator.
/// </summary>
internal static class VariationalTestDriver
{
    /// <summary>
    /// Integrate <paramref name="steps"/> steps of <paramref name="h"/> seconds from <paramref name="start"/>, advancing
    /// <paramref name="y"/> and <paramref name="q"/> in place.
    /// </summary>
    /// <returns>The final position and velocity.</returns>
    internal static (Vector3 Position, Vector3 Velocity) Integrate(IReadOnlyList<ForceBase> forces,
        VariationalEquations equations, ForceEvaluationContext context, StateVector start, double h, int steps,
        double[] y, double[] q)
    {
        var stepper = new RK78Stepper(forces) { Context = context };
        stepper.Reset(start.Observer, start.Frame, start.Epoch);
        var y1 = new double[y.Length];
        var q1 = new double[q.Length];
        var position = start.Position;
        var velocity = start.Velocity;
        for (int k = 0; k < steps; k++)
        {
            stepper.Step(position, velocity, start.Epoch, k * h, h, out var nextPosition, out var nextVelocity,
                out _, out _);
            equations.Step(stepper.StageStates, forces, context, h, y, q, y1, q1);
            y1.CopyTo(y, 0);
            q1.CopyTo(q, 0);
            position = nextPosition;
            velocity = nextVelocity;
        }

        return (position, velocity);
    }

    /// <summary>Y = [I | 0] for <paramref name="equations"/>.</summary>
    internal static double[] Identity(VariationalEquations equations)
    {
        var y = new double[equations.YLength];
        equations.SetIdentity(y);
        return y;
    }

    /// <summary>Q = 0, or an empty buffer without process noise.</summary>
    internal static double[] ZeroCovariance(VariationalEquations equations)
    {
        return new double[equations.HasProcessNoise ? VariationalEquations.CovarianceLength : 0];
    }
}
