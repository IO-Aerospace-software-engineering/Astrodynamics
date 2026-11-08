"""References of the managed TEME frame (#451, lot 5 of #457) for IO.Astrodynamics.Tests/Frame/TemeFrameTests.cs.

TEME is defined by Vallado, Crawford, Hujsak and Kelso (2006), AIAA 2006-6753 Rev2, appendix C (confirmed by
S. Guillet, 2026-11-08): r_PEF = R3(GMST82(UT1)) r_TEME, the PEF taken as the TIRS. Quasi-inertial: v' = R v. For each
point (a UTC epoch and a TEME state) this script gives the ICRF state by three routes:

  - "gmat":    GMAT R2026a, a Spacecraft in a TEME coordinate system (Axes = TEME) reported in EarthICRF, run here;
  - "cip_a":   ERFA's CIP route, TEME -> GCRS = c2i06a^T . rz(GMST82 - ERA), IAU 2006/2000A, UT1 = UTC (UT1 - UTC
               cancels between GMST82 and the ERA to 0.03 mm in LEO and 0.2 mm in GEO);
  - "cip_b":   the same route with IAU 2006 precession and IAU 2000B nutation (nut00b, pn06, bpn2xy, s06), the model of
               the library's Iau2006Model;
and ERFA's GMST82 at the point (UT1 = UTC), "gmst82_rad".

The points: a LEO and a GEO state at 13 epochs from 1980 to 2026, the five epochs of the TEME cases of the conformance
suite (frame_transformation 032 to 036; 036 with the state of Vallado's appendix C), and the SGP4 states at their own
epoch (tsince = 0) of five real TLEs, and of the ISS's 6 h later, from Vallado's code (python sgp4 2.27, WGS-72 old, the AFSPC mode 'a' of
sgp4init), which is the model of CSPICE's evsgp4_c that the library calls. Positions in km, velocities in km/s.

Run from the repository root (pyerfa 2.0.1.5, sgp4 2.27, spiceypy 8.3.0 with CSPICE N0067, numpy; GMAT R2026a):
  python3 -I tools/teme_reference/teme_ref.py <GMAT R2026a bin directory> <scratch directory> \
      IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem \
      > IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/TemeReference/teme_reference.json
"""
import json
import math
import os
import subprocess
import sys

import erfa
import numpy as np
import spiceypy as sp
from sgp4.api import Satrec, WGS72OLD

EPOCHS = [
    "1980-01-01T00:00:00.000", "1985-07-01T06:00:00.000", "1990-01-01T00:00:00.000",
    "1995-06-15T12:00:00.000", "2000-01-01T12:00:00.000", "2004-04-06T07:51:28.386",
    "2008-09-20T03:00:00.000", "2012-02-17T00:00:00.000", "2016-01-01T00:00:00.000",
    "2021-01-20T12:50:13.663", "2021-03-20T11:58:50.814", "2024-08-26T22:34:20.000",
    "2026-05-01T00:00:00.000",
]
A = math.radians(30.0)
VG = 3.0746676
STATES = {
    "LEO": ([7000.0, 1000.0, 500.0], [-0.5, 7.2, 1.1]),
    "GEO": ([42164.0 * math.cos(A), 42164.0 * math.sin(A), 10.0], [-VG * math.sin(A), VG * math.cos(A), 0.01]),
}
# Vallado et al. (2006), appendix C: the TEME state of the example (as skyfield's tests read it)
VALLADO = ("2004-04-06T07:51:28.386", [5094.18016210, 6127.64465950, 6380.34453270],
           [-4.746131487, 0.785818041, 5.531931288])
ISS_2021 = ("1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703")
ISS_2024 = ("1 25544U 98067A   24153.17509025  .00020162  00000+0  35104-3 0  9990",
            "2 25544  51.6393  34.6631 0005642 260.2910 238.1766 15.50732314456064")
