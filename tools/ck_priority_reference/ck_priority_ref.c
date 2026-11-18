/*
 * Reference kernels and CSPICE reads of the CK segment priority of SpiceAPI.ReadOrientation (#724).
 *
 *   ./ck_priority_ref <output directory>
 *
 * Writes, in the output directory, the clock and two CKs of the spacecraft -472401 CKPRIO:
 *
 *   ckprio.tsc          its clock, as the library's template (ClockTemplate.tsc: SCLK type 1, 65 536 ticks a second)
 *   ckprio_rates.bc     a type 3 CK with angular velocity: a rotation about z at 1e-3 rad/s, a record every 100 s over
 *                       [0, 1000] s TDB
 *   ckprio_norates.bc   a type 3 CK without angular velocity (ckw03_c, avflag false): a rotation about x at 2e-3 rad/s, a
 *                       record every 50 s over [300, 700] s TDB
 *
 * each record the SPICE quaternion (cos(wt/2), sin(wt/2) a) of the rotation of angle wt about the axis a, which the linear
 * interpolation of a type 3 segment reproduces between records. Then loads the clock and the two CKs in each order and
 * prints, as JSON, what ckgp_c gives (the segment of the highest priority that gives a pointing: the file loaded last) at
 * the epochs 0, 50, ..., 1000 s, 299.5 s and 700.5 s with a tolerance of 1 s (65 536 ticks): "rates_last" (the CK with
 * rates loaded last), "norates_last" (the CK without rates loaded last), each a list of { "et": the TDB seconds of the
 * record (sct2e_c of ckgp_c's clkout), "cmat": the C-matrix, J2000 to the spacecraft frame, row-major }, 17 significant
 * digits.
 *
 * Build from the repository root with the vendored CSPICE (N0067):
 *   gcc -O2 -I external-lib/includeLinux tools/ck_priority_reference/ck_priority_ref.c external-lib/cspice.a -lm -o ck_priority_ref
 *   ./ck_priority_ref IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/CkPriorityReference \
 *       > IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/CkPriorityReference/ck_priority_reference.json
 */
#include <math.h>
#include <stdio.h>
#include "SpiceUsr.h"

#define ID (-472401)
#define NEPOCHS 23

static void write_ck(const char *path, double start, double step, int n, const double axis[3], double rate, SpiceBoolean withRates) {
    SpiceDouble sclk[32], quats[32][4], avvs[32][3];
    for (int i = 0; i < n; i++) {
        double t = start + step * i;
        sce2c_c(ID, t, &sclk[i]);
        double angle = rate * t;
        quats[i][0] = cos(angle / 2.0);
        for (int k = 0; k < 3; k++) {
            quats[i][1 + k] = sin(angle / 2.0) * axis[k];
            avvs[i][k] = rate * axis[k];
        }
    }
    remove(path);
    SpiceInt handle;
    ckopn_c(path, "IO CK priority reference", 0, &handle);
    ckw03_c(handle, sclk[0], sclk[n - 1], ID * 1000, "J2000", withRates, withRates ? "RATES" : "NO RATES", n, sclk, quats, avvs, 1, sclk);
    ckcls_c(handle);
}

static void print_reads(const char *key, const double *epochs, int last) {
    printf("  \"%s\": [\n", key);
    for (int i = 0; i < NEPOCHS; i++) {
        SpiceDouble sclk, cmat[3][3], clkout, et;
        SpiceBoolean found;
        sce2c_c(ID, epochs[i], &sclk);
        ckgp_c(ID * 1000, sclk, 65536.0, "J2000", cmat, &clkout, &found);
        sct2e_c(ID, clkout, &et);
        printf("    { \"epoch\": %.17g, \"et\": %.17g, \"cmat\": [", epochs[i], et);
        for (int r = 0; r < 3; r++) {
            for (int c = 0; c < 3; c++) {
                printf("%.17g%s", cmat[r][c], r == 2 && c == 2 ? "" : ", ");
            }
        }
        printf("] }%s\n", i == NEPOCHS - 1 ? "" : ",");
    }
    printf("  ]%s\n", last ? "" : ",");
}

int main(int argc, char **argv) {
    if (argc != 2) {
        fprintf(stderr, "usage: ck_priority_ref <output directory>\n");
        return 1;
    }
    char clock[1024], rates[1024], norates[1024];
    snprintf(clock, sizeof clock, "%s/ckprio.tsc", argv[1]);
    snprintf(rates, sizeof rates, "%s/ckprio_rates.bc", argv[1]);
    snprintf(norates, sizeof norates, "%s/ckprio_norates.bc", argv[1]);
    FILE *f = fopen(clock, "w");
    fputs("KPL/SCLK\n\\begindata\nSCLK_KERNEL_ID           = ( @1957-01-01/00:00:00.0 )\nSCLK_DATA_TYPE_472401        = ( 1 )\n"
          "SCLK01_TIME_SYSTEM_472401    = ( 1 )\nSCLK01_N_FIELDS_472401       = ( 2 )\nSCLK01_MODULI_472401         = ( 4294967296 65536 )\n"
          "SCLK01_OFFSETS_472401        = ( 0 0 )\nSCLK01_OUTPUT_DELIM_472401   = ( 2 )\nSCLK_PARTITION_START_472401  = ( 0.0000000000000E+00 )\n"
          "SCLK_PARTITION_END_472401    = ( 2.8147497671065E+14 )\n"
          "SCLK01_COEFFICIENTS_472401   = ( 0.0000000000000E+00     -1.3569552000000E+09     1.0000000000000E+00 )\n\\begintext\n",
          f);
    fclose(f);
    furnsh_c(clock);
    const double z[3] = {0.0, 0.0, 1.0}, x[3] = {1.0, 0.0, 0.0};
    write_ck(rates, 0.0, 100.0, 11, z, 1.0e-3, SPICETRUE);
    write_ck(norates, 300.0, 50.0, 9, x, 2.0e-3, SPICEFALSE);

    double epochs[NEPOCHS];
    for (int i = 0; i <= 20; i++) {
        epochs[i] = 50.0 * i;
    }
    epochs[21] = 299.5;
    epochs[22] = 700.5;
    printf("{\n  \"spacecraft\": %d,\n  \"tolerance\": 1.0,\n", ID);
    furnsh_c(norates);
    furnsh_c(rates);
    print_reads("rates_last", epochs, 0);
    unload_c(rates);
    unload_c(norates);
    furnsh_c(rates);
    furnsh_c(norates);
    print_reads("norates_last", epochs, 1);
    printf("}\n");
    return failed_c() ? 1 : 0;
}
