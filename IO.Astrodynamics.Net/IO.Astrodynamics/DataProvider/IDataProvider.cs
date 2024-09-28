using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.DataProvider;

public interface IDataProvider
{
    Task<IEnumerable<StateOrientation>> FrameTransformationAsync(Window window, Frame source, Frame target, TimeSpan stepSize);
    StateOrientation FrameTransformation(in Time date, Frame source, Frame target);
    OrbitalParameters.OrbitalParameters GetEphemeris(in Time date, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration);
    Task<IEnumerable<OrbitalParameters.OrbitalParameters>> GetEphemerisAsync(Window window, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration, TimeSpan stepSize);
    Task<DTO.CelestialBody> GetCelestialBodyInfoAsync(int naifId);
    DTO.CelestialBody GetCelestialBodyInfo(int naifId);
    void WriteEphemeris(FileInfo outputFile, INaifObject naifObject, IEnumerable<StateVector> stateVectors);
    void WriteOrientation(FileInfo outputFile, INaifObject naifObject, IEnumerable<StateOrientation> stateOrientations);
}