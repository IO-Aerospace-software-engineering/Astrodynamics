// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The reference of F1: Φ and Ψ by central differences of whole propagations, each perturbed propagation with its own
/// steps, and the errors that compare them with the integrated Φ and Ψ.
/// </summary>
/// <remarks>
/// Column j of Φ is <c>(x(t; x0 + δ e_j) − x(t; x0 − δ e_j)) / (2 δ)</c>, and Ψ for a coefficient C is
/// <c>(x(t; C + h) − x(t; C − h)) / (2 h)</c>: the central difference of W. H. Press, S. A. Teukolsky,
/// W. T. Vetterling, B. P. Flannery, <i>Numerical Recipes</i>, 3rd edition, 2007, section 5.7, applied to the flow of the
/// propagation. Its truncation is of order δ², and the integration error of the perturbed propagations, which choose
/// their own steps, adds a noise of order ε / δ: the steps come from the plateau study of
/// <c>WholePropagationStepStudyTests</c>.
/// </remarks>
internal static class WholePropagationDifferences
{
    /// <summary>The state with <paramref name="delta"/> added to its component <paramref name="component"/> (0 to 5).</summary>
    internal static StateVector Perturbed(StateVector state, int component, double delta)
    {
        var p = state.Position;
        var v = state.Velocity;
        var position = component switch
        {
            0 => new Vector3(p.X + delta, p.Y, p.Z),
            1 => new Vector3(p.X, p.Y + delta, p.Z),
            2 => new Vector3(p.X, p.Y, p.Z + delta),
            _ => p
        };
        var velocity = component switch
        {
            3 => new Vector3(v.X + delta, v.Y, v.Z),
            4 => new Vector3(v.X, v.Y + delta, v.Z),
            5 => new Vector3(v.X, v.Y, v.Z + delta),
            _ => v
        };
        return new StateVector(position, velocity, state.Observer, state.Epoch, state.Frame);
    }

    /// <summary>The state at the end of the last accepted step of <paramref name="solution"/>.</summary>
    internal static double[] FinalState(PropagationSolution solution)
    {
        var step = solution.Segments[^1].Steps[^1];
        return new[]
        {
            step.EndPosition.X, step.EndPosition.Y, step.EndPosition.Z,
            step.EndVelocity.X, step.EndVelocity.Y, step.EndVelocity.Z
        };
    }

    /// <summary>The state of <paramref name="solution"/> at <paramref name="epoch"/>, by its dense output.</summary>
    internal static double[] State(PropagationSolution solution, TimeSystem.Time epoch)
    {
        var (position, velocity) = solution.InterpolateAt(epoch);
        return new[] { position.X, position.Y, position.Z, velocity.X, velocity.Y, velocity.Z };
    }

    /// <summary>
    /// Φ (6×6, row-major) at the end of the propagation: 12 propagations from <paramref name="start"/> perturbed by
    /// ±<paramref name="positionStep"/> on each position component and ±<paramref name="velocityStep"/> on each velocity
    /// component.
    /// </summary>
    internal static double[] Phi(Func<StateVector, PropagationSolution> propagate, StateVector start,
        double positionStep, double velocityStep)
    {
        return Phi(propagate, start, positionStep, velocityStep, solution => new[] { FinalState(solution) }, 1)[0];
    }

    /// <summary>Φ at each of <paramref name="epochs"/>, from the same 12 propagations.</summary>
    internal static double[][] Phi(Func<StateVector, PropagationSolution> propagate, StateVector start,
        double positionStep, double velocityStep, IReadOnlyList<TimeSystem.Time> epochs)
    {
        return Phi(propagate, start, positionStep, velocityStep, solution => States(solution, epochs), epochs.Count);
    }

    /// <summary>
    /// Ψ (6 components) at the end of the propagation: 2 propagations with the coefficient at
    /// <paramref name="coefficient"/> ± <paramref name="step"/>.
    /// </summary>
    internal static double[] Psi(Func<double, PropagationSolution> propagate, double coefficient, double step)
    {
        return Difference(FinalState(propagate(coefficient + step)), FinalState(propagate(coefficient - step)), step);
    }

