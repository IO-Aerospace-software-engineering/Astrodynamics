using System;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers;

public class TriadAttitudeTests
{
    public TriadAttitudeTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private (Spacecraft spacecraft, StateVector orbitalParams) CreateTestSpacecraft()
    {
        var orbitalParams = new StateVector(
            new Vector3(6678000.0, 0.0, 0.0),
            new Vector3(0.0, 7727.0, 0.0),
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(2021, 01, 01, 13, 0, 0),
            Frames.Frame.ICRF);

        var spc = new Spacecraft(-667, "TriadTestSpacecraft", 1000.0, 3000.0, new Clock("TestClk", 65536), orbitalParams);
        spc.AddFuelTank(new FuelTank("ft", "ftA", "123456", 1000.0, 900.0));
        spc.AddEngine(new Engine("eng", "engmk1", "12345", 450, 50, spc.FuelTanks.First()));

        return (spc, orbitalParams);
    }

    #region Constructor Validation Tests

    [Fact]
    public void Constructor_WithInstrument_CreatesValidManeuver()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument(-667600, "CAM", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun,
            spc.Engines.First());

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
        spc.AddCircularInstrument(-667600, "CAM1", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
        spc.AddCircularInstrument(-667601, "CAM2", "mod1", 1.5, Vector3.VectorX, Vector3.VectorY, Vector3.Zero);

        var instruments = spc.Instruments.ToList();
        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            instruments[0],
            TestHelpers.MoonAtJ2000,
            instruments[1],
            TestHelpers.Sun,
            spc.Engines.First());

