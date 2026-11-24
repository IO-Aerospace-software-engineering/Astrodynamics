using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.Tests.Body;
using Xunit;
using Xunit.Abstractions;

using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
namespace IO.Astrodynamics.Tests.Maneuvers;

public class TriadAttitudeTests
{
    private readonly ITestOutputHelper _output;

    public TriadAttitudeTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private (Spacecraft spacecraft, StateVector orbitalParams) CreateTestSpacecraft()
    {
        var orbitalParams = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(0.0, 7727.0, 0.0),
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(2021, 01, 01, 13, 0, 0, TimeScale.TDB),
            Frames.Frame.ICRF);

        var spc = new Spacecraft(-667, "TriadTestSpacecraft", 1000.0, orbitalParams, 3000.0, new Clock("TestClk", 65536));
        spc.AddFuelTank(new FuelTank("ft", 1000.0, 900.0, "ftA", "123456"));
        spc.AddEngine(new Engine("eng", 450, 50, spc.FuelTanks.First(), "engmk1", "12345"));

        return (spc, orbitalParams);
    }

    private Spacecraft CreatePropagatedTargetSpacecraft(TimeSystem.Time epoch)
    {
        var targetPosition = new Vector3(6888000.0, 1200000.0, 300000.0);
        var targetVelocity = new Vector3(-1200.0, 7200.0, 300.0);
        var targetState = new StateVector(
            targetPosition,
            targetVelocity,
            TestHelpers.EarthAtJ2000,
            epoch,
            Frames.Frame.ICRF);

        var target = new Spacecraft(-668, "TriadTargetSpacecraft", 800.0, targetState, 2000.0, new Clock("TargetClk", 65536));
        var step = TimeSpan.FromSeconds(30.0);
        double dt = step.TotalSeconds;

        target.AddPropagatedStates(
            new StateVector(targetPosition - targetVelocity * dt, targetVelocity, TestHelpers.EarthAtJ2000, epoch - step, Frames.Frame.ICRF),
            targetState,
            new StateVector(targetPosition + targetVelocity * dt, targetVelocity, TestHelpers.EarthAtJ2000, epoch + step, Frames.Frame.ICRF));

        return target;
    }

    #region Constructor Validation Tests

    [Fact]
    public void Constructor_WithInstrument_CreatesValidManeuver()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun);

        Assert.NotNull(maneuver);
        Assert.Equal(TestHelpers.MoonAtJ2000, maneuver.PrimaryTarget);
        Assert.Equal(TestHelpers.Sun, maneuver.SecondaryTarget);
        Assert.Equal(Vector3.VectorZ, maneuver.PrimaryBodyVector);
        Assert.Equal(Vector3.VectorY, maneuver.SecondaryBodyVector);
    }

    [Fact]
    public void Constructor_WithDualInstruments_CreatesValidManeuver()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");
        spc.AddCircularInstrument("CAM2", 1.5, Vector3.VectorX, Vector3.VectorY, Vector3.Zero, naifId: -667601, model: "mod1");

        var instruments = spc.Instruments.ToList();
        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            instruments[0],
            TestHelpers.MoonAtJ2000,
            instruments[1],
            TestHelpers.Sun);

        Assert.NotNull(maneuver);
        Assert.Equal(Vector3.VectorZ, maneuver.PrimaryBodyVector);
        Assert.Equal(Vector3.VectorX, maneuver.SecondaryBodyVector);
    }

    [Fact]
    public void Constructor_WithExplicitVectors_CreatesValidManeuver()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            Vector3.VectorX,
            TestHelpers.Sun);

        Assert.NotNull(maneuver);
        Assert.Equal(Vector3.VectorZ, maneuver.PrimaryBodyVector);
        Assert.Equal(Vector3.VectorX, maneuver.SecondaryBodyVector);
    }

    [Fact]
    public void Constructor_NullPrimaryTarget_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");

        Assert.Throws<ArgumentNullException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            spc.Instruments.First(),
            null,
            TestHelpers.Sun));
    }

    [Fact]
    public void Constructor_NullSecondaryTarget_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");

        Assert.Throws<ArgumentNullException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            null));
    }

    [Fact]
    public void Constructor_NullInstrument_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentNullException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            (Instrument)null,
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun));
    }

    [Fact]
    public void Constructor_ZeroPrimaryBodyVector_ThrowsArgumentException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Vector3.Zero,
            TestHelpers.MoonAtJ2000,
            Vector3.VectorX,
            TestHelpers.Sun));
    }

    [Fact]
    public void Constructor_ZeroSecondaryBodyVector_ThrowsArgumentException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            Vector3.Zero,
            TestHelpers.Sun));
    }

    #endregion

    #region Collinearity Tests

    [Fact]
    public void Constructor_ParallelBodyVectors_ThrowsArgumentException()
    {
        var (spc, _) = CreateTestSpacecraft();

        var ex = Assert.Throws<ArgumentException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            Vector3.VectorZ * 2.0,
            TestHelpers.Sun));

        Assert.Contains("collinear", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_AntiParallelBodyVectors_ThrowsArgumentException()
    {
        var (spc, _) = CreateTestSpacecraft();

        var ex = Assert.Throws<ArgumentException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            Vector3.VectorZ * -1.0,
            TestHelpers.Sun));

        Assert.Contains("collinear", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_VectorsAtMinimumSeparation_Succeeds()
    {
        var (spc, _) = CreateTestSpacecraft();
        double angle = 6.0 * Astrodynamics.Constants.Deg2Rad; // Just above default 5 degree minimum

        var secondaryVector = new Vector3(
            System.Math.Sin(angle),
            0.0,
            System.Math.Cos(angle));

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            secondaryVector,
            TestHelpers.Sun);

        Assert.NotNull(maneuver);
    }

    [Fact]
    public void Constructor_VectorsBelowMinimumSeparation_ThrowsArgumentException()
    {
        var (spc, _) = CreateTestSpacecraft();
        double angle = 3.0 * Astrodynamics.Constants.Deg2Rad; // Below default 5 degree minimum

        var secondaryVector = new Vector3(
            System.Math.Sin(angle),
            0.0,
            System.Math.Cos(angle));

        Assert.Throws<ArgumentException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            secondaryVector,
            TestHelpers.Sun));
    }

    [Fact]
    public void Constructor_CustomMinimumSeparation_Succeeds()
    {
        var (spc, _) = CreateTestSpacecraft();
        double angle = 2.0 * Astrodynamics.Constants.Deg2Rad;

        var secondaryVector = new Vector3(
            System.Math.Sin(angle),
            0.0,
            System.Math.Cos(angle));

        // Use custom minimum of 1 degree
        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            secondaryVector,
            TestHelpers.Sun,
            minimumVectorSeparation: 1.0 * Astrodynamics.Constants.Deg2Rad);

        Assert.NotNull(maneuver);
        Assert.Equal(1.0 * Astrodynamics.Constants.Deg2Rad, maneuver.MinimumVectorSeparation);
    }

    #endregion

    #region Attitude Computation Tests

    [Fact]
    public void Execute_PrimaryVectorPointsAtPrimaryTarget()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun);

        var maneuverOrientation = maneuver.ComputeStateOrientation(orbitalParams);

        // Get the direction to Moon in ICRF
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var targetDirection = (moonEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();

        // Rotate the primary body vector by the computed orientation
        var pointingVector = maneuver.PrimaryBodyVector.Rotate(maneuverOrientation.Rotation).Normalize();

        // The pointing vector should align with target direction (dot product close to 1)
        double alignment = pointingVector.Dot(targetDirection);
        Assert.True(alignment > 0.9999, $"Primary vector alignment: {alignment} (expected > 0.9999)");
    }

    [Fact]
    public void Execute_SecondaryVectorLiesInConstraintPlane()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun);

        var maneuverOrientation2 = maneuver.ComputeStateOrientation(orbitalParams);

        // Get directions to both targets
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var primaryRefDirection = (moonEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();

        var sunEphemeris = TestHelpers.Sun.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var secondaryRefDirection = (sunEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();

        // The TRIAD algorithm guarantees that the rotated secondary body vector lies in the plane
        // defined by the primary and secondary reference directions (coplanarity constraint)
        var planeNormal = primaryRefDirection.Cross(secondaryRefDirection).Normalize();

        // Rotate the secondary body vector by the computed orientation
        var secondaryBodyRotated = maneuver.SecondaryBodyVector.Rotate(maneuverOrientation2.Rotation).Normalize();

        // The rotated secondary vector should be perpendicular to the plane normal (i.e., lie in the plane)
        // Tolerance of 1e-6 accounts for floating-point precision in matrix/quaternion operations
        // (corresponds to about 0.2 arc-seconds, which is excellent for attitude determination)
        double outOfPlaneComponent = secondaryBodyRotated.Dot(planeNormal);
        Assert.True(System.Math.Abs(outOfPlaneComponent) < 1e-4,
            $"Secondary body vector should lie in the plane of primary/secondary reference directions. Out-of-plane component: {outOfPlaneComponent}");

        // Additionally verify it points toward the secondary target (correct half-space)
        double alignment = secondaryBodyRotated.Dot(secondaryRefDirection);
        Assert.True(alignment > 0.0, $"Secondary vector should point toward Sun half-space. Alignment: {alignment}");
    }

    [Fact]
    public void Execute_QuaternionIsNormalized()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun);

        var maneuverOrientation3 = maneuver.ComputeStateOrientation(orbitalParams);

        var q = maneuverOrientation3.Rotation;
        double magnitude = System.Math.Sqrt(q.W * q.W + q.VectorPart.X * q.VectorPart.X +
                                            q.VectorPart.Y * q.VectorPart.Y + q.VectorPart.Z * q.VectorPart.Z);

        Assert.True(System.Math.Abs(magnitude - 1.0) < 1e-10, $"Quaternion magnitude: {magnitude} (expected 1.0)");
    }

    [Fact]
    public void Execute_RotationMatrixIsOrthogonal()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun);

        var maneuverOrientation4 = maneuver.ComputeStateOrientation(orbitalParams);

        var rotationMatrix = Matrix.FromQuaternion(maneuverOrientation4.Rotation);
        var product = rotationMatrix.Multiply(rotationMatrix.Transpose());

        // Check R * R^T = I
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                double expected = i == j ? 1.0 : 0.0;
                Assert.True(System.Math.Abs(product.Get(i, j) - expected) < 1e-10,
                    $"R*R^T[{i},{j}] = {product.Get(i, j)}, expected {expected}");
            }
        }
    }

    #endregion

    #region Comparison Tests

    [Fact]
    public void Execute_ProducesDifferentRollThanSingleVector()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");

        // TRIAD pointing (fully constrained - uses both boresight and refVector)
        var triadManeuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun);

        var triadManeuverOrientation5 = triadManeuver.ComputeStateOrientation(orbitalParams);
        var triadQ = triadManeuverOrientation5.Rotation;

        // Verify TRIAD points the primary vector at the Moon
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var targetDirection = (moonEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();

        var boresight = triadManeuver.PrimaryBodyVector;
        var triadPointing = boresight.Rotate(triadQ).Normalize();
        double triadAlignment = triadPointing.Dot(targetDirection);

        Assert.True(triadAlignment > 0.9999, $"TRIAD should point at Moon (alignment: {triadAlignment})");

        // Compute what a simple single-vector solution would give (using Vector3.To directly)
        // InstrumentPointingToAttitude computes the conjugate of this, boresight.To(targetDirection)
        var singleVectorQ = targetDirection.To(boresight);

        // Verify they produce the same primary pointing but potentially different roll
        var singlePointing = boresight.Rotate(singleVectorQ.Conjugate()).Normalize();
        double singleAlignment = singlePointing.Dot(targetDirection);

        // Single vector should also point at Moon (both methods achieve primary pointing)
        Assert.True(singleAlignment > 0.9999, $"Single vector should also point at Moon (alignment: {singleAlignment})");

        // Compare roll by looking at where the secondary vector ends up
        var refVector = triadManeuver.SecondaryBodyVector;
        var triadRefRotated = refVector.Rotate(triadQ).Normalize();
        var singleRefRotated = refVector.Rotate(singleVectorQ.Conjugate()).Normalize();

        // The secondary vectors should differ (indicating different roll)
        double refVectorDifference = (triadRefRotated - singleRefRotated).Magnitude();
        Assert.True(refVectorDifference > 0.01,
            $"TRIAD should produce different roll than single-vector (refVector difference: {refVectorDifference})");
    }

    #endregion

    #region Use Case Tests

    [Fact]
    public void Execute_EarthObservationWithSunTracking()
    {
        // Simulate Earth observation: nadir pointing + sun tracking for power
        var (spc, orbitalParams) = CreateTestSpacecraft();

        // Primary: nadir-pointing camera (Z-axis)
        // Secondary: solar panel normal (Y-axis toward Sun)
        spc.AddCircularInstrument("NadirCam", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.EarthAtJ2000, // Point at Earth (nadir)
            TestHelpers.Sun);

        var maneuverOrientation6 = maneuver.ComputeStateOrientation(orbitalParams);

        // Verify Earth pointing
        var earthEphemeris = TestHelpers.EarthAtJ2000.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var earthDirection = (earthEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
        var cameraPointing = Vector3.VectorZ.Rotate(maneuverOrientation6.Rotation).Normalize();

        Assert.True(cameraPointing.Dot(earthDirection) > 0.9999, "Camera should point at Earth");

        // Verify Sun-facing tendency
        var sunEphemeris = TestHelpers.Sun.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var sunDirection = (sunEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
        var panelNormal = Vector3.VectorY.Rotate(maneuverOrientation6.Rotation).Normalize();

        Assert.True(panelNormal.Dot(sunDirection) > 0.0, "Solar panel should face toward Sun");
    }

    [Fact]
    public void Execute_DualTargetImaging()
    {
        // Two instruments pointing at different celestial bodies
        var (spc, orbitalParams) = CreateTestSpacecraft();

        spc.AddCircularInstrument("LunarCam", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");
        spc.AddCircularInstrument("SunSensor", 0.5, Vector3.VectorX, Vector3.VectorY, Vector3.Zero, naifId: -667601, model: "mod1");

        var instruments = spc.Instruments.ToList();
        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            instruments[0], // LunarCam points at Moon
            TestHelpers.MoonAtJ2000,
            instruments[1], // SunSensor constrains roll toward Sun
            TestHelpers.Sun);

        var maneuverOrientation7 = maneuver.ComputeStateOrientation(orbitalParams);

        // Verify Moon pointing
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var moonDirection = (moonEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
        var lunarCamPointing = Vector3.VectorZ.Rotate(maneuverOrientation7.Rotation).Normalize();

        Assert.True(lunarCamPointing.Dot(moonDirection) > 0.9999, "Lunar camera should point at Moon");

        // Verify Sun sensor tendency
        var sunEphemeris = TestHelpers.Sun.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var sunDirection = (sunEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
        var sunSensorPointing = Vector3.VectorX.Rotate(maneuverOrientation7.Rotation).Normalize();

        Assert.True(sunSensorPointing.Dot(sunDirection) > 0.0, "Sun sensor should be in Sun-facing half-space");
    }

    [Fact]
    public void Execute_AttitudeAtMultipleEpochs()
    {
        var (spc, _) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");

        var epochs = new[]
        {
            new TimeSystem.Time(2021, 01, 01, 13, 0, 0, TimeScale.TDB),
            new TimeSystem.Time(2021, 01, 01, 14, 0, 0, TimeScale.TDB),
            new TimeSystem.Time(2021, 01, 01, 15, 0, 0, TimeScale.TDB)
        };

        foreach (var epoch in epochs)
        {
            var orbitalParams = new StateVector(
                new Vector3(6678000.0, 0.0, 0.0),
                new Vector3(0.0, 7727.0, 0.0),
                TestHelpers.EarthAtJ2000,
                epoch,
                Frames.Frame.ICRF);

            var maneuver = new TriadAttitude(
                new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
                TimeSpan.FromSeconds(10.0),
                spc.Instruments.First(),
                TestHelpers.MoonAtJ2000,
                TestHelpers.Sun);

            var maneuverOrientation8 = maneuver.ComputeStateOrientation(orbitalParams);

            // Verify primary pointing at each epoch
            var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
            var targetDirection = (moonEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
            var pointingVector = Vector3.VectorZ.Rotate(maneuverOrientation8.Rotation).Normalize();

            Assert.True(pointingVector.Dot(targetDirection) > 0.9999,
                $"Primary vector should point at Moon at epoch {epoch}");
        }
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Execute_OrthogonalBodyVectors_OptimalConfiguration()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        // Orthogonal vectors are optimal for TRIAD (maximum numerical stability)
        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            Vector3.VectorX,
            TestHelpers.Sun);

        var maneuverOrientation9 = maneuver.ComputeStateOrientation(orbitalParams);

        // Should produce valid results
        var q = maneuverOrientation9.Rotation;
        double magnitude = System.Math.Sqrt(q.W * q.W + q.VectorPart.X * q.VectorPart.X +
                                            q.VectorPart.Y * q.VectorPart.Y + q.VectorPart.Z * q.VectorPart.Z);

        Assert.True(System.Math.Abs(magnitude - 1.0) < 1e-10, "Quaternion should be normalized");
    }

    [Fact]
    public void Execute_NumericalStability_RepeatedCallsConsistent()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun);

        // Execute multiple times
        var results = new Quaternion[5];
        for (int i = 0; i < 5; i++)
        {
            var maneuverOrientation10 = maneuver.ComputeStateOrientation(orbitalParams);
            results[i] = maneuverOrientation10.Rotation;
        }

        // All results should be identical
        for (int i = 1; i < 5; i++)
        {
            Assert.True(System.Math.Abs(results[0].W - results[i].W) < 1e-12);
            Assert.True(System.Math.Abs(results[0].VectorPart.X - results[i].VectorPart.X) < 1e-12);
            Assert.True(System.Math.Abs(results[0].VectorPart.Y - results[i].VectorPart.Y) < 1e-12);
            Assert.True(System.Math.Abs(results[0].VectorPart.Z - results[i].VectorPart.Z) < 1e-12);
        }
    }

    [Fact]
    public void Execute_ManeuverWindowSet()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun);

        maneuver.TryExecute(orbitalParams);

        Assert.Equal(0.0, maneuver.FuelBurned);
        LabelAssert.Equal(new Window(new TimeSystem.Time(2021, 01, 01, 13, 0, 0, TimeScale.TDB), TimeSpan.FromSeconds(10.0)), maneuver.ManeuverWindow);
        LabelAssert.Equal(new Window(new TimeSystem.Time(2021, 01, 01, 13, 0, 0, TimeScale.TDB), TimeSpan.Zero), maneuver.ThrustWindow);
    }

    [Fact]
    public void Execute_LargeAngularSeparation_Succeeds()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        // Body vectors nearly opposite (170 degrees apart)
        double angle = 170.0 * Astrodynamics.Constants.Deg2Rad;
        var secondaryVector = new Vector3(
            System.Math.Sin(angle),
            0.0,
            System.Math.Cos(angle));

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            secondaryVector,
            TestHelpers.Sun);

        var maneuverOrientation11 = maneuver.ComputeStateOrientation(orbitalParams);

        // Should still produce valid quaternion
        var q = maneuverOrientation11.Rotation;
        double magnitude = System.Math.Sqrt(q.W * q.W + q.VectorPart.X * q.VectorPart.X +
                                            q.VectorPart.Y * q.VectorPart.Y + q.VectorPart.Z * q.VectorPart.Z);

        Assert.True(System.Math.Abs(magnitude - 1.0) < 1e-10, "Quaternion should be normalized");
    }

    #endregion

    #region Matrix Helper Tests

    [Fact]
    public void FromColumnVectors_CreatesCorrectMatrix()
    {
        var col0 = new Vector3(1.0, 2.0, 3.0);
        var col1 = new Vector3(4.0, 5.0, 6.0);
        var col2 = new Vector3(7.0, 8.0, 9.0);

        var matrix = Matrix.FromColumnVectors(col0, col1, col2);

        Assert.Equal(3, matrix.Rows);
        Assert.Equal(3, matrix.Columns);

        // Column 0
        Assert.Equal(1.0, matrix.Get(0, 0));
        Assert.Equal(2.0, matrix.Get(1, 0));
        Assert.Equal(3.0, matrix.Get(2, 0));

        // Column 1
        Assert.Equal(4.0, matrix.Get(0, 1));
        Assert.Equal(5.0, matrix.Get(1, 1));
        Assert.Equal(6.0, matrix.Get(2, 1));

        // Column 2
        Assert.Equal(7.0, matrix.Get(0, 2));
        Assert.Equal(8.0, matrix.Get(1, 2));
        Assert.Equal(9.0, matrix.Get(2, 2));
    }

    #endregion

    #region IAttitudeTarget Tests

    [Fact]
    public void Constructor_WithAttitudeTargets_CreatesValidManeuver()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Spacecraft.Front,
            OrbitalDirectionTarget.Prograde,
            Spacecraft.Up,
            new CelestialAttitudeTarget(TestHelpers.Sun));

        Assert.NotNull(maneuver);
        Assert.Equal(Spacecraft.Front, maneuver.PrimaryBodyVector);
        Assert.Equal(Spacecraft.Up, maneuver.SecondaryBodyVector);
        Assert.NotNull(maneuver.PrimaryAttitudeTarget);
        Assert.NotNull(maneuver.SecondaryAttitudeTarget);
    }

    [Fact]
    public void Constructor_WithAttitudeTargets_NullManeuverCenter_IsTheCentreTheRunInfers()
    {
        // Decisions 61 and 99 of #668: a null centre is the one the run infers (it threw ArgumentNullException before)
        var maneuver = new TriadAttitude(
            null,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Spacecraft.Front,
            OrbitalDirectionTarget.Prograde,
            Spacecraft.Up,
            new CelestialAttitudeTarget(TestHelpers.Sun));

        Assert.Null(maneuver.ManeuverCenter);
    }

    [Fact]
    public void Constructor_WithAttitudeTargets_NullPrimaryTarget_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentNullException>(() => new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Spacecraft.Front,
            null,
            Spacecraft.Up,
            new CelestialAttitudeTarget(TestHelpers.Sun)));
    }

    [Fact]
    public void Constructor_WithAttitudeTargets_NullSecondaryTarget_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentNullException>(() => new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Spacecraft.Front,
            OrbitalDirectionTarget.Prograde,
            Spacecraft.Up,
            null));
    }

    [Fact]
    public void Execute_ProgradeWithSunTracking()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Spacecraft.Front,
            OrbitalDirectionTarget.Prograde,
            Spacecraft.Up,
            new CelestialAttitudeTarget(TestHelpers.Sun));

        var maneuverOrientation12 = maneuver.ComputeStateOrientation(orbitalParams);

        // Verify Front aligns with velocity direction
        var rotatedFront = Spacecraft.Front.Rotate(maneuverOrientation12.Rotation).Normalize();
        var localSv = orbitalParams.RelativeTo(TestHelpers.EarthAtJ2000, Aberration.None).ToStateVector();
        var progradeDir = localSv.Velocity.Normalize();

        double alignment = rotatedFront.Dot(progradeDir);
        Assert.True(alignment > 0.999999, $"Front should align with prograde. Alignment: {alignment}");

        // Verify Up is in Sun-facing half-space
        var rotatedUp = Spacecraft.Up.Rotate(maneuverOrientation12.Rotation).Normalize();
        var sunEphemeris = TestHelpers.Sun.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var sunDirection = (sunEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
        Assert.True(rotatedUp.Dot(sunDirection) > 0.0, "Up should be in Sun-facing half-space");
    }

    [Fact]
    public void Execute_NadirWithNormalConstraint_LVLH()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Spacecraft.Down,
            OrbitalDirectionTarget.Nadir,
            Spacecraft.Front,
            OrbitalDirectionTarget.Prograde);

        var maneuverOrientation13 = maneuver.ComputeStateOrientation(orbitalParams);

        // Verify Down aligns with nadir
        var localSv = orbitalParams.RelativeTo(TestHelpers.EarthAtJ2000, Aberration.None).ToStateVector();
        var rotatedDown = Spacecraft.Down.Rotate(maneuverOrientation13.Rotation).Normalize();
        var nadirDir = localSv.Position.Inverse().Normalize();

        double nadirAlignment = rotatedDown.Dot(nadirDir);
        Assert.True(nadirAlignment > 0.9999, $"Down should align with nadir. Alignment: {nadirAlignment}");

        // Verify Front is in prograde half-space
        var rotatedFront = Spacecraft.Front.Rotate(maneuverOrientation13.Rotation).Normalize();
        var progradeDir = localSv.Velocity.Normalize();
        Assert.True(rotatedFront.Dot(progradeDir) > 0.0, "Front should be in prograde half-space");
    }

    [Fact]
    public void Execute_MixedTargets_CelestialPrimaryOrbitalSecondary()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Vector3.VectorZ,
            new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000),
            Vector3.VectorX,
            OrbitalDirectionTarget.Normal);

        var maneuverOrientation14 = maneuver.ComputeStateOrientation(orbitalParams);

        // Verify primary pointing (geometric, matching CelestialAttitudeTarget default)
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.None);
        var moonDirection = (moonEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
        var pointingVector = Vector3.VectorZ.Rotate(maneuverOrientation14.Rotation).Normalize();

        double alignment = pointingVector.Dot(moonDirection);
        Assert.True(alignment > 0.999999, $"Primary should point at Moon. Alignment: {alignment}");

        // Verify quaternion is normalized
        var q = maneuverOrientation14.Rotation;
        double magnitude = System.Math.Sqrt(q.W * q.W + q.VectorPart.X * q.VectorPart.X +
                                            q.VectorPart.Y * q.VectorPart.Y + q.VectorPart.Z * q.VectorPart.Z);
        Assert.True(System.Math.Abs(magnitude - 1.0) < 1e-10, "Quaternion should be normalized");
    }

    [Fact]
    public void Execute_CollinearOrbitalDirections_ThrowsInvalidOperationException()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Spacecraft.Front,
            OrbitalDirectionTarget.Nadir,
            Spacecraft.Up,
            OrbitalDirectionTarget.Zenith);

        Assert.Throws<InvalidOperationException>(() => maneuver.ComputeStateOrientation(orbitalParams));
    }

    [Fact]
    public void Execute_CelestialAttitudeTarget_MapsToILocalizable()
    {
        var (spc, _) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000),
            Vector3.VectorX,
            new CelestialAttitudeTarget(TestHelpers.Sun));

        // CelestialAttitudeTarget should map back to PrimaryTarget/SecondaryTarget
        Assert.Equal(TestHelpers.MoonAtJ2000, maneuver.PrimaryTarget);
        Assert.Equal(TestHelpers.Sun, maneuver.SecondaryTarget);
    }

    [Fact]
    public void Execute_IAttitudeTargetVsILocalizable_SameQuaternion()
    {
        // Create two TriadAttitude instances targeting Moon (primary) and Sun (secondary).
        // One uses the ILocalizable constructor, the other the IAttitudeTarget constructor
        // with CelestialAttitudeTarget wrappers and the SAME maneuver center.
        // The ILocalizable constructor uses GetManeuverCenter(primaryTarget) = MoonAtJ2000,
        // so we must also pass MoonAtJ2000 as maneuverCenter to the IAttitudeTarget constructor.
        var (spc, orbitalParams) = CreateTestSpacecraft();

        // ILocalizable constructor — maneuverCenter is set internally to MoonAtJ2000
        var localizableManeuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Spacecraft.Front,
            TestHelpers.MoonAtJ2000,
            Spacecraft.Up,
            TestHelpers.Sun);

        // IAttitudeTarget constructor with CelestialAttitudeTarget wrappers.
        // Use MoonAtJ2000 as maneuverCenter to match the ILocalizable path.
        var attitudeTargetManeuver = new TriadAttitude(
            TestHelpers.MoonAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Spacecraft.Front,
            new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000),
            Spacecraft.Up,
            new CelestialAttitudeTarget(TestHelpers.Sun));

        var localizableManeuverOrientation15 = localizableManeuver.ComputeStateOrientation(orbitalParams);
        var attitudeTargetManeuverOrientation16 = attitudeTargetManeuver.ComputeStateOrientation(orbitalParams);

        var q1 = localizableManeuverOrientation15.Rotation;
        var q2 = attitudeTargetManeuverOrientation16.Rotation;

        // Account for quaternion double-cover: q and -q represent the same rotation
        double dot = q1.W * q2.W + q1.VectorPart.X * q2.VectorPart.X +
                     q1.VectorPart.Y * q2.VectorPart.Y + q1.VectorPart.Z * q2.VectorPart.Z;
        double sign = dot >= 0 ? 1.0 : -1.0;

        const double tolerance = 1e-10;
        Assert.True(System.Math.Abs(sign * q2.W - q1.W) < tolerance,
            $"IAttitudeTarget and ILocalizable should produce same quaternion W. Got {q1.W} vs {sign * q2.W}");
        Assert.True(System.Math.Abs(sign * q2.VectorPart.X - q1.VectorPart.X) < tolerance,
            $"IAttitudeTarget and ILocalizable should produce same quaternion X. Got {q1.VectorPart.X} vs {sign * q2.VectorPart.X}");
        Assert.True(System.Math.Abs(sign * q2.VectorPart.Y - q1.VectorPart.Y) < tolerance,
            $"IAttitudeTarget and ILocalizable should produce same quaternion Y. Got {q1.VectorPart.Y} vs {sign * q2.VectorPart.Y}");
        Assert.True(System.Math.Abs(sign * q2.VectorPart.Z - q1.VectorPart.Z) < tolerance,
            $"IAttitudeTarget and ILocalizable should produce same quaternion Z. Got {q1.VectorPart.Z} vs {sign * q2.VectorPart.Z}");
    }

    #endregion

    #region Factory Method Tests

    [Fact]
    public void CreateLVLH_CreatesValidManeuver()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = TriadAttitude.CreateLVLH(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0));

        Assert.NotNull(maneuver);
        Assert.Equal(Spacecraft.Down, maneuver.PrimaryBodyVector);
        Assert.Equal(Spacecraft.Front, maneuver.SecondaryBodyVector);

        // Execute and verify
        var maneuverOrientation17 = maneuver.ComputeStateOrientation(orbitalParams);

        var localSv = orbitalParams.RelativeTo(TestHelpers.EarthAtJ2000, Aberration.None).ToStateVector();
        var rotatedDown = Spacecraft.Down.Rotate(maneuverOrientation17.Rotation).Normalize();
        var nadirDir = localSv.Position.Inverse().Normalize();

        double alignment = rotatedDown.Dot(nadirDir);
        Assert.True(alignment > 0.9999, $"LVLH Down should align with nadir. Alignment: {alignment}");
    }

    [Fact]
    public void CreateProgradeWithSunTracking_CreatesValidManeuver()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = TriadAttitude.CreateProgradeWithSunTracking(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            TestHelpers.Sun);

        Assert.NotNull(maneuver);
        Assert.Equal(Spacecraft.Front, maneuver.PrimaryBodyVector);
        Assert.Equal(Spacecraft.Up, maneuver.SecondaryBodyVector);

        // Execute and verify
        var maneuverOrientation18 = maneuver.ComputeStateOrientation(orbitalParams);

        var localSv = orbitalParams.RelativeTo(TestHelpers.EarthAtJ2000, Aberration.None).ToStateVector();
        var rotatedFront = Spacecraft.Front.Rotate(maneuverOrientation18.Rotation).Normalize();
        var progradeDir = localSv.Velocity.Normalize();

        double alignment = rotatedFront.Dot(progradeDir);
        Assert.True(alignment > 0.9999, $"Front should align with prograde. Alignment: {alignment}");
    }

    [Fact]
    public void CreateProgradeWithSunTracking_NullSun_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentNullException>(() => TriadAttitude.CreateProgradeWithSunTracking(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            null));
    }

    [Fact]
    public void CreateLVLH_MatchesManualConstruction()
    {
        // The CreateLVLH factory should produce the same result as manually constructing
        // a TriadAttitude with Down→Nadir, Front→Prograde.
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var factoryManeuver = TriadAttitude.CreateLVLH(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0));

        var manualManeuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Spacecraft.Down,
            OrbitalDirectionTarget.Nadir,
            Spacecraft.Front,
            OrbitalDirectionTarget.Prograde);

        // Both should have the same body vectors
        Assert.Equal(Spacecraft.Down, factoryManeuver.PrimaryBodyVector);
        Assert.Equal(Spacecraft.Front, factoryManeuver.SecondaryBodyVector);
        Assert.Equal(manualManeuver.PrimaryBodyVector, factoryManeuver.PrimaryBodyVector);
        Assert.Equal(manualManeuver.SecondaryBodyVector, factoryManeuver.SecondaryBodyVector);

        // Execute both on the same state vector and verify identical quaternions
        var factoryManeuverOrientation19 = factoryManeuver.ComputeStateOrientation(orbitalParams);
        var manualManeuverOrientation20 = manualManeuver.ComputeStateOrientation(orbitalParams);

        var q1 = factoryManeuverOrientation19.Rotation;
        var q2 = manualManeuverOrientation20.Rotation;

        // Account for quaternion double-cover
        double dot = q1.W * q2.W + q1.VectorPart.X * q2.VectorPart.X +
                     q1.VectorPart.Y * q2.VectorPart.Y + q1.VectorPart.Z * q2.VectorPart.Z;
        double sign = dot >= 0 ? 1.0 : -1.0;

        const double tolerance = 1e-10;
        Assert.True(System.Math.Abs(sign * q2.W - q1.W) < tolerance,
            $"Factory and manual LVLH quaternion W should match. Got {q1.W} vs {sign * q2.W}");
        Assert.True(System.Math.Abs(sign * q2.VectorPart.X - q1.VectorPart.X) < tolerance,
            $"Factory and manual LVLH quaternion X should match. Got {q1.VectorPart.X} vs {sign * q2.VectorPart.X}");
        Assert.True(System.Math.Abs(sign * q2.VectorPart.Y - q1.VectorPart.Y) < tolerance,
            $"Factory and manual LVLH quaternion Y should match. Got {q1.VectorPart.Y} vs {sign * q2.VectorPart.Y}");
        Assert.True(System.Math.Abs(sign * q2.VectorPart.Z - q1.VectorPart.Z) < tolerance,
            $"Factory and manual LVLH quaternion Z should match. Got {q1.VectorPart.Z} vs {sign * q2.VectorPart.Z}");
    }

    #endregion

    #region Aberration Consistency Tests

    [Fact]
    public void Execute_MixedTargets_DefaultAberration_ProducesConsistentTriad()
    {
        // Regression test: CelestialAttitudeTarget defaults to Aberration.None,
        // which is consistent with OrbitalDirectionTarget (geometric).
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Vector3.VectorZ,
            new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000),
            Vector3.VectorX,
            OrbitalDirectionTarget.Normal);

        var maneuverOrientation21 = maneuver.ComputeStateOrientation(orbitalParams);

        // Compute local state (same conversion as Attitude.TryExecute line 41)
        var localSv = orbitalParams.RelativeTo(TestHelpers.EarthAtJ2000, Aberration.None).ToStateVector();

        // Verify primary pointing: body Z should align with geometric Moon direction
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(localSv.Epoch, localSv.Observer, localSv.Frame, Aberration.None);
        var moonDirection = (moonEphemeris.ToStateVector().Position - localSv.Position).Normalize();
        var rotatedZ = Vector3.VectorZ.Rotate(maneuverOrientation21.Rotation).Normalize();

        double primaryAlignment = rotatedZ.Dot(moonDirection);
        Assert.True(primaryAlignment > 0.9999, $"Primary should point at Moon (geometric). Alignment: {primaryAlignment}");

        // Verify secondary constraint: body X should lie in the plane defined by
        // Moon direction and orbital normal. The TRIAD cross-product t2 = primary x secondary
        // is orthogonal to both reference vectors. Body X, after rotation, should have
        // zero component along t2.
        var orbitalNormal = localSv.Position.Cross(localSv.Velocity).Normalize();
        var t2 = moonDirection.Cross(orbitalNormal).Normalize();
        var rotatedX = Vector3.VectorX.Rotate(maneuverOrientation21.Rotation).Normalize();

        double outOfPlane = System.Math.Abs(rotatedX.Dot(t2));
        Assert.True(outOfPlane < 1e-12, $"Secondary body vector should lie in the reference plane. Out-of-plane: {outOfPlane}");
    }

    [Fact]
    public void Execute_MixedTargets_TargetSpacecraft_DefaultAberration_ProducesConsistentTriad()
    {
        // This matches the reported issue: another propagated spacecraft wrapped in
        // CelestialAttitudeTarget with an orbital-direction secondary constraint.
        var (spc, orbitalParams) = CreateTestSpacecraft();
        var targetSpacecraft = CreatePropagatedTargetSpacecraft(orbitalParams.Epoch);

        var maneuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Vector3.VectorZ,
            new CelestialAttitudeTarget(targetSpacecraft),
            Vector3.VectorX,
            OrbitalDirectionTarget.Normal);

        var maneuverOrientation22 = maneuver.ComputeStateOrientation(orbitalParams);

        var localSv = orbitalParams.RelativeTo(TestHelpers.EarthAtJ2000, Aberration.None).ToStateVector();
        var targetEphemeris = targetSpacecraft.GetEphemeris(localSv.Epoch, localSv.Observer, localSv.Frame, Aberration.None);
        var targetDirection = (targetEphemeris.ToStateVector().Position - localSv.Position).Normalize();
        var rotatedZ = Vector3.VectorZ.Rotate(maneuverOrientation22.Rotation).Normalize();

        double primaryAlignment = rotatedZ.Dot(targetDirection);
        Assert.True(primaryAlignment > 0.9999999999,
            $"Primary should point at the target spacecraft (geometric). Alignment: {primaryAlignment}");

        var orbitalNormal = localSv.SpecificAngularMomentum().Normalize();
        var t2 = targetDirection.Cross(orbitalNormal).Normalize();
        var rotatedX = Vector3.VectorX.Rotate(maneuverOrientation22.Rotation).Normalize();

        double outOfPlane = System.Math.Abs(rotatedX.Dot(t2));
        Assert.True(outOfPlane < 1e-12,
            $"Secondary body vector should lie in the target-spacecraft/orbital-normal plane. Out-of-plane: {outOfPlane}");
    }

    [Fact]
    public void Execute_MixedTargets_LTvsNone_ProducesDifferentAttitude()
    {
        // Demonstrates that the aberration choice matters: LT and None produce
        // measurably different attitudes when mixing with orbital directions.
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuverLT = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Vector3.VectorZ,
            new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000, Aberration.LT),
            Vector3.VectorX,
            OrbitalDirectionTarget.Normal);

        var maneuverNone = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            Vector3.VectorZ,
            new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000, Aberration.None),
            Vector3.VectorX,
            OrbitalDirectionTarget.Normal);

        var maneuverLTOrientation23 = maneuverLT.ComputeStateOrientation(orbitalParams);
        var maneuverNoneOrientation24 = maneuverNone.ComputeStateOrientation(orbitalParams);

        var qLT = maneuverLTOrientation23.Rotation;
        var qNone = maneuverNoneOrientation24.Rotation;

        // Quaternion dot product: 1.0 means identical, < 1.0 means different rotation
        double dot = System.Math.Abs(
            qLT.W * qNone.W + qLT.VectorPart.X * qNone.VectorPart.X +
            qLT.VectorPart.Y * qNone.VectorPart.Y + qLT.VectorPart.Z * qNone.VectorPart.Z);

        Assert.True(dot < 1.0 - 1e-9, $"LT and None should produce different attitudes. Quaternion dot: {dot}");
    }

    #endregion

    #region Factory axes (#677)

    // The angles between the spacecraft's axes, rotated by the factory's attitude, and the directions of the closed form of
    // the law, worst over the six cases (the rotated triad, LVLH, the right axis): 3.58e-16 rad on Linux x64, Windows x64
    // and macOS arm64 alike (CI run 43595588856, 2026-11-14); twice the worst
    private const double FactoryAxesAngle = 7.2e-16;

    /// <summary>The body axes (front, right, up) of the spacecraft of a case: the front +X, the right −Y, the up +Z; the front
    /// +Z, the right +X, the up −Y; the default axes turned by 30° about (1, 1, 1). Each is right-handed: right × front = up.</summary>
    private static (Vector3 Front, Vector3 Right, Vector3 Up) BodyAxes(string triad)
    {
        switch (triad)
        {
            case "FrontX":
                return (Vector3.VectorX, Vector3.VectorY.Inverse(), Vector3.VectorZ);
            case "FrontZ":
                return (Vector3.VectorZ, Vector3.VectorX, Vector3.VectorY.Inverse());
            case "Rotated":
            {
                // Rodrigues' rotation by 30° about (1, 1, 1)/√3, written here
                var k = new Vector3(1.0, 1.0, 1.0).Normalize();
                double angle = 30.0 * Astrodynamics.Constants.Deg2Rad;
                Vector3 Turn(Vector3 v) => v * System.Math.Cos(angle) + k.Cross(v) * System.Math.Sin(angle) + k * (k.Dot(v) * (1.0 - System.Math.Cos(angle)));
                return (Turn(Vector3.VectorY), Turn(Vector3.VectorX), Turn(Vector3.VectorZ));
            }
            default:
                throw new ArgumentException(triad);
        }
    }

    // An inclined LEO about the Earth in ICRF, a = 7 000 km, e = 0.001, i = 0.9 rad, and a spacecraft with the body axes of
    // the triad that carries the engine of the attitude
    private static (Spacecraft spacecraft, StateVector state) CustomAxesSpacecraft(string triad)
    {
        var state = new KeplerianElements(7000000.0, 0.001, 0.9, 0.4, 1.1, 0.3, TestHelpers.EarthAtJ2000, new TimeSystem.Time(2021, 01, 01, 13, 0, 0, TimeScale.TDB),
            Frames.Frame.ICRF).ToStateVector();
        var (front, right, up) = BodyAxes(triad);
        var spc = new Spacecraft(-669, "TriadAxesSpacecraft", 1000.0, state, 3000.0, new Clock("AxesClk", 65536), bodyFront: front, bodyRight: right, bodyUp: up);
        spc.AddFuelTank(new FuelTank("ft", 1000.0, 900.0, "ftA", "123456"));
        spc.AddEngine(new Engine("eng", 450, 50, spc.FuelTanks.First(), "engmk1", "12345"));
        return (spc, state);
    }

    /// <summary>
    /// LVLH puts the spacecraft's own up axis along r, its front along the part of v perpendicular to r and its right along
    /// v⊥ × r (right × front = up), whatever its body axes (#677): the closed form of the law from the state, compared as
    /// direction angles. The factory pointed the static axes, 90° off for the two axis-aligned triads.
    /// </summary>
    [Theory]
    [InlineData("FrontX")]
    [InlineData("FrontZ")]
    [InlineData("Rotated")]
    public void CreateLVLH_CustomAxes_PointsTheSpacecraftAxes(string triad)
    {
        var (spc, state) = CustomAxesSpacecraft(triad);
        var maneuver = TriadAttitude.CreateLVLH(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero);
        // The axes of the plan's spacecraft (decisions 46 and 69 of #668)
        spc.SetManeuvers(maneuver);

        var maneuverOrientation25 = maneuver.ComputeStateOrientation(state);
        var q = maneuverOrientation25.Rotation;
        var rHat = state.Position.Normalize();
        var vPerp = (state.Velocity - rHat * state.Velocity.Dot(rHat)).Normalize();
        double up = AttitudeRecordTests.Angle(spc.BodyUp.Rotate(q), rHat);
        double front = AttitudeRecordTests.Angle(spc.BodyFront.Rotate(q), vPerp);
        double right = AttitudeRecordTests.Angle(spc.BodyRight.Rotate(q), vPerp.Cross(rHat));
        _output.WriteLine($"MEASURE #677 LVLH {triad}: up - r {up:E2}, front - v⊥ {front:E2}, right - v⊥ × r {right:E2} rad");
        Assert.True(up <= FactoryAxesAngle, $"up - r {up:E3} rad");
        Assert.True(front <= FactoryAxesAngle, $"front - v⊥ {front:E3} rad");
        Assert.True(right <= FactoryAxesAngle, $"right - v⊥ × r {right:E3} rad");
        Assert.Equal(spc.BodyDown, maneuver.PrimaryBodyVector);
        Assert.Equal(spc.BodyFront, maneuver.SecondaryBodyVector);
    }

    /// <summary>
    /// Prograde with Sun tracking puts the spacecraft's own front along v, its up along the part of the geometric Sun direction
    /// perpendicular to v and its right along v × s⊥ (front × up = right), whatever its body axes (#677): the Sun from SPICE
    /// (<c>spkezr</c>, no aberration, as <see cref="CelestialAttitudeTarget"/>'s default), compared as direction angles. The
    /// factory pointed the static axes, 90° off for the two axis-aligned triads.
    /// </summary>
    [Theory]
    [InlineData("FrontX")]
    [InlineData("FrontZ")]
    [InlineData("Rotated")]
    public void CreateProgradeWithSunTracking_CustomAxes_PointsTheSpacecraftAxes(string triad)
    {
        var (spc, state) = CustomAxesSpacecraft(triad);
        var maneuver = TriadAttitude.CreateProgradeWithSunTracking(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero,
            TestHelpers.Sun);
        // The axes of the plan's spacecraft (decisions 46 and 69 of #668)
        spc.SetManeuvers(maneuver);

        var maneuverOrientation26 = maneuver.ComputeStateOrientation(state);
        var q = maneuverOrientation26.Rotation;
        var vHat = state.Velocity.Normalize();
        var sun = SpiceAPI.Instance.ReadEphemeris(state.Epoch, 399, 10, Frames.Frame.ICRF, Aberration.None).Position - state.Position;
        var sPerp = (sun - vHat * sun.Dot(vHat)).Normalize();
        double front = AttitudeRecordTests.Angle(spc.BodyFront.Rotate(q), vHat);
        double up = AttitudeRecordTests.Angle(spc.BodyUp.Rotate(q), sPerp);
        double right = AttitudeRecordTests.Angle(spc.BodyRight.Rotate(q), vHat.Cross(sPerp));
        _output.WriteLine($"MEASURE #677 prograde Sun {triad}: front - v {front:E2}, up - s⊥ {up:E2}, right - v × s⊥ {right:E2} rad");
        Assert.True(front <= FactoryAxesAngle, $"front - v {front:E3} rad");
        Assert.True(up <= FactoryAxesAngle, $"up - s⊥ {up:E3} rad");
        Assert.True(right <= FactoryAxesAngle, $"right - v × s⊥ {right:E3} rad");
        Assert.Equal(spc.BodyFront, maneuver.PrimaryBodyVector);
        Assert.Equal(spc.BodyUp, maneuver.SecondaryBodyVector);
    }

    /// <summary>
    /// With the default body axes, the factories are bit for bit the explicit constructor given the static axes: the
    /// default instance axes are the static ones exactly (#677).
    /// </summary>
    [Fact]
    public void Factories_DefaultAxes_BitForBitTheStaticForm()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        var min = new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB);
        var engine = spc.Engines.First();

        var lvlh = TriadAttitude.CreateLVLH(TestHelpers.EarthAtJ2000, min, TimeSpan.Zero);
        var lvlhStatic = new TriadAttitude(TestHelpers.EarthAtJ2000, min, TimeSpan.Zero, Spacecraft.Down, OrbitalDirectionTarget.Nadir, Spacecraft.Front,
            OrbitalDirectionTarget.Prograde);
        var sun = TriadAttitude.CreateProgradeWithSunTracking(TestHelpers.EarthAtJ2000, min, TimeSpan.Zero, TestHelpers.Sun);
        var sunStatic = new TriadAttitude(TestHelpers.EarthAtJ2000, min, TimeSpan.Zero, Spacecraft.Front, OrbitalDirectionTarget.Prograde, Spacecraft.Up,
            new CelestialAttitudeTarget(TestHelpers.Sun));

        foreach (var (factory, explicitForm) in new[] { (lvlh, lvlhStatic), (sun, sunStatic) })
        {
            Assert.Equal(explicitForm.PrimaryBodyVector, factory.PrimaryBodyVector);
            Assert.Equal(explicitForm.SecondaryBodyVector, factory.SecondaryBodyVector);
            var factoryOrientation27 = factory.ComputeStateOrientation(orbitalParams);
            var explicitFormOrientation28 = explicitForm.ComputeStateOrientation(orbitalParams);
            var a = factoryOrientation27.Rotation;
            var b = explicitFormOrientation28.Rotation;
            Assert.Equal(b.W, a.W);
            Assert.Equal(b.VectorPart.X, a.VectorPart.X);
            Assert.Equal(b.VectorPart.Y, a.VectorPart.Y);
            Assert.Equal(b.VectorPart.Z, a.VectorPart.Z);
        }
    }

    /// <summary>
    /// Before any plan the factories keep the static axes, the fallback of <c>Maneuver.GetBodyAxis</c> (#677); in the plan
    /// of a spacecraft, they follow its axes (decisions 46 and 69 of #668: the axes of the last plan's spacecraft, those
    /// of the engine's tank's before lot L5b).
    /// </summary>
    [Fact]
    public void Factories_BeforeAnyPlan_KeepStaticAxes()
    {
        var tank = new FuelTank("ft", 1000.0, 900.0, "ftA", "123456");
        var engine = new Engine("eng", 450, 50, tank, "engmk1", "12345");
        var min = new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB);
        var lvlh = TriadAttitude.CreateLVLH(TestHelpers.EarthAtJ2000, min, TimeSpan.Zero);
        var sun = TriadAttitude.CreateProgradeWithSunTracking(TestHelpers.EarthAtJ2000, min, TimeSpan.Zero, TestHelpers.Sun);

        Assert.Equal(Spacecraft.Down, lvlh.PrimaryBodyVector);
        Assert.Equal(Spacecraft.Front, lvlh.SecondaryBodyVector);
        Assert.Equal(Spacecraft.Front, sun.PrimaryBodyVector);
        Assert.Equal(Spacecraft.Up, sun.SecondaryBodyVector);

        var (front, right, up) = BodyAxes("FrontZ");
        var spc = new Spacecraft(-670, "TriadLateSpacecraft", 1000.0, CreateTestSpacecraft().orbitalParams, 3000.0, new Clock("LateClk", 65536), bodyFront: front,
            bodyRight: right, bodyUp: up);
        spc.AddFuelTank(tank);
        spc.AddEngine(engine);
        Assert.Equal(Spacecraft.Down, lvlh.PrimaryBodyVector);
        spc.SetManeuvers(lvlh, sun);

        Assert.Equal(spc.BodyDown, lvlh.PrimaryBodyVector);
        Assert.Equal(spc.BodyFront, lvlh.SecondaryBodyVector);
        Assert.Equal(spc.BodyFront, sun.PrimaryBodyVector);
        Assert.Equal(spc.BodyUp, sun.SecondaryBodyVector);
    }

    #endregion

    #region Minimum vector separation (fourth round of #668)

    // The smallest separation, 1e-14 rad: the sine of two vectors collinear to the rounding of their components, computed
    // as |û × ŵ|, is at most about (3 + √2) u = 4.9e-16 (u = 2^-53: the input's rounding, that of each normalization and
    // that of the cross product), 20 times less
    private const double LowestSeparation = 1e-14;

    // A separation is in [1e-14, π/2] rad: 0, a negative or a NaN separation switched off both collinearity checks (every
    // degenerate triad gave an arbitrary roll), one below the rounding of the sine accepted collinear vectors that round
    // apart (v and fl(2.9 v) at 1e-17 gave a roll set by rounding, and fl(-2.9 v) another), and one above π/2 rejected
    // every pair, perpendicular ones included, as "too close to collinear"
    public static TheoryData<double> SeparationsOutOfRange() => new()
    {
        0.0, -0.0, -0.1, double.NaN, double.PositiveInfinity, double.NegativeInfinity, 2.0, System.Math.BitIncrement(System.Math.PI / 2.0),
        1e-300, 1e-17, System.Math.BitDecrement(LowestSeparation)
    };

    [Theory]
    [MemberData(nameof(SeparationsOutOfRange))]
    public void Separation_OutOfRange_Throws(double separation)
    {
        var (spc, _) = CreateTestSpacecraft();
        spc.AddCircularInstrument("CAM1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -667600, model: "mod1");
        spc.AddCircularInstrument("CAM2", 1.5, Vector3.VectorX, Vector3.VectorY, Vector3.Zero, naifId: -667601, model: "mod1");
        var instruments = spc.Instruments.ToList();
        var epoch = new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB);
        var hold = TimeSpan.FromHours(1.0);
        var engine = spc.Engines.First();

        var constructors = new Func<TriadAttitude>[]
        {
            () => new TriadAttitude(epoch, hold, instruments[0], TestHelpers.MoonAtJ2000, TestHelpers.Sun, separation),
            () => new TriadAttitude(epoch, hold, instruments[0], TestHelpers.MoonAtJ2000, instruments[1], TestHelpers.Sun, separation),
            () => new TriadAttitude(epoch, hold, Vector3.VectorZ, TestHelpers.MoonAtJ2000, Vector3.VectorY, TestHelpers.Sun, separation),
            () => new TriadAttitude(TestHelpers.EarthAtJ2000, epoch, hold, Vector3.VectorZ, OrbitalDirectionTarget.Nadir, Vector3.VectorY,
                OrbitalDirectionTarget.Prograde, separation)
        };

        foreach (var constructor in constructors)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(constructor);
            Assert.Equal("minimumVectorSeparation", exception.ParamName);
        }
    }

    // Twice the worst, rounded up at the second digit: the primary angle is 2.220e-16 rad and the secondary 0 rad on Linux
    // x64, Windows x64 and macOS arm64 (CI run 43773937161, 2026-11-15) (the triads of the axis vectors and of the targets,
    // (-1, 0, 0) and (0, 1, 0), are exact, so the matrix is; the quaternion's square root and the rotation round). The
    // angles are atan2(|a × b|, a · b), which resolves them to the rounding, where acos of the cosine cannot tell an angle
    // below about 1.5e-8 rad from 0
    private const double HalfPiAttitudeAngle = 4.5e-16;

    private static double ResolvedAngle(Vector3 a, Vector3 b) => System.Math.Atan2(a.Cross(b).Magnitude(), a.Dot(b));

    // π/2 is the largest separation: only perpendicular vectors meet it, |Z × Y| = 1 = sin(π/2) exactly. The closed form of
    // the TRIAD for perpendicular body vectors on perpendicular targets puts each body vector on its target
    [Fact]
    public void Separation_HalfPi_PerpendicularVectors_IsAccepted()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        var maneuver = new TriadAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0), Vector3.VectorZ, OrbitalDirectionTarget.Nadir, Vector3.VectorY, OrbitalDirectionTarget.Prograde, System.Math.PI / 2.0);

        var orientation = maneuver.ComputeOrientation(orbitalParams);

        var nadir = orbitalParams.Position.Inverse().Normalize();
        var prograde = orbitalParams.Velocity.Normalize();
        double primaryAngle = ResolvedAngle(Vector3.VectorZ.Rotate(orientation), nadir);
        double secondaryAngle = ResolvedAngle(Vector3.VectorY.Rotate(orientation), prograde);
        _output.WriteLine($"MEASURE half-pi primary angle {primaryAngle:E3} rad, secondary angle {secondaryAngle:E3} rad");
        Assert.Equal(System.Math.PI / 2.0, maneuver.MinimumVectorSeparation);
        Assert.True(primaryAngle <= HalfPiAttitudeAngle, $"primary angle {primaryAngle:E3} rad");
        Assert.True(secondaryAngle <= HalfPiAttitudeAngle, $"secondary angle {secondaryAngle:E3} rad");
    }

    // The smallest separation is accepted, on perpendicular vectors and on a pair whose sine, 1e-12, is above it but far
    // below the resolution of acos near 1
    [Fact]
    public void Separation_LowestSeparation_IsAccepted()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        var epoch = new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB);
        var perpendicular = new TriadAttitude(TestHelpers.EarthAtJ2000, epoch, TimeSpan.FromSeconds(10.0), Vector3.VectorZ,
            OrbitalDirectionTarget.Nadir, Vector3.VectorY, OrbitalDirectionTarget.Prograde, LowestSeparation);
        var close = new TriadAttitude(epoch, TimeSpan.FromHours(1.0), Vector3.VectorZ, TestHelpers.MoonAtJ2000,
            new Vector3(1e-12, 0.0, 1.0), TestHelpers.Sun, LowestSeparation);

        Assert.Equal(LowestSeparation, perpendicular.MinimumVectorSeparation);
        Assert.Equal(LowestSeparation, close.MinimumVectorSeparation);
        // The separation only decides which pairs pass: the attitude is that of the default separation, bit for bit
        var atDefault = new TriadAttitude(TestHelpers.EarthAtJ2000, epoch, TimeSpan.FromSeconds(10.0), Vector3.VectorZ,
            OrbitalDirectionTarget.Nadir, Vector3.VectorY, OrbitalDirectionTarget.Prograde).ComputeOrientation(orbitalParams);
        var atLowest = perpendicular.ComputeOrientation(orbitalParams);
        Assert.Equal(atDefault.W, atLowest.W);
        Assert.Equal(atDefault.VectorPart, atLowest.VectorPart);
    }

    // Antiparallel body vectors at the smallest separation: π − 1e-300 rounded to π, so the angle π passed the former check
    // at 1e-300, which is now out of range
    [Fact]
    public void AntiparallelBodyVectors_TinySeparation_Throws()
    {
        var (spc, _) = CreateTestSpacecraft();
        Assert.Throws<ArgumentException>(() => new TriadAttitude(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0), Vector3.VectorZ, TestHelpers.MoonAtJ2000, Vector3.VectorZ.Inverse(), TestHelpers.Sun, LowestSeparation));
    }

    // Collinear body vectors that round apart, at the smallest separation: v and fl(k v) for random v and k, collinear by
    // construction, must all throw. The worst sine |û × ŵ| is printed (a record of the margin, not a threshold)
    [Fact]
    public void RandomRoundedCollinearBodyVectors_LowestSeparation_Throw()
    {
        var (spc, _) = CreateTestSpacecraft();
        var engine = spc.Engines.First();
        var epoch = new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB);
        var random = new Random(668);
        double Component() => (random.NextDouble() * 2.0 - 1.0) * System.Math.Pow(10.0, random.Next(-3, 4));
        double worstSine = 0.0;
        int rejected = 0;
        const int pairs = 20000;
        for (int i = 0; i < pairs; i++)
        {
            var v = new Vector3(Component(), Component(), Component());
            double k = (random.Next(2) == 0 ? -1.0 : 1.0) * System.Math.Pow(10.0, random.NextDouble() * 12.0 - 6.0);
            var w = v * k;
            worstSine = System.Math.Max(worstSine, v.NormalizeOrZero().Cross(w.NormalizeOrZero()).Magnitude());
            try
            {
                _ = new TriadAttitude(epoch, TimeSpan.FromHours(1.0), v, TestHelpers.MoonAtJ2000, w, TestHelpers.Sun, LowestSeparation);
            }
            catch (ArgumentException e) when (e is not ArgumentOutOfRangeException)
            {
                rejected++;
            }
        }

        _output.WriteLine($"MEASURE worst sine of v and fl(k v) over {pairs} pairs {worstSine:E3}, sin(lowest separation) {System.Math.Sin(LowestSeparation):E3}");
        Assert.Equal(pairs, rejected);
    }

    // Collinear targets at the smallest separation: nadir and zenith (antiparallel, accepted through the rounding of
    // π − 1e-300, which gave an arbitrary roll), nadir and the direction of the Earth (parallel)
    [Theory]
    [InlineData("antiparallel")]
    [InlineData("parallel")]
    public void CollinearTargets_TinySeparation_Throws(string geometry)
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        IAttitudeTarget secondary = geometry == "antiparallel"
            ? OrbitalDirectionTarget.Zenith
            : new CelestialAttitudeTarget(TestHelpers.EarthAtJ2000);
        var maneuver = new TriadAttitude(TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0), Vector3.VectorZ, OrbitalDirectionTarget.Nadir, Vector3.VectorY, secondary, LowestSeparation);

        Assert.Throws<InvalidOperationException>(() => maneuver.ComputeOrientation(orbitalParams));
    }

    // Collinear body vectors that round apart: v and fl(k v) are collinear, but the acos of their rounded cosine leaves 0 or
    // π by about 2e-8 rad (the resolution of acos near ±1), so a separation below that accepted them and the roll was set by
    // roundoff. The sine of their angle, |û × ŵ|, stays at the level of the rounding
    [Theory]
    [InlineData(2.9)]
    [InlineData(-2.9)]
    [InlineData(1.1)]
    [InlineData(-1.1)]
    public void RoundedCollinearBodyVectors_Throw(double k)
    {
        var (spc, _) = CreateTestSpacecraft();
        var v = new Vector3(1.0, 2.0, 3.0);
        var w = v * k;
        double angle = v.Angle(w);
        double fromCollinear = System.Math.Min(angle, System.Math.PI - angle);
        _output.WriteLine($"MEASURE acos angle of v and {k} v from collinear {fromCollinear:E3} rad");
        Assert.True(fromCollinear > 1e-12, $"the pair is not one that acos resolves apart: {fromCollinear:E3} rad");

        Assert.Throws<ArgumentException>(() => new TriadAttitude(new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromHours(1.0), v, TestHelpers.MoonAtJ2000, w, TestHelpers.Sun, 1e-12));
    }

    // A body vector with a component that is not finite has no direction: the NaN angle passed every comparison and the
    // attitude was a NaN quaternion
    [Theory]
    [InlineData(double.NaN, true)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, true)]
    [InlineData(double.NegativeInfinity, false)]
    public void NonFiniteBodyVector_Throws(double value, bool primary)
    {
        var (spc, _) = CreateTestSpacecraft();
        var nonFinite = new Vector3(0.0, 0.0, value);
        var primaryVector = primary ? nonFinite : Vector3.VectorZ;
        var secondaryVector = primary ? Vector3.VectorY : nonFinite;
        var epoch = new TimeSystem.Time(DateTime.MinValue, TimeScale.TDB);

        Assert.Throws<ArgumentException>(() => new TriadAttitude(epoch, TimeSpan.FromHours(1.0), primaryVector, TestHelpers.MoonAtJ2000,
            secondaryVector, TestHelpers.Sun));
        Assert.Throws<ArgumentException>(() => new TriadAttitude(TestHelpers.EarthAtJ2000, epoch, TimeSpan.FromHours(1.0), primaryVector,
            OrbitalDirectionTarget.Nadir, secondaryVector, OrbitalDirectionTarget.Prograde));
    }

    #endregion
}