    /// <summary>Ψ at each of <paramref name="epochs"/>, from the same 2 propagations.</summary>
    internal static double[][] Psi(Func<double, PropagationSolution> propagate, double coefficient, double step,
        IReadOnlyList<TimeSystem.Time> epochs)
    {
        var plus = States(propagate(coefficient + step), epochs);
        var minus = States(propagate(coefficient - step), epochs);
        var psi = new double[epochs.Count][];
        for (int k = 0; k < epochs.Count; k++)
        {
            psi[k] = Difference(plus[k], minus[k], step);
        }

        return psi;
    }

    /// <summary>
    /// The relative Frobenius error of a Ψ column, position rows and velocity rows apart.
    /// </summary>
    internal static (double Position, double Velocity) PsiError(double[] actual, double[] expected)
    {
        return (RiddersDerivative.RelativeFrobeniusError(actual[..3], expected[..3]),
            RiddersDerivative.RelativeFrobeniusError(actual[3..], expected[3..]));
    }

    /// <summary>
    /// The worst error of a Ψ column over the arc, position and velocity rows apart, each relative to the largest norm
    /// of the expected rows over the arc rather than to the local one: the measure of #394 for a Ψ that starts from zero,
    /// where the local value is below what the reference resolves.
    /// </summary>
    internal static (double Position, double Velocity) PsiArcError(IReadOnlyList<double[]> actual,
        IReadOnlyList<double[]> expected)
    {
        double Worst(int r0)
        {
            double largest = 0.0;
            double worst = 0.0;
            for (int k = 0; k < expected.Count; k++)
            {
                double norm = 0.0;
                double difference = 0.0;
                for (int i = r0; i < r0 + 3; i++)
                {
                    norm += expected[k][i] * expected[k][i];
                    difference += (actual[k][i] - expected[k][i]) * (actual[k][i] - expected[k][i]);
                }

                largest = System.Math.Max(largest, System.Math.Sqrt(norm));
                worst = System.Math.Max(worst, System.Math.Sqrt(difference));
            }

            return worst / largest;
        }

        return (Worst(0), Worst(3));
    }

    /// <summary>
    /// A propagation that starts from exactly the state of its spacecraft: <see cref="CentralBodyPropagator"/> takes it
    /// through <c>OrbitalParameters.AtEpoch</c>, a round trip through the Keplerian elements that moves a near-circular,
    /// near-equatorial state by up to decimetres and is not smooth
    /// (<a href="https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/398">#398</a>); the
    /// differences would differentiate it with the flow.
    /// </summary>
    internal sealed class ExactStartPropagator : CentralBodyPropagator
    {
        internal ExactStartPropagator(TimeSystem.Window window, Spacecraft spacecraft, Integrator integrator,
            IEnumerable<CelestialItem> bodies, bool drag, bool srp, TimeSpan deltaT, StateVector start)
            : base(window, spacecraft, integrator, bodies, drag, srp, deltaT)
        {
            InitialState = start;
        }
    }

    private static double[][] Phi(Func<StateVector, PropagationSolution> propagate, StateVector start,
        double positionStep, double velocityStep, Func<PropagationSolution, double[][]> states, int count)
    {
        var phi = new double[count][];
        for (int k = 0; k < count; k++)
        {
            phi[k] = new double[36];
        }

        for (int column = 0; column < 6; column++)
        {
            double delta = column < 3 ? positionStep : velocityStep;
            var plus = states(propagate(Perturbed(start, column, delta)));
            var minus = states(propagate(Perturbed(start, column, -delta)));
            for (int k = 0; k < count; k++)
            {
                for (int row = 0; row < 6; row++)
                {
                    phi[k][row * 6 + column] = (plus[k][row] - minus[k][row]) / (2.0 * delta);
                }
            }
        }

        return phi;
    }

    private static double[][] States(PropagationSolution solution, IReadOnlyList<TimeSystem.Time> epochs)
    {
        var states = new double[epochs.Count][];
        for (int k = 0; k < epochs.Count; k++)
        {
            states[k] = State(solution, epochs[k]);
        }

        return states;
    }

    private static double[] Difference(double[] plus, double[] minus, double step)
    {
        var difference = new double[6];
        for (int i = 0; i < 6; i++)
        {
            difference[i] = (plus[i] - minus[i]) / (2.0 * step);
        }

        return difference;
    }
}
