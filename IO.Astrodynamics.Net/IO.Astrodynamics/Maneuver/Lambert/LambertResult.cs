using System.Collections.Generic;
using System.Linq;

namespace IO.Astrodynamics.Maneuver.Lambert;

public class LambertResult
{
    public IReadOnlyCollection<LambertSolution> Solutions { get; }
    private List<LambertSolution> _solutions { get; }
    public int MaxRevolutions { get; }
    
    public LambertResult(int maxRevolutions)
    {
        MaxRevolutions = maxRevolutions;
        Solutions = new List<LambertSolution>();
    }
    
    public void AddSolution(LambertSolution solution)
    {
        _solutions.Add(solution);
    }
    
    public LambertSolution GetZeroRevolutionSolution()
    {
        return Solutions.FirstOrDefault(s => s.Revolutions == 0);
    }
    
    public IEnumerable<LambertSolution> GetMultiRevolutionSolutions(int revolutions)
    {
        return Solutions.Where(s => s.Revolutions == revolutions);
    }
}