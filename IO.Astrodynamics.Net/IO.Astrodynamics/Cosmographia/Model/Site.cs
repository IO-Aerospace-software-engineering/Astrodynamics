// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Cosmographia.Model;

internal class SiteRootObject
{
    public string version { get; set; }
    public string name { get; set; }
    public SiteItem[] items { get; set; }
}

internal class SiteItem
{
    public string type { get; set; }
    public string name { get; set; }
    public string body { get; set; }
    public SiteFeature[] features { get; set; }
}

internal class SiteFeature
{
    public string origin { get; set; }
    public double diameter { get; set; }
    public string code { get; set; }
    public string name { get; set; }
    public double longitude { get; set; }
    public string link { get; set; }
    public double latitude { get; set; }
}



