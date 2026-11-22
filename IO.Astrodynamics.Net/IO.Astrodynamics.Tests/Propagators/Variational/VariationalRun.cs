// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The propagation of the tests that integrate internal variational options (a subset of the sensitivity parameters,
/// the variational equations in the step control or the degree of the geopotential partials without the covariance
/// options that name them): the forces of the options path (<see cref="CentralBodyPropagator"/> of
/// <see cref="PropagationOptions"/>, the one force planner, decision 3 of lot L4 of #668), with
/// <paramref name="variationalOptions"/> enabled before the run. It replaced the internal
/// <c>Spacecraft.PropagateWithVariationalEquations</c> of flags, bit for bit.
/// </summary>
internal static class VariationalRun
{
    /// <summary>
    /// Propagates <paramref name="spacecraft"/> over <paramref name="window"/> with the settings of
    /// <paramref name="options"/>, which propagate no covariance of their own, integrating
    /// <paramref name="variationalOptions"/> when it is not null.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="options"/> asks for a covariance propagation of its own.</exception>
    internal static PropagationSolution PropagateWithVariational(this Spacecraft spacecraft, Window window,
        PropagationOptions options, VariationalOptions variationalOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.CovariancePropagation is { IsActive: true })
        {
            throw new ArgumentException("The variational equations of these tests are given by variationalOptions.",
                nameof(options));
        }

        var propagator = new CentralBodyPropagator(window, spacecraft,
            options with { CovariancePropagation = CovariancePropagationOptions.None });
        if (variationalOptions != null)
        {
            propagator.EnableVariationalEquations(variationalOptions);
        }

        return propagator.Propagate();
    }
}
