using System.Collections.Generic;
using System.Linq;

namespace IO.Astrodynamics.Maneuver.Lambert;

public class LambertResult
{
    public IReadOnlyCollection<LambertSolution> Solutions => _solutions.AsReadOnly();
    private List<LambertSolution> _solutions { get; }
    public ushort MaxRevolutions { get; }

    public LambertResult(ushort maxRevolutions)
    {
        MaxRevolutions = maxRevolutions;
        _solutions = new List<LambertSolution>((int)maxRevolutions * 2 + 1);
    }

    public void AddSolution(LambertSolution solution)
    {
        _solutions.Add(solution);
    }

    public LambertSolution GetZeroRevolutionSolution()
    {
        return _solutions.FirstOrDefault(s => s.Revolutions == 0);
    }

    public IEnumerable<LambertSolution> GetMultiRevolutionSolutions(uint revolutions)
    {
        return _solutions.Where(s => s.Revolutions == revolutions);
    }
}