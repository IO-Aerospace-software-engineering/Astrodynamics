"""Compute GMAT's A matrix (the partials of the acceleration) of each force at the four states of the force partials
of IO.Astrodynamics (B7), through the official GMAT API shipped with the GMAT R2026a build (bin/gmatpy, not the
third-party PyPI package). Part of the comparison of IO.Astrodynamics with GMAT (H2 of phase 2, feature 1; #408).

For each state and force of the "partials" section of cases.json, writes <output>/partials/<state>_<force>.txt: one
line of 42 numbers, GMAT's derivative array with the A matrix on (PropagationStateManager property "AMatrix"): the
derivative of the state (km/s, km/s^2), then A, 6 x 6 row-major ([0, I; da/dr, da/dv], 1/s and 1/s^2), turned from
GMAT's EarthMJ2000Eq axes to EarthICRF, the axes of SPICE J2000 (the state is given in EarthICRF).

Forces, with the models aligned as in generate_scripts.py:
  earth_point_mass     GravityField of degree 0, the GM of IO.Astrodynamics
  earth_geopotential   GravityField of the converted EGM2008 to the degree of cases.json
  moon, sun            PointMassForce, the GM of IO.Astrodynamics
  srp                  SolarRadiationPressure, spherical, the flux at 1 AU and the AU of IO.Astrodynamics
  drag                 DragForce with NRLMSISE00 and constant F10.7, F10.7A and Kp; GMAT adds a drag force only with
                       a primary body, so the point mass of degree 0 is added, and its derivative, written before by the
                       run of earth_point_mass, is subtracted

Usage, from GMAT's bin directory or any directory:
  python3 -I gmat_partials.py <GMAT bin directory> <cases.json> <SPK> <.cof> <output directory> <state id> <force>
One state and one force per process: the API keeps its objects for the life of the process.
"""
import json
import os
import sys


def setup(gmat_bin):
    sys.path.insert(0, gmat_bin)
    import gmatpy as gmat  # noqa: E402 (the build's API, found through gmat_bin)
    gmat.Setup(os.path.join(gmat_bin, "api_startup_file.txt"))
    return gmat


def derivative(gmat, case, partials, state, forces, spk, cof):
    solar_system = gmat.GetSolarSystem()
    solar_system.SetField("EphemerisSource", "SPICE")
    solar_system.SetField("SPKFilename", spk)
    earth = solar_system.GetBody("Earth")
    earth.SetField("Mu", case["earth"]["gm_m3_s2"] / 1.0e9)
    earth.SetField("EquatorialRadius", case["earth"]["equatorial_radius_m"] / 1000.0)
    earth.SetField("Flattening", case["earth"]["flattening"])
    solar_system.GetBody("Sun").SetField("EquatorialRadius", case["sun_equatorial_radius_m"] / 1000.0)
    solar_system.GetBody("Sun").SetField("Mu", partials["sun_gm_m3_s2"] / 1.0e9)
    solar_system.GetBody("Luna").SetField("Mu", partials["moon_gm_m3_s2"] / 1.0e9)

    epoch = partials["epoch_tdb"]
    months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]
    date, time = epoch.split("T")
    year, month, day = date.split("-")
    sat = gmat.Construct("Spacecraft", "Sat")
    sat.SetField("DateFormat", "TDBGregorian")
    sat.SetField("Epoch", "%s %s %s %s" % (day, months[int(month) - 1], year, time))
    sat.SetField("CoordinateSystem", "EarthICRF")
    for name, value in zip(["X", "Y", "Z", "VX", "VY", "VZ"], state["state_m_m_s"]):
        sat.SetField(name, value / 1000.0)
    spacecraft = case["spacecraft"]
    sat.SetField("DryMass", spacecraft["mass_kg"])
    sat.SetField("SRPArea", spacecraft["area_m2"])
    sat.SetField("DragArea", spacecraft["area_m2"])
    sat.SetField("Cd", spacecraft["drag_coefficient"])
    sat.SetField("Cr", spacecraft["reflectivity_coefficient"])

    fm = gmat.Construct("ForceModel", "FM")
    fm.SetField("CentralBody", "Earth")
    kept = []
    for index, force in enumerate(forces):
        name = "F%d" % index
        if force in ("earth_point_mass", "earth_geopotential"):
            degree = 0 if force == "earth_point_mass" else partials["geopotential_degree"]
            f = gmat.Construct("GravityField", name)
            f.SetField("BodyName", "Earth")
            f.SetField("PotentialFile", cof)
            f.SetField("Degree", degree)
            f.SetField("Order", degree)
        elif force in ("moon", "sun"):
            f = gmat.Construct("PointMassForce", name)
            f.SetField("BodyName", "Luna" if force == "moon" else "Sun")
        elif force == "srp":
            f = gmat.Construct("SolarRadiationPressure", name)
            f.SetField("Flux", case["srp_flux_at_1_au_w_m2"])
            f.SetField("Nominal_Sun", case["au_m"] / 1000.0)
        elif force == "drag":
            weather = case["space_weather"]
            f = gmat.Construct("DragForce", name)
            f.SetField("AtmosphereModel", "NRLMSISE00")
            atmosphere = gmat.Construct("NRLMSISE00", name + "Atmosphere")
            f.SetReference(atmosphere)
            f.SetField("F107", weather["f107"])
            f.SetField("F107A", weather["f107a"])
            f.SetField("MagneticIndex", weather["kp"])
            kept.append(atmosphere)
        else:
            raise ValueError("Unknown force " + force)
        fm.AddForce(f)
        kept.append(f)

    psm = gmat.PropagationStateManager()
    psm.SetObject(sat)
    psm.SetProperty("AMatrix")
    psm.BuildState()
    fm.SetPropStateManager(psm)
    fm.SetState(psm.GetState())
    gmat.Initialize()
    fm.BuildModelFromMap()
    fm.UpdateInitialData()
    mj2000 = list(sat.GetState().GetState())[:6]
    fm.GetDerivatives(sat.GetState().GetState())
    return to_icrf(list(fm.GetDerivativeArray()), [x / 1000.0 for x in state["state_m_m_s"]], mj2000)


