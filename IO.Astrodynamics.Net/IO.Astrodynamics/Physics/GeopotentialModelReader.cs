// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace IO.Astrodynamics.Physics;

public class GeopotentialModelReader
{
    public StreamReader ModelFile { get; }
    private Dictionary<(ushort nidx, ushort midx), GeopotentialCoefficient> _geopotentialCoefficients = new();

    public GeopotentialModelReader(StreamReader modelFile)
    {
        ModelFile = modelFile;
        using (var stream =  ModelFile)
        {
            var data = stream.ReadToEnd().Replace('D', 'E');
            var lines = data.Split(Environment.NewLine);
            
            foreach (var line in lines)
            {
                var tmp = line.Split(' ').ToList();
                tmp.RemoveAll(string.IsNullOrEmpty);
                var coeff = new GeopotentialCoefficient(ushort.Parse(tmp[0],CultureInfo.InvariantCulture), ushort.Parse(tmp[1],CultureInfo.InvariantCulture), double.Parse(tmp[2],CultureInfo.InvariantCulture), double.Parse(tmp[3],CultureInfo.InvariantCulture), double.Parse(tmp[4],CultureInfo.InvariantCulture),
                    double.Parse(tmp[5],CultureInfo.InvariantCulture));
                _geopotentialCoefficients[(coeff.N, coeff.M)] = coeff;
            }
        }
    }

    public GeopotentialCoefficient ReadCoefficient(ushort n, ushort m)
    {
        if (m > n)
        {
            throw new ArgumentException("order m cannot be greater than degree n");
        }
        return _geopotentialCoefficients[(n, m)];
    }
}