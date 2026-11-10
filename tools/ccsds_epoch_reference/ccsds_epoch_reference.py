"""References of the CCSDS date parser (#490, lot 6 of #457) for IO.Astrodynamics.Tests/CCSDS/Common/CcsdsEpochParserTests.cs.

A CCSDS date (the date branch of the NDM/XML epochType) is a calendar date YYYY-MM-DD or a day of year YYYY-DDD, then
Thh:mm:ss, an optional fraction and an optional Z or +-hh:mm. For each day-of-year text this script gives:

  - "calendar": the same instant as a calendar text, from the proleptic Gregorian ordinal (Python's %j), the fraction and
    the zone kept as written;
  - "ticks": the UTC instant in 100 ns ticks from 0001-01-01 (.NET's DateTime.Ticks), exact for up to 7 fraction digits,
    the offset subtracted (local time minus its offset from UTC);
  - CSPICE's check: str2et of the day-of-year text and of the calendar text, zone removed, give the same ET, and et2utc of
    that ET (ISOC, 7 decimals) is the calendar instant to one tick (the resolution of an ET near 8e8 s is 1.2e-7 s).

Days out of range (day 0, day 366 of a common year, day 367) are rejected by str2et (SPICE(BADTIMESTRING)): "valid" false.
The calendar rows are the controls: their ticks by the same closed form.

Run from the repository root (spiceypy 8.3.0 with CSPICE N0067):
  python3 -I tools/ccsds_epoch_reference/ccsds_epoch_reference.py \
      IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem/latest_leapseconds.tls \
      > IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/CcsdsEpochReference/ccsds_epoch_reference.json
"""
import datetime
import json
import re
import sys

import spiceypy as sp

PATTERN = re.compile(r"^(\d{4})-(?:(\d{2})-(\d{2})|(\d{3}))T(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?(Z|[+-]\d{2}:\d{2})?$")
TICKS_PER_DAY = 864_000_000_000
EPOCH_ORDINAL = datetime.date(1, 1, 1).toordinal()

DAY_OF_YEAR = [
    "2000-001T00:00:00", "2021-079T12:00:00", "2021-079T12:00:00Z", "2021-079T12:00:00.1",
    "2021-079T12:00:00.123", "2021-079T12:00:00.123456", "2021-079T12:00:00.1234567",
    "2021-079T12:00:00.1234567Z", "2023-001T00:00:00.000", "2023-365T23:59:59.9999999",
    "2024-167T10:30:00", "2024-167T10:30:00+00:00", "2024-167T10:30:00+05:30", "2024-167T10:30:00-08:00",
    "2026-013T05:00:55.752768", "2026-013T05:00:55+00:00", "2026-069T00:00:00", "2026-075T12:34:56+00:00",
    "2026-077T23:59:59.5Z", "2020-366T23:59:59", "2024-060T00:00:00", "2024-366T00:00:00.0000001Z",
    "2010-072T22:37:52.618", "2014-021T17:42:47.000", "1999-365T23:59:59.999Z",
]
INVALID_DAY_OF_YEAR = ["2021-366T05:00:55", "2021-000T05:00:55", "2020-367T00:00:00", "2024-999T00:00:00"]
CALENDAR = [
    "2021-03-20T12:00:00", "2021-03-20T12:00:00Z", "2021-03-20T12:00:00.1", "2021-03-20T12:00:00.12",
    "2021-03-20T12:00:00.123", "2021-03-20T12:00:00.1234", "2021-03-20T12:00:00.12345",
    "2021-03-20T12:00:00.123456", "2021-03-20T12:00:00.1234567", "2021-03-20T12:00:00.1234567Z",
    "2021-03-20T12:00:00.123456Z", "2010-03-13T22:37:52.6180000Z", "2026-01-13T05:00:55.752768",
    "2024-06-15T10:30:00+00:00", "2024-06-15T10:30:00+05:30", "2024-06-15T10:30:00-08:00", "2024-02-29T00:00:00",
]


def split(text):
    m = PATTERN.match(text)
    year, month, day, doy, hh, mm, ss, frac, zone = m.groups()
    year = int(year)
    if doy is not None:
        date = datetime.date(year, 1, 1) + datetime.timedelta(days=int(doy) - 1)
        if date.year != year or int(doy) < 1:
            raise ValueError(text)
    else:
        date = datetime.date(year, int(month), int(day))
    return date, int(hh), int(mm), int(ss), frac or "", zone or ""


def ticks(text):
    date, hh, mm, ss, frac, zone = split(text)
    assert len(frac) <= 7
    t = (date.toordinal() - EPOCH_ORDINAL) * TICKS_PER_DAY + ((hh * 60 + mm) * 60 + ss) * 10_000_000
    t += int(frac.ljust(7, "0")) if frac else 0
    if zone and zone != "Z":
        offset = (int(zone[1:3]) * 60 + int(zone[4:6])) * 60 * 10_000_000
        t -= offset if zone[0] == "+" else -offset
    return t


def calendar_text(text):
    date, hh, mm, ss, frac, zone = split(text)
    return f"{date.isoformat()}T{hh:02d}:{mm:02d}:{ss:02d}" + (f".{frac}" if frac else "") + zone


def without_zone(text):
    return re.sub(r"(Z|[+-]\d{2}:\d{2})$", "", text)


def spice_check(doy, calendar, expected_ticks):
    et_doy = sp.str2et(without_zone(doy))
    et_cal = sp.str2et(without_zone(calendar))
    assert et_doy == et_cal, (doy, calendar, et_doy, et_cal)
    # et2utc of the ET, the zone's offset removed, against the closed form, to one tick
    iso = sp.et2utc(et_doy, "ISOC", 7)
    date_part, time_part = iso.split("T")
    y, mo, d = (int(x) for x in date_part.split("-"))
    h, mi, s = time_part.split(":")
    local = ((datetime.date(y, mo, d).toordinal() - EPOCH_ORDINAL) * TICKS_PER_DAY
             + ((int(h) * 60 + int(mi)) * 60) * 10_000_000 + round(float(s) * 10_000_000))
    zone_ticks = ticks(without_zone(doy)) - expected_ticks
    assert abs(local - zone_ticks - expected_ticks) <= 1, (doy, iso, local - zone_ticks - expected_ticks)
    return et_doy, iso, local - zone_ticks - expected_ticks


def main():
    sp.furnsh(sys.argv[1])
    rows = []
    for doy in DAY_OF_YEAR:
        calendar = calendar_text(doy)
        expected = ticks(doy)
        et, iso, delta = spice_check(doy, calendar, expected)
        rows.append({"text": doy, "valid": True, "calendar": calendar, "ticks": expected, "spice_et": et,
                     "spice_isoc": iso, "spice_minus_ticks": delta})
    for doy in INVALID_DAY_OF_YEAR:
        try:
            sp.str2et(doy)
            raise AssertionError(doy)
        except sp.stypes.SpiceyError as error:
            rows.append({"text": doy, "valid": False, "spice_error": error.short})
    rows.append({"text": "2021-DDDT12:00:00", "valid": False, "spice_error": "not a date: the literal DDD"})
    controls = [{"text": c, "ticks": ticks(c)} for c in CALENDAR]
    json.dump({"source": "tools/ccsds_epoch_reference/ccsds_epoch_reference.py, spiceypy " + sp.__version__ + ", "
               + sp.tkvrsn("TOOLKIT"), "day_of_year": rows, "calendar": controls}, sys.stdout, indent=1)
    sys.stdout.write("\n")


if __name__ == "__main__":
    main()
