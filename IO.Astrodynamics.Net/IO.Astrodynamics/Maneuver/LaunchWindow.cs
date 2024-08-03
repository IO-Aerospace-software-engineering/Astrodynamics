
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.Maneuver
{
    public class LaunchWindow 
    {
        public Window Window { get; }
        public double InertialInsertionVelocity { get; }
        public double NonInertialInsertionVelocity { get; }
        public double InertialAzimuth { get; }
        public double NonInertialAzimuth { get; }

        public LaunchWindow(Window window, double inertialInsertionVelocity, double nonInertialInsertionVelocity, double inertialAzimuth, double nonInertialAzimuth)
        {
            Window = window;
            InertialInsertionVelocity = inertialInsertionVelocity;
            NonInertialInsertionVelocity = nonInertialInsertionVelocity;
            InertialAzimuth = inertialAzimuth;
            NonInertialAzimuth = nonInertialAzimuth;
        }
    }
}
