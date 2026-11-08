"""References of the library's SGP4 and TLE paths (lot 5 of #457: #599, #603, #600, #601, #602, #480, #479) for
IO.Astrodynamics.Tests/OrbitalParameters/Sgp4ReferenceTests.cs.

SGP4 is Vallado's code, python sgp4 2.27 (B. Rhodes' wrapper of Vallado's C++), in the configuration of the library's
CSPICE evsgp4_c: the WGS-72 "old" constants (WGS72OLD, xke = 0.0743669161 exactly) and the AFSPC mode, sgp4init(..., 'a',
...) from the fields twoline2rv reads (twoline2rv itself sets the improved mode 'i'). CSPICE N0067 (spiceypy 8.3.0) is
the second reference: getelm_c for the epoch, evsgp4_c with the same constants at that epoch + 60 tsince.

It writes, as JSON on stdout:
  - "verification": the element sets of Vallado's SGP4-VER.TLE (shipped with sgp4 2.27) but the suborbital 28872 and
    the error sets 33333 to 33335, each with its lines, its UTC epoch decoded in exact decimal (ticks of 100 ns), the
    getelm_c epoch (ET), and at each tsince of its rows in tcppver.out (also shipped) the sgp4 state in TEME (km, km/s);
    a sample where sgp4 reports an error is left out. Per set, the worst CSPICE - sgp4 difference (km, km/s);
  - "instants": 06251 (SGP4-VER.TLE) and the ISS of CelesTrak's GP data of 2026-11-07 at the first whole UTC hour after
    the epoch and hourly for 24 h, at tsince = the UTC days from the epoch, in exact decimal (Vallado's driver; the TT
    seconds away from a leap second, and no window holds one). Not sgp4(jd, fr) after sgp4init: sgp4init keeps the
    epoch as days from 1949-12-31 in one double, 1.46 us off the line's epoch for 06251, 0.1 m in LEO;
  - "errors": element sets SGP4 cannot evaluate, with the tsince of the evaluation and both references' errors;
  - "epoch_fields": the epoch field of line 1 (YYDDD.DDDDDDDD) of UTC instants, the instant rounded in exact decimal to
    the nearest 1e-8 day (a tie up), and of TDB, TT and TAI instants through CSPICE's UTC (et2utc, 7 decimals).

Run from the repository root (sgp4 2.27, spiceypy 8.3.0 with CSPICE N0067):
  python3 -I tools/sgp4_reference/sgp4_ref.py IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem \
      > IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/Sgp4Reference/sgp4_reference.json
"""
import json
import os
import sys
from decimal import ROUND_HALF_UP, Decimal

import numpy as np
import sgp4
import spiceypy as sp
from sgp4.api import WGS72OLD, Satrec

KERNELS = sys.argv[1]
sp.furnsh(os.path.join(KERNELS, "latest_leapseconds.tls"))
PKG = os.path.dirname(sgp4.__file__)
# J2 J3 J4 KE QO SO ER AE: the constants the library's native layer passes to evsgp4_c
GEOPHS = [1.082616e-3, -2.53881e-6, -1.65597e-6, 7.43669161e-2, 120.0, 78.0, 6378.135, 1.0]
EXCLUDED = {28872, 33333, 33334, 33335}
ISS_2026 = ("1 25544U 98067A   26311.20783432  .00014294  00000+0  26479-3 0  9992",
            "2 25544  51.6321 312.3516 0007247 357.8301   2.2655 15.49235271593925")
TICKS_PER_DAY = Decimal(864000000000)


def afspc(l1, l2):
    s0 = Satrec.twoline2rv(l1, l2, WGS72OLD)
    s = Satrec()
    s.sgp4init(WGS72OLD, 'a', s0.satnum, s0.jdsatepoch + s0.jdsatepochF - 2433281.5, s0.bstar, s0.ndot, s0.nddot,
               s0.ecco, s0.argpo, s0.inclo, s0.mo, s0.no_kozai, s0.nodeo)
    return s


