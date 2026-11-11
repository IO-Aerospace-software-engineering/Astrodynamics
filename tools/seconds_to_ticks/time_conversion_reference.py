"""The time conversions of the CLI tests by an exact replica of the conversions of Time, for #625.

UTC + TAI - UTC (37 s in 2020) = TAI, + 32.184 s = TT, + TDB - TT = TDB, the series of TDBTimeFrame
(0.001658 sin(g + 0.0167 sin g), g = 2 pi (357.528 + 35999.05 T) / 360, T the Julian centuries of the ticks from J2000,
in doubles as the library evaluates it) at the TT centuries, rounded to the nearest tick (Python Fraction, ties to
even); back, TDB - 32.184 s - the series at the TDB centuries. A TDB value in seconds is its nearest tick; a printed
value of seconds is the ticks over 1e7 in double, as TimeSpan.TotalSeconds gives it. Mode trunc, the truncation toward
zero of every conversion, gives the strings the tests held before #625.

Run: python3 -I time_conversion_reference.py
"""
import datetime as D
import math
from fractions import Fraction


def ticks(dt):
    d = dt - D.datetime(1, 1, 1)
    return (d.days * 86400 + d.seconds) * 10 ** 7 + d.microseconds * 10


J = ticks(D.datetime(2000, 1, 1, 12))
LEAP = 37 * 10 ** 7
TT_TAI = 321840000
MODES = {'nearest': lambda x: round(Fraction(x) * 10 ** 7), 'trunc': lambda x: math.trunc(Fraction(x) * 10 ** 7)}


def delta(t):
    c = ((t - J) / 864000000000.0) / 36525.0
    g = 2 * math.pi * (357.528 + 35999.05 * c) / 360.0
    return 0.001658 * math.sin(g + 0.0167 * math.sin(g))


def label(t):
    s, r = divmod(t, 10 ** 7)
    return (D.datetime(1, 1, 1) + D.timedelta(seconds=s)).isoformat() + '.%07d' % r


for mode, f in MODES.items():
    utc = ticks(D.datetime(2020, 1, 1, 12))
    tt = utc + LEAP + TT_TAI
    tdb = tt + f(delta(tt))
    print(mode, '2020-01-01T12:00:00Z in TDB seconds', repr((tdb - J) / 1e7), 'label', label(tdb), 'TDB')
    utc1 = utc + 1
    tt1 = utc1 + LEAP + TT_TAI
    tdb1 = tt1 + f(delta(tt1))
    print(mode, '2020-01-01T12:00:00.0000001Z in TDB seconds', repr((tdb1 - J) / 1e7))
    tdb2 = J + f(631152069.1839273)
    tai2 = tdb2 - TT_TAI - f(delta(tdb2))
    print(mode, '631152069.1839273 TDB in UTC', label(tai2 - LEAP) + 'Z')
    utc3 = ticks(D.datetime(2000, 1, 1, 12))
    tt3 = utc3 + 32 * 10 ** 7 + TT_TAI
    print(mode, '2000-01-01T12:00:00Z in TDB (TAI - UTC 32 s)', label(tt3 + f(delta(tt3))), 'TDB')
