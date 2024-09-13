using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.DataProvider;

public interface IDataProvider
{
    public StateOrientation FrameTransformation(Frame source, Frame target, in Time date);
}