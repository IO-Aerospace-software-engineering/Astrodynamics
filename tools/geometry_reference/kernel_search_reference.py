"""CSPICE references for the window searches that the library hands to CSPICE (#466, #488): gfdist, gfposc and gfilum on
objects of the kernels, converged to 1e-9 s (gfstol), finer than the 1e-6 s of the native layer's searches.

#466, the distance searches of a kernel spacecraft (CelestialItem and Site.FindWindowsOnDistanceConstraint, gfdist):
  - LRO (-85, the SPK of the tests) from the Moon beyond, within and at 1815.148 km, from the Earth-Moon barycenter beyond
    364 500 km, from DSS-13 (399013, earthstns_itrf93_201023.bsp) beyond 372 000 km, and from a second kernel spacecraft
    within 3000 km: a two-body lunar orbiter (-85901; a = 2500 km, e = 0.2, i = 60 deg, Omega = 30 deg, omega = 45 deg,
    M = 0 at 2023-12-15 00:00 TDB, the Moon's GM of gm_de440.tpc), written here as a type 5 SPK
    (kernel_search_orbiter.bsp, states every 600 s from 2023-12-14 23:00 to 2023-12-16 01:00 TDB);
  - the coordinate searches of the same objects (gfposc), whose distances are in km: LRO from the Moon Z > 500 km and
    range > 1815.148 km in J2000, DSS-13 from LRO range > 372 000 km in J2000.
  Search window 2023-12-15 00:05 to 12:05 TDB (the SPK of LRO starts at 00:00 UTC, 00:01:09.18 TDB), step 60 s, no
  aberration correction.

#488, the illumination searches of a kernel site and of SpiceAPI.FindWindowsOnIlluminationConstraint (gfilum, method
ELLIPSOID, the Sun as source, the incidence angle):
  - DSS-13 as a kernel site: the surface point is its position at J2000 TDB (spkpos 399013 in ITRF93), the position
    from which the library computes its planetodetic coordinates; incidence > 90 deg without correction, < 90.8 deg
    and < 96 deg with LT+S (Site.FindDayWindows at 0.8 and 6 deg), > 60 deg with CN+S; observer 399013;
  - points given by their coordinates, georec(lon, lat, altitude in km) on the ellipsoid of pck00011 (the equatorial
    radius and the flattening (a - c) / a, as the native layer builds the point): the longitude and latitude of DSS-13
    at 0, 1.071 and 5 km, latitudes 0 and 60 deg at 1.071 km (observer 399013), incidence > 90 deg without correction;
    the Moon (target 301, its frame from cidfrm, observer 399) at longitude 0.5 rad, latitude 0.3 rad, 1.071 km, over
    30 days at a 1 h step.
  Search window 2023-12-15 00:05 TDB to 2023-12-16 00:05 TDB (the Moon: to 2024-01-14 00:05 TDB), step 60 s.

#678, the managed threshold searches against CSPICE at steps much longer than their windows:
  - a two-body LEO (-67801; a = 6800 km, e = 1e-4, i = 51.6 deg, Omega = omega = M = 0 at 2024-03-01 00:00 TDB, the
    Earth's GM of gm_de440.tpc), written here as a type 5 SPK (kernel_search_leo.bsp, states every 60 s from 2024-02-29
    23:00 to 2024-03-02 01:00 TDB, centre 399, J2000), each state the conic at the TT seconds from the epoch to its own,
    as the library's two-body motion and propagation count them about the Earth (TT - TDB moves by up to 2.9e-5 s a day);
    seen from DSS-13 (399013) over 2024-03-01 00:00 to 2024-03-02 00:00 TDB: elevation > 30 deg (gfposc, declination
    in DSS-13_TOPO) and range < 1000 km (gfdist), step 60 s, no aberration correction; and the elevation in the
    topocentric frame of the library's site (north, west, zenith from the planetodetic coordinates of DSS-13 at J2000 on
    pck00011, relative to ITRF93), defined here as DSS-13_J2000_TOPO (1678013), which the frame kernel's DSS-13_TOPO
    (its coordinates at 2020-10-23 on GRS80) differs from by 7.6e-8 rad in longitude and 1.7e-8 rad in latitude;
  - the point of DSS-13's longitude and latitude at 0 m on the ellipsoid (georec, as above), incidence of the Sun
    < 58.83 deg (the minimum over the window, 58.5315 deg, plus 0.3 deg, rounded), without correction, observer 399013,
    over 2023-12-15 00:05 to 2023-12-18 00:05 TDB, step 60 s.

Each entry is "<label>": a list of [start, end] in TDB seconds from J2000. The script also prints the string that the
CLI command find-windows-from-illumination-constraint prints for the point at longitude 0.0349 rad, latitude 0.8378 rad
and 1071 m (the window and step of CLI GeometryFinderTests.IlluminationConstraint, CN+S, incidence < 90.8 deg), the
native search replicated at its default tolerance with each edge the nearest tick (as tools/seconds_to_ticks
/cli_windows_reference.py does), on the kernels of the CLI tests.

Run from this directory (needs spiceypy, CSPICE N0067):
  python3 -I kernel_search_reference.py
It writes IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/GeometryReference/kernel_search_reference.json,
kernel_search_orbiter.bsp and kernel_search_leo.bsp.
"""
import datetime as D
import json
import math
import os
from fractions import Fraction

import spiceypy as sp

HERE = os.path.dirname(os.path.abspath(__file__))
TESTS = os.path.join(HERE, '..', '..', 'IO.Astrodynamics.Net', 'IO.Astrodynamics.Tests', 'Data')
CLI = os.path.join(HERE, '..', '..', 'IO.Astrodynamics.Net', 'IO.Astrodynamics.CLI.Tests', 'Data')
OUT = os.path.join(TESTS, 'GeometryReference')
ORBITER = os.path.join(OUT, 'kernel_search_orbiter.bsp')
LEO = os.path.join(OUT, 'kernel_search_leo.bsp')
EXTENSIONS = ('.tls', '.bsp', '.tpc', '.bpc', '.tf', '.ker')


def load_directory(path):
    for f in sorted(os.listdir(path)):
        if os.path.splitext(f)[1] in EXTENSIONS:
            sp.furnsh(os.path.join(path, f))


def windows(cell):
    return [list(sp.wnfetd(cell, i)) for i in range(sp.wncard(cell))]


def window(first, last):
    cnfine = sp.cell_double(2)
    sp.wninsd(first, last, cnfine)
    return cnfine


def distance(target, observer, relate, km, first, last, step):
    result = sp.cell_double(2000)
    sp.gfdist(target, 'NONE', observer, relate, km, 0.0, step, 1000, window(first, last), result)
    return windows(result)


def coordinate(target, frame, observer, system, coord, relate, value, first, last, step):
    result = sp.cell_double(2000)
    sp.gfposc(target, frame, 'NONE', observer, system, coord, relate, value, 0.0, step, 1000, window(first, last), result)
    return windows(result)


def illumination(target, fixref, abcorr, observer, point, relate, angle, first, last, step):
    result = sp.cell_double(2000)
    sp.gfilum('ELLIPSOID', 'INCIDENCE', target, '10', fixref, abcorr, observer, point, relate, angle, 0.0, step, 1000,
              window(first, last), result)
    return windows(result)


def write_orbiter(gm_moon, first, last):
    """A two-body lunar orbiter as a type 5 SPK: states every 600 s, conic interpolation between them."""
    if os.path.exists(ORBITER):
        os.remove(ORBITER)
    t0 = sp.str2et('2023-12-15 00:00:00 TDB')
    a, e = 2500.0, 0.2
    elements = [a * (1.0 - e), e, math.radians(60.0), math.radians(30.0), math.radians(45.0), 0.0, t0, gm_moon]
    epochs = [first + 600.0 * i for i in range(int(round((last - first) / 600.0)) + 1)]
    states = [sp.conics(elements, t) for t in epochs]
    handle = sp.spkopn(ORBITER, 'kernel_search_orbiter', 0)
    sp.spkw05(handle, -85901, 301, 'J2000', epochs[0], epochs[-1], 'two-body lunar orbiter', gm_moon, len(epochs), states,
              epochs)
    sp.spkcls(handle)


def site_frame(frame_id, name, centre, lon, lat):
    """The topocentric frame of a site at planetodetic coordinates relative to ITRF93: X north, Y west, Z zenith."""
    sp.pipool(f'FRAME_{name}', [frame_id])
    sp.pcpool(f'FRAME_{frame_id}_NAME', [name])
    sp.pipool(f'FRAME_{frame_id}_CLASS', [4])
    sp.pipool(f'FRAME_{frame_id}_CLASS_ID', [frame_id])
    sp.pipool(f'FRAME_{frame_id}_CENTER', [centre])
    sp.pcpool(f'TKFRAME_{frame_id}_RELATIVE', ['ITRF93'])
    sp.pcpool(f'TKFRAME_{frame_id}_SPEC', ['ANGLES'])
    sp.pcpool(f'TKFRAME_{frame_id}_UNITS', ['RADIANS'])
    sp.pipool(f'TKFRAME_{frame_id}_AXES', [3, 2, 3])
    sp.pdpool(f'TKFRAME_{frame_id}_ANGLES', [-lon, -(math.pi / 2.0 - lat), math.pi])


