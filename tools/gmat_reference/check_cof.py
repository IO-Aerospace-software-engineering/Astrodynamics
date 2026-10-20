"""Check that the committed .cof is the conversion of the EGM2008 file of IO.Astrodynamics with the GM and the radius
of cases.json (every case has the same Earth), to degree 70, byte for byte.

Usage: python3 -I -B check_cof.py <EGM2008 file> <committed .cof> <cases.json>
"""
import json
import os
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from egm2008_to_cof import convert  # noqa: E402

DEGREE = 70


def main():
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    source, committed, cases_path = sys.argv[1:]
    with open(cases_path) as f:
        earths = {(c["earth"]["gm_m3_s2"], c["earth"]["equatorial_radius_m"]) for c in json.load(f)["cases"]}
    if len(earths) != 1:
        sys.exit("The cases do not share one Earth: %s" % sorted(earths))
    gm, radius = earths.pop()
    with tempfile.TemporaryDirectory() as scratch:
        expected = os.path.join(scratch, "expected.cof")
        convert(source, expected, gm, radius, DEGREE)
        with open(expected, "rb") as a, open(committed, "rb") as b:
            if a.read() != b.read():
                sys.exit("%s is not the conversion of %s; run egm2008_to_cof.py again" % (committed, source))


if __name__ == "__main__":
    main()