def triad(r, v):
    """The orthonormal triad of a position and a velocity, as the columns of a 3 x 3 matrix (rows of lists)."""
    def unit(a):
        n = sum(x * x for x in a) ** 0.5
        return [x / n for x in a]

    def cross(a, b):
        return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]

    e1 = unit(r)
    e3 = unit(cross(r, v))
    e2 = cross(e3, e1)
    return [[e1[i], e2[i], e3[i]] for i in range(3)]


def to_icrf(values, icrf, mj2000):
    """GMAT's derivative array (in EarthMJ2000Eq axes, the axes of its state) turned to EarthICRF, the axes of SPICE
    J2000: R = T_icrf T_mj2000^T from the triads of the same state in both, then the derivative of the state by R and
    each 3 x 3 block of A by R . R^T."""
    a, b = triad(icrf[:3], icrf[3:]), triad(mj2000[:3], mj2000[3:])
    rotation = [[sum(a[i][k] * b[j][k] for k in range(3)) for j in range(3)] for i in range(3)]

    def rotate(vector):
        return [sum(rotation[i][k] * vector[k] for k in range(3)) for i in range(3)]

    result = rotate(values[0:3]) + rotate(values[3:6])
    matrix = [values[6 + 6 * i:12 + 6 * i] for i in range(6)]
    rotated = [[0.0] * 6 for _ in range(6)]
    for bi in (0, 3):
        for bj in (0, 3):
            for i in range(3):
                for j in range(3):
                    rotated[bi + i][bj + j] = sum(rotation[i][k] * matrix[bi + k][bj + m] * rotation[j][m]
                                                  for k in range(3) for m in range(3))
    return result + [x for row in rotated for x in row]


def main():
    if len(sys.argv) != 8:
        sys.exit(__doc__)
    gmat_bin, cases_path, spk, cof, output, state_id, force = sys.argv[1:]
    with open(cases_path) as f:
        document = json.load(f)
    case = document["cases"][0]
    partials = document["partials"]
    state = next(s for s in partials["states"] if s["id"] == state_id)
    gmat = setup(gmat_bin)
    directory = os.path.join(output, "partials")
    os.makedirs(directory, exist_ok=True)
    if force == "drag":
        # The derivative of the drag force alone: with the point mass, minus the point mass alone, written before by
        # the run of earth_point_mass (the first force of every state)
        with_point_mass = derivative(gmat, case, partials, state, ["earth_point_mass", "drag"], spk, cof)
        with open(os.path.join(directory, "%s_earth_point_mass.txt" % state_id)) as f:
            point_mass = [float(x) for x in f.read().split()]
        values = [a - b for a, b in zip(with_point_mass, point_mass)]
    else:
        values = derivative(gmat, case, partials, state, [force], spk, cof)
    if len(values) != 42:
        sys.exit("GMAT returned %d derivatives, not 42" % len(values))
    with open(os.path.join(directory, "%s_%s.txt" % (state_id, force)), "w", newline="\n") as f:
        f.write(" ".join(repr(v) for v in values) + "\n")


if __name__ == "__main__":
    main()
