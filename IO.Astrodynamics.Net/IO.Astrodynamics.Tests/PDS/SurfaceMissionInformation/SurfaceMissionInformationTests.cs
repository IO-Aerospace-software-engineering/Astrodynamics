// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.IO;
using IO.Astrodynamics.Pds.V4.MissionInformation;
using IO.Astrodynamics.Pds.V4.Schemas;
using Xunit;

namespace IO.Astrodynamics.Tests.PDS.SurfaceMissionInformation;

public class SurfaceMissionInformationTests
{
    [Fact]
    public void Create()
    {
        PdsSurfaceMissionInformation pds = new PdsSurfaceMissionInformation();
        Surface_Mission_Information_1K00_1220 ms = new Surface_Mission_Information_1K00_1220();
    }

    [Fact]
    public void ValidateGoodFile()
    {
        FileInfo validFile = new FileInfo("PDS/SurfaceMissionInformation/test1_VALID.xml");
        PdsSurfaceMissionInformation pds = new PdsSurfaceMissionInformation();
        var res = pds.ValidateArchive(validFile);
        Assert.Empty(res);
    }

    [Fact]
    public void ValidateBadFile()
    {
        FileInfo validFile = new FileInfo("PDS/SurfaceMissionInformation/test1_FAIL.xml");
        PdsSurfaceMissionInformation pds = new PdsSurfaceMissionInformation();
        var res = pds.ValidateArchive(validFile);
        Assert.Equal(2,res.Length);
        Assert.Equal(
            "The 'http://pds.nasa.gov/pds4/msn_surface/v1:telemetry_source_start_time' element is invalid - The value '2000-06-01T00:00:00.000000Z' is invalid according to its datatype 'http://pds.nasa.gov/pds4/msn_surface/v1:telemetry_source_start_time' - The Pattern constraint failed.",
            res[0]);
        Assert.Equal(
            "The 'http://pds.nasa.gov/pds4/msn_surface/v1:earth_received_stop_date_time' element is invalid - The value '2000-064T00:00:00.000000Z' is invalid according to its datatype 'http://pds.nasa.gov/pds4/msn_surface/v1:earth_received_stop_date_time' - The Pattern constraint failed.",
            res[1]);
    }


    [Fact]
    public void GenerateArchive()
    {
        FileInfo validFile = new FileInfo("PDS/MissionInformation/test1_VALID.xml");
        PdsSurfaceMissionInformation pds = new PdsSurfaceMissionInformation();
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