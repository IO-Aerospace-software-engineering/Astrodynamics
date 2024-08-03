using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;

namespace IO.Astrodynamics.Tests.Math
{
    public class LagrangeTests
    {
        public LagrangeTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }
        [Fact]
        public void InterpolateSquare()
        {
            //Interpolate square function
            (double x, double y)[] data = new (double, double)[10];
            for (int i = 0; i < 10; i++)
            {
                data[i] = (i, i * i);
            }

            double res = Lagrange.Interpolate(data, 3);
            Assert.Equal(9.0, res);

            res = Lagrange.Interpolate(data, -3);
            Assert.Equal(9.0, res);

            res = Lagrange.Interpolate(data, 11);
            Assert.Equal(121.0, res, 11);
        }

        [Fact]
        public void InterpolateCubic()
        {
            //Interpolate square function
            (double x, double y)[] data = new (double, double)[10];
            for (int i = 0; i < 10; i++)
            {
                data[i] = (i, i * i * i);
            }

            double res = Lagrange.Interpolate(data, 3);
            Assert.Equal(27.0, res);

            res = Lagrange.Interpolate(data, -3);
            Assert.Equal(-27.0, res);

            res = Lagrange.Interpolate(data, 11);
            Assert.Equal(1331.0, res, 9);
        }

        [Fact]
        public void InterpolateStateVectorSquare()
        {

            //Interpolate square function
            CelestialBody earth = new CelestialBody( PlanetsAndMoons.EARTH);
            StateVector[] data = new StateVector[10];
            var start = new DateTime(2021, 01, 01, 0, 0, 0);
            for (int i = 0; i < 10; i++)
            {
                data[i] = new StateVector(new Vector3(i * i, 0.0, 0.0), new Vector3(i * i, 0.0, 0.0), earth, start.AddSeconds(i), Frames.Frame.ICRF);
            }

            var res = Lagrange.Interpolate(data, start.AddSeconds(3));
            Assert.Equal(9.0, res.Position.X);
            Assert.Equal(0.0, res.Position.Y);
            Assert.Equal(0.0, res.Position.Z);

            res = Lagrange.Interpolate(data, start.AddSeconds(-3));
            Assert.Equal(9.0, res.Position.X, 11);
            Assert.Equal(0.0, res.Position.Y);
            Assert.Equal(0.0, res.Position.Z);

            res = Lagrange.Interpolate(data, start.AddSeconds(11));
            Assert.Equal(121.0, res.Position.X, 10);
            Assert.Equal(0.0, res.Position.Y);
            Assert.Equal(0.0, res.Position.Z);
        }

        [Fact]
        public void InterpolateStateVectorCubic()
        {
            //Interpolate square function
            CelestialBody earth = new CelestialBody( PlanetsAndMoons.EARTH);
            StateVector[] data = new StateVector[10];
            var start = new DateTime(2021, 01, 01, 0, 0, 0);
            for (int i = 0; i < 10; i++)
            {
                data[i] = new StateVector(new Vector3(i * i * i, 0.0, 0.0), new Vector3(i * i * i, 0.0, 0.0), earth, start.AddSeconds(i), Frames.Frame.ICRF);
            }

            var res = Lagrange.Interpolate(data, start.AddSeconds(3));
            Assert.Equal(27.0, res.Position.X);
            Assert.Equal(0.0, res.Position.Y);
            Assert.Equal(0.0, res.Position.Z);

            res = Lagrange.Interpolate(data, start.AddSeconds(-3));
            Assert.Equal(-27.0, res.Position.X);
            Assert.Equal(0.0, res.Position.Y);
            Assert.Equal(0.0, res.Position.Z);

            res = Lagrange.Interpolate(data, start.AddSeconds(11));
            Assert.Equal(1331.0, res.Position.X, 9);
            Assert.Equal(0.0, res.Position.Y);
            Assert.Equal(0.0, res.Position.Z);
        }

        [Fact]
        public void InterpolateStateOrientation()
        {
            StateOrientation[] data = new StateOrientation[10];
            var start = new DateTime(2021, 01, 01, 0, 0, 0);
            for (int i = 0; i < 10; i++)
            {
                data[i] = new StateOrientation(new Quaternion(i * i, 1000.0 + i * i, 10000.0 + i * i, 100000 + i * i), new Vector3(i * i, 0.0, 0.0), start.AddSeconds(i), Frames.Frame.ICRF);
            }
        }
    }
}