/*
 * Generates IO.Astrodynamics/Frames/Iau2006CioLocatorData.cs: the series of the IAU 2006 CIO locator
 * s + XY/2, as implemented by SOFA iauS06 (IERS Conventions 2010, Table 5.2d).
 *
 * The coefficient tables are static locals of iauS06, so they cannot be linked against. Instead of
 * copying them by hand, this program parses them from the vendored SOFA source (s06.c), then checks
 * the parsed series against iauS06 itself before printing anything: the C# file is produced only if
 * the series evaluated here reproduces iauS06 to rounding level.
 *
 * Usage, from the repository root: build it against every SOFA source file of external-lib/sofa-src
 * (see README.md), then run
 *   ./extract_s06 external-lib/sofa-src/s06.c \
 *       > IO.Astrodynamics.Net/IO.Astrodynamics/Frames/Iau2006CioLocatorData.cs
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include "sofa.h"
#include "sofam.h"

#define MAX_TERMS 64

typedef struct {
    int nfa[8];
    double s, c; /* microarcseconds */
} Term;

static double poly[6];
static int npoly = 0;
static Term series[5][MAX_TERMS];
static int nterms[5];

/* Parses "{{ 0,  0,  0,  0,  1,  0,  0,  0}, -2640.73e-6,   0.39e-6 }" into a term (arcsec -> µas). */
static int parse_term(const char *line, Term *term)
{
    double s, c;
    int n = sscanf(line, " {{ %d , %d , %d , %d , %d , %d , %d , %d } , %lf , %lf",
                   &term->nfa[0], &term->nfa[1], &term->nfa[2], &term->nfa[3],
                   &term->nfa[4], &term->nfa[5], &term->nfa[6], &term->nfa[7], &s, &c);
    if (n != 10) return 0;
    term->s = s * 1e6;
    term->c = c * 1e6;
    return 1;
}

static void parse_source(const char *path)
{
    FILE *f = fopen(path, "r");
    if (!f) { perror(path); exit(1); }

    char line[512];
    int section = -2; /* -2: none, -1: polynomial, 0..4: series of order t^k */
    while (fgets(line, sizeof line, f)) {
        if (strstr(line, "static const double sp[]")) { section = -1; continue; }
        if (strstr(line, "static const TERM s0[]")) { section = 0; continue; }
        if (strstr(line, "static const TERM s1[]")) { section = 1; continue; }
        if (strstr(line, "static const TERM s2[]")) { section = 2; continue; }
        if (strstr(line, "static const TERM s3[]")) { section = 3; continue; }
        if (strstr(line, "static const TERM s4[]")) { section = 4; continue; }
        if (strstr(line, "};")) { section = -2; continue; }

        if (section == -1) {
            double v;
            if (sscanf(line, " %lf", &v) == 1) poly[npoly++] = v * 1e6;
        } else if (section >= 0) {
            Term term;
            if (parse_term(line, &term)) {
                if (nterms[section] == MAX_TERMS) { fprintf(stderr, "too many terms\n"); exit(1); }
                series[section][nterms[section]++] = term;
            }
        }
    }
    fclose(f);

    if (npoly != 6 || nterms[0] != 33 || nterms[1] != 3 || nterms[2] != 25 || nterms[3] != 4 || nterms[4] != 1) {
        fprintf(stderr, "unexpected table sizes: poly=%d s0=%d s1=%d s2=%d s3=%d s4=%d\n",
                npoly, nterms[0], nterms[1], nterms[2], nterms[3], nterms[4]);
        exit(1);
    }
}

/* Evaluates the parsed series exactly as iauS06 does. */
static double evaluate(double date1, double date2, double x, double y)
{
    double t = ((date1 - DJ00) + date2) / DJC;
    double fa[8] = {
        iauFal03(t), iauFalp03(t), iauFaf03(t), iauFad03(t),
        iauFaom03(t), iauFave03(t), iauFae03(t), iauFapa03(t)
    };
    double w[6];
    for (int k = 0; k < 6; k++) w[k] = poly[k];
    for (int k = 0; k < 5; k++) {
        for (int i = nterms[k] - 1; i >= 0; i--) {
            double a = 0.0;
            for (int j = 0; j < 8; j++) a += (double) series[k][i].nfa[j] * fa[j];
            w[k] += series[k][i].s * sin(a) + series[k][i].c * cos(a);
        }
    }
    double sum = (w[0] + (w[1] + (w[2] + (w[3] + (w[4] + w[5] * t) * t) * t) * t) * t);
    return sum * 1e-6 * DAS2R - x * y / 2.0;
}

