// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Text.Json.Serialization;

namespace IO.Astrodynamics.Cosmographia.Model;

public class BodyGlobeRootObject
{
    public string version { get; set; }
    public string name { get; set; }
    public BodyGlobeItems[] items { get; set; }
}

public class BodyGlobeItems
{
    [JsonPropertyName("class")]
    public string bodyGlobeClass { get; set; }
    public string name { get; set; }
    public string mass { get; set; }
    public int density { get; set; }
    public string center { get; set; }
    public BodyGlobeTrajectory trajectory { get; set; }
    public BodyGlobeBodyFrame bodyFrame { get; set; }
    public BodyGlobeGeometry geometry { get; set; }
    public BodyGlobeLabel label { get; set; }
    public BodyGlobeTrajectoryPlot trajectoryPlot { get; set; }
}

public class BodyGlobeTrajectory
{
    public string type { get; set; }
    public string target { get; set; }
    public string center { get; set; }
}

public class BodyGlobeBodyFrame
{
    public string type { get; set; }
    public string name { get; set; }
}

public class BodyGlobeGeometry
{
    public string type { get; set; }
    public int[] radii { get; set; }
    public string baseMap { get; set; }
}

public class BodyGlobeLabel
{
    public int[] color { get; set; }
}

public class BodyGlobeTrajectoryPlot
{
    public int[] color { get; set; }
    public string duration { get; set; }
    public int fade { get; set; }
}

