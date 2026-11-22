// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Text.Json.Serialization;

namespace IO.Astrodynamics.Cosmographia.Model;

internal class ArcRootObject
{
    public string version { get; set; }
    public string name { get; set; }
    public ArcItems[] items { get; set; }
}

internal class ArcItems
{
    [JsonPropertyName("class")]
    public string arcClass { get; set; }
    public string name { get; set; }
    public string startTime { get; set; }
    public Arcs[] arcs { get; set; }
    public ArcGeometry geometry { get; set; }
    public ArcLabel label { get; set; }
    public ArcTrajectoryPlot trajectoryPlot { get; set; }
}

internal class Arcs
{
    public string endTime { get; set; }
    public string center { get; set; }
    public ArcTrajectory trajectory { get; set; }
    public ArcBodyFrame bodyFrame { get; set; }
}

internal class ArcTrajectory
{
    public string type { get; set; }
    public string target { get; set; }
    public string center { get; set; }
}

internal class ArcBodyFrame
{
    public string type { get; set; }
    public string name { get; set; }
}

internal class ArcGeometry
{
    public string type { get; set; }
    public int[] meshRotation { get; set; }
    public int size { get; set; }
    public string source { get; set; }
}

internal class ArcLabel
{
    public int[] color { get; set; }
}

internal class ArcTrajectoryPlot
{
    public int[] color { get; set; }
    public string duration { get; set; }
    public int fade { get; set; }
}

