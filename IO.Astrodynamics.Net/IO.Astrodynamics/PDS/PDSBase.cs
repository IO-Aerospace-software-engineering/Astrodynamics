// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using IO.Astrodynamics.Helpers;

namespace IO.Astrodynamics.PDS;

/// <summary>
/// Base class to manage PDS archives.
/// You can use the specific classes implemented in this framework or implement your own by extending this class
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class PDSBase<T> where T : class
{
    public PDSConfiguration Configuration { get; }

    /// <summary>
    /// Instantiate PDS from given configuation
    /// </summary>
    /// <param name="configuration">The configuration hold a set of xml schema and associated namespace</param>
    /// <exception cref="ArgumentNullException"></exception>
    public PDSBase(PDSConfiguration configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Validate PDS archive from a given file
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public string[] ValidateArchive(FileInfo file)
    {
        using var stream = file.OpenRead();
        return ValidateArchive(stream);
    }

    /// <summary>
    /// Validate PDS archive from Xml stream and return detailed errors if any
    /// </summary>
    /// <param name="xmlStream"></param>
    /// <returns></returns>
    public string[] ValidateArchive(Stream xmlStream)
    {
        List<string> errors = new List<string>();
        XmlReaderSettings pdsSettings = new XmlReaderSettings();

        foreach (var schema in Configuration.Schemas)
        {
            var xmlReader = XmlReader.Create(schema.stream);
            pdsSettings.Schemas.Add(schema.nms, xmlReader);
        }

        pdsSettings.ValidationType = ValidationType.Schema;
        pdsSettings.ValidationEventHandler += (_, e) => errors.Add(e.Message);

        XmlReader pdsArchive = XmlReader.Create(xmlStream, pdsSettings);

        while (pdsArchive.Read())
        {
        }

        return errors.ToArray();
    }

    /// <summary>
    /// Generate xml data from pds entity 
    /// </summary>
    /// <param name="pdsEntity"></param>
    /// <returns></returns>
    public string GenerateArchive(T pdsEntity)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(T));
        using var sw = new Utf8StringWriter();
        serializer.Serialize(sw, pdsEntity);
        return sw.ToString();
    }

    /// <summary>
    /// Load PDS Archive from xml file
    /// </summary>
    /// <param name="file">Xml Archive</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public T LoadArchive(FileInfo file)
    {
        XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
        using Stream stream = file.OpenRead();
        XmlReader reader = XmlReader.Create(stream);
        if (stream.Position > 0)
        {
            stream.Position = 0;
        }

        if (!xmlSerializer.CanDeserialize(reader))
        {
            throw new ArgumentException("Invalid xml");
        }

        if (stream.Position > 0)
        {
            stream.Position = 0;
        }

        return xmlSerializer.Deserialize(stream) as T;
    }
}