def write_leo(gm_earth, t0, first, last):
    """A two-body LEO as a type 5 SPK: states every 60 s, each the conic at the TT seconds from t0 to its epoch."""
    if os.path.exists(LEO):
        os.remove(LEO)
    a, e = 6800.0, 1.0e-4
    elements = [a * (1.0 - e), e, math.radians(51.6), 0.0, 0.0, 0.0, t0, gm_earth]
    tt0 = sp.unitim(t0, 'TDB', 'TT')
    epochs = [first + 60.0 * i for i in range(int(round((last - first) / 60.0)) + 1)]
    states = [sp.conics(elements, t0 + (sp.unitim(t, 'TDB', 'TT') - tt0)) for t in epochs]
    handle = sp.spkopn(LEO, 'kernel_search_leo', 0)
    sp.spkw05(handle, -67801, 399, 'J2000', epochs[0], epochs[-1], 'two-body LEO', gm_earth, len(epochs), states, epochs)
    sp.spkcls(handle)


def main():
    load_directory(os.path.join(TESTS, 'SolarSystem'))
    sp.furnsh(os.path.join(TESTS, 'LRO', 'lro-reconstructed-2023349_2024075_v01_little_endian.bsp'))
    gm_moon = sp.bodvcd(301, 'GM', 1)[1][0]
    write_orbiter(gm_moon, sp.str2et('2023-12-14 23:00:00 TDB'), sp.str2et('2023-12-16 01:00:00 TDB'))
    sp.furnsh(ORBITER)
    gm_earth = sp.bodvcd(399, 'GM', 1)[1][0]
    write_leo(gm_earth, sp.str2et('2024-03-01 00:00:00 TDB'), sp.str2et('2024-02-29 23:00:00 TDB'),
              sp.str2et('2024-03-02 01:00:00 TDB'))
    sp.furnsh(LEO)
    sp.gfstol(1e-9)

    ref = {}
    a0, a1 = sp.str2et('2023-12-15 00:05:00 TDB'), sp.str2et('2023-12-15 12:05:00 TDB')
    for relate, label in (('>', '>'), ('<', '<'), ('=', '=')):
        ref[f'lro_moon_distance {label} 1815.148 km'] = distance('-85', '301', relate, 1815.148, a0, a1, 60.0)
    ref['lro_emb_distance > 364500 km'] = distance('-85', '3', '>', 364500.0, a0, a1, 60.0)
    ref['lro_dss13_distance > 372000 km'] = distance('399013', '-85', '>', 372000.0, a0, a1, 60.0)
    ref['lro_orbiter_distance < 3000 km'] = distance('-85', '-85901', '<', 3000.0, a0, a1, 60.0)
    ref['lro_moon_z > 500 km'] = coordinate('-85', 'J2000', '301', 'RECTANGULAR', 'Z', '>', 500.0, a0, a1, 60.0)
    ref['lro_moon_range > 1815.148 km'] = coordinate('-85', 'J2000', '301', 'RA/DEC', 'RANGE', '>', 1815.148, a0, a1, 60.0)
    ref['lro_dss13_range > 372000 km'] = coordinate('399013', 'J2000', '-85', 'RA/DEC', 'RANGE', '>', 372000.0, a0, a1, 60.0)

    radii = sp.bodvcd(399, 'RADII', 3)[1]
    f = (radii[0] - radii[2]) / radii[0]
    dss13 = sp.spkpos('399013', 0.0, 'ITRF93', 'NONE', '399')[0]
    lon, lat, alt = sp.recgeo(dss13, radii[0], f)
    print(f'DSS-13 at J2000 on pck00011: lon {math.degrees(lon)!r} deg, lat {math.degrees(lat)!r} deg, alt {alt * 1000.0!r} m')
    i0, i1 = sp.str2et('2023-12-15 00:05:00 TDB'), sp.str2et('2023-12-16 00:05:00 TDB')
    half_pi = math.pi / 2.0
    for label, abcorr, relate, angle in (('> 90 deg NONE', 'NONE', '>', half_pi),
                                         ('< 90.8 deg LT+S', 'LT+S', '<', half_pi + math.radians(0.8)),
                                         ('< 96 deg LT+S', 'LT+S', '<', half_pi + math.radians(6.0)),
                                         ('> 60 deg CN+S', 'CN+S', '>', math.radians(60.0))):
        ref[f'dss13_sun_incidence {label}'] = illumination('399', 'ITRF93', abcorr, '399013', dss13, relate, angle, i0, i1,
                                                           60.0)
    for label, latitude, km in (('dss13 0 m', lat, 0.0), ('dss13 1071 m', lat, 1.071), ('dss13 5000 m', lat, 5.0),
                                ('lat 0 1071 m', 0.0, 1.071), ('lat 60 1071 m', math.radians(60.0), 1.071)):
        point = sp.georec(lon, latitude, km, radii[0], f)
        ref[f'point_sun_incidence {label} > 90 deg NONE'] = illumination('399', 'ITRF93', 'NONE', '399013', point, '>',
                                                                        half_pi, i0, i1, 60.0)
    point = sp.georec(lon, lat, 0.0, radii[0], f)
    ref['point_sun_incidence dss13 0 m < 58.83 deg NONE 3 days'] = illumination('399', 'ITRF93', 'NONE', '399013', point, '<',
                                                                              math.radians(58.83), i0, i0 + 3.0 * 86400.0,
                                                                              60.0)
    l0, l1 = sp.str2et('2024-03-01 00:00:00 TDB'), sp.str2et('2024-03-02 00:00:00 TDB')
    ref['leo_dss13_elevation > 30 deg'] = coordinate('-67801', 'DSS-13_TOPO', '399013', 'RA/DEC', 'DECLINATION', '>',
                                                     math.radians(30.0), l0, l1, 60.0)
    site_frame(1678013, 'DSS-13_J2000_TOPO', 399013, lon, lat)
    ref['leo_dss13_j2000_topo_elevation > 30 deg'] = coordinate('-67801', 'DSS-13_J2000_TOPO', '399013', 'RA/DEC',
                                                                'DECLINATION', '>', math.radians(30.0), l0, l1, 60.0)
    ref['leo_dss13_distance < 1000 km'] = distance('-67801', '399013', '<', 1000.0, l0, l1, 60.0)
    moon_radii = sp.bodvcd(301, 'RADII', 3)[1]
    moon_frame = sp.cidfrm(301)[1]
    point = sp.georec(0.5, 0.3, 1.071, moon_radii[0], (moon_radii[0] - moon_radii[2]) / moon_radii[0])
    m1 = sp.str2et('2024-01-14 00:05:00 TDB')
    ref['moon_point_sun_incidence 1071 m > 90 deg NONE'] = illumination('301', moon_frame, 'NONE', '399', point, '>',
                                                                        half_pi, i0, m1, 3600.0)
    print(f'Moon frame {moon_frame}')

    with open(os.path.join(OUT, 'kernel_search_reference.json'), 'w', newline='\n') as out:
        out.write('{\n' + ',\n'.join(f'  {json.dumps(k)}: [' + ', '.join(f'[{a!r}, {b!r}]' for a, b in v) + ']'
                                     for k, v in ref.items()) + '\n}\n')
    for k, v in ref.items():
        print(f'{k}: {len(v)} windows')
    cli()


