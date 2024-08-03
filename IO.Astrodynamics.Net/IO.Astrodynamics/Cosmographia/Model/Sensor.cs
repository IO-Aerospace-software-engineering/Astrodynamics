using System.Text.Json.Serialization;

namespace IO.Astrodynamics.Cosmographia.Model;

public class SensorRootObject
{
    public string version { get; set; }
    public string name { get; set; }
    public SensorItem[] items { get; set; }
}

public class SensorItem
{
    [JsonPropertyName("class")]
    public string sensorClass { get; set; }
    public string name { get; set; }
    public string parent { get; set; }
    public string startTime { get; set; }
    public string endTime { get; set; }
    public string center { get; set; }
    public SensorTrajectoryFrame trajectoryFrame { get; set; }
    public SensorGeometry geometry { get; set; }
}

public class SensorTrajectoryFrame
{
    public string type { get; set; }
    public string body { get; set; }
}

public class SensorGeometry
{
    public string type { get; set; }
    public string instrName { get; set; }
    public string target { get; set; }
    public int range { get; set; }
    public bool rangeTracking { get; set; }
    public double[] frustumColor { get; set; }
    public int frustumBaseLineWidth { get; set; }
    public double frustumOpacity { get; set; }
    public double gridOpacity { get; set; }
    public double footprintOpacity { get; set; }
    public int sideDivisions { get; set; }
    public bool onlyVisibleDuringObs { get; set; }
}

