// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;

namespace IO.Astrodynamics.PDS;

/// <summary>
/// This class handles configuration to manage PDS archive
/// </summary>
public class PDSConfiguration
{
    private readonly HashSet<(string nms,Stream stream)> _schemas;
    public IReadOnlyCollection<(string nms,Stream stream)> Schemas => _schemas;

    /// <summary>
    /// Instantiate a PDS configuration from namespace and schemas 
    /// </summary>
    /// <param name="schemas"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public PDSConfiguration(params (string nms,Stream stream)[] schemas)
    {
        if (schemas == null) throw new ArgumentNullException(nameof(schemas));
        _schemas = new HashSet<(string nms,Stream stream)>(schemas);
    }
}