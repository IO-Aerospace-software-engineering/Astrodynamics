// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// States in and around the shadow of the Earth for the SRP partials, and an evaluation of the shadow function that does
/// not share the code of the library, without cancellation, as a reference near the contacts.
/// </summary>
/// <remarks>
/// The states are on a circle of the radius of a reference orbit, in the plane of the direction of the Sun and of the
/// pole, parametrized by the angle from the anti-Sun axis: 0 on the axis, in total eclipse, growing towards the sunlit
/// side. The geometry is that of <c>SolarRadiationPressure</c>: geometric positions of the Sun and of the Earth, the
/// angular radii asin(R / d) and the angular separation of the two discs seen from the spacecraft.
/// </remarks>
internal static class PenumbraStates
{
    /// <summary>The state at <paramref name="angle"/> from the anti-Sun axis, at the radius of <paramref name="orbit"/>.</summary>
    internal static StateVector At(PartialsTestCases cases, string orbit, double angle)
    {
        double radius = PartialsTestCases.State(orbit, cases.Earth).Position.Magnitude();
        var sunDirection = cases.Sun.GetEphemeris(PartialsTestCases.Epoch, cases.Earth, Frames.Frame.ICRF,
            Aberration.None).ToStateVector().Position.Normalize();
        var side = sunDirection.Cross(Vector3.VectorZ).Normalize();
        var velocity = Vector3.VectorZ * System.Math.Sqrt(cases.Earth.GM / radius);
        return new StateVector((sunDirection * -System.Math.Cos(angle) + side * System.Math.Sin(angle)) * radius,
            velocity, cases.Earth, PartialsTestCases.Epoch, Frames.Frame.ICRF);
    }

    /// <summary>
    /// The angle where the shadow fraction of the library is <paramref name="shadowFraction"/>, by bisection: 0, on the
    /// anti-Sun axis, for a total eclipse.
    /// </summary>
    internal static double AngleOfShadowFraction(PartialsTestCases cases, string orbit, double shadowFraction)
    {
        if (shadowFraction >= 1.0)
        {
            return 0.0;
        }

        return Bisect(angle => cases.Sun.ShadowFraction(cases.Earth, At(cases, orbit, angle), Aberration.None) >
                               shadowFraction);
    }

    /// <summary>
    /// The angles of the inner contact (total eclipse below it) and of the outer contact (sunlight above it), by
    /// bisection on the geometry of the discs, not on the shadow fraction, which is rounding alone near a contact.
    /// </summary>
    internal static (double Inner, double Outer) ContactAngles(PartialsTestCases cases, string orbit)
    {
        double inner = Bisect(angle =>
        {
            var (a, b, c) = Discs(cases, At(cases, orbit, angle), cases.Earth);
            return c <= b - a;
        });
        double outer = Bisect(angle =>
        {
            var (a, b, c) = Discs(cases, At(cases, orbit, angle), cases.Earth);
            return c < a + b;
        });
        return (inner, outer);
    }

    /// <summary>
    /// The angular radii of the Sun and of <paramref name="occultingBody"/> and their angular separation, seen from
    /// <paramref name="state"/>, in rad.
    /// </summary>
    internal static (double SunRadius, double BodyRadius, double Separation) Discs(PartialsTestCases cases,
        StateVector state, CelestialBody occultingBody)
    {
        var position = state.Position;
        var toSun = cases.Sun.GetEphemeris(state.Epoch, state.Observer, state.Frame, Aberration.None).ToStateVector()
            .Position - position;
        var toBody = (occultingBody.Equals(state.Observer)
            ? Vector3.Zero
            : occultingBody.GetEphemeris(state.Epoch, state.Observer, state.Frame, Aberration.None).ToStateVector()
                .Position) - position;
        return (System.Math.Asin(cases.Sun.EquatorialRadius / toSun.Magnitude()),
            System.Math.Asin(occultingBody.EquatorialRadius / toBody.Magnitude()),
            System.Math.Atan2(toSun.Cross(toBody).Magnitude(), toSun * toBody));
    }

    /// <summary>
    /// The fraction of the solar disc of angular radius <paramref name="a"/> covered by a disc of angular radius
    /// <paramref name="b"/> at the angular separation <paramref name="c"/>, without cancellation: the overlap as the sum
    /// of two circular segments, r² (θ − sin θ cos θ) = r² (u − sin u) / 2 with u = 2θ, u − sin u by its series below
    /// u = 0.1, the half chord from Heron's product and the half angles θ from atan2.
    /// </summary>
    /// <remarks>
    /// The area of two overlapping circles as the sum A(R, d₁) + A(r, d₂) of the two circular segments cut by their
    /// common chord, A(R′, d′) = R′² acos(d′ / R′) − d′ √(R′² − d′²) = R′² (θ − sin θ cos θ), θ = acos(d′ / R′): E. W.
    /// Weisstein, "Circle-Circle Intersection", MathWorld (read on 2026-10-22), the same area as eq. 3.150 of the GMAT
    /// Mathematical Specification, R2026a. Written independently of the library, for the tests.
    /// </remarks>
    internal static double CancellationFreeShadowFraction(double a, double b, double c)
    {
        if (c >= a + b)
        {
            return 0.0;
        }

        if (c <= b - a)
        {
            return 1.0;
        }

        if (c <= a - b)
        {
            return b * b / (a * a);
        }

        double halfChord = System.Math.Sqrt((a + b - c) * (b + c - a) * (c + a - b) * (a + b + c)) / (2.0 * c);
        double c1 = ((c - b) * (c + b) + a * a) / (2.0 * c);
        double alpha = System.Math.Atan2(halfChord, c1);
        double beta = System.Math.Atan2(halfChord, c - c1);
        double area = 0.5 * (a * a * UMinusSinU(2.0 * alpha) + b * b * UMinusSinU(2.0 * beta));
        return area / (System.Math.PI * a * a);
    }

    // u − sin u, by its series below 0.1, where the difference would cancel
    private static double UMinusSinU(double u)
    {
        if (u >= 0.1)
        {
            return u - System.Math.Sin(u);
        }

        double u2 = u * u;
        double term = u * u2 / 6.0;
        double sum = 0.0;
        for (int k = 1; k <= 8; k++)
        {
            sum += term;
            term *= -u2 / ((2.0 * k + 2.0) * (2.0 * k + 3.0));
        }

        return sum;
    }

    // The largest angle in [0, π/2] where the predicate holds, the predicate true below it and false above
    private static double Bisect(System.Func<double, bool> below)
    {
        double low = 0.0;
        double high = System.Math.PI / 2.0;
        for (int k = 0; k < 200 && high - low > 1e-16; k++)
        {
            double middle = 0.5 * (low + high);
            if (below(middle))
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return 0.5 * (low + high);
    }
}
