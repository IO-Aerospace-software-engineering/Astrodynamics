using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.DataProvider;

public interface IDataProvider
{
    StateOrientation FrameTransformation(Frame source, Frame target, in Time date);
    OrbitalParameters.OrbitalParameters GetEphemeris(in Time epoch, ILocalizable observer, ILocalizable target, Frame frame, Aberration aberration);
    DTO.CelestialBody GetCelestialBodyInfo(int naifId);
}