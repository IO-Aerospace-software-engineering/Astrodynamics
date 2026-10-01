using System.Collections.Generic;
using IO.Astrodynamics.ConformanceRunner.Models;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

public interface ICategorySolver
{
    string Category { get; }

    /// <summary>
    /// Solve a conformance test case and return computed outputs as a dictionary.
    /// Keys are metric names matching the expected-result schema.
    /// </summary>
    Dictionary<string, object> Solve(CaseInput caseInput);
}