# (name, line 1, line 2, instant or None for the TLE epoch); an instant is "<ISO> UTC" or "<ISO> TDB", converted by CSPICE
# with the leapseconds kernel of the tests; tsince = the TT seconds from the TLE epoch over 60 (the windows hold no leap
# second). The ISS of 2024 at the two TDB instants of ScenarioTests.PropagateSpacecraftFromTLE, with the state of DSS-63
# (earthstns_itrf93_201023.bsp and the Earth's binary PCK of the tests) relative to the Earth in J2000 there.
TLES = [
    ("ISS 2024 start", *ISS_2024, "2024-06-03 00:00:00.000 TDB"),
    ("ISS 2024 end", *ISS_2024, "2024-06-04 00:00:00.000 TDB"),
    ("ISS 2021", *ISS_2021, None),
    ("ISS 2021 +6 h", *ISS_2021, "2021-01-20 18:50:13.663 UTC"),
    ("CZ-3C DEB", "1 39348U 10057N   24238.91466777  .00000306  00000-0  19116-2 0  9995",
     "2 39348  20.0230 212.2863 7218258 312.9449   5.6833  2.25781763 89468", None),
    ("ISS 2026", "1 25544U 98067A   26311.20783432  .00014294  00000+0  26479-3 0  9992",
     "2 25544  51.6321 312.3516 0007247 357.8301   2.2655 15.49235271593925", None),
    ("TDRS 6", "1 22314U 93003B   26311.02217943 -.00000298  00000+0  00000+0 0  9996",
     "2 22314  14.1867 355.9844 0009055 216.4317 156.0344  1.00271830123845", None),
    ("WIND", "1 23333U 94071A   94305.49999999 -.00172956  26967-3  10000-3 0    15",
     "2 23333  28.7490   2.3720 9728298  30.4360   1.3500  0.07309491    70", None),
]
MON = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]


def utc_jd(iso):
    d, t = iso.split("T")
    y, mo, da = map(int, d.split("-"))
    h, mi, s = t.split(":")
    return erfa.dtf2d("UTC", y, mo, da, int(h), int(mi), float(s))


def teme_to_gcrs(u1, u2, model):
    a1, a2 = erfa.utctai(u1, u2)
    t1, t2 = erfa.taitt(a1, a2)
    if model == "A":
        c2i = erfa.c2i06a(t1, t2)
    else:
        dpsi, deps = erfa.nut00b(t1, t2)
        x, y = erfa.bpn2xy(erfa.pn06(t1, t2, dpsi, deps)[-1])
        c2i = erfa.c2ixys(x, y, erfa.s06(t1, t2, x, y))
    return c2i.T @ erfa.rz(erfa.gmst82(u1, u2) - erfa.era00(u1, u2), np.eye(3))


def sgp4_state(l1, l2, utc):
    s0 = Satrec.twoline2rv(l1, l2, WGS72OLD)
    s = Satrec()
    s.sgp4init(WGS72OLD, "a", s0.satnum, s0.jdsatepoch + s0.jdsatepochF - 2433281.5, s0.bstar, s0.ndot, s0.nddot,
               s0.ecco, s0.argpo, s0.inclo, s0.mo, s0.no_kozai, s0.nodeo)
    # The TLE epoch as a UTC Julian date (two parts), as sgp4 reads it
    jd = (s0.jdsatepoch, s0.jdsatepochF)
    tsince = 0.0
    if utc is not None:
        y, mo, d, hmsf = erfa.d2dtf("UTC", 6, s0.jdsatepoch, s0.jdsatepochF)
        epoch = f"{int(y):04d}-{int(mo):02d}-{int(d):02d} {int(hmsf['h']):02d}:{int(hmsf['m']):02d}:{int(hmsf['s']):02d}.{int(hmsf['f']):06d} UTC"
        et_epoch, et = sp.str2et(epoch), sp.str2et(utc)
        tsince = (sp.unitim(et, "ET", "TDT") - sp.unitim(et_epoch, "ET", "TDT")) / 60.0
        jd = utc_jd(sp.et2utc(et, "ISOC", 9))
    err, r, v = s.sgp4_tsince(tsince)
    if err != 0:
        raise RuntimeError(f"sgp4 error {err} for {l1}")
    return jd, tsince, list(r), list(v)


