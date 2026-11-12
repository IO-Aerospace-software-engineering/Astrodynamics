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
            TestKernels.EnsureSolarSystem();
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

        /// <summary>
        /// <see cref="ImpulseManeuver.TryExecute"/> leaves the state it is given unchanged and returns the state after the
        /// impulse as a new state (#588): before, it added the delta-V to the velocity of the given state, so that a
        /// spacecraft built on that state, or on Keplerian elements whose cached state it was, started its next
        /// propagation from the state after the burn while its elements still described the orbit before it. Every
        /// impulse maneuver, from a state vector (with and without a covariance, which the state after carries) and from
        /// Keplerian elements.
        /// </summary>
        [Theory]
        [InlineData(nameof(ApogeeHeightManeuver), "elements")]
        [InlineData(nameof(ApogeeHeightManeuver), "state")]
        [InlineData(nameof(ApogeeHeightManeuver), "covariance")]
        [InlineData(nameof(PerigeeHeightManeuver), "elements")]
        [InlineData(nameof(PerigeeHeightManeuver), "state")]
        [InlineData(nameof(CombinedManeuver), "elements")]
        [InlineData(nameof(CombinedManeuver), "state")]
        [InlineData(nameof(PlaneAlignmentManeuver), "elements")]
        [InlineData(nameof(PlaneAlignmentManeuver), "state")]
        [InlineData(nameof(ApsidalAlignmentManeuver), "elements")]
        [InlineData(nameof(ApsidalAlignmentManeuver), "state")]
        [InlineData(nameof(PhasingManeuver), "elements")]
        [InlineData(nameof(PhasingManeuver), "state")]
        [InlineData("PhasingManeuverTrueLongitude", "elements")]
        [InlineData("PhasingManeuverTrueLongitude", "state")]
        [InlineData("PhasingManeuverTrueLongitude", "covariance")]
        public void TryExecute_LeavesTheGivenStateUnchanged(string maneuver, string initial)
        {
            var center = TestHelpers.EarthAtJ2000;
            var elements = new KeplerianElements(7000000.0, 0.1, 0.3, 0.2, 0.4, 1.0, center, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            var covariance = new Matrix(6, 6);
            for (int i = 0; i < 6; i++)
            {
                covariance.Set(i, i, i < 3 ? 100.0 : 1.0e-4);
            }

            IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit = initial switch
            {
                "elements" => elements,
                "state" => new StateVector(elements.ToStateVector().Position, elements.ToStateVector().Velocity, center, elements.Epoch, elements.Frame),
                _ => new StateVector(elements.ToStateVector().Position, elements.ToStateVector().Velocity, center, elements.Epoch, elements.Frame, covariance)
            };
            // Fuel enough for any of the burns: the state is not at the event of the maneuver, which TryExecute does not check
            var spc = new Spacecraft(-1588, "Impulse1588", 1000.0, 1.0e7, new Clock("Clk1588", 65536), orbit);
            spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1.0e6, 9.0e5));
            spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
            var engine = spc.Engines.First();
            var target = new KeplerianElements(8000000.0, 0.15, 0.5, 0.3, 0.6, 0.0, center, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            // The apsidal alignment intersects two orbits of the same plane: the orbit rotated by 0.05 rad in its plane
            var coplanar = new KeplerianElements(7000000.0, 0.1, 0.3, 0.2, 0.45, 1.0, center, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
            ImpulseManeuver impulse = maneuver switch
            {
                nameof(ApogeeHeightManeuver) => new ApogeeHeightManeuver(center, MinimumEpoch, TimeSpan.Zero, 9000000.0, engine),
                nameof(PerigeeHeightManeuver) => new PerigeeHeightManeuver(center, MinimumEpoch, TimeSpan.Zero, 6800000.0, engine),
                nameof(CombinedManeuver) => new CombinedManeuver(center, MinimumEpoch, TimeSpan.Zero, 6500000.0, 0.1, engine),
                nameof(PlaneAlignmentManeuver) => new PlaneAlignmentManeuver(MinimumEpoch, TimeSpan.Zero, target, engine),
                nameof(ApsidalAlignmentManeuver) => new ApsidalAlignmentManeuver(MinimumEpoch, TimeSpan.Zero, coplanar, engine),
                nameof(PhasingManeuver) => new PhasingManeuver(MinimumEpoch, TimeSpan.Zero, elements.AtEpoch(elements.Epoch + TimeSpan.FromMinutes(10.0)), 2, engine),
                _ => new PhasingManeuver(center, MinimumEpoch, TimeSpan.Zero, 1.0, 2, engine)
            };
            spc.SetStandbyManeuver(impulse);

            var given = orbit.ToStateVector();
            var position = given.Position;
            var velocity = given.Velocity;
            var (after, _) = impulse.TryExecute(given);

            // The given state, and the initial orbit of the spacecraft, bit for bit as before the call
            Assert.Equal(position, given.Position);
            Assert.Equal(velocity, given.Velocity);
            Assert.Equal(position, spc.InitialOrbitalParameters.ToStateVector().Position);
            Assert.Equal(velocity, spc.InitialOrbitalParameters.ToStateVector().Velocity);

            // The state after the impulse: a new state, the delta-V added to the velocity, the rest of the given state
            Assert.NotSame(given, after);
            Assert.NotEqual(Vector3.Zero, impulse.DeltaV);
            Assert.Equal(position, after.Position);
            Assert.Equal(velocity + impulse.DeltaV, after.Velocity);
            Assert.Equal(given.Epoch, after.Epoch);
            Assert.Equal(given.Frame, after.Frame);
            Assert.Same(given.Observer, after.Observer);
            Assert.Equal(given.Covariance.HasValue, after.Covariance.HasValue);
            for (int i = 0; given.Covariance.HasValue && i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    Assert.Equal(given.Covariance.Value.Get(i, j), after.Covariance!.Value.Get(i, j));
                }
            }

            Assert.Equal(initial == "covariance", after.Covariance.HasValue);
        }
    }
}