static void self_check(void)
{
    /* 1990 to 2040, every 73 days or so: the generated series must reproduce iauS06. */
    double worst = 0.0;
    for (double days = -3652.5; days <= 14610.0; days += 73.05) {
        double x, y;
        iauXy06(DJ00, days, &x, &y);
        double diff = fabs(evaluate(DJ00, days, x, y) - iauS06(DJ00, days, x, y));
        if (diff > worst) worst = diff;
    }
    if (worst > 1e-17) {
        fprintf(stderr, "parsed series differs from iauS06 by %.3e rad\n", worst);
        exit(1);
    }
}

static void print_series(const char *name, int k)
{
    printf("    internal static readonly (int l, int lp, int f, int d, int om, int lve, int le, int pa, double s, double c)[] %s =\n", name);
    printf("    [\n");
    for (int i = 0; i < nterms[k]; i++) {
        const Term *term = &series[k][i];
        printf("        (%d, %d, %d, %d, %d, %d, %d, %d, %.2f, %.2f)%s\n",
               term->nfa[0], term->nfa[1], term->nfa[2], term->nfa[3],
               term->nfa[4], term->nfa[5], term->nfa[6], term->nfa[7],
               term->s, term->c, i < nterms[k] - 1 ? "," : "");
    }
    printf("    ];\n");
}

int main(int argc, char **argv)
{
    if (argc != 2) {
        fprintf(stderr, "usage: %s path/to/sofa-src/s06.c\n", argv[0]);
        return 1;
    }
    parse_source(argv[1]);
    self_check();

    printf("// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)\n");
    printf("// Generated by tools/sofa_reference/extract_s06.c from the SOFA iauS06 source. Do not edit by hand.\n");
    printf("namespace IO.Astrodynamics.Frames;\n\n");
    printf("/// <summary>\n");
    printf("/// Series of the IAU 2006 CIO locator <c>s + XY/2</c> (IERS Conventions 2010, Table 5.2d), as\n");
    printf("/// implemented by SOFA iauS06: a polynomial plus %d periodic terms grouped by power of t.\n",
           nterms[0] + nterms[1] + nterms[2] + nterms[3] + nterms[4]);
    printf("/// Amplitudes are in microarcseconds. The integer multipliers apply to the fundamental arguments\n");
    printf("/// l, l', F, D, Omega, the mean longitudes of Venus and Earth, and the general precession pA.\n");
    printf("/// </summary>\n");
    printf("internal static class Iau2006CioLocatorData\n");
    printf("{\n");
    printf("    /// <summary>\n");
    printf("    /// Polynomial part, coefficients of t^0 to t^5 (microarcseconds).\n");
    printf("    /// </summary>\n");
    printf("    internal static readonly double[] Polynomial = [");
    for (int k = 0; k < 6; k++) printf("%.2f%s", poly[k], k < 5 ? ", " : "");
    printf("];\n\n");
    print_series("Order0", 0);
    printf("\n");
    print_series("Order1", 1);
    printf("\n");
    print_series("Order2", 2);
    printf("\n");
    print_series("Order3", 3);
    printf("\n");
    print_series("Order4", 4);
    printf("\n");
    /* Declared after the arrays it groups: static fields are initialized in textual order. */
    printf("    /// <summary>\n");
    printf("    /// Periodic terms: <c>Series[k]</c> multiplies t^k. Each term adds <c>s sin(a) + c cos(a)</c>,\n");
    printf("    /// where a is the integer combination of the eight fundamental arguments.\n");
    printf("    /// </summary>\n");
    printf("    internal static readonly (int l, int lp, int f, int d, int om, int lve, int le, int pa, double s, double c)[][] Series =\n");
    printf("        [Order0, Order1, Order2, Order3, Order4];\n");
    printf("}\n");
    return 0;
}
