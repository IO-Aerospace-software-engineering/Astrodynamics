using System;
using System.IO;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body
{
    public class ClockTests
    {
        public ClockTests()
        {
            TestKernels.EnsureSolarSystem();
        }
        [Fact]
        public void CreateClock()
        {
            Clock clock = new Clock("clk", 256);
            Assert.Equal("clk", clock.Name);
            Assert.Equal((uint)256, clock.Resolution);
        }

        [Fact]
        public async Task WriteClock()
        {
            Clock clock = new Clock("clk", 256);
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clock,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    new TimeSystem.Time(DateTime.MinValue,TimeFrame.TDBFrame), Frames.Frame.ICRF));
            await clock.WriteAsync(new FileInfo("clock.tsc"));
            TextReader tr = new StreamReader("clock.tsc");
            var res = await tr.ReadToEndAsync();
            Assert.Equal($"KPL/SCLK{Environment.NewLine}\\begindata{Environment.NewLine}SCLK_KERNEL_ID           = ( @1957-01-01/00:00:00.0 ){Environment.NewLine}SCLK_DATA_TYPE_1001        = ( 1 ){Environment.NewLine}SCLK01_TIME_SYSTEM_1001    = ( 1 ){Environment.NewLine}SCLK01_N_FIELDS_1001       = ( 2 ){Environment.NewLine}SCLK01_MODULI_1001         = ( 4294967296 256 ){Environment.NewLine}SCLK01_OFFSETS_1001        = ( 0 0 ){Environment.NewLine}SCLK01_OUTPUT_DELIM_1001   = ( 2 ){Environment.NewLine}SCLK_PARTITION_START_1001  = ( 0.0000000000000E+00 ){Environment.NewLine}SCLK_PARTITION_END_1001    = ( 2.8147497671065E+14 ){Environment.NewLine}SCLK01_COEFFICIENTS_1001   = ( 0.0000000000000E+00     -1.3569552000000E+09     1.0000000000000E+00 ){Environment.NewLine}\\begintext{Environment.NewLine}", res);
        }

        [Fact]
        public void CreateInvalidClock()
        {
            Assert.Throws<ArgumentException>(() => new Clock("", 256));
            Assert.Throws<ArgumentException>(() => new Clock("clk", 0));
        }

        [Fact]
        public void Equality()
        {
            Clock clock = new Clock("clk", 256);
            Clock clock2 = new Clock("clk", 128);
            Assert.True(clock != clock2);
            Assert.False(clock == clock2);
            Assert.False(clock.Equals(clock2));
            Assert.False(clock.Equals((object)clock2));
            Assert.False(clock.Equals(null));
            Assert.False(clock.Equals((object)null));
        }

        private static StateVector LeoAbout(CelestialBody earth, TimeSystem.Time epoch) =>
            new(new Vector3(6800e3, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0), earth, epoch, Frames.Frame.ICRF);

        /// <summary>
        /// A clock attached to a spacecraft of another NAIF id is rejected by the three ways of building a spacecraft
        /// (#676): an SCLK identifies one spacecraft id (SCLK_DATA_TYPE_&lt;id&gt;), so the first spacecraft would write
        /// the second's. The clock stays with the first.
        /// </summary>
        [Theory]
        [InlineData("Propagated")]
        [InlineData("KernelBacked")]
        [InlineData("Opm")]
        public async Task ClockOfAnotherSpacecraft_Throws(string construction)
        {
            // Arrange
            var earth = new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH);
            var epoch = new TimeSystem.Time(2024, 1, 1);
            var clock = new Clock("SHARED", 65536);
            var first = new Spacecraft(-1431, "FIRST", 100.0, 1000.0, clock, LeoAbout(earth, epoch));

            // Act
            var exception = Record.Exception(() => construction switch
            {
                "Propagated" => new Spacecraft(-1432, "SECOND", 100.0, 1000.0, clock, LeoAbout(earth, epoch)),
                "KernelBacked" => new Spacecraft(-1432, "SECOND", epoch, clock: clock),
                _ => IO.Astrodynamics.CCSDS.OPM.Opm.CreateFromStateVector("SECOND", "2024-001A", LeoAbout(earth, epoch)).ToSpacecraft(-1432, 1000.0, clock, earth)
            });

            // Assert
            var rejected = Assert.IsType<ArgumentException>(exception);
            Assert.Equal("clock", rejected.ParamName);
            Assert.Same(first, clock.Spacecraft);
            var file = new FileInfo(Path.Combine(Constants.OutputPath.CreateSubdirectory("Clock676").FullName, $"first_{construction}.tsc"));
            await first.Clock.WriteAsync(file);
            var text = await File.ReadAllTextAsync(file.FullName);
            Assert.Contains("SCLK_DATA_TYPE_1431 ", text);
            Assert.DoesNotContain("1432", text);
        }

        /// <summary>
        /// A clock given to a constructor that throws stays free (#676): the same clock then builds a valid spacecraft.
        /// </summary>
        [Fact]
        public void ClockKeptAfterFailedConstructor()
        {
            // Arrange
            var earth = new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH);
            var clock = new Clock("RETRY", 65536);

            // Act: non-orthogonal body axes
            Assert.Throws<ArgumentException>(() => new Spacecraft(-1433, "FAILED", 100.0, 1000.0, clock, LeoAbout(earth, new TimeSystem.Time(2024, 1, 1)),
                bodyFront: Vector3.VectorX, bodyRight: Vector3.VectorX, bodyUp: Vector3.VectorZ));

            // Assert
            Assert.Null(clock.Spacecraft);
            var valid = new Spacecraft(-1433, "VALID", 100.0, 1000.0, clock, LeoAbout(earth, new TimeSystem.Time(2024, 1, 1)));
            Assert.Same(valid, clock.Spacecraft);
        }

        /// <summary>
        /// A clock held by another instance of the same NAIF id, a propagated spacecraft and its kernel-backed twin, writes the
        /// same SCLK: accepted, and it stays with its first spacecraft (#676).
        /// </summary>
        [Fact]
        public void ClockOfSameId_Accepted()
        {
            var earth = new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH);
            var clock = new Clock("SAME", 65536);
            var first = new Spacecraft(-1434, "FIRST", 100.0, 1000.0, clock, LeoAbout(earth, new TimeSystem.Time(2024, 1, 1)));
            var second = new Spacecraft(-1434, "SECOND", 100.0, 1000.0, clock, LeoAbout(earth, new TimeSystem.Time(2024, 1, 1)));
            Assert.Same(clock, second.Clock);
            Assert.Same(first, clock.Spacecraft);
        }

        /// <summary>
        /// Control (#676): the spacecraft's own SCLK declares its id, the CK is written against it, and SPICE's rotation on the
        /// CK (<c>sxform_c</c>, the SCLK through <c>sce2c_c</c>) equals the attitude record within the CK's 2024 accuracy
        /// (docs/reference/spacecraft.md, attitude and kernels).
        /// </summary>
        [Fact]
        public async Task OwnClock_SclkReadsTheCk()
        {
            // Arrange
            var earth = new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH);
            var epoch = new TimeSystem.Time(2024, 1, 1, 0, 0, 0, frame: TimeFrame.TDBFrame);
            var spacecraft = new Spacecraft(-1435, "OWNCLK", 100.0, 1000.0, new Clock("OWNCLK", 256),
                new KeplerianElements(7000e3, 0.001, 0.9, 0.0, 0.0, 0.0, earth, epoch, Frames.Frame.ICRF));
            var tank = new FuelTank("t", "m", "s", 100.0, 100.0);
            spacecraft.AddFuelTank(tank);
            var engine = new Engine("e", "m", "s", 300.0, 1.0, tank);
            spacecraft.AddEngine(engine);
            spacecraft.SetStandbyManeuver(Maneuver.TriadAttitude.CreateLVLH(earth, epoch, TimeSpan.Zero, engine));
            spacecraft.Propagate(new Window(epoch, epoch.AddSeconds(3600.0)), [earth], new Propagator.Integrators.RK78Integrator(1e-10, 1e-10),
                false, false, TimeSpan.FromSeconds(60.0));
            var directory = Constants.OutputPath.CreateSubdirectory("Clock676").CreateSubdirectory(Guid.NewGuid().ToString("N")[..8]);
            var sclk = new FileInfo(Path.Combine(directory.FullName, "ownclk.tsc"));
            var fk = new FileInfo(Path.Combine(directory.FullName, "ownclk.tf"));
            var ck = new FileInfo(Path.Combine(directory.FullName, "ownclk.bc"));
            await spacecraft.Clock.WriteAsync(sclk);
            await spacecraft.Frame.WriteAsync(fk);
            Assert.Contains("SCLK_DATA_TYPE_1435 ", await File.ReadAllTextAsync(sclk.FullName));

            SpiceAPI.Instance.LoadKernels(sclk);
            try
            {
                // Act
                spacecraft.WriteOrientation(ck);
                fk.Refresh();
                ck.Refresh();
                Assert.True(fk.Exists && ck.Exists);
                SpiceAPI.Instance.LoadKernels(fk);
                SpiceAPI.Instance.LoadKernels(ck);
                try
                {
                    var t = epoch.AddSeconds(1800.0);
                    var spice = SpiceAPI.Instance.TransformFrame(t, new Frames.Frame("OWNCLK_FRAME"), Frames.Frame.ICRF).Rotation;

                    // Assert
                    double angle = AttitudeRecordTests.RotationAngle(spacecraft.Frame.GetStateOrientationToICRF(t).Rotation, spice);
                    Assert.True(angle <= 6.1e-10, $"{angle:E3} rad");
                }
                finally
                {
                    SpiceAPI.Instance.UnloadKernels(ck);
                    SpiceAPI.Instance.UnloadKernels(fk);
                }
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(sclk);
            }
        }
    }
}
