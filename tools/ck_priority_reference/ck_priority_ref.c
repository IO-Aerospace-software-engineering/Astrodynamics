/*
 * Reference kernels and CSPICE reads of the CK segment priority of SpiceApi.ReadOrientation (#724).
 *
 *   ./ck_priority_ref <output directory>
 *
 * Writes, in the output directory, the clock, the frame and three CKs of the spacecraft -472401 CKPRIO:
 *
 *   ckprio.tsc          its clock, as the library's template (ClockTemplate.tsc: SCLK type 1, 65 536 ticks a second)
 *   ckprio.tf           its CK frame CKPRIO_SC (-472401000)
 *   ckprio_rates.bc     a type 3 CK with angular velocity: a rotation about z at 1e-3 rad/s, a record every 100 s over
 *                       [0, 1000] s TDB
 *   ckprio_norates.bc   a type 3 CK without angular velocity (ckw03_c, avflag false): a rotation about x at 2e-3 rad/s, a
 *                       record every 50 s over [300, 700] s TDB
 *   ckprio_gap.bc       a type 3 CK without angular velocity, about x at 2e-3 rad/s, records at 300, 350, 400, 600, 650 and
 *                       700 s TDB in two interpolation intervals, [300, 400] s and [600, 700] s (no pointing between them)
 *
 * each record the SPICE quaternion (cos(wt/2), sin(wt/2) a) of the rotation of angle wt about the axis a, which the linear
 * interpolation of a type 3 segment reproduces between records. Then prints, as JSON, what ckgp_c gives (the segment of
 * the highest priority that gives a pointing: the file loaded last) at the epochs 0, 50, ..., 1000 s, 299.5 s and 700.5 s
 * with a tolerance of 1 s (65 536 ticks), each read with the CKs loaded again (a search of its own, as in a fresh
 * process): "rates_last" (the CK with rates loaded last), "norates_last" (the CK without rates loaded last), "gap_last"
 * (the CK with rates, then ckprio_gap.bc loaded last), each a list of { "epoch", "et": the TDB seconds of the
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

static void write_ck(const char *path, const double *times, int n, const double *starts, int nints, const double axis[3], double rate,
                     SpiceBoolean withRates, const char *segid) {
    SpiceDouble sclk[32], quats[32][4], avvs[32][3], intervals[32];
    for (int i = 0; i < n; i++) {
        double t = times[i];
        sce2c_c(ID, t, &sclk[i]);
        double angle = rate * t;
        quats[i][0] = cos(angle / 2.0);
        for (int k = 0; k < 3; k++) {
            quats[i][1 + k] = sin(angle / 2.0) * axis[k];
            avvs[i][k] = rate * axis[k];
        }
    }
    for (int i = 0; i < nints; i++) {
        sce2c_c(ID, starts[i], &intervals[i]);
    }
    remove(path);
    SpiceInt handle;
    ckopn_c(path, "IO CK priority reference", 0, &handle);
    ckw03_c(handle, sclk[0], sclk[n - 1], ID * 1000, "J2000", withRates, segid, n, sclk, quats, avvs, nints, intervals);
    ckcls_c(handle);
}

/* ckgp_c at each epoch, each read with the CKs unloaded and loaded again, the file 'last' loaded last, so that no read
 * depends on the segment search of an earlier one (ckbsr.c keeps a re-use interval per instrument), as in a fresh
 * process */
static void print_reads(const char *key, const double *epochs, const char *first, const char *last, int isLast) {
    printf("  \"%s\": [\n", key);
    for (int i = 0; i < NEPOCHS; i++) {
        SpiceDouble sclk, cmat[3][3], clkout, et;
        SpiceBoolean found;
        furnsh_c(first);
        furnsh_c(last);
        sce2c_c(ID, epochs[i], &sclk);
        ckgp_c(ID * 1000, sclk, 65536.0, "J2000", cmat, &clkout, &found);
        sct2e_c(ID, clkout, &et);
        unload_c(last);
        unload_c(first);
        printf("    { \"epoch\": %.17g, \"et\": %.17g, \"cmat\": [", epochs[i], et);
        for (int r = 0; r < 3; r++) {
            for (int c = 0; c < 3; c++) {
                printf("%.17g%s", cmat[r][c], r == 2 && c == 2 ? "" : ", ");
            }
        }
        printf("] }%s\n", i == NEPOCHS - 1 ? "" : ",");
    }
    printf("  ]%s\n", isLast ? "" : ",");
}

