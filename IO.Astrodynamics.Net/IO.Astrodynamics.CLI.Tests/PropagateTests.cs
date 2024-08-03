// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Tests;

public class PropagateTests
{
    [Fact]
    public void PropagateWithPerturbations()
    {
        lock (Configuration.objLock)
        {
            var command = new PropagateCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Propagate("Data", "MyBody",
                new Commands.Parameters.OrbitalParameters
                {
                    CenterOfMotionId = 399, OrbitalParametersEpoch = "0.0", Frame = "ICRF", OrbitalParametersValues = "6800000.0 0.0 0.0 0.0 8000.0 0.0",
                    FromStateVector = true
                }, new WindowParameters { Begin = "0.0", End = "3600.0" }, "PropagatorExport", true,
                true, 20,celestialBodies:[10, 301]).Wait();

            var res = sb.ToString();
            Assert.Contains("Propagation completed", res);
        }
    }
}