using System.Reflection;

namespace IO.Astrodynamics.PDS.V4.MissionInformation;

public class PDSMissionInformation : PDSBase<Mission_Information_1K00_1300>
{
    public PDSMissionInformation() : base(new PDSConfiguration(("http://pds.nasa.gov/pds4/pds/v1", Assembly.GetAssembly(typeof(PDSMissionInformation))?.GetManifestResourceStream("IO.Astrodynamics.PDS.V4.Schemas.PDS4_PDS_1K00.xsd")),
        ("http://pds.nasa.gov/pds4/msn/v1", Assembly.GetAssembly(typeof(PDSMissionInformation))?.GetManifestResourceStream("IO.Astrodynamics.PDS.V4.Schemas.PDS4_MSN_1K00_1300.xsd"))))
    {
    }
}