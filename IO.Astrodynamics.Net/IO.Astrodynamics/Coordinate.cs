// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics;

public enum Coordinate
{
    [Description("X")] X,
    [Description("Y")] Y,
    [Description("Z")] Z,
    [Description("ALTITUDE")] Altitude,
    [Description("COLATITUDE")] Colatitude,
    [Description("DECLINATION")] Declination,
    [Description("LATITUDE")] Latitude,
    [Description("LONGITUDE")] Longitude,
    [Description("RADIUS")] Radius,
    [Description("RANGE")] Range,
    [Description("RIGHT ASCENSION")] RightAscension
}