def cli():
    """The CLI's illumination search at 1071 m, replicated as the native layer runs it, on the kernels of the CLI tests."""
    sp.kclear()
    for root, _, files in sorted(os.walk(CLI)):
        for name in sorted(files):
            if name.split('.')[-1].lower() in ('bsp', 'spk', 'tf', 'tpc', 'tls', 'bpc', 'ck', 'tsc', 'ti', 'ker'):
                sp.furnsh(os.path.join(root, name))
    sp.gfstol(1e-6)
    radii = sp.bodvcd(399, 'RADII', 3)[1]
    point = sp.georec(0.0349, 0.8378, 1071.0 / 1000.0, radii[0], (radii[0] - radii[2]) / radii[0])
    cnfine = window(round(Fraction(674524800.0) * 10 ** 7) / 1e7, round(Fraction(674611200.0) * 10 ** 7) / 1e7)
    result = sp.cell_double(2000)
    sp.gfilum('Ellipsoid', 'INCIDENCE', '399', '10', 'ITRF93', 'CN+S', '10', point, '<', math.pi * 0.5 - (-0.8 * math.pi / 180.0),
              0.0, 4.5 * 3600.0, 1000, cnfine, result)
    out = []
    for x, y in windows(result):
        tx, ty = round(Fraction(x) * 10 ** 7), round(Fraction(y) * 10 ** 7)
        out.append(f'From {label(tx)} TDB to {label(ty)} TDB - Length {span(ty - tx)}')
    print('CLI illumination 1071 m: ' + ' | '.join(out))


def label(ticks):
    s, r = divmod(ticks, 10 ** 7)
    return (D.datetime(2000, 1, 1, 12) + D.timedelta(seconds=s)).isoformat() + '.%07d' % r


def span(ticks):
    s, r = divmod(ticks, 10 ** 7)
    h, rest = divmod(s, 3600)
    m, s = divmod(rest, 60)
    return f'{h:02d}:{m:02d}:{s:02d}' + (f'.{r:07d}' if r else '')


if __name__ == '__main__':
    main()
