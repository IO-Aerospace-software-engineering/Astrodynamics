"""References of the TT seconds the conformance runner counts about the Earth (#630), for
IO.Astrodynamics.Net/IO.Astrodynamics.ConformanceRunner.Tests/TerrestrialSecondsTests.cs.

The runner gives tca_s (conjunction), burns[].elapsed_s (maneuver) and events.*[].elapsed_s (orbital_events) as the TT
seconds from a UTC epoch to a TDB label (Utilities/TerrestrialSeconds.cs). For two UTC epochs, 2026-01-20T06:00:00 UTC
(the epoch of the conjunction cases, TDB - TT = +0.47 ms) and 2026-10-20T06:00:00 UTC (TDB - TT = -1.61 ms), this
script takes 100 TDB labels each, in 100 ns ticks (.NET's DateTime.Ticks), every 216 s of TT after the epoch up to 6 h,
each moved by a fixed fraction of a tick count (a linear congruential sequence, so that the fraction of TDB - TT in
ticks covers [0, 1)); the last is the label 6 h after the epoch. For each label it gives, at 50 digits (mpmath):

  - "tt_seconds": the exact TT seconds from the epoch to the instant of the label, on the library's TDB - TT series
    (TDBTimeFrame.TdbMinusTt), Delta(T) = 0.001658 sin(g + 0.0167 sin g), g = 2 pi (357.528 + 35999.05 T) / 360, T the
    Julian centuries of TT from J2000: TT = TDB - Delta(TT), solved by fixed point to the 50 digits;
  - "former_error_ticks": informative, the error of the expression the runner used before #630,
    (label.ToTDT() - epoch.ToTDT()).TotalSeconds, whose TDB -> TT conversion subtracts Delta at the centuries of the TDB
    label truncated toward zero to the tick (TimeSpan.FromSeconds): Delta - trunc(Delta), in ticks.

The TT of a UTC epoch is the epoch plus TAI - UTC (37 s since 2017-01-01) plus 32.184 s, a whole number of ticks. With
the leapseconds kernel as argument, CSPICE checks it (spiceypy: unitim of str2et of the UTC epoch, TDB to TT, to the
tick); without it the check is skipped and the file says so.

Each value is written with 30 significant digits, so the reference is exact for the labels and measures the runner's
double computation alone (its resolution near 2e4 s is 3.6e-12 s).

Run from the repository root (mpmath 1.4.1; spiceypy 8.3.0 with CSPICE N0067 for the check):
  python3 -I tools/conformance/terrestrial_seconds_reference.py \\
      IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem/latest_leapseconds.tls \\
      > IO.Astrodynamics.Net/IO.Astrodynamics.ConformanceRunner.Tests/Data/TerrestrialSecondsReference/terrestrial_seconds_reference.json
"""
import datetime
import json
import sys

import mpmath as mp

mp.mp.dps = 50

TICKS_PER_SECOND = 10 ** 7
EPOCH_ORDINAL = datetime.date(1, 1, 1).toordinal()
J2000_TICKS = 630822816000000000 + 12 * 3600 * TICKS_PER_SECOND  # 2000-01-01T12:00:00, Time.J2000
TAI_MINUS_UTC = 37  # s, since 2017-01-01 (latest_leapseconds.tls)
TT_MINUS_TAI_TICKS = 321840000  # 32.184 s
SECONDS_PER_CENTURY = mp.mpf(86400) * 36525
EPOCHS = ["2026-01-20T06:00:00", "2026-10-20T06:00:00"]
LABELS = 100
SPACING_TICKS = 216 * TICKS_PER_SECOND  # 100 labels over 6 h of TT


def ticks(text):
    """The DateTime.Ticks of a calendar text without fraction."""
    d = datetime.datetime.strptime(text, "%Y-%m-%dT%H:%M:%S")
    days = d.date().toordinal() - EPOCH_ORDINAL
    return (days * 86400 + d.hour * 3600 + d.minute * 60 + d.second) * TICKS_PER_SECOND


def delta(tt_seconds):
    """The library's TDB - TT at the TT seconds from J2000, in mpmath."""
    t = tt_seconds / SECONDS_PER_CENTURY
    g = 2 * mp.pi * (mp.mpf("357.528") + mp.mpf("35999.05") * t) / 360
    return mp.mpf("0.001658") * mp.sin(g + mp.mpf("0.0167") * mp.sin(g))


def tt_of_label(label_ticks):
    """The TT seconds from J2000 of the instant of a TDB label: TT = TDB - Delta(TT), by fixed point."""
    tdb = mp.mpf(label_ticks - J2000_TICKS) / TICKS_PER_SECOND
    tt = tdb
    for _ in range(12):  # Delta moves by 3.4e-10 s per second: 12 steps leave far below 1e-50
        tt = tdb - delta(tt)
    return tt


def former_error_ticks(label_ticks):
    """Delta - trunc(Delta) in ticks, Delta taken at the centuries of the TDB label as TDBTimeFrame.ConvertToTAI does."""
    exact = delta(tt_of_label(label_ticks)) * TICKS_PER_SECOND
    at_label = delta(mp.mpf(label_ticks - J2000_TICKS) / TICKS_PER_SECOND) * TICKS_PER_SECOND
    return exact - int(at_label)  # int truncates toward zero, as TimeSpan.FromSeconds


def main():
    check = None
    if len(sys.argv) > 1:
        import spiceypy as sp
        sp.furnsh(sys.argv[1])
        check = []
    seed = 12345
    cases = []
    for epoch in EPOCHS:
        utc_ticks = ticks(epoch)
        tt_ticks = utc_ticks + TAI_MINUS_UTC * TICKS_PER_SECOND + TT_MINUS_TAI_TICKS
        epoch_tt = mp.mpf(tt_ticks - J2000_TICKS) / TICKS_PER_SECOND
        if check is not None:
            et = sp.str2et(epoch.replace("T", " ") + " UTC")
            cspice = sp.unitim(et, "TDB", "TT")
            assert abs(cspice - float(epoch_tt)) < 0.5e-7, (epoch, cspice, float(epoch_tt))
            check.append(f"{epoch} UTC: TT {cspice:.7f} s from J2000 (CSPICE unitim), {float(epoch_tt):.7f} s (ticks)")
        labels = []
        for k in range(1, LABELS + 1):
            seed = (seed * 1103515245 + 12345) % 2 ** 31
            # The TDB label near TT = epoch + k * 216 s, moved by up to 2000 ticks; the last is 6 h of TDB label after
            # the TT of the epoch, plus the TDB - TT of that date rounded to the tick
            near = tt_ticks + k * SPACING_TICKS
            offset = int(mp.nint(delta(mp.mpf(near - J2000_TICKS) / TICKS_PER_SECOND) * TICKS_PER_SECOND))
            label = near + offset + (0 if k == LABELS else seed % 2001 - 1000)
            tt = tt_of_label(label) - epoch_tt
            labels.append({
                "tdb_ticks": label,
                "tt_seconds": mp.nstr(tt, 30),
                "former_error_ticks": mp.nstr(former_error_ticks(label), 6),
            })
        cases.append({"utc_epoch": epoch, "utc_ticks": utc_ticks, "labels": labels})
    result = {
        "generator": "tools/conformance/terrestrial_seconds_reference.py (mpmath %s, 50 digits)" % mp.__version__,
        "cspice_check": check if check is not None else "skipped: no leapseconds kernel given",
        "epochs": cases,
    }
    json.dump(result, sys.stdout, indent=1)
    sys.stdout.write("\n")


if __name__ == "__main__":
    main()