def epoch_utc(l1):
    """The UTC epoch of line 1, exact: (year, day of year, ticks of the fraction)."""
    yy = int(l1[18:20])
    year = 2000 + yy if yy < 57 else 1900 + yy
    d = Decimal(l1[20:32].strip())
    day = int(d)
    ticks = ((d - day) * TICKS_PER_DAY).quantize(Decimal(1), rounding=ROUND_HALF_UP)
    return year, day, int(ticks)


def iso(year, day, ticks):
    import datetime
    t = datetime.datetime(year, 1, 1) + datetime.timedelta(days=day - 1)
    s, r = divmod(ticks, 10_000_000)
    h, s = divmod(s, 3600)
    m, s = divmod(s, 60)
    return f"{t:%Y-%m-%d}T{h:02d}:{m:02d}:{s:02d}.{r:07d}"


def parse_ver():
    lines = open(os.path.join(PKG, "SGP4-VER.TLE")).read().splitlines()
    out = []
    i = 0
    while i < len(lines):
        if lines[i].startswith("1 "):
            out.append((lines[i][:69], lines[i + 1][:69]))
            i += 2
        else:
            i += 1
    return out


def parse_tcppver():
    blocks = []
    for ln in open(os.path.join(PKG, "tcppver.out")).read().replace("\r", "").splitlines():
        f = ln.split()
        if len(f) >= 2 and f[1] == "xx":
            blocks.append((int(f[0]), []))
        elif f:
            blocks[-1][1].append(float(f[0]))
    return blocks


def cspice(l1, l2, tsince):
    sp.reset()
    ep, el = sp.getelm(1957, [l1, l2])
    try:
        return ep, np.array(sp.evsgp4(ep + tsince * 60.0, GEOPHS, el)), None
    except Exception as ex:  # noqa: BLE001 (a SPICE error is a result here)
        sp.reset()
        return ep, None, getattr(ex, "short", str(ex))


verification = []
sets, blocks = parse_ver(), parse_tcppver()
assert len(sets) == len(blocks) == 33
for (l1, l2), (satnum, rows) in zip(sets, blocks):
    if satnum in EXCLUDED:
        continue
    s = afspc(l1, l2)
    year, day, ticks = epoch_utc(l1)
    samples, wr, wv = [], 0.0, 0.0
    et0 = None
    for t in rows:
        e, r, v = s.sgp4_tsince(t)
        if e != 0:
            continue
        et0, c, _ = cspice(l1, l2, t)
        if c is not None:
            wr = max(wr, float(np.abs(c[:3] - np.array(r)).max()))
            wv = max(wv, float(np.abs(c[3:] - np.array(v)).max()))
        samples.append({"tsince_min": t, "r_km": list(r), "v_km_s": list(v)})
    if et0 is None:
        et0 = sp.getelm(1957, [l1, l2])[0]
    verification.append({"satnum": satnum, "line1": l1, "line2": l2, "epoch_utc": iso(year, day, ticks),
                         "epoch_et": et0, "cspice_minus_sgp4_km": wr, "cspice_minus_sgp4_km_s": wv,
                         "samples": samples})

