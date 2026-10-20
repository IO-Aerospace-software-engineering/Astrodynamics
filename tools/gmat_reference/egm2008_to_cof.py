"""Convert the EGM2008 file of IO.Astrodynamics into a GMAT gravity file (.cof).

The input, IO.Astrodynamics.Tests/Data/SolarSystem/EGM2008_to70_TideFree, holds one line per (n, m): n, m, C_nm,
S_nm and their sigmas, fully normalized, with Fortran D exponents, from degree 2. The output has the layout of the
.cof files shipped with GMAT R2026a (data/gravity/earth/JGM3.cof): a POTFIELD header (degree, order, the GM in m^3/s^2,
the reference radius in m, 1 for normalized coefficients), then one fixed-width RECOEF line per (n, m), C_nm and, for
m > 0, S_nm, each as %21.14e.

The header carries the GM and the reference radius with which IO.Astrodynamics evaluates the field (those of its SPICE
kernels, written in cases.json), not the constants of EGM2008, so that both tools evaluate the same field.

Usage: python3 -I -B egm2008_to_cof.py <EGM2008 file> <output .cof> <GM, m^3/s^2> <radius, m> <degree>
"""
import sys


def convert(source, destination, gm, radius, degree):
    with open(source) as f, open(destination, "w", newline="\r\n") as out:
        out.write("COMMENT   2\n")
        out.write("C EGM2008 tide-free to degree %d, from IO.Astrodynamics EGM2008_to70_TideFree\n" % degree)
        out.write("C GM and reference radius of IO.Astrodynamics (gm_de440.tpc, pck00011.tpc), not of EGM2008\n")
        out.write("POTFIELD %2d %2d  1 %.17e %.17e %.17e\n" % (degree, degree, gm, radius, 1.0))
        for line in f:
            fields = line.replace("D", "e").split()
            if len(fields) < 4:
                continue
            n, m = int(fields[0]), int(fields[1])
            if n > degree:
                break
            c, s = float(fields[2]), float(fields[3])
            out.write("RECOEF%5d%3d   %21.14e" % (n, m, c) + ("" if m == 0 else "%21.14e" % s) + "\n")
        out.write("END\n")


if __name__ == "__main__":
    if len(sys.argv) != 6:
        sys.exit(__doc__)
    convert(sys.argv[1], sys.argv[2], float(sys.argv[3]), float(sys.argv[4]), int(sys.argv[5]))
