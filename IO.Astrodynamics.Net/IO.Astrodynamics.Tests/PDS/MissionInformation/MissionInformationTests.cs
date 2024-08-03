// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.IO;
using IO.Astrodynamics.PDS.V4.MissionInformation;
using Xunit;

namespace IO.Astrodynamics.Tests.PDS.MissionInformation;

public class MissionInformationTests
{
    [Fact]
    public void Create()
    {
        PDSMissionInformation pds = new PDSMissionInformation();
        Mission_Information_1K00_1300 ms = new Mission_Information_1K00_1300();
    }

    [Fact]
    public void ValidateGoodFile()
    {
        FileInfo validFile = new FileInfo("PDS/MissionInformation/test1_VALID.xml");
        PDSMissionInformation pds = new PDSMissionInformation();
        var res = pds.ValidateArchive(validFile);
        Assert.Empty(res);
    }

    [Fact]
    public void ValidateBadFile()
    {
        FileInfo validFile = new FileInfo("PDS/MissionInformation/test1_FAIL.xml");
        PDSMissionInformation pds = new PDSMissionInformation();
        var res = pds.ValidateArchive(validFile);
        Assert.Single(res);
        Assert.Equal(
            "The 'http://pds.nasa.gov/pds4/msn/v1:instrument_start_time' element is invalid - The value '2000-01-01T20:00:00' is invalid according to its datatype 'http://pds.nasa.gov/pds4/msn/v1:instrument_start_time' - The Pattern constraint failed.",
            res[0]);
    }


    [Fact]
    public void GenerateArchive()
    {
        FileInfo validFile = new FileInfo("PDS/MissionInformation/test1_VALID.xml");
        PDSMissionInformation pds = new PDSMissionInformation();
        var archive = pds.LoadArchive(validFile);
        var xml = pds.GenerateArchive(archive);
        byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(xml);
        string[] errors = null;
        using (MemoryStream stream = new MemoryStream(byteArray))
        {
            errors = pds.ValidateArchive(stream);
        }
        Assert.Empty(errors);
    }
}