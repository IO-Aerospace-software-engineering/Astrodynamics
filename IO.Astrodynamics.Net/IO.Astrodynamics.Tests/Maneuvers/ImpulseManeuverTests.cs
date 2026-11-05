using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers
{
    /// <summary>
    /// The arguments of the target-orbit constructor of <see cref="ImpulseManeuver"/>, through the three maneuvers built on
    /// it (#552).
    /// </summary>
    public class ImpulseManeuverTests
    {
        public ImpulseManeuverTests()
        {
            SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        private static readonly TimeSystem.Time MinimumEpoch = new(DateTime.MinValue, TimeFrame.TDBFrame);

        private static ImpulseManeuver Create(string maneuver, IO.Astrodynamics.OrbitalParameters.OrbitalParameters targetOrbit, Engine engine) => maneuver switch
        {
            nameof(ApsidalAlignmentManeuver) => new ApsidalAlignmentManeuver(MinimumEpoch, TimeSpan.Zero, targetOrbit, engine),
            nameof(PlaneAlignmentManeuver) => new PlaneAlignmentManeuver(MinimumEpoch, TimeSpan.Zero, targetOrbit, engine),
            nameof(PhasingManeuver) => new PhasingManeuver(MinimumEpoch, TimeSpan.Zero, targetOrbit, 2, engine),
            _ => throw new ArgumentOutOfRangeException(nameof(maneuver))
        };

        private static Engine Engine(bool attached)
        {
            if (!attached)
            {
                return new Engine("eng", "engmk1", "12345", 450, 50, new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            }

            var orbit = new KeplerianElements(7000000.0, 0.01, 0.1, 0.0, 0.0, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var spc = new Spacecraft(-1552, "Impulse1552", 1000.0, 3000.0, new Clock("Clk1552", 65536), orbit);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            return spc.Engines.First();
        }

        private static KeplerianElements Target(Frames.Frame frame) =>
            new(8000000.0, 0.1, 0.5, 0.2, 0.3, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, frame);

        /// <summary>
        /// A null target orbit is an argument error naming it, whether the fuel tank of the engine is attached to a
        /// spacecraft or not, not the <see cref="NullReferenceException"/> of its dereference.
        /// </summary>
        [Theory]
        [InlineData(nameof(ApsidalAlignmentManeuver), false)]
        [InlineData(nameof(ApsidalAlignmentManeuver), true)]
        [InlineData(nameof(PlaneAlignmentManeuver), false)]
        [InlineData(nameof(PlaneAlignmentManeuver), true)]
        [InlineData(nameof(PhasingManeuver), false)]
        [InlineData(nameof(PhasingManeuver), true)]
        public void ANullTargetOrbit_IsAnArgumentError(string maneuver, bool attached)
        {
            var engine = Engine(attached);

            var exception = Assert.Throws<ArgumentNullException>(() => Create(maneuver, null, engine));

            Assert.Equal("targetOrbit", exception.ParamName);
        }

        /// <summary>
        /// A null target orbit is reported before a null engine; a null engine with a target orbit is the error of the
        /// engine.
        /// </summary>
        [Theory]
        [InlineData(nameof(ApsidalAlignmentManeuver))]
        [InlineData(nameof(PlaneAlignmentManeuver))]
        [InlineData(nameof(PhasingManeuver))]
        public void ANullEngine_IsAnArgumentError(string maneuver)
        {
            Assert.Equal("targetOrbit", Assert.Throws<ArgumentNullException>(() => Create(maneuver, null, null)).ParamName);
            Assert.Equal("engine", Assert.Throws<ArgumentNullException>(() => Create(maneuver, Target(Frames.Frame.ICRF), null)).ParamName);
        }

        /// <summary>
        /// A target orbit in a frame other than that of the spacecraft of the engine is rejected; without a spacecraft,
        /// or in its frame, the maneuver takes the target orbit and its observer as center.
        /// </summary>
        [Theory]
        [InlineData(nameof(ApsidalAlignmentManeuver))]
        [InlineData(nameof(PlaneAlignmentManeuver))]
        [InlineData(nameof(PhasingManeuver))]
        public void TheFrameOfTheTargetOrbit_IsChecked(string maneuver)
        {
            var exception = Assert.Throws<ArgumentException>(() => Create(maneuver, Target(Frames.Frame.ECLIPTIC_J2000), Engine(true)));
            Assert.IsNotType<ArgumentNullException>(exception);

            var target = Target(Frames.Frame.ICRF);
            foreach (var engine in new[] { Engine(true), Engine(false) })
            {
                var created = Create(maneuver, target, engine);
                Assert.Same(target, created.TargetOrbit);
                Assert.Equal(TestHelpers.EarthAtJ2000.NaifId, created.ManeuverCenter.NaifId);
                Assert.Same(engine, created.Engine);
            }

            var unattached = Create(maneuver, Target(Frames.Frame.ECLIPTIC_J2000), Engine(false));
            Assert.Equal(Frames.Frame.ECLIPTIC_J2000, unattached.TargetOrbit.Frame);
        }
    }
}