int main(int argc, char **argv) {
    if (argc != 2) {
        fprintf(stderr, "usage: ck_priority_ref <output directory>\n");
        return 1;
    }
    char clock[1024], frame[1024], rates[1024], norates[1024], gap[1024];
    snprintf(clock, sizeof clock, "%s/ckprio.tsc", argv[1]);
    snprintf(frame, sizeof frame, "%s/ckprio.tf", argv[1]);
    snprintf(rates, sizeof rates, "%s/ckprio_rates.bc", argv[1]);
    snprintf(norates, sizeof norates, "%s/ckprio_norates.bc", argv[1]);
    snprintf(gap, sizeof gap, "%s/ckprio_gap.bc", argv[1]);
    FILE *f = fopen(clock, "w");
    fputs("KPL/SCLK\n\\begindata\nSCLK_KERNEL_ID           = ( @1957-01-01/00:00:00.0 )\nSCLK_DATA_TYPE_472401        = ( 1 )\n"
          "SCLK01_TIME_SYSTEM_472401    = ( 1 )\nSCLK01_N_FIELDS_472401       = ( 2 )\nSCLK01_MODULI_472401         = ( 4294967296 65536 )\n"
          "SCLK01_OFFSETS_472401        = ( 0 0 )\nSCLK01_OUTPUT_DELIM_472401   = ( 2 )\nSCLK_PARTITION_START_472401  = ( 0.0000000000000E+00 )\n"
          "SCLK_PARTITION_END_472401    = ( 2.8147497671065E+14 )\n"
          "SCLK01_COEFFICIENTS_472401   = ( 0.0000000000000E+00     -1.3569552000000E+09     1.0000000000000E+00 )\n\\begintext\n",
          f);
    fclose(f);
    furnsh_c(clock);
    f = fopen(frame, "w");
    fputs("KPL/FK\n\\begindata\nFRAME_CKPRIO_SC              = -472401000\nFRAME_-472401000_NAME        = 'CKPRIO_SC'\n"
          "FRAME_-472401000_CLASS       = 3\nFRAME_-472401000_CLASS_ID    = -472401000\nFRAME_-472401000_CENTER      = -472401\n"
          "CK_-472401000_SCLK           = -472401\nCK_-472401000_SPK            = -472401\n\\begintext\n",
          f);
    fclose(f);
    const double z[3] = {0.0, 0.0, 1.0}, x[3] = {1.0, 0.0, 0.0};
    double ratesTimes[11], noRatesTimes[9];
    for (int i = 0; i < 11; i++) {
        ratesTimes[i] = 100.0 * i;
    }
    for (int i = 0; i < 9; i++) {
        noRatesTimes[i] = 300.0 + 50.0 * i;
    }
    const double gapTimes[6] = {300.0, 350.0, 400.0, 600.0, 650.0, 700.0}, gapStarts[2] = {300.0, 600.0};
    write_ck(rates, ratesTimes, 11, ratesTimes, 1, z, 1.0e-3, SPICETRUE, "RATES");
    write_ck(norates, noRatesTimes, 9, noRatesTimes, 1, x, 2.0e-3, SPICEFALSE, "NO RATES");
    write_ck(gap, gapTimes, 6, gapStarts, 2, x, 2.0e-3, SPICEFALSE, "NO RATES GAP");

    double epochs[NEPOCHS];
    for (int i = 0; i <= 20; i++) {
        epochs[i] = 50.0 * i;
    }
    epochs[21] = 299.5;
    epochs[22] = 700.5;
    printf("{\n  \"spacecraft\": %d,\n  \"tolerance\": 1.0,\n", ID);
    print_reads("rates_last", epochs, norates, rates, 0);
    print_reads("norates_last", epochs, rates, norates, 0);
    print_reads("gap_last", epochs, rates, gap, 1);
    printf("}\n");
    return failed_c() ? 1 : 0;
}
