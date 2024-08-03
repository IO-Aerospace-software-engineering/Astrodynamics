// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Text.Json.Serialization;

namespace IO.Astrodynamics.Cosmographia.Model;

public class BodyMeshRootObject
{
    public string version { get; set; }
    public string name { get; set; }
    public BodyMeshItems[] items { get; set; }
}

public class BodyMeshItems
{
    [JsonPropertyName("class")]
    public string bodyMeshClass { get; set; }
    public string name { get; set; }
    public string mass { get; set; }
    public int density { get; set; }
    public string center { get; set; }
    public BodyMeshTrajectory trajectory { get; set; }
    public BodyMeshBodyFrame bodyFrame { get; set; }
    public BodyMeshGeometry geometry { get; set; }
    public BodyMeshLabel label { get; set; }
    public BodyMeshTrajectoryPlot trajectoryPlot { get; set; }
}

public class BodyMeshTrajectory
{
    public string type { get; set; }
    public string target { get; set; }
    public string center { get; set; }
}

public class BodyMeshBodyFrame
{
    public string type { get; set; }
    public string name { get; set; }
}

public class BodyMeshGeometry
{
    public string type { get; set; }
    public int[] meshRotation { get; set; }
    public int size { get; set; }
    public string source { get; set; }
}

public class BodyMeshLabel
{
    public int[] color { get; set; }
}

public class BodyMeshTrajectoryPlot
{
    public int[] color { get; set; }
    public string duration { get; set; }
    public int fade { get; set; }
}

