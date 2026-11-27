using System.Reflection;

namespace IO.Astrodynamics.Pds.V4.MissionInformation;

public class PdsSurfaceMissionInformation : PdsBase<Mission_Information_1K00_1300>
{
    public PdsSurfaceMissionInformation() : base(new PdsConfiguration(("http://pds.nasa.gov/pds4/pds/v1", Assembly.GetAssembly(typeof(PdsMissionInformation)).GetManifestResourceStream("IO.Astrodynamics.PDS.V4.Schemas.PDS4_PDS_1K00.xsd")),
        ("http://pds.nasa.gov/pds4/msn_surface/v1", Assembly.GetAssembly(typeof(PdsSurfaceMissionInformation)).GetManifestResourceStream("IO.Astrodynamics.PDS.V4.Schemas.PDS4_MSN_SURFACE_1K00_1220.xsd"))))
    {
    }
}