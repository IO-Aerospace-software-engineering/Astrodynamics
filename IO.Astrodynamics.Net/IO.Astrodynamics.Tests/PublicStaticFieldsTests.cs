using System.Linq;
using System.Reflection;
using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests;

public class PublicStaticFieldsTests
{
    [Fact]
    public void EveryPublicStaticFieldOfTheLibrary_IsReadOnly()
    {
        // #571: a public static field that is not readonly lets any caller change a value the whole process shares (the
        // identity rotation, a body of SolarSystemObjects). Every public static field of every public type of the library is
        // a constant or init-only. Reflection reads the field metadata only: no type initializer runs.
        var mutable = typeof(Quaternion).Assembly.GetExportedTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(f => !f.IsInitOnly && !f.IsLiteral)
                .Select(f => $"{t.FullName}.{f.Name}"))
            .ToArray();
        Assert.Empty(mutable);
    }

    [Fact]
    public void TheBodiesOfSolarSystemObjects_AreReadOnly()
    {
        // The fields #571 names: the NaifObject and CelestialBody fields of PlanetsAndMoons and Stars, with those of the other
        // classes of the namespace (the barycenters, the asteroids, the comets, the ground stations, the Lagrange points)
        var types = typeof(Quaternion).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == "IO.Astrodynamics.SolarSystemObjects" && t.IsAbstract && t.IsSealed)
            .ToArray();
        Assert.Contains(typeof(SolarSystemObjects.PlanetsAndMoons), types);
        Assert.Contains(typeof(SolarSystemObjects.Stars), types);
        var fields = types.SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static)).ToArray();
        Assert.True(fields.Length > 300);
        Assert.All(fields, f => Assert.True(f.IsInitOnly, f.DeclaringType!.Name + "." + f.Name));
        Assert.True(typeof(SolarSystemObjects.PlanetsAndMoons).GetField(nameof(SolarSystemObjects.PlanetsAndMoons.EARTH_BODY))!.IsInitOnly);
        Assert.True(typeof(SolarSystemObjects.Stars).GetField(nameof(SolarSystemObjects.Stars.SUN_BODY))!.IsInitOnly);
    }
}
