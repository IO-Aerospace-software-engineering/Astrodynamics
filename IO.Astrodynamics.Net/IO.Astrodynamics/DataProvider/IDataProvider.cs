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
    StateOrientation FrameTransformationToICRF(in Time date, Frame source);
    OrbitalParameters.OrbitalParameters GetEphemeris(in Time date, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration);
    DTO.CelestialBody GetCelestialBodyInfo(int naifId);
    void WriteEphemeris(FileInfo outputFile, INaifObject naifObject, IEnumerable<StateVector> stateVectors);
    void WriteOrientation(FileInfo outputFile, INaifObject naifObject, IEnumerable<StateOrientation> stateOrientations);
}