using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Maneuvers
{
    /// <summary>
    /// The arguments of the target-orbit constructor of <see cref="ImpulseManeuver"/>, through the three maneuvers built on
    /// it (#552), and of the target-orbit constructors of <see cref="ApogeeHeightManeuver"/> and
    /// <see cref="PerigeeHeightManeuver"/>, which chain to its maneuver-centre constructor (#555).
    /// </summary>
    public class ImpulseManeuverTests
    {
        private readonly ITestOutputHelper _output;

        public ImpulseManeuverTests(ITestOutputHelper output)
        {
            _output = output;
            TestKernels.EnsureSolarSystem();
        }

        private static readonly TimeSystem.Time MinimumEpoch = new(DateTime.MinValue, TimeScale.TDB);

        private static ImpulseManeuver Create(string maneuver, IO.Astrodynamics.OrbitalParameters.OrbitalParameters targetOrbit, Engine engine) => maneuver switch
        {
            nameof(ApsidalAlignmentManeuver) => new ApsidalAlignmentManeuver(MinimumEpoch, TimeSpan.Zero, targetOrbit, engine),
            nameof(PlaneAlignmentManeuver) => new PlaneAlignmentManeuver(MinimumEpoch, TimeSpan.Zero, targetOrbit, engine),
            nameof(PhasingManeuver) => new PhasingManeuver(MinimumEpoch, TimeSpan.Zero, targetOrbit, 2, engine),
            nameof(ApogeeHeightManeuver) => new ApogeeHeightManeuver(MinimumEpoch, TimeSpan.Zero, targetOrbit, engine),
            nameof(PerigeeHeightManeuver) => new PerigeeHeightManeuver(MinimumEpoch, TimeSpan.Zero, targetOrbit, engine),
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
        /// spacecraft or not, not the <see cref="NullReferenceException"/> of its dereference. #555: the two height
        /// maneuvers threw that <see cref="NullReferenceException"/> on a85098d, reading the apogee or perigee vector of the
        /// null orbit in their constructor chain.
        /// </summary>
        [Theory]
        [InlineData(nameof(ApsidalAlignmentManeuver), false)]
        [InlineData(nameof(ApsidalAlignmentManeuver), true)]
        [InlineData(nameof(PlaneAlignmentManeuver), false)]
        [InlineData(nameof(PlaneAlignmentManeuver), true)]
        [InlineData(nameof(PhasingManeuver), false)]
        [InlineData(nameof(PhasingManeuver), true)]
        [InlineData(nameof(ApogeeHeightManeuver), false)]
        [InlineData(nameof(ApogeeHeightManeuver), true)]
        [InlineData(nameof(PerigeeHeightManeuver), false)]
        [InlineData(nameof(PerigeeHeightManeuver), true)]
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
        [InlineData(nameof(ApogeeHeightManeuver))]
        [InlineData(nameof(PerigeeHeightManeuver))]
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
        /// On a spacecraft whose initial orbit is a TLE or an osculating TEME state, propagated in ICRF (#675), the target
        /// orbit is accepted in the frame of the initial orbit (TEME, as before) or in ICRF, the frame of the propagation,
        /// which threw before; any other frame is rejected, as before. A set of mean elements in TEME is not propagated in
        /// ICRF: an ICRF target is rejected on it, as before.
        /// </summary>
        [Theory]
        [InlineData(nameof(ApsidalAlignmentManeuver))]
        [InlineData(nameof(PlaneAlignmentManeuver))]
        [InlineData(nameof(PhasingManeuver))]
        public void OnATleOrTemeSpacecraft_TheTargetIsTakenInTemeOrIcrf(string maneuver)
        {
            var tle = new IO.Astrodynamics.OrbitalParameters.TLE.TLE("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
                "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
            var sgp4 = tle.ToOsculating();
            var orbits = new IO.Astrodynamics.OrbitalParameters.OrbitalParameters[]
            {
                tle, new StateVector(sgp4.Position, sgp4.Velocity, tle.Observer, sgp4.Epoch, Frames.Frame.TEME), tle.ToMeanKeplerianElements()
            };
            for (int k = 0; k < orbits.Length; k++)
            {
                var spc = new Spacecraft(-1553 - k, $"Impulse{1553 + k}", 1000.0, 3000.0, new Clock($"Clk{1553 + k}", 65536), orbits[k]);
                spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
                spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
                var engine = spc.Engines.First();
                bool mean = orbits[k] is KeplerianElements { ElementsType: OrbitalElementsType.Mean };

                Assert.Equal(Frames.Frame.TEME, Create(maneuver, Target(Frames.Frame.TEME), engine).TargetOrbit.Frame);
                if (mean)
                {
                    Assert.Throws<ArgumentException>(() => Create(maneuver, Target(Frames.Frame.ICRF), engine));
                }
                else
                {
                    Assert.Equal(Frames.Frame.ICRF, Create(maneuver, Target(Frames.Frame.ICRF), engine).TargetOrbit.Frame);
                }

                Assert.Throws<ArgumentException>(() => Create(maneuver, Target(Frames.Frame.ECLIPTIC_J2000), engine));
            }
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
            LabelAssert.Equal(given.Epoch, after.Epoch);
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

        /// <summary>
        /// Review of #680's known limitation: <see cref="Vector3.To"/> throws for a zero target now, and an impulse whose
        /// delta-V is zero (a target already reached) has no direction to point its front along. Its attitude stays the
        /// identity, which the attitude record holds for "no attitude", bit for bit the value the impulse returned before
        /// 11.0.0, and its state after is the state before.
        /// </summary>
        [Fact]
        public void AZeroDeltaV_KeepsTheIdentityAttitude()
        {
            var engine = Engine(true);
            var spc = engine.FuelTank.Spacecraft;
            var impulse = new IO.Astrodynamics.Tests.Propagators.Variational.FixedImpulse(TestHelpers.EarthAtJ2000, MinimumEpoch,
                TimeSystem.Time.J2000TDB, Vector3.Zero, engine);
            spc.SetStandbyManeuver(impulse);

            var given = spc.InitialOrbitalParameters.ToStateVector();
            var (after, orientation) = impulse.TryExecute(given);

            Assert.Equal(Quaternion.Identity, orientation.Rotation);
            Assert.Equal(Vector3.Zero, impulse.DeltaV);
            Assert.Equal(given.Position, after.Position);
            Assert.Equal(given.Velocity, after.Velocity);
        }

        // #685: a plane change of 2° between circular orbits of 7000 km about the point-mass Earth, RK7(8) over 2 h, against
        // the closed form 2 v sin(Δi/2) with the GM of the Earth. Measured on each of Linux x64, Windows x64 and macOS arm64
        // (CI run 43745883709, 2026-11-15): |ΔV| − closed form 6.986e-9 m/s, the final angle between h and the target's h
        // 3.996e-11°; thresholds twice those, rounded up at the second significant digit
        private const double PlaneChangeDeltaVThreshold = 1.4e-8;
        private const double PlaneChangeFinalAngleThreshold = 8.0e-11 * IO.Astrodynamics.Constants.Deg2Rad;

        private static readonly TimeSystem.Time PlaneChangeStart = TimeSystem.Time.J2000TDB;
        private static readonly Window PlaneChangeWindow = new(PlaneChangeStart, PlaneChangeStart.AddHours(2.0));
        private static readonly TimeSpan PlaneChangeStep = TimeSpan.FromSeconds(10.0);

        // The spacecraft starts 1 rad past the ascending node, so that the plane change happens at the descending node
        private static KeplerianElements CircularOrbit(IO.Astrodynamics.Body.CelestialBody earth, double inclinationDeg) =>
            new(7000000.0, 0.0, inclinationDeg * IO.Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 1.0, earth, PlaneChangeStart, Frames.Frame.ICRF);

        // The target of each maneuver: the circular orbit at 32° of the plane change, or for the apsidal alignment an
        // ellipse of the plane of the spacecraft whose apsides are 90° from its node, which the circular orbit crosses
        private static KeplerianElements TargetOf(string maneuver, IO.Astrodynamics.Body.CelestialBody earth) =>
            maneuver == nameof(ApsidalAlignmentManeuver)
                ? new KeplerianElements(7000000.0, 0.1, 30.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, System.Math.PI / 2.0, 0.0, earth,
                    PlaneChangeStart, Frames.Frame.ICRF)
                : CircularOrbit(earth, 32.0);

        private static Integrator PlaneChangeIntegrator(string integrator) =>
            integrator == "VV" ? new VVIntegrator(PlaneChangeStep) : new RK78Integrator();

        private static Spacecraft PlaneChangeSpacecraft(int id, IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit) =>
            new(id, $"PlaneChange{-id}", 1000.0, 3000.0, new Clock($"PlaneChangeClk{-id}", 65536), orbit);

        private static (Spacecraft, Engine) PlaneChangeSpacecraftWithEngine(int id, IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit)
        {
            var spacecraft = PlaneChangeSpacecraft(id, orbit);
            spacecraft.AddFuelTank(new FuelTank("ft685", "ftA", $"sn{-id}", 1000.0, 900.0));
            spacecraft.AddEngine(new Engine("eng685", "engmk1", $"sn{-id}", 450, 50, spacecraft.FuelTanks.First()));
            return (spacecraft, spacecraft.Engines.First());
        }

        private static ImpulseManeuver CreateAt(string maneuver, IO.Astrodynamics.OrbitalParameters.OrbitalParameters targetOrbit, Engine engine) =>
            maneuver switch
            {
                nameof(ApsidalAlignmentManeuver) => new ApsidalAlignmentManeuver(PlaneChangeStart, TimeSpan.Zero, targetOrbit, engine),
                nameof(PlaneAlignmentManeuver) => new PlaneAlignmentManeuver(PlaneChangeStart, TimeSpan.Zero, targetOrbit, engine),
                nameof(PhasingManeuver) => new PhasingManeuver(PlaneChangeStart, TimeSpan.Zero, targetOrbit, 2, engine),
                _ => throw new ArgumentOutOfRangeException(nameof(maneuver))
            };

        /// <summary>
        /// A target orbit in another frame than the frame the spacecraft is propagated in is rejected when the propagation
        /// is built, on every spacecraft (#685): built before its tank is on the spacecraft, or before the spacecraft gets
        /// another initial orbit, the maneuver escaped the check of its constructor, and the propagation read the
        /// components of the target as if they were in the integration frame (2807.185 m/s for a plane change of 2°,
        /// whose ΔV is 263.394 m/s). Leading or chained, both integrators; nothing runs and the previous propagation is
        /// left as it was.
        /// </summary>
        [Theory]
        [InlineData(nameof(PlaneAlignmentManeuver), "tank after", false, "VV")]
        [InlineData(nameof(PlaneAlignmentManeuver), "tank after", true, "RK78")]
        [InlineData(nameof(PlaneAlignmentManeuver), "initial orbit after", false, "RK78")]
        [InlineData(nameof(PlaneAlignmentManeuver), "initial orbit after", true, "VV")]
        [InlineData(nameof(ApsidalAlignmentManeuver), "tank after", false, "RK78")]
        [InlineData(nameof(ApsidalAlignmentManeuver), "initial orbit after", true, "VV")]
        [InlineData(nameof(PhasingManeuver), "tank after", true, "VV")]
        [InlineData(nameof(PhasingManeuver), "initial orbit after", false, "RK78")]
        public void TargetOrbitInAnotherFrame_IsRejectedWhenThePropagationIsBuilt(string maneuver, string order, bool chained, string integrator)
        {
            // Arrange: the spacecraft in ICRF, the target in ECLIPJ2000
            var earth = TestHelpers.EarthAtJ2000;
            var orbit = CircularOrbit(earth, 30.0);
            var targetEcl = TargetOf(maneuver, earth).ToFrame(Frames.Frame.ECLIPTIC_J2000);
            Spacecraft spacecraft;
            ImpulseManeuver rejected;
            bool previous = order == "initial orbit after";
            if (previous)
            {
                // In ECLIPJ2000 with the target: accepted, and propagated; then another initial orbit, in ICRF
                Engine engine;
                (spacecraft, engine) = PlaneChangeSpacecraftWithEngine(-6851, orbit.ToFrame(Frames.Frame.ECLIPTIC_J2000));
                rejected = CreateAt(maneuver, targetEcl, engine);
            }
            else
            {
                // Built before its tank is on the spacecraft: no check at construction
                var tank = new FuelTank("ft685", "ftA", "sn6852", 1000.0, 900.0);
                var engine = new Engine("eng685", "engmk1", "sn6852", 450, 50, tank);
                rejected = CreateAt(maneuver, targetEcl, engine);
                spacecraft = PlaneChangeSpacecraft(-6852, orbit);
                spacecraft.AddFuelTank(tank);
                spacecraft.AddEngine(engine);
            }

            ImpulseManeuver leading = rejected;
            if (chained)
            {
                leading = new ApogeeHeightManeuver(earth, PlaneChangeStart, TimeSpan.Zero, 7100000.0, spacecraft.Engines.First());
                leading.SetNextManeuver(rejected);
            }

            spacecraft.SetStandbyManeuver(leading);
            Window? leadingThrust = null, rejectedThrust = null;
            StateVector[] previousStates = null;
            if (previous)
            {
                spacecraft.Propagate(PlaneChangeWindow, [earth], PlaneChangeIntegrator(integrator), false, false, PlaneChangeStep);
                leadingThrust = leading.ThrustWindow;
                rejectedThrust = rejected.ThrustWindow;
                previousStates = spacecraft.PropagatedStates.ToArray();
                spacecraft.SetInitialOrbitalParameters(orbit);
            }

            // Act
            var exception = Assert.Throws<ArgumentException>(() =>
                spacecraft.Propagate(PlaneChangeWindow, [earth], PlaneChangeIntegrator(integrator), false, false, PlaneChangeStep));

            // Assert: the frames and the fix named; nothing ran
            Assert.Equal("spacecraft", exception.ParamName);
            Assert.Contains(maneuver, exception.Message);
            Assert.Contains("ECLIPJ2000", exception.Message);
            Assert.Contains("ToFrame(Frame.ICRF)", exception.Message);
            Assert.Equal(previous, spacecraft.IsPropagated);
            LabelAssert.Equal(leadingThrust, leading.ThrustWindow);
            LabelAssert.Equal(rejectedThrust, rejected.ThrustWindow);
            if (previous)
            {
                LabelAssert.Equal(previousStates, spacecraft.PropagatedStates.ToArray());
            }
            else
            {
                Assert.Null(rejected.ThrustWindow);
                Assert.Null(rejected.ManeuverWindow);
                Assert.Empty(spacecraft.PropagatedStates);
            }
        }

        /// <summary>
        /// A target orbit in another frame than the propagation's, on a maneuver attached after the propagator is built,
        /// is rejected when the propagator propagates (#685, second review): <see cref="Propagator.PropagatorBase.Propagate"/>
        /// reads the chain of the spacecraft again, and the propagation read the components of the ECLIPJ2000 target as if
        /// they were in ICRF (2807.185 m/s for a plane change of 2°, whose ΔV is 263.394 m/s). The state of the spacecraft,
        /// not an argument, so an <see cref="InvalidOperationException"/>, as for its engines (#676). Public constructor and
        /// builder, both integrators; nothing runs.
        /// </summary>
        [Theory]
        [InlineData("constructor", "VV")]
        [InlineData("constructor", "RK78")]
        [InlineData("builder", "VV")]
        [InlineData("builder", "RK78")]
        public void TargetOrbitInAnotherFrame_AttachedAfterThePropagatorIsBuilt_IsRejectedWhenItPropagates(string path, string integrator)
        {
            // Arrange: the propagator of a spacecraft in ICRF without maneuver, then a plane change on an ECLIPJ2000 target,
            // built before its tank is on the spacecraft (no check at construction)
            var earth = TestHelpers.EarthAtJ2000;
            var spacecraft = PlaneChangeSpacecraft(path == "builder" ? -6855 : -6856, CircularOrbit(earth, 30.0));
            using var propagator = path == "builder"
                ? new Propagator.CentralBodyPropagatorBuilder(PlaneChangeWindow, spacecraft, PlaneChangeIntegrator(integrator), PlaneChangeStep)
                    .WithPerturbingBody(earth).Build()
                : new Propagator.CentralBodyPropagator(PlaneChangeWindow, spacecraft, PlaneChangeIntegrator(integrator), [earth], false, false,
                    PlaneChangeStep);
            var tank = new FuelTank("ft685", "ftA", "sn6855", 1000.0, 900.0);
            var engine = new Engine("eng685", "engmk1", "sn6855", 450, 50, tank);
            var rejected = CreateAt(nameof(PlaneAlignmentManeuver), CircularOrbit(earth, 32.0).ToFrame(Frames.Frame.ECLIPTIC_J2000), engine);
            spacecraft.AddFuelTank(tank);
            spacecraft.AddEngine(engine);
            spacecraft.SetStandbyManeuver(rejected);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => propagator.Propagate());

            // Assert: the frames and the fix named; nothing ran
            Assert.Contains(nameof(PlaneAlignmentManeuver), exception.Message);
            Assert.Contains("ECLIPJ2000", exception.Message);
            Assert.Contains("ToFrame(Frame.ICRF)", exception.Message);
            Assert.Null(rejected.ThrustWindow);
            Assert.Null(rejected.ManeuverWindow);
            Assert.Equal(900.0, tank.Quantity);
        }

        // An orbit of 7000 km in the frame given, 1 rad past its ascending node and its periapsis, circular as CircularOrbit
        // by default
        private static KeplerianElements CircularOrbitIn(IO.Astrodynamics.Body.CelestialBody earth, double inclinationDeg, Frames.Frame frame,
            double eccentricity = 0.0) =>
            new(7000000.0, eccentricity, inclinationDeg * IO.Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, 1.0, earth, PlaneChangeStart, frame);

        // The plane change of 2° from the orbit at 30° in initialFrame to the orbit at 32° in targetFrame, the maneuver
        // leading, over 2 h, or chained after an apogee raise, over 6 h, attached in the order given: "tank before" (the constructor checks the
        // frame of the target), "tank after" (the propagator, when it is built), or after a propagator built by its
        // "constructor" or its "builder" (the propagator, when it propagates). Returns the maneuver and the final state
        private (PlaneAlignmentManeuver Maneuver, StateVector Final) RunPlaneChange(int id, Frames.Frame initialFrame, Frames.Frame targetFrame,
            string order, bool chained, string integrator)
        {
            var earth = TestHelpers.EarthAtJ2000;
            // Chained, the orbit has an eccentricity of 1e-3, so that the apogee raise fires at its periapsis (RK7(8) keeps
            // r·v of a circular orbit at its rounding, which it never crosses), and the plane change at a later node
            var orbit = CircularOrbitIn(earth, 30.0, initialFrame, chained ? 1.0e-3 : 0.0);
            var target = CircularOrbitIn(earth, 32.0, targetFrame);
            var window = chained ? new Window(PlaneChangeStart, PlaneChangeStart.AddHours(6.0)) : PlaneChangeWindow;
            Spacecraft spacecraft;
            PlaneAlignmentManeuver maneuver;
            Propagator.CentralBodyPropagator propagator = null;
            if (order == "tank before")
            {
                Engine engine;
                (spacecraft, engine) = PlaneChangeSpacecraftWithEngine(id, orbit);
                maneuver = new PlaneAlignmentManeuver(PlaneChangeStart, TimeSpan.Zero, target, engine);
            }
            else
            {
                var tank = new FuelTank("ft685", "ftA", $"sn{-id}", 1000.0, 900.0);
                var engine = new Engine("eng685", "engmk1", $"sn{-id}", 450, 50, tank);
                maneuver = new PlaneAlignmentManeuver(PlaneChangeStart, TimeSpan.Zero, target, engine);
                spacecraft = PlaneChangeSpacecraft(id, orbit);
                if (order != "tank after")
                {
                    propagator = order == "builder"
                        ? new Propagator.CentralBodyPropagatorBuilder(window, spacecraft, PlaneChangeIntegrator(integrator), PlaneChangeStep)
                            .WithPerturbingBody(earth).Build()
                        : new Propagator.CentralBodyPropagator(window, spacecraft, PlaneChangeIntegrator(integrator), [earth], false, false,
                            PlaneChangeStep);
                }

                spacecraft.AddFuelTank(tank);
                spacecraft.AddEngine(engine);
            }

            ImpulseManeuver leading = maneuver;
            if (chained)
            {
                leading = new ApogeeHeightManeuver(earth, PlaneChangeStart, TimeSpan.Zero, 7100000.0, spacecraft.Engines.First());
                leading.SetNextManeuver(maneuver);
            }

            spacecraft.SetStandbyManeuver(leading);
            if (propagator != null)
            {
                using (propagator)
                {
                    var solution = propagator.Propagate();
                    return (maneuver, solution.StateVectors[^1]);
                }
            }

            spacecraft.Propagate(window, [earth], PlaneChangeIntegrator(integrator), false, false, PlaneChangeStep);
            return (maneuver, spacecraft.PropagatedStates.Last());
        }

        // The run with a target in another frame than the propagation's, which share their axes, burns bit for bit the run
        // whose target is in the frame of the propagation, and, leading and in RK7(8), the closed form 2 v sin(Δi/2)
        private void AssertSameBurn((PlaneAlignmentManeuver Maneuver, StateVector Final) run, (PlaneAlignmentManeuver Maneuver, StateVector Final) twin,
            bool chained, string integrator)
        {
            Assert.NotNull(run.Maneuver.ThrustWindow);
            LabelAssert.Equal(twin.Maneuver.ThrustWindow, run.Maneuver.ThrustWindow);
            Assert.Equal(twin.Maneuver.DeltaV, run.Maneuver.DeltaV);
            Assert.Equal(twin.Final.Position, run.Final.Position);
            Assert.Equal(twin.Final.Velocity, run.Final.Velocity);
            if (!chained && integrator == "RK78")
            {
                double expected = 2.0 * System.Math.Sqrt(TestHelpers.EarthAtJ2000.GM / 7000000.0) * System.Math.Sin(1.0 * IO.Astrodynamics.Constants.Deg2Rad);
                double deltaVError = System.Math.Abs(run.Maneuver.DeltaV.Magnitude() - expected);
                _output.WriteLine($"MEASURE plane change, target in GCRF or ICRF on the other: |dV| - closed form {deltaVError:E3} m/s");
                Assert.True(deltaVError < PlaneChangeDeltaVThreshold, $"|dV| - closed form {deltaVError:E3} m/s");
            }
        }

        /// <summary>
        /// A target orbit in GCRF on a spacecraft propagated in ICRF is taken as it is (third review of #685): the library's
        /// GCRF has the axes of ICRF (<see cref="Frames.GcrfFrame"/>, the identity rotation), so its components are those the
        /// integration needs. The comparison by name rejected it, in the constructor of the maneuver ("must be defined in
        /// the same frame"), when the propagator was built and when it propagated, while the relabel it asked for,
        /// <c>targetOrbit.ToFrame(Frame.ICRF)</c>, changes no number. Every order of attachment, leading or chained, both
        /// integrators: bit for bit the run with the target in ICRF, and, leading in RK7(8), the closed form of the plane
        /// change 2 v sin(Δi/2) with the GM of the Earth.
        /// </summary>
        [Theory]
        [InlineData("tank before", false, "VV")]
        [InlineData("tank before", false, "RK78")]
        [InlineData("tank before", true, "RK78")]
        [InlineData("tank after", false, "RK78")]
        [InlineData("tank after", true, "VV")]
        [InlineData("constructor", false, "RK78")]
        [InlineData("constructor", true, "VV")]
        [InlineData("builder", false, "VV")]
        [InlineData("builder", true, "RK78")]
        public void TargetOrbitInGcrf_OnAnIcrfSpacecraft_BurnsAsInIcrf(string order, bool chained, string integrator)
        {
            // Act
            var gcrf = RunPlaneChange(-6857, Frames.Frame.ICRF, Frames.Frame.GCRF, order, chained, integrator);
            var icrf = RunPlaneChange(-6858, Frames.Frame.ICRF, Frames.Frame.ICRF, order, chained, integrator);

            // Assert
            AssertSameBurn(gcrf, icrf, chained, integrator);
        }

        /// <summary>
        /// The reverse pair (third review of #685): a target orbit in ICRF (J2000, or a frame built by that name) on a
        /// spacecraft whose initial orbit is in GCRF, propagated in GCRF, burns bit for bit the run whose target is in
        /// GCRF, and leading in RK7(8) the closed form of the plane change. Only the pair that shares its axes is
        /// accepted: a target in ECLIPJ2000 is still rejected on it.
        /// </summary>
        [Theory]
        [InlineData("tank before", "VV")]
        [InlineData("tank before", "RK78")]
        [InlineData("tank after", "VV")]
        [InlineData("tank after", "RK78")]
        public void TargetOrbitInIcrf_OnAGcrfSpacecraft_IsAccepted(string order, string integrator)
        {
            // Act
            var icrf = RunPlaneChange(-6859, Frames.Frame.GCRF, new Frames.Frame("j2000"), order, false, integrator);
            var gcrf = RunPlaneChange(-6860, Frames.Frame.GCRF, Frames.Frame.GCRF, order, false, integrator);

            // Assert
            AssertSameBurn(icrf, gcrf, false, integrator);
            var exception = Record.Exception(() => RunPlaneChange(-6861, Frames.Frame.GCRF, Frames.Frame.ECLIPTIC_J2000, order, false, integrator));
            Assert.IsType<ArgumentException>(exception);
        }

        /// <summary>
        /// On a spacecraft whose initial orbit is a TLE or an osculating TEME state, propagated in ICRF (#675), a target
        /// orbit in GCRF, which has the axes of ICRF, is accepted as one in ICRF (third review of #685); on a set of mean
        /// elements in TEME, which is not propagated in ICRF, it is rejected, as an ICRF target is. A TLE spacecraft burns
        /// with a GCRF target bit for bit what it burns with the same target in ICRF; a TEME target is still rejected when
        /// the propagation is built.
        /// </summary>
        [Theory]
        [InlineData(nameof(ApsidalAlignmentManeuver))]
        [InlineData(nameof(PlaneAlignmentManeuver))]
        [InlineData(nameof(PhasingManeuver))]
        public void TargetOrbitInGcrf_OnATleSpacecraft_IsAccepted(string maneuver)
        {
            var tle = new IO.Astrodynamics.OrbitalParameters.TLE.TLE("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
                "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
            var sgp4 = tle.ToOsculating();
            var orbits = new IO.Astrodynamics.OrbitalParameters.OrbitalParameters[]
            {
                tle, new StateVector(sgp4.Position, sgp4.Velocity, tle.Observer, sgp4.Epoch, Frames.Frame.TEME), tle.ToMeanKeplerianElements()
            };
            for (int k = 0; k < orbits.Length; k++)
            {
                var spc = new Spacecraft(-1563 - k, $"Impulse{1563 + k}", 1000.0, 3000.0, new Clock($"Clk{1563 + k}", 65536), orbits[k]);
                spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
                spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));
                var engine = spc.Engines.First();
                if (orbits[k] is KeplerianElements { ElementsType: OrbitalElementsType.Mean })
                {
                    Assert.Throws<ArgumentException>(() => Create(maneuver, Target(Frames.Frame.GCRF), engine));
                }
                else
                {
                    Assert.Equal(Frames.Frame.GCRF, Create(maneuver, Target(Frames.Frame.GCRF), engine).TargetOrbit.Frame);
                }
            }

            // A plane change of about 1° on the TLE spacecraft, the target in GCRF and in ICRF
            var start = tle.Epoch.ToTDB();
            var window = new Window(start, start.AddHours(2.0));
            (PlaneAlignmentManeuver, StateVector) Run(int id, Frames.Frame frame)
            {
                var spacecraft = new Spacecraft(id, $"Impulse{-id}", 1000.0, 3000.0, new Clock($"Clk{-id}", 65536), tle);
                spacecraft.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
                spacecraft.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spacecraft.FuelTanks.First()));
                var target = new KeplerianElements(6800000.0, 0.0, 52.6423 * IO.Astrodynamics.Constants.Deg2Rad, 353.0312 * IO.Astrodynamics.Constants.Deg2Rad,
                    0.0, 0.0, tle.Observer, start, frame);
                var plane = new PlaneAlignmentManeuver(start, TimeSpan.Zero, target, spacecraft.Engines.First());
                spacecraft.SetStandbyManeuver(plane);
                var solution = spacecraft.Propagate(window, Array.Empty<IO.Astrodynamics.Body.CelestialItem>(), new RK78Integrator(), false, false,
                    TimeSpan.FromSeconds(60.0));
                return (plane, solution.StateVectors[^1]);
            }

            var (gcrfManeuver, gcrfEnd) = Run(-1566, Frames.Frame.GCRF);
            var (icrfManeuver, icrfEnd) = Run(-1567, Frames.Frame.ICRF);
            Assert.NotNull(gcrfManeuver.ThrustWindow);
            LabelAssert.Equal(icrfManeuver.ThrustWindow, gcrfManeuver.ThrustWindow);
            Assert.Equal(icrfManeuver.DeltaV, gcrfManeuver.DeltaV);
            Assert.Equal(icrfEnd.Position, gcrfEnd.Position);
            Assert.Equal(icrfEnd.Velocity, gcrfEnd.Velocity);
        }

        /// <summary>
        /// The same target given in the frame of the propagation burns the closed form of the plane change, 2 v sin(Δi/2)
        /// with the GM of the Earth (#685): the ECLIPJ2000 target rotated to ICRF on a spacecraft in ICRF, and a spacecraft
        /// and a target both in ECLIPJ2000, the burn not depending on the frame. RK7(8), whose integration resolves the closed
        /// form, unlike Velocity-Verlet at 10 s (1.2e-2 m/s off after half an orbit).
        /// </summary>
        [Theory]
        [InlineData("ICRF")]
        [InlineData("ECLIPJ2000")]
        public void TargetOrbitInThePropagationFrame_BurnsTheClosedForm(string frame)
        {
            // Arrange
            var earth = TestHelpers.EarthAtJ2000;
            var orbit = CircularOrbit(earth, 30.0);
            var targetIcrf = CircularOrbit(earth, 32.0);
            var targetEcl = targetIcrf.ToFrame(Frames.Frame.ECLIPTIC_J2000);
            var (spacecraft, engine) = frame == "ICRF"
                ? PlaneChangeSpacecraftWithEngine(-6853, orbit)
                : PlaneChangeSpacecraftWithEngine(-6854, orbit.ToFrame(Frames.Frame.ECLIPTIC_J2000));
            var target = frame == "ICRF" ? targetEcl.ToFrame(Frames.Frame.ICRF) : targetEcl;
            var maneuver = new PlaneAlignmentManeuver(PlaneChangeStart, TimeSpan.Zero, target, engine);
            spacecraft.SetStandbyManeuver(maneuver);

            // Act
            spacecraft.Propagate(PlaneChangeWindow, [earth], new RK78Integrator(), false, false, PlaneChangeStep);

            // Assert: the closed form, and the final plane that of the target
            double expected = 2.0 * System.Math.Sqrt(earth.GM / 7000000.0) * System.Math.Sin(1.0 * IO.Astrodynamics.Constants.Deg2Rad);
            double deltaVError = System.Math.Abs(maneuver.DeltaV.Magnitude() - expected);
            var final = spacecraft.PropagatedStates.Last().ToFrame(Frames.Frame.ICRF).ToStateVector();
            var h = final.SpecificAngularMomentum();
            var hTarget = targetIcrf.ToStateVector().SpecificAngularMomentum();
            double angle = System.Math.Atan2(h.Cross(hTarget).Magnitude(), h * hTarget);
            _output.WriteLine($"MEASURE plane change in {frame}: |dV| - closed form {deltaVError:E3} m/s (|dV| {maneuver.DeltaV.Magnitude():R}, " +
                              $"closed form {expected:R}), final angle(h, h target) {angle * IO.Astrodynamics.Constants.Rad2Deg:E3} deg, burn at {maneuver.ThrustWindow}");
            Assert.True(deltaVError < PlaneChangeDeltaVThreshold, $"|dV| - closed form {deltaVError:E3} m/s");
            Assert.True(angle < PlaneChangeFinalAngleThreshold, $"final angle {angle * IO.Astrodynamics.Constants.Rad2Deg:E3} deg");
        }
    }
}
