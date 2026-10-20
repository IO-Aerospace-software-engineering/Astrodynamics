using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.ConformanceRunner.Models;

namespace IO.Astrodynamics.ConformanceRunner.Comparison;

/// <summary>
/// Compares a state transition matrix with its golden by 3×3 block: Φ_rr, Φ_rv, Φ_vr and Φ_vv, each by the Frobenius
/// norm of its difference, absolute and relative to the norm of the golden block.
/// </summary>
/// <remarks>
/// The blocks have different units (none, s, 1/s, none), so each is measured against itself; a component-wise
/// tolerance under the OR rule of spec.md §6 would let a near-zero element pass anything. A block passes when its
/// absolute delta is within <c>abs_tol</c> or its relative delta within <c>rel_tol</c> of the tolerance
/// <see cref="ToleranceKey"/>, the OR rule applied to the block norms.
/// </remarks>
public static class StmComparer
{
    /// <summary>The tolerance of the blocks in <c>tolerances.yaml</c> and in the overrides of a case.</summary>
    public const string ToleranceKey = "stm_block";

    /// <summary>The names of the four deltas, in the order of the blocks.</summary>
    public static readonly string[] BlockNames = { "stm_rr", "stm_rv", "stm_vr", "stm_vv" };

    /// <summary>
    /// The deltas of the four blocks of <paramref name="computed"/> (row-major 6×6) against
    /// <paramref name="golden"/> (6 arrays of 6 numbers).
    /// </summary>
    /// <exception cref="ArgumentException">The golden is not 6 rows of 6 numbers.</exception>
    public static Dictionary<string, DeltaValue> Compare(double[] computed, JsonElement golden, TolerancePair tolerance)
    {
        var expected = Parse(golden);
        var deltas = new Dictionary<string, DeltaValue>();
        for (int block = 0; block < 4; block++)
        {
            int rowOffset = block < 2 ? 0 : 3;
            int columnOffset = block % 2 == 0 ? 0 : 3;
            double difference = 0.0;
            double norm = 0.0;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int index = 6 * (rowOffset + i) + columnOffset + j;
                    difference += (computed[index] - expected[index]) * (computed[index] - expected[index]);
                    norm += expected[index] * expected[index];
                }
            }

            double absolute = System.Math.Sqrt(difference);
            double relative = norm > 0.0 ? absolute / System.Math.Sqrt(norm) : absolute == 0.0 ? 0.0 : double.PositiveInfinity;
            deltas[BlockNames[block]] = new DeltaValue
            {
                MaxAbsDelta = absolute,
                MaxRelDelta = relative,
                Passed = absolute <= tolerance.AbsTol || relative <= tolerance.RelTol
            };
        }

        return deltas;
    }

    private static double[] Parse(JsonElement golden)
    {
        if (golden.ValueKind != JsonValueKind.Array || golden.GetArrayLength() != 6)
        {
            throw new ArgumentException("final_stm must be 6 rows of 6 numbers", nameof(golden));
        }

        var rows = golden.EnumerateArray().Select(row => row.EnumerateArray().Select(x => x.GetDouble()).ToArray())
            .ToArray();
        if (rows.Any(row => row.Length != 6))
        {
            throw new ArgumentException("final_stm must be 6 rows of 6 numbers", nameof(golden));
        }

        return rows.SelectMany(row => row).ToArray();
    }
}
