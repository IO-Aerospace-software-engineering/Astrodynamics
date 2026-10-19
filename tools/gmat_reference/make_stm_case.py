"""Write the conformance fixture of the stm category (F4 of phase 2, feature 1; design of #408): the schemas v1 of the
conformance-tests suite extended with the category, its tolerance, and the case stm_24h_leo_twobody_001, whose golden
is GMAT's Phi of the case G1 (the reference case R1: LEO 700 km, two-body, one day), turned to ICRF.

Written into <output>:
  schemas/v1/case.schema.json, expected.schema.json, runner-report.schema.json
      the schemas of the suite with "stm" added to the categories: its inputs those of "propagator", its outputs
      final_stm (6 rows of 6 numbers, Phi(t_end, t_start) in the frame of the case) and, optionally, the final state
  tolerances.yaml
      the tolerances of the suite with stm_block: the Frobenius norm of the difference of each 3 x 3 block of Phi,
      absolute (abs_tol) or relative to the norm of the golden block (rel_tol), under the OR rule of spec.md section 6
  cases/stm/001/inputs.yaml, expected-result.json
      the case and its golden: GMAT's nominal report of G1 (Sat.OrbitSTM and the final state, EarthMJ2000Eq), turned to
      ICRF by the rotation R0 between the initial state given to GMAT in EarthICRF and the first state of the report

This is the extension proposed to the conformance-tests suite; the fixture lets the runner be tested on it before.

Usage: python3 -I make_stm_case.py <conformance-tests schemas/v1> <conformance-tests tolerances.yaml> <cases.json>
                                  <GMAT G1/nominal.txt> <output directory>
"""
import json
import os
import sys

CASE_ID = "stm_24h_leo_twobody_001"


def triad(state):
    def unit(a):
        n = sum(x * x for x in a) ** 0.5
        return [x / n for x in a]

    def cross(a, b):
        return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]

    r, v = state[:3], state[3:]
    e1 = unit(r)
    e3 = unit(cross(r, v))
    e2 = cross(e3, e1)
    return [[e1[i], e2[i], e3[i]] for i in range(3)]


def rotation(icrf, mj2000):
    a, b = triad(icrf), triad(mj2000)
    return [[sum(a[i][k] * b[j][k] for k in range(3)) for j in range(3)] for i in range(3)]


def extend_case_schema(schema):
    schema["properties"]["category"]["enum"].append("stm")
    schema.setdefault("allOf", []).append({
        "if": {"properties": {"category": {"const": "stm"}}},
        "then": {"properties": {"inputs": {"$ref": "#/$defs/propagator_inputs"}}}
    })
    return schema


def extend_expected_schema(schema):
    schema["properties"]["category"]["enum"].append("stm")
    row = {"type": "array", "items": {"$ref": "#/$defs/nullable_number"}, "minItems": 6, "maxItems": 6}
    schema["$defs"]["stm_outputs"] = {
        "description": "Phi(t_end, t_start), 6 rows of 6 numbers, in the frame of the case; the same in km and km/s "
                       "as in m and m/s (the rv block in s, the vr block in 1/s). Compared by 3 x 3 block (stm_block). "
                       "The final state is optional, compared as for the propagator category.",
        "type": "object",
        "properties": dict({"final_stm": {"type": "array", "items": row, "minItems": 6, "maxItems": 6}},
                           **{k: {"$ref": "#/$defs/nullable_number"} for k in
                              ["final_x_km", "final_y_km", "final_z_km", "final_vx_km_s", "final_vy_km_s",
                               "final_vz_km_s"]}),
        "required": ["final_stm"],
        "additionalProperties": False
    }
    schema.setdefault("allOf", []).append({
        "if": {"properties": {"category": {"const": "stm"}}},
        "then": {"properties": {"outputs": {"$ref": "#/$defs/stm_outputs"}}}
    })
    return schema


def km(value):
    return value / 1000.0


