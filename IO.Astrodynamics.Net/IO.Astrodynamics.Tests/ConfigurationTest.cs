using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.DTO;
using IO.Astrodynamics.Math;
using Xunit;
using CelestialBody = IO.Astrodynamics.DTO.CelestialBody;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;

namespace IO.Astrodynamics.Tests;

public class ConfigurationTest
{
    public ConfigurationTest()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void SetDataProviderException()
    {
        // Arrange

        Assert.Throws<ArgumentNullException>(() => Configuration.Instance.SetDataProvider(null));
    }

    [Fact]
    public void FrameTransformationToICRF_ThrowsArgumentException_WhenFrameNotFound()
    {
        var dataProvider = new MemoryDataProvider();
        var frame = new Frames.Frame("NonExistentFrame");
        var date = TimeSystem.Time.J2000TDB;

        Assert.Throws<ArgumentException>(() => dataProvider.FrameTransformationToICRF(date, frame));
    }

    [Fact]
    public void FrameTransformationToICRF_ReturnsStateOrientation_WhenDateExists()
    {
        var dataProvider = new MemoryDataProvider();
        var frame = Frames.Frame.ICRF;
        var date = TimeSystem.Time.J2000TDB;
        var stateOrientation = new StateOrientation(Quaternion.Zero, Vector3.Zero, date, frame);

        dataProvider.AddStateOrientationToICRF(frame, date, stateOrientation);

        var result = dataProvider.FrameTransformationToICRF(date, frame);

        Assert.Equal(stateOrientation, result);
    }

    [Fact]
    public void FrameTransformationToICRF_InterpolatesStateOrientation_WhenDateDoesNotExist()
    {
        var dataProvider = new MemoryDataProvider();
        var frame = Frames.Frame.ICRF;
        var date1 = TimeSystem.Time.J2000TDB;
        var date2 = date1.AddHours(1.0);
        var dateToInterpolate = date1.AddHours(0.5);
        var stateOrientation1 = new StateOrientation(Quaternion.Zero, Vector3.Zero, TimeSystem.Time.J2000TDB, frame);
        var stateOrientation2 = new StateOrientation(new Quaternion(Vector3.VectorZ, Astrodynamics.Constants.PI2), new Vector3(1.0, 1.0, 1.0), date2, frame);

        dataProvider.AddStateOrientationToICRF(frame, date1, stateOrientation1);
        dataProvider.AddStateOrientationToICRF(frame, date2, stateOrientation2);

        var result = dataProvider.FrameTransformationToICRF(dateToInterpolate, frame);

        Assert.NotNull(result);
    }

    [Fact]
    public void FrameTransformationToICRF_InterpolatesStateOrientation_WhenDateOutOfRange()
    {
        var dataProvider = new MemoryDataProvider();
        var frame = Frames.Frame.ICRF;
        var date1 = TimeSystem.Time.J2000TDB;
        var date2 = date1.AddHours(1.0);
        var dateToInterpolate = date1.AddHours(2);
        var stateOrientation1 = new StateOrientation(Quaternion.Zero, Vector3.Zero, TimeSystem.Time.J2000TDB, frame);
        var stateOrientation2 = new StateOrientation(new Quaternion(Vector3.VectorZ, Astrodynamics.Constants.PI2), new Vector3(1.0, 1.0, 1.0), date2, frame);

        dataProvider.AddStateOrientationToICRF(frame, date1, stateOrientation1);
        dataProvider.AddStateOrientationToICRF(frame, date2, stateOrientation2);

        Assert.Throws<ArgumentException>(() => dataProvider.FrameTransformationToICRF(dateToInterpolate, frame));
    }

    [Fact]
    public void GetEphemerisFromICRF_ThrowsArgumentException_WhenTargetNotFound()
    {
        var dataProvider = new MemoryDataProvider();
        var target = TestHelpers.EarthAtJ2000;
        var date = TimeSystem.Time.J2000TDB;
        var frame = Frames.Frame.ICRF;

        Assert.Throws<ArgumentException>(() => dataProvider.GetEphemerisFromICRF(date, target, frame, Aberration.None));
    }

    [Fact]
    public void GetEphemerisFromICRF_ReturnsStateVector_WhenDateExists()
    {
        var dataProvider = new MemoryDataProvider();
        var target = TestHelpers.EarthAtJ2000;
        var date = TimeSystem.Time.J2000TDB;
        var frame = Frames.Frame.ICRF;
        var stateVector = new StateVector(Vector3.Zero, Vector3.Zero, target, date, frame);


        dataProvider.AddStateVector(target.NaifId, date, stateVector);

        var result = dataProvider.GetEphemerisFromICRF(date, target, frame, Aberration.None);

        Assert.Equal(stateVector, result);
    }

    [Fact]
    public void GetEphemerisFromICRF_InterpolatesStateVector_WhenDateDoesNotExist()
    {
        var dataProvider = new MemoryDataProvider();
        var target = TestHelpers.EarthAtJ2000;
        var frame = Frames.Frame.ICRF;
        var date1 = TimeSystem.Time.J2000TDB;
        var date2 = date1.AddHours(1.0);
        var dateToInterpolate = date1.AddHours(0.5);
        var stateVector1 = new StateVector(Vector3.Zero, Vector3.Zero, target, date1, frame);
        var stateVector2 = new StateVector(new Vector3(1.0, 1.0, 1.0), new Vector3(1.0, 1.0, 1.0), target, date2, frame);

        dataProvider.AddStateVector(target.NaifId, date1, stateVector1);
        dataProvider.AddStateVector(target.NaifId, date2, stateVector2);

        var result = dataProvider.GetEphemerisFromICRF(dateToInterpolate, target, frame, Aberration.None);

        Assert.NotNull(result);
    }
    
    [Fact]
    public void GetEphemerisFromICRF_InterpolatesStateVector_WhenDateOutOfRange()
    {
        var dataProvider = new MemoryDataProvider();
        var target = TestHelpers.EarthAtJ2000;
        var frame = Frames.Frame.ICRF;
        var date1 = TimeSystem.Time.J2000TDB;
        var date2 = date1.AddHours(1.0);
        var dateToInterpolate = date1.AddHours(2.0);
        var stateVector1 = new StateVector(Vector3.Zero, Vector3.Zero, target, date1, frame);
        var stateVector2 = new StateVector(new Vector3(1.0, 1.0, 1.0), new Vector3(1.0, 1.0, 1.0), target, date2, frame);

        dataProvider.AddStateVector(target.NaifId, date1, stateVector1);
        dataProvider.AddStateVector(target.NaifId, date2, stateVector2);

        Assert.Throws<ArgumentException>(()=> dataProvider.GetEphemerisFromICRF(dateToInterpolate, target, frame, Aberration.None));
    }

    [Fact]
    public void GetCelestialBodyInfo_ReturnsCelestialBody_WhenNaifIdExists()
    {
        var dataProvider = new MemoryDataProvider();
        var celestialBody = new CelestialBody(10, 0, 0, "Sun", new Vector3D(150000000, 150000000, 0), 12345698, "IAU_SUN", 0, 2, 3, 4);

        dataProvider.AddCelestialBodyInfo(celestialBody);

        var result = dataProvider.GetCelestialBodyInfo(10);

        Assert.Equal(celestialBody, result);
    }

    [Fact]
    public void GetCelestialBodyInfo_ThrowsKeyNotFoundException_WhenNaifIdDoesNotExist()
    {
        var dataProvider = new MemoryDataProvider();

        Assert.Throws<KeyNotFoundException>(() => dataProvider.GetCelestialBodyInfo(999));
    }
}