def main(gmat_bin, scratch, kernels):
    for name in ("latest_leapseconds.tls", "de440s.bsp", "earth_latest_high_prec.bpc", "earth_fixed.tf",
                 "earth_assoc_itrf93.tf", "earth_topo_201023.tf", "earthstns_itrf93_201023.bsp", "pck00011.tpc"):
        sp.furnsh(os.path.join(kernels, name))
    points = []
    for iso in EPOCHS:
        for name, (r, v) in STATES.items():
            points.append(dict(kind="frame", name=name, utc=iso, jd=utc_jd(iso), r=r, v=v))
    points.append(dict(kind="frame", name="VALLADO", utc=VALLADO[0], jd=utc_jd(VALLADO[0]), r=VALLADO[1], v=VALLADO[2]))
    for name, l1, l2, utc in TLES:
        jd, tsince, r, v = sgp4_state(l1, l2, utc)
        point = dict(kind="tle", name=name, line1=l1, line2=l2, utc=utc, tsince=tsince, jd=jd, r=r, v=v)
        if name.startswith("ISS 2024"):
            point["dss63"] = list(sp.spkezr("DSS-63", sp.str2et(utc), "J2000", "NONE", "EARTH")[0])
        points.append(point)

    # GMAT: one Spacecraft per point in TEME axes, reported in EarthICRF; the epoch as a UTC modified Julian date of GMAT
    # (JD - 2430000), 17 digits
    os.makedirs(scratch, exist_ok=True)
    report = os.path.join(scratch, "teme_gmat.txt")
    lines = ["Create CoordinateSystem TEMEcs;", "GMAT TEMEcs.Origin = Earth;", "GMAT TEMEcs.Axes = TEME;"]
    for i, p in enumerate(points):
        mjd = (p["jd"][0] - 2430000.0) + p["jd"][1]
        lines += [f"Create Spacecraft S{i};", f"GMAT S{i}.DateFormat = UTCModJulian;", f"GMAT S{i}.Epoch = '{mjd:.17g}';",
                  f"GMAT S{i}.CoordinateSystem = TEMEcs;", f"GMAT S{i}.DisplayStateType = Cartesian;"]
        lines += [f"GMAT S{i}.{k} = {x!r};" for k, x in zip(("X", "Y", "Z", "VX", "VY", "VZ"), p["r"] + p["v"])]
    lines += ["Create ReportFile rf;", f"GMAT rf.Filename = '{report}';", "GMAT rf.Precision = 17;",
              "GMAT rf.WriteHeaders = false;", "BeginMissionSequence;"]
    lines += [f"Report rf S{i}.UTCModJulian " + " ".join(f"S{i}.EarthICRF.{k}" for k in ("X", "Y", "Z", "VX", "VY", "VZ")) + ";"
              for i in range(len(points))]
    script = os.path.join(scratch, "teme.script")
    with open(script, "w") as f:
        f.write("\n".join(lines) + "\n")
    if os.path.exists(report):
        os.remove(report)
    with open(os.path.join(scratch, "teme.log"), "w") as log:
        subprocess.run(["./GmatConsole", "--run", script], cwd=gmat_bin, stdout=log, stderr=subprocess.STDOUT, check=True)
    rows = [list(map(float, l.split())) for l in open(report) if l.strip()]
    if len(rows) != len(points):
        raise RuntimeError(f"GMAT reported {len(rows)} rows for {len(points)} points")

    out = []
    for p, g in zip(points, rows):
        e = {k: p[k] for k in ("kind", "name")}
        if p["kind"] == "frame":
            e["utc"] = p["utc"]
        else:
            e["line1"], e["line2"], e["tsince_min"] = p["line1"], p["line2"], p["tsince"]
            if p["utc"] is not None:
                e["instant"] = p["utc"]
            if "dss63" in p:
                e["dss63_j2000"] = p["dss63"]
        e["gmat_utc_mjd"] = g[0]
        e["teme"] = p["r"] + p["v"]
        e["gmat"] = g[1:7]
        e["gmst82_rad"] = erfa.gmst82(p["jd"][0], p["jd"][1])
        for key, model in (("cip_a", "A"), ("cip_b", "B")):
            m = teme_to_gcrs(p["jd"][0], p["jd"][1], model)
            e[key] = list(m @ np.array(p["r"])) + list(m @ np.array(p["v"]))
        out.append(e)
    json.dump({"generator": "tools/teme_reference/teme_ref.py", "erfa": erfa.__version__,
               "gmat": "R2026a", "points": out}, sys.stdout, indent=1)
    sys.stdout.write("\n")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3])
