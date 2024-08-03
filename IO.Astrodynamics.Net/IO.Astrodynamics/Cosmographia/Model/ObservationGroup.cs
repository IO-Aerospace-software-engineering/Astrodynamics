// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Text.Json.Serialization;

namespace IO.Astrodynamics.Cosmographia.Model;

public class ObservationGroupRootObject
{
    public string version { get; set; }
    public string name { get; set; }
    public ObservationGroupItems[] items { get; set; }
}

public class ObservationGroupItems
{
    [JsonPropertyName("class")]
    public string obsGroupClass { get; set; }
    public string name { get; set; }
    public string startTime { get; set; }
    public string endTime { get; set; }
    public string center { get; set; }
    public ObservationGroupTrajectoryFrame trajectoryFrame { get; set; }
    public ObservationGroupBodyFrame bodyFrame { get; set; }
    public ObservationGroupGeometry geometry { get; set; }
}

public class ObservationGroupTrajectoryFrame
{
    public string type { get; set; }
    public string body { get; set; }
}

public class ObservationGroupBodyFrame
{
    public string type { get; set; }
    public string body { get; set; }
}

public class ObservationGroupGeometry
{
    public string type { get; set; }
    public string sensor { get; set; }
    public ObservationGroupGroups[] groups { get; set; }
    public int[] footprintColor { get; set; }
    public int footprintOpacity { get; set; }
    public bool showResWithColor { get; set; }
    public int alongTrackDivisions { get; set; }
    public int shadowVolumeScaleFactor { get; set; }
    public bool fillInObservations { get; set; }
}

public class ObservationGroupGroups
{
    public string startTime { get; set; }
    public string endTime { get; set; }
    public int obsRate { get; set; }
}

