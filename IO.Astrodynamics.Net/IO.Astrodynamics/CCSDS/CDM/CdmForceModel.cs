// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Variational;

namespace IO.Astrodynamics.CCSDS.CDM;

/// <summary>
/// The force model of an RK7(8) propagation in the words of the CDM metadata (CCSDS 508.0-B-1, §3.3:
/// <c>GRAVITY_MODEL</c>, <c>ATMOSPHERIC_MODEL</c>, <c>N_BODY_PERTURBATIONS</c>, <c>SOLAR_RAD_PRESSURE</c>), from the
/// forces the propagation actually used (step 10, E5).
/// </summary>
/// <param name="GravityModel">The gravity of the central body: <c>GEOPOTENTIAL: nD nO</c> for spherical harmonics to
/// degree and order n, <c>POINT MASS</c> otherwise.</param>
/// <param name="AtmosphericModel">The atmosphere of the drag, <c>NONE</c> without drag.</param>
/// <param name="NBodyPerturbations">The third bodies, comma-separated, <c>NONE</c> without any.</param>
/// <param name="SolarRadiationPressure">Whether solar radiation pressure was modeled.</param>
internal sealed record CdmForceModel(string GravityModel, string AtmosphericModel, string NBodyPerturbations,
    bool SolarRadiationPressure)
{
    /// <summary>
    /// The force model of the dynamics of a propagation, its central body being <paramref name="dynamics"/>' observer.
    /// </summary>
    internal static CdmForceModel From(PropagationDynamics dynamics)
    {
        var forces = dynamics.Forces;
        int centralId = dynamics.Observer.NaifId;

        var central = forces.OfType<GravitationalAcceleration>().FirstOrDefault(g => g.CelestialItem.NaifId == centralId);
        string gravity = central?.CelestialItem.Field is GeopotentialGravitationalField geopotential
            ? string.Create(CultureInfo.InvariantCulture, $"GEOPOTENTIAL: {geopotential.MaxDegree}D {geopotential.MaxDegree}O")
            : "POINT MASS";

        var drag = forces.OfType<AtmosphericDrag>().FirstOrDefault();
        string atmosphere = drag?.CelestialBody.Atmosphere is { } model ? AtmosphereName(model) : "NONE";

        var bodies = new List<string>();
        foreach (var force in forces)
        {
            string? name = force switch
            {
                ThirdBodyPerturbation thirdBody => thirdBody.PerturbingBody.Name,
                GravitationalAcceleration gravityOf when gravityOf.CelestialItem.NaifId != centralId =>
                    gravityOf.CelestialItem.Name,
                _ => null
            };
            if (name != null && !bodies.Contains(name.ToUpperInvariant()))
            {
                bodies.Add(name.ToUpperInvariant());
            }
        }

        return new CdmForceModel(gravity, atmosphere, bodies.Count > 0 ? string.Join(", ", bodies) : "NONE",
            forces.OfType<SolarRadiationPressure>().Any());
    }

    private static string AtmosphereName(IAtmosphericModel model) => model switch
    {
        Nrlmsise00Model => "NRLMSISE-00",
        EarthStandardAtmosphere => "US STANDARD ATMOSPHERE 1976",
        MarsStandardAtmosphere => "MARS STANDARD ATMOSPHERE",
        _ => model.GetType().Name.ToUpperInvariant()
    };
}