instants = []
by_num = {int(l1[2:7]): (l1, l2) for l1, l2 in sets}
for name, (l1, l2) in (("06251", by_num[6251]), ("ISS 2026", ISS_2026)):
    s = afspc(l1, l2)
    year, day, ticks = epoch_utc(l1)
    e_iso = iso(year, day, ticks)
    import datetime
    t0 = datetime.datetime.fromisoformat(e_iso[:19]).replace(minute=0, second=0) + datetime.timedelta(hours=1)
    e0 = datetime.datetime(year, 1, 1) + datetime.timedelta(days=day - 1, microseconds=ticks // 10)
    assert ticks % 10 == 0
    rows = []
    for k in range(25):
        t = t0 + datetime.timedelta(hours=k)
        # tsince exactly, from the UTC instants in whole microseconds: not s.sgp4(jd, fr) after sgp4init, which keeps the
        # epoch as days from 1949-12-31 in one double, 1.46 us off for 06251 (0.1 m in LEO)
        d = t - e0
        tsince = float((Decimal(d.days * 86400 + d.seconds) + Decimal(d.microseconds) / Decimal(10 ** 6)) / Decimal(60))
        e, r, v = s.sgp4_tsince(tsince)
        assert e == 0
        rows.append({"utc": f"{t:%Y-%m-%dT%H:%M:%S}.0000000", "r_km": list(r), "v_km_s": list(v)})
    instants.append({"name": name, "line1": l1, "line2": l2, "epoch_utc": e_iso, "samples": rows})


def fix_checksum(line):
    c = sum(int(ch) if ch.isdigit() else (1 if ch == "-" else 0) for ch in line[:68]) % 10
    return line[:68] + str(c)


errors = []
l1, l2 = sets[[int(a[2:7]) for a, _ in sets].index(33334)]
l1, l2 = fix_checksum(l1), fix_checksum(l2)
e, _, _ = afspc(l1, l2).sgp4_tsince(0.0)
_, c, cerr = cspice(l1, l2, 0.0)
errors.append({"name": "33334 (checksums recomputed)", "line1": l1, "line2": l2, "tsince_min": 0.0,
               "sgp4_error": e, "cspice_error": cerr})
l1, l2 = by_num[28350]
s = afspc(l1, l2)
t = next(t for t in np.arange(1440.0, 20000.0, 60.0) if s.sgp4_tsince(float(t))[0] != 0)
e = s.sgp4_tsince(float(t))[0]
_, c, cerr = cspice(l1, l2, float(t))
errors.append({"name": "28350 after its decay", "line1": l1, "line2": l2, "tsince_min": float(t),
               "sgp4_error": e, "cspice_error": cerr, "cspice_state_km": None if c is None else list(c)})


def field(year, dt):
    """YYDDD.DDDDDDDD of a UTC calendar instant (datetime, microseconds), rounded half up to 1e-8 day, carried."""
    import datetime
    tod = Decimal(dt.hour * 3600 + dt.minute * 60 + dt.second) + Decimal(dt.microsecond) / Decimal(10 ** 6)
    units = (tod / Decimal(86400) * Decimal(10 ** 8)).quantize(Decimal(1), rounding=ROUND_HALF_UP)
    d = dt.date()
    if units == 10 ** 8:
        d, units = d + datetime.timedelta(days=1), Decimal(0)
    return f"{d.year % 100:02d}{d.timetuple().tm_yday:03d}.{int(units):08d}"


import datetime
epoch_fields = []
for u in ("2026-11-07T23:59:59.999600", "2026-12-31T23:59:59.999600", "2026-11-07T23:59:59.999500",
          "2021-03-20T12:00:00.000000", "2024-02-29T23:59:59.999568"):
    dt = datetime.datetime.fromisoformat(u)
    epoch_fields.append({"utc": u, "field": field(dt.year, dt)})
for scale, instant in (("TDB", "2021-03-20T12:00:00"), ("TDB", "2008-06-15T00:00:00"), ("TDT", "2021-03-20T12:00:00"),
                       ("TAI", "2021-03-20T12:00:00")):
    text = instant.replace("T", " ")
    if scale == "TAI":
        # str2et reads no TAI tag: TAI hh:mm:ss is TT hh:mm:(ss + 32.184); the instants of this list have ss = 00
        assert text.endswith(":00")
        text = text[:-2] + "32.184 TDT"
    else:
        text += " " + scale
    et = sp.str2et(text)
    u = sp.et2utc(et, "ISOC", 7)
    dt = datetime.datetime.fromisoformat(u[:26])
    epoch_fields.append({"scale": "TT" if scale == "TDT" else scale, "instant": instant, "utc_cspice": u,
                         "field": field(dt.year, dt)})

json.dump({"generator": "tools/sgp4_reference/sgp4_ref.py", "sgp4": sgp4.__version__,
           "cspice": sp.tkvrsn("TOOLKIT"), "verification": verification, "instants": instants,
           "errors": errors, "epoch_fields": epoch_fields}, sys.stdout, indent=1)