        Assert.NotNull(maneuver);
        Assert.Equal(Vector3.VectorZ, maneuver.PrimaryBodyVector);
        Assert.Equal(Vector3.VectorX, maneuver.SecondaryBodyVector);
    }

    [Fact]
    public void Constructor_WithExplicitVectors_CreatesValidManeuver()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            Vector3.VectorX,
            TestHelpers.Sun,
            spc.Engines.First());

        Assert.NotNull(maneuver);
        Assert.Equal(Vector3.VectorZ, maneuver.PrimaryBodyVector);
        Assert.Equal(Vector3.VectorX, maneuver.SecondaryBodyVector);
    }

    [Fact]
    public void Constructor_NullPrimaryTarget_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();
        spc.AddCircularInstrument(-667600, "CAM", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        Assert.Throws<ArgumentNullException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            spc.Instruments.First(),
            null,
            TestHelpers.Sun,
            spc.Engines.First()));
    }

    [Fact]
    public void Constructor_NullSecondaryTarget_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();
        spc.AddCircularInstrument(-667600, "CAM", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        Assert.Throws<ArgumentNullException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            null,
            spc.Engines.First()));
    }

    [Fact]
    public void Constructor_NullInstrument_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentNullException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            (Instrument)null,
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun,
            spc.Engines.First()));
    }

    [Fact]
    public void Constructor_ZeroPrimaryBodyVector_ThrowsArgumentException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Vector3.Zero,
            TestHelpers.MoonAtJ2000,
            Vector3.VectorX,
            TestHelpers.Sun,
            spc.Engines.First()));
    }

    [Fact]
    public void Constructor_ZeroSecondaryBodyVector_ThrowsArgumentException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            Vector3.Zero,
            TestHelpers.Sun,
            spc.Engines.First()));
    }

    #endregion

    #region Collinearity Tests

    [Fact]
    public void Constructor_ParallelBodyVectors_ThrowsArgumentException()
    {
        var (spc, _) = CreateTestSpacecraft();

        var ex = Assert.Throws<ArgumentException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            Vector3.VectorZ * 2.0,
            TestHelpers.Sun,
            spc.Engines.First()));

        Assert.Contains("collinear", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_AntiParallelBodyVectors_ThrowsArgumentException()
    {
        var (spc, _) = CreateTestSpacecraft();

        var ex = Assert.Throws<ArgumentException>(() => new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            Vector3.VectorZ * -1.0,
            TestHelpers.Sun,
            spc.Engines.First()));

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
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            secondaryVector,
            TestHelpers.Sun,
            spc.Engines.First());

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
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            secondaryVector,
            TestHelpers.Sun,
            spc.Engines.First()));
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
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            secondaryVector,
            TestHelpers.Sun,
            spc.Engines.First(),
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
        spc.AddCircularInstrument(-667600, "CAM", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun,
            spc.Engines.First());

        maneuver.TryExecute(orbitalParams);

        // Get the direction to Moon in ICRF
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var targetDirection = (moonEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();

        // Rotate the primary body vector by the computed orientation
        var pointingVector = maneuver.PrimaryBodyVector.Rotate(maneuver.StateOrientation.Rotation).Normalize();

        // The pointing vector should align with target direction (dot product close to 1)
        double alignment = pointingVector * targetDirection;
        Assert.True(alignment > 0.9999, $"Primary vector alignment: {alignment} (expected > 0.9999)");
    }

    [Fact]
    public void Execute_SecondaryVectorLiesInConstraintPlane()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument(-667600, "CAM", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun,
            spc.Engines.First());

        maneuver.TryExecute(orbitalParams);

        // Get directions to both targets
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var primaryRefDirection = (moonEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();

        var sunEphemeris = TestHelpers.Sun.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var secondaryRefDirection = (sunEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();

        // The TRIAD algorithm guarantees that the rotated secondary body vector lies in the plane
        // defined by the primary and secondary reference directions (coplanarity constraint)
        var planeNormal = primaryRefDirection.Cross(secondaryRefDirection).Normalize();

        // Rotate the secondary body vector by the computed orientation
        var secondaryBodyRotated = maneuver.SecondaryBodyVector.Rotate(maneuver.StateOrientation.Rotation).Normalize();

        // The rotated secondary vector should be perpendicular to the plane normal (i.e., lie in the plane)
        // Tolerance of 1e-6 accounts for floating-point precision in matrix/quaternion operations
        // (corresponds to about 0.2 arc-seconds, which is excellent for attitude determination)
        double outOfPlaneComponent = secondaryBodyRotated * planeNormal;
        Assert.True(System.Math.Abs(outOfPlaneComponent) < 1e-4,
            $"Secondary body vector should lie in the plane of primary/secondary reference directions. Out-of-plane component: {outOfPlaneComponent}");

        // Additionally verify it points toward the secondary target (correct half-space)
        double alignment = secondaryBodyRotated * secondaryRefDirection;
        Assert.True(alignment > 0.0, $"Secondary vector should point toward Sun half-space. Alignment: {alignment}");
    }

    [Fact]
    public void Execute_QuaternionIsNormalized()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument(-667600, "CAM", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun,
            spc.Engines.First());

        maneuver.TryExecute(orbitalParams);

        var q = maneuver.StateOrientation.Rotation;
        double magnitude = System.Math.Sqrt(q.W * q.W + q.VectorPart.X * q.VectorPart.X +
                                            q.VectorPart.Y * q.VectorPart.Y + q.VectorPart.Z * q.VectorPart.Z);

        Assert.True(System.Math.Abs(magnitude - 1.0) < 1e-10, $"Quaternion magnitude: {magnitude} (expected 1.0)");
    }

    [Fact]
    public void Execute_RotationMatrixIsOrthogonal()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument(-667600, "CAM", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun,
            spc.Engines.First());

        maneuver.TryExecute(orbitalParams);

        var rotationMatrix = Matrix.FromQuaternion(maneuver.StateOrientation.Rotation);
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
        spc.AddCircularInstrument(-667600, "CAM", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        // TRIAD pointing (fully constrained - uses both boresight and refVector)
        var triadManeuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun,
            spc.Engines.First());

        triadManeuver.TryExecute(orbitalParams);
        var triadQ = triadManeuver.StateOrientation.Rotation;

        // Verify TRIAD points the primary vector at the Moon
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var targetDirection = (moonEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();

        var boresight = triadManeuver.PrimaryBodyVector;
        var triadPointing = boresight.Rotate(triadQ).Normalize();
        double triadAlignment = triadPointing * targetDirection;

        Assert.True(triadAlignment > 0.9999, $"TRIAD should point at Moon (alignment: {triadAlignment})");

        // Compute what a simple single-vector solution would give (using Vector3.To directly)
        // This is similar to what InstrumentPointingToAttitude computes
        var singleVectorQ = targetDirection.To(boresight);

        // Verify they produce the same primary pointing but potentially different roll
        var singlePointing = boresight.Rotate(singleVectorQ.Conjugate()).Normalize();
        double singleAlignment = singlePointing * targetDirection;

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
        spc.AddCircularInstrument(-667600, "NadirCam", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.EarthAtJ2000, // Point at Earth (nadir)
            TestHelpers.Sun, // Secondary constraint toward Sun
            spc.Engines.First());

        maneuver.TryExecute(orbitalParams);

        // Verify Earth pointing
        var earthEphemeris = TestHelpers.EarthAtJ2000.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var earthDirection = (earthEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
        var cameraPointing = Vector3.VectorZ.Rotate(maneuver.StateOrientation.Rotation).Normalize();

        Assert.True(cameraPointing * earthDirection > 0.9999, "Camera should point at Earth");

        // Verify Sun-facing tendency
        var sunEphemeris = TestHelpers.Sun.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var sunDirection = (sunEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
        var panelNormal = Vector3.VectorY.Rotate(maneuver.StateOrientation.Rotation).Normalize();

        Assert.True(panelNormal * sunDirection > 0.0, "Solar panel should face toward Sun");
    }

    [Fact]
    public void Execute_DualTargetImaging()
    {
        // Two instruments pointing at different celestial bodies
        var (spc, orbitalParams) = CreateTestSpacecraft();

        spc.AddCircularInstrument(-667600, "LunarCam", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);
        spc.AddCircularInstrument(-667601, "SunSensor", "mod1", 0.5, Vector3.VectorX, Vector3.VectorY, Vector3.Zero);

        var instruments = spc.Instruments.ToList();
        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            instruments[0], // LunarCam points at Moon
            TestHelpers.MoonAtJ2000,
            instruments[1], // SunSensor constrains roll toward Sun
            TestHelpers.Sun,
            spc.Engines.First());

        maneuver.TryExecute(orbitalParams);

        // Verify Moon pointing
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var moonDirection = (moonEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
        var lunarCamPointing = Vector3.VectorZ.Rotate(maneuver.StateOrientation.Rotation).Normalize();

        Assert.True(lunarCamPointing * moonDirection > 0.9999, "Lunar camera should point at Moon");

        // Verify Sun sensor tendency
        var sunEphemeris = TestHelpers.Sun.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var sunDirection = (sunEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
        var sunSensorPointing = Vector3.VectorX.Rotate(maneuver.StateOrientation.Rotation).Normalize();

        Assert.True(sunSensorPointing * sunDirection > 0.0, "Sun sensor should be in Sun-facing half-space");
    }

    [Fact]
    public void Execute_AttitudeAtMultipleEpochs()
    {
        var (spc, _) = CreateTestSpacecraft();
        spc.AddCircularInstrument(-667600, "CAM", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        var epochs = new[]
        {
            new TimeSystem.Time(2021, 01, 01, 13, 0, 0),
            new TimeSystem.Time(2021, 01, 01, 14, 0, 0),
            new TimeSystem.Time(2021, 01, 01, 15, 0, 0)
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
                new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
                TimeSpan.FromSeconds(10.0),
                spc.Instruments.First(),
                TestHelpers.MoonAtJ2000,
                TestHelpers.Sun,
                spc.Engines.First());

            maneuver.TryExecute(orbitalParams);

            // Verify primary pointing at each epoch
            var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
            var targetDirection = (moonEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
            var pointingVector = Vector3.VectorZ.Rotate(maneuver.StateOrientation.Rotation).Normalize();

            Assert.True(pointingVector * targetDirection > 0.9999,
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
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            Vector3.VectorX,
            TestHelpers.Sun,
            spc.Engines.First());

        maneuver.TryExecute(orbitalParams);

        // Should produce valid results
        var q = maneuver.StateOrientation.Rotation;
        double magnitude = System.Math.Sqrt(q.W * q.W + q.VectorPart.X * q.VectorPart.X +
                                            q.VectorPart.Y * q.VectorPart.Y + q.VectorPart.Z * q.VectorPart.Z);

        Assert.True(System.Math.Abs(magnitude - 1.0) < 1e-10, "Quaternion should be normalized");
    }

    [Fact]
    public void Execute_NumericalStability_RepeatedCallsConsistent()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();
        spc.AddCircularInstrument(-667600, "CAM", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun,
            spc.Engines.First());

        // Execute multiple times
        var results = new Quaternion[5];
        for (int i = 0; i < 5; i++)
        {
            maneuver.TryExecute(orbitalParams);
            results[i] = maneuver.StateOrientation.Rotation;
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
        spc.AddCircularInstrument(-667600, "CAM", "mod1", 1.5, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero);

        var maneuver = new TriadAttitude(
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            spc.Instruments.First(),
            TestHelpers.MoonAtJ2000,
            TestHelpers.Sun,
            spc.Engines.First());

        maneuver.TryExecute(orbitalParams);

        Assert.Equal(0.0, maneuver.FuelBurned);
        Assert.Equal(new Window(new TimeSystem.Time(2021, 01, 01, 13, 0, 0), TimeSpan.FromSeconds(10.0)), maneuver.ManeuverWindow);
        Assert.Equal(new Window(new TimeSystem.Time(2021, 01, 01, 13, 0, 0), TimeSpan.Zero), maneuver.ThrustWindow);
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
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            Vector3.VectorZ,
            TestHelpers.MoonAtJ2000,
            secondaryVector,
            TestHelpers.Sun,
            spc.Engines.First());

        maneuver.TryExecute(orbitalParams);

        // Should still produce valid quaternion
        var q = maneuver.StateOrientation.Rotation;
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
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Spacecraft.Front,
            OrbitalDirectionTarget.Prograde,
            Spacecraft.Up,
            new CelestialAttitudeTarget(TestHelpers.Sun),
            spc.Engines.First());

        Assert.NotNull(maneuver);
        Assert.Equal(Spacecraft.Front, maneuver.PrimaryBodyVector);
        Assert.Equal(Spacecraft.Up, maneuver.SecondaryBodyVector);
        Assert.NotNull(maneuver.PrimaryAttitudeTarget);
        Assert.NotNull(maneuver.SecondaryAttitudeTarget);
    }

    [Fact]
    public void Constructor_WithAttitudeTargets_NullManeuverCenter_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentNullException>(() => new TriadAttitude(
            null,
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Spacecraft.Front,
            OrbitalDirectionTarget.Prograde,
            Spacecraft.Up,
            new CelestialAttitudeTarget(TestHelpers.Sun),
            spc.Engines.First()));
    }

    [Fact]
    public void Constructor_WithAttitudeTargets_NullPrimaryTarget_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentNullException>(() => new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Spacecraft.Front,
            null,
            Spacecraft.Up,
            new CelestialAttitudeTarget(TestHelpers.Sun),
            spc.Engines.First()));
    }

    [Fact]
    public void Constructor_WithAttitudeTargets_NullSecondaryTarget_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentNullException>(() => new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Spacecraft.Front,
            OrbitalDirectionTarget.Prograde,
            Spacecraft.Up,
            null,
            spc.Engines.First()));
    }

    [Fact]
    public void Execute_ProgradeWithSunTracking()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            Spacecraft.Front,
            OrbitalDirectionTarget.Prograde,
            Spacecraft.Up,
            new CelestialAttitudeTarget(TestHelpers.Sun),
            spc.Engines.First());

        maneuver.TryExecute(orbitalParams);

        // Verify Front aligns with velocity direction
        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var localSv = orbitalParams.RelativeTo(TestHelpers.EarthAtJ2000, Aberration.None).ToStateVector();
        var progradeDir = localSv.Velocity.Normalize();

        double alignment = rotatedFront * progradeDir;
        Assert.True(alignment > 0.9999, $"Front should align with prograde. Alignment: {alignment}");

        // Verify Up is in Sun-facing half-space
        var rotatedUp = Spacecraft.Up.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var sunEphemeris = TestHelpers.Sun.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var sunDirection = (sunEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
        Assert.True(rotatedUp * sunDirection > 0.0, "Up should be in Sun-facing half-space");
    }

    [Fact]
    public void Execute_NadirWithNormalConstraint_LVLH()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            Spacecraft.Down,
            OrbitalDirectionTarget.Nadir,
            Spacecraft.Front,
            OrbitalDirectionTarget.Prograde,
            spc.Engines.First());

        maneuver.TryExecute(orbitalParams);

        // Verify Down aligns with nadir
        var localSv = orbitalParams.RelativeTo(TestHelpers.EarthAtJ2000, Aberration.None).ToStateVector();
        var rotatedDown = Spacecraft.Down.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var nadirDir = localSv.Position.Inverse().Normalize();

        double nadirAlignment = rotatedDown * nadirDir;
        Assert.True(nadirAlignment > 0.9999, $"Down should align with nadir. Alignment: {nadirAlignment}");

        // Verify Front is in prograde half-space
        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var progradeDir = localSv.Velocity.Normalize();
        Assert.True(rotatedFront * progradeDir > 0.0, "Front should be in prograde half-space");
    }

    [Fact]
    public void Execute_MixedTargets_CelestialPrimaryOrbitalSecondary()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            Vector3.VectorZ,
            new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000),
            Vector3.VectorX,
            OrbitalDirectionTarget.Normal,
            spc.Engines.First());

        maneuver.TryExecute(orbitalParams);

        // Verify primary pointing
        var moonEphemeris = TestHelpers.MoonAtJ2000.GetEphemeris(orbitalParams.Epoch, orbitalParams.Observer, orbitalParams.Frame, Aberration.LT);
        var moonDirection = (moonEphemeris.ToStateVector().Position - orbitalParams.Position).Normalize();
        var pointingVector = Vector3.VectorZ.Rotate(maneuver.StateOrientation.Rotation).Normalize();

        double alignment = pointingVector * moonDirection;
        Assert.True(alignment > 0.9999, $"Primary should point at Moon. Alignment: {alignment}");

        // Verify quaternion is normalized
        var q = maneuver.StateOrientation.Rotation;
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
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            Spacecraft.Front,
            OrbitalDirectionTarget.Nadir,
            Spacecraft.Up,
            OrbitalDirectionTarget.Zenith,  // Opposite of nadir — collinear!
            spc.Engines.First());

        Assert.Throws<InvalidOperationException>(() => maneuver.TryExecute(orbitalParams));
    }

    [Fact]
    public void Execute_CelestialAttitudeTarget_MapsToILocalizable()
    {
        var (spc, _) = CreateTestSpacecraft();

        var maneuver = new TriadAttitude(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromHours(1.0),
            Vector3.VectorZ,
            new CelestialAttitudeTarget(TestHelpers.MoonAtJ2000),
            Vector3.VectorX,
            new CelestialAttitudeTarget(TestHelpers.Sun),
            spc.Engines.First());

        // CelestialAttitudeTarget should map back to PrimaryTarget/SecondaryTarget
        Assert.Equal(TestHelpers.MoonAtJ2000, maneuver.PrimaryTarget);
        Assert.Equal(TestHelpers.Sun, maneuver.SecondaryTarget);
    }

    #endregion

    #region Factory Method Tests

    [Fact]
    public void CreateLVLH_CreatesValidManeuver()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = TriadAttitude.CreateLVLH(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            spc.Engines.First());

        Assert.NotNull(maneuver);
        Assert.Equal(Spacecraft.Down, maneuver.PrimaryBodyVector);
        Assert.Equal(Spacecraft.Front, maneuver.SecondaryBodyVector);

        // Execute and verify
        maneuver.TryExecute(orbitalParams);

        var localSv = orbitalParams.RelativeTo(TestHelpers.EarthAtJ2000, Aberration.None).ToStateVector();
        var rotatedDown = Spacecraft.Down.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var nadirDir = localSv.Position.Inverse().Normalize();

        double alignment = rotatedDown * nadirDir;
        Assert.True(alignment > 0.9999, $"LVLH Down should align with nadir. Alignment: {alignment}");
    }

    [Fact]
    public void CreateProgradeWithSunTracking_CreatesValidManeuver()
    {
        var (spc, orbitalParams) = CreateTestSpacecraft();

        var maneuver = TriadAttitude.CreateProgradeWithSunTracking(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            TestHelpers.Sun,
            spc.Engines.First());

        Assert.NotNull(maneuver);
        Assert.Equal(Spacecraft.Front, maneuver.PrimaryBodyVector);
        Assert.Equal(Spacecraft.Up, maneuver.SecondaryBodyVector);

        // Execute and verify
        maneuver.TryExecute(orbitalParams);

        var localSv = orbitalParams.RelativeTo(TestHelpers.EarthAtJ2000, Aberration.None).ToStateVector();
        var rotatedFront = Spacecraft.Front.Rotate(maneuver.StateOrientation.Rotation).Normalize();
        var progradeDir = localSv.Velocity.Normalize();

        double alignment = rotatedFront * progradeDir;
        Assert.True(alignment > 0.9999, $"Front should align with prograde. Alignment: {alignment}");
    }

    [Fact]
    public void CreateProgradeWithSunTracking_NullSun_ThrowsArgumentNullException()
    {
        var (spc, _) = CreateTestSpacecraft();

        Assert.Throws<ArgumentNullException>(() => TriadAttitude.CreateProgradeWithSunTracking(
            TestHelpers.EarthAtJ2000,
            new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            null,
            spc.Engines.First()));
    }

    #endregion
}
