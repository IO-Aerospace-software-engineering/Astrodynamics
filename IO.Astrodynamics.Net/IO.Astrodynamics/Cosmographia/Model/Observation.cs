// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Text.Json.Serialization;

namespace IO.Astrodynamics.Cosmographia.Model;

public class ObservationRootObject
{
    public string version { get; set; }
    public string name { get; set; }
    public ObservationItems[] items { get; set; }
}

public class ObservationItems
{
    [JsonPropertyName("class")]
    public string obsClass { get; set; }
    public string name { get; set; }
    public string startTime { get; set; }
    public string endTime { get; set; }
    public string center { get; set; }
    public ObservationTrajectoryFrame trajectoryFrame { get; set; }
    public ObservationBodyFrame bodyFrame { get; set; }
    public ObservationGeometry geometry { get; set; }
}

public class ObservationTrajectoryFrame
{
    public string type { get; set; }
    public string body { get; set; }
}

public class ObservationBodyFrame
{
    public string type { get; set; }
    public string body { get; set; }
}

public class ObservationGeometry
{
    public string type { get; set; }
    public string sensor { get; set; }
    public ObservationGroup[] groups { get; set; }
    public double[] footprintColor { get; set; }
    public double footprintOpacity { get; set; }
    public bool showResWithColor { get; set; }
    public int alongTrackDivisions { get; set; }
    public int sideDivisions { get; set; }
    public double shadowVolumeScaleFactor { get; set; }
    public bool fillInObservations { get; set; }
}

public class ObservationGroup
{
    public string startTime { get; set; }
    public string endTime { get; set; }
    public int obsRate { get; set; }
}