def main():
    if len(sys.argv) != 6:
        sys.exit(__doc__)
    schemas, tolerances, cases_path, report, output = sys.argv[1:]
    with open(cases_path) as f:
        case = next(c for c in json.load(f)["cases"] if c["name"] == "G1")
    rows = [[float(x) for x in line.split()] for line in open(report) if line.strip()]
    first, last_state, last_phi = rows[0], rows[-7], rows[-6:]
    r0 = rotation(case["state_m_m_s"], [x * 1000.0 for x in first])

    def rotate(v):
        return [sum(r0[i][k] * v[k] for k in range(3)) for i in range(3)]

    phi = [[0.0] * 6 for _ in range(6)]
    for bi in (0, 3):
        for bj in (0, 3):
            for i in range(3):
                for j in range(3):
                    phi[bi + i][bj + j] = sum(r0[i][k] * last_phi[bi + k][bj + m] * r0[j][m]
                                              for k in range(3) for m in range(3))
    final = rotate(last_state[:3]) + rotate(last_state[3:])

    os.makedirs(os.path.join(output, "schemas", "v1"), exist_ok=True)
    for name, extend in (("case.schema.json", extend_case_schema), ("expected.schema.json", extend_expected_schema),
                         ("runner-report.schema.json", lambda s: s)):
        with open(os.path.join(schemas, name)) as f:
            schema = extend(json.load(f))
        with open(os.path.join(output, "schemas", "v1", name), "w", newline="\n") as f:
            json.dump(schema, f, indent=1, ensure_ascii=False)
            f.write("\n")

    with open(tolerances) as f:
        text = f.read()
    with open(os.path.join(output, "tolerances.yaml"), "w", newline="\n") as f:
        f.write(text.rstrip("\n") + "\n\n"
                "  # State transition matrix (stm category): the Frobenius norm of the difference of each 3 x 3 block,\n"
                "  # absolute or relative to the norm of the golden block. 1e-7 relative: the threshold of the two-body\n"
                "  # case R1 of the validation of IO.Astrodynamics against central differences of whole propagations.\n"
                "  stm_block:\n"
                "    abs_tol: 0.0\n"
                "    rel_tol: 1.0e-7\n")

    state = case["state_m_m_s"]
    directory = os.path.join(output, "cases", "stm", "001")
    os.makedirs(directory, exist_ok=True)
    epoch = case["epoch_tdb"] + " TDB"
    end_seconds = case["duration_s"]
    end = "2021-03-21T12:00:00.000 TDB" if end_seconds == 86400 else None
    if end is None or not case["epoch_tdb"].startswith("2021-03-20T12:00:00"):
        sys.exit("The case G1 no longer starts on 2021-03-20 12:00 TDB for one day; update the end of the window")
    with open(os.path.join(directory, "inputs.yaml"), "w", newline="\n") as f:
        f.write('id: "%s"\n' % CASE_ID)
        f.write('schema_version: "v1"\nversion: "0.1.0"\ncategory: "stm"\ncreated: "2026-10-19"\n')
        f.write("description: >\n"
                "  State transition matrix of a 24-hour two-body propagation, LEO 700 km, e = 0.001, i = 45 deg\n"
                "  (the reference case R1 of IO.Astrodynamics). Golden: GMAT R2026a, Sat.OrbitSTM, PrinceDormand78\n"
                "  at a fixed step of 10 s, the GM of DE440, turned from EarthMJ2000Eq to ICRF.\n\n")
        f.write('metadata:\n  ephemeris_kernel: "de440s.bsp"\n  reference_frame: "ICRF"\n  time_scale: "TDB"\n\n')
        f.write("inputs:\n")
        f.write('  epoch: "%s"\n' % epoch)
        f.write('  orbit:\n    type: "state_vector"\n')
        f.write("    position_km: [%r, %r, %r]\n" % tuple(km(x) for x in state[:3]))
        f.write("    velocity_km_s: [%r, %r, %r]\n" % tuple(km(x) for x in state[3:]))
        f.write('  propagation_window:\n    start: "%s"\n    end:   "%s"\n' % (epoch, end))
        f.write('  central_body: "Earth"\n  geopotential_degree: 0\n  geopotential_model: "none"\n')
        f.write("  perturbation_bodies: []\n  force_model:\n    drag: false\n    srp: false\n  step_size_s: 3600.0\n\n")
        f.write("tolerances_override:\n"
                "  stm_block:\n"
                "    abs_tol: 0.0\n"
                "    rel_tol: 1.0e-10\n"
                "    justification: >-\n"
                "      Two-body, the same model in both tools: IO.Astrodynamics against GMAT R2026a, worst block 7.6e-12\n"
                "      over the day (GmatReferenceTests, 2026-10-19); GMAT's Phi at 10 s against 5 s, 2.0e-12. The\n"
                "      threshold of the design of IO.Astrodynamics #408. rel_tol is binding, abs_tol 0.\n")
    expected = {
        "id": CASE_ID,
        "category": "stm",
        "case_version": "0.1.0",
        "generated_by": {
            "tool": "GMAT",
            "tool_version": "R2026a",
            "kernel": "de440s.bsp",
            "gravity_model": "point-mass Earth, GM = %r km^3/s^2 (SPICE DE440)" % (case["earth"]["gm_m3_s2"] / 1e9),
            "perturbations": "None (two-body only)",
            "integrator": "PrinceDormand78 (RK7/8), fixed step %r s" % case["fixed_step_s"],
            "method": "Sat.OrbitSTM and the final state in EarthMJ2000Eq, turned to ICRF by the rotation between the "
                      "initial state in EarthICRF and in EarthMJ2000Eq (tools/gmat_reference/make_stm_case.py of "
                      "IO.Astrodynamics)",
            "date": "2026-10-19",
            "reference_frame": "ICRF"
        },
        "outputs": dict({"final_stm": phi},
                        **dict(zip(["final_x_km", "final_y_km", "final_z_km", "final_vx_km_s", "final_vy_km_s",
                                    "final_vz_km_s"], final)))
    }
    with open(os.path.join(directory, "expected-result.json"), "w", newline="\n") as f:
        json.dump(expected, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
