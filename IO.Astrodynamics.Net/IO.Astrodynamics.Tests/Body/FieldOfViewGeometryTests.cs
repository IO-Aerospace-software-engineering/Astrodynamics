using System;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Occultation;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The margin of <see cref="FieldOfViewGeometry"/> (#515) against independent references: the angle of a direction from
/// the boresight (CSPICE's <c>vsep</c>), the tangent-plane conditions of <c>zzgffvu</c> for a point in an ellipse or a
/// rectangle, and, for a rectangle against an ellipsoid, a brute-force grid of rays through the rectangle, each tested
/// for an intersection with the ellipsoid in front of the observer (the condition of <c>zzelvupy</c>).
/// </summary>
public class FieldOfViewGeometryTests
{
    private static readonly FieldOfViewAxes Axes = FieldOfViewGeometry.Axes(new Vector3(0.0, 0.0, 2.0), new Vector3(1.0, 0.0, 0.3));

    private static Vector3 Direction(double alongRef, double alongCross)
    {
        // The ray b + tan(alongCross) b2 + tan(alongRef) b1, in the plane at unit distance along b
        return Axes.Boresight + Axes.Cross * System.Math.Tan(alongCross) + Axes.Ref * System.Math.Tan(alongRef);
    }

    [Fact]
    public void Axes_ReferenceVectorMadePerpendicular()
    {
        Assert.Equal(1.0, Axes.Boresight.Magnitude(), 15);
        Assert.Equal(0.0, Axes.Ref * Axes.Boresight, 15);
        Assert.Equal(1.0, Axes.Ref.Magnitude(), 15);
        Assert.Equal(new Vector3(1.0, 0.0, 0.0), Axes.Ref);
        Assert.Equal(new Vector3(0.0, 1.0, 0.0), Axes.Cross);
    }

    [Theory]
    [InlineData(0.1, 0.0)]
    [InlineData(0.29, 0.05)]
    [InlineData(0.31, 0.0)]
    [InlineData(-0.2, 0.25)]
    [InlineData(0.0, -0.4)]
    public void CirclePoint_IsTheAngleFromTheBoresight(double alongRef, double alongCross)
    {
        var d = Direction(alongRef, alongCross) * 7.0e6;
        double margin = FieldOfViewGeometry.Margin(InstrumentShape.Circular, 0.3, 0.3, Axes, OccultationBody.Point(d));
        Assert.Equal(0.3 - d.Angle(Axes.Boresight), margin, 12);
    }

    [Theory]
    [InlineData(0.39, 0.0, true)]
    [InlineData(0.41, 0.0, false)]
    [InlineData(0.0, 0.14, true)]
    [InlineData(0.0, 0.16, false)]
    [InlineData(0.3, 0.09, true)]
    [InlineData(0.3, 0.11, false)]
    [InlineData(-0.3, -0.09, true)]
    public void EllipsePoint_RefAngleAlongTheReferenceVector(double alongRef, double alongCross, bool inView)
    {
        // zzgffvu: (x / tan CROSS)² + (y / tan REF)² ≤ 1 in the plane at unit distance, REF along b1 (#518)
        double refAngle = 0.4, crossAngle = 0.15;
        double x = System.Math.Tan(alongCross), y = System.Math.Tan(alongRef);
        bool expected = System.Math.Pow(x / System.Math.Tan(crossAngle), 2) + System.Math.Pow(y / System.Math.Tan(refAngle), 2) <= 1.0;
        Assert.Equal(inView, expected);
        double margin = FieldOfViewGeometry.Margin(InstrumentShape.Elliptical, refAngle, crossAngle, Axes, OccultationBody.Point(Direction(alongRef, alongCross) * 7.0e6));
        Assert.Equal(expected, margin >= 0.0);
    }

    [Theory]
    [InlineData(0.39, 0.14, true)]
    [InlineData(0.41, 0.0, false)]
    [InlineData(0.0, 0.16, false)]
    [InlineData(-0.39, -0.14, true)]
    [InlineData(0.39, -0.16, false)]
    public void RectanglePoint_OnTheInstrumentAxes(double alongRef, double alongCross, bool inView)
    {
        double margin = FieldOfViewGeometry.Margin(InstrumentShape.Rectangular, 0.4, 0.15, Axes, OccultationBody.Point(Direction(alongRef, alongCross) * 7.0e6));
        Assert.Equal(inView, margin >= 0.0);
        Assert.Equal(System.Math.Min(0.4 - System.Math.Abs(alongRef), 0.15 - System.Math.Abs(alongCross)), margin, 12);
    }

    /// <summary>
    /// A rectangle against an oblate ellipsoid: the sign of the closed-form margin equals that of a brute-force grid of
    /// 401 × 401 rays through the rectangle, for targets inside the field, crossing each side and each corner, holding the
    /// field, beside it, behind the observer, and grazing it.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0, 0.02, 1.0e7)]
    [InlineData(0.42, 0.0, 0.05, 1.0e7)]
    [InlineData(0.46, 0.0, 0.05, 1.0e7)]
    [InlineData(0.0, 0.19, 0.05, 1.0e7)]
    [InlineData(0.0, 0.21, 0.05, 1.0e7)]
    [InlineData(0.43, 0.17, 0.05, 1.0e7)]
    [InlineData(0.47, 0.22, 0.05, 1.0e7)]
    [InlineData(0.1, 0.05, 0.9, 1.0e7)]
    [InlineData(1.2, 0.0, 0.3, 1.0e7)]
    [InlineData(0.0, 0.0, 0.3, -1.0e7)]
    [InlineData(0.6, 0.6, 0.4, 1.0e7)]
    public void RectangleEllipsoid_SignOfABruteForceRayGrid(double alongRef, double alongCross, double angularRadius, double distance)
    {
        double refAngle = 0.4, crossAngle = 0.15;
        // The direction of the center at the given angles from the boresight; a negative distance puts it behind
        var u = Direction(alongRef, alongCross).Normalize();
        var center = u * distance;
        double a = System.Math.Abs(distance) * System.Math.Sin(angularRadius);
        // Oblate and turned: semi-axes a, a, 0.9 a about axes that are not the field's
        var e1 = new Vector3(1.0, 1.0, 0.0).Normalize();
        var e2 = new Vector3(-1.0, 1.0, 0.5).Normalize();
        e2 = (e2 - e1 * (e2 * e1)).Normalize();
        var e3 = e1.Cross(e2);
        var target = OccultationBody.Ellipsoid(center, e1 * a, e2 * a, e3 * (0.9 * a));
        double margin = FieldOfViewGeometry.Margin(InstrumentShape.Rectangular, refAngle, crossAngle, Axes, target);

        bool hit = false;
        double tx = System.Math.Tan(crossAngle), ty = System.Math.Tan(refAngle);
        for (int i = 0; i <= 400 && !hit; i++)
        {
            for (int j = 0; j <= 400 && !hit; j++)
            {
                var ray = Axes.Boresight + Axes.Cross * (tx * (2.0 * i / 400.0 - 1.0)) + Axes.Ref * (ty * (2.0 * j / 400.0 - 1.0));
                hit = Meets(ray, center, e1, e2, e3, a, a, 0.9 * a);
            }
        }

        Assert.Equal(hit, margin >= 0.0);
    }

    // The ray t v, t > 0, meets the ellipsoid (x·e1/a)² + (x·e2/b)² + (x·e3/c)² = 1 about the center: the discriminant of the
    // quadratic in t is not negative and a root is positive
    private static bool Meets(Vector3 v, Vector3 center, Vector3 e1, Vector3 e2, Vector3 e3, double a, double b, double c)
    {
        double[] vs = [v * e1 / a, v * e2 / b, v * e3 / c];
        double[] cs = [center * e1 / a, center * e2 / b, center * e3 / c];
        double qa = 0.0, qb = 0.0, qc = -1.0;
        for (int i = 0; i < 3; i++)
        {
            qa += vs[i] * vs[i];
            qb -= 2.0 * vs[i] * cs[i];
            qc += cs[i] * cs[i];
        }

        double discriminant = qb * qb - 4.0 * qa * qc;
        if (discriminant < 0.0)
        {
            return false;
        }

        return (-qb + System.Math.Sqrt(discriminant)) / (2.0 * qa) > 0.0;
    }

    [Fact]
    public void RectangleEllipsoid_ObserverInside_Throws()
    {
        var target = OccultationBody.Ellipsoid(new Vector3(0.0, 0.0, 1.0e5), Vector3.VectorX * 6.4e6, Vector3.VectorY * 6.4e6, Vector3.VectorZ * 6.3e6);
        Assert.Throws<InvalidOperationException>(() => FieldOfViewGeometry.Margin(InstrumentShape.Rectangular, 0.4, 0.15, Axes, target));
    }
}
