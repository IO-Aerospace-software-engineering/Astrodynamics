using System.Text.Json.Serialization;

namespace IO.Astrodynamics.Cosmographia.Model;

public class SpacecraftRootObject
{
    public string version { get; set; }
    public string name { get; set; }
    public SpacecraftItem[] items { get; set; }
}

public class SpacecraftItem
{
    [JsonPropertyName("class")] public string spacecraftClass { get; set; }
    public string name { get; set; }
    public string startTime { get; set; }
    public string endTime { get; set; }
    public string center { get; set; }
    public SpacecraftTrajectory trajectory { get; set; }
    public SpacecraftBodyFrame bodyFrame { get; set; }
    public SpacecraftGeometry geometry { get; set; }
    public SpacecraftLabel label { get; set; }
    public SpacecraftTrajectoryPlot trajectoryPlot { get; set; }
}

public class SpacecraftTrajectory
{
    public string type { get; set; }
    public string target { get; set; }
    public string center { get; set; }
}

public class SpacecraftBodyFrame
{
    public string type { get; set; }
    public string name { get; set; }
}

public class SpacecraftGeometry
{
    public string type { get; set; }
    public int[] meshRotation { get; set; }
    public double[] radii { get; set; }
    public int size { get; set; }
    public string source { get; set; }
}

public class SpacecraftLabel
{
    public double[] color { get; set; }
    public bool showText { get; set; }
}

public class SpacecraftTrajectoryPlot
{
    public double[] color { get; set; }
    public string duration { get; set; }
    public int fade { get; set; }
    public bool visible { get; set; }
    public int lineWidth { get; set; }
    public string lead { get; set; }
}