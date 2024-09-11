// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.IO;

namespace IO.Astrodynamics.Body
{
    /// <summary>
    /// Represents the parameters for a geopotential model.
    /// </summary>
    public class GeopotentialModelParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GeopotentialModelParameters"/> class with the specified geopotential model path and degree.
        /// </summary>
        /// <param name="geopotentialModelPath">The file path to the geopotential model.</param>
        /// <param name="geopotentialDegree">The degree of the geopotential model. Default is 60.</param>
        public GeopotentialModelParameters(string geopotentialModelPath, ushort geopotentialDegree = 60) : this(
            new FileStream(geopotentialModelPath, FileMode.Open, FileAccess.Read, FileShare.Read),
            geopotentialDegree)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeopotentialModelParameters"/> class with the specified geopotential model stream and degree.
        /// </summary>
        /// <param name="geopotentialModelStream">The stream of the geopotential model.</param>
        /// <param name="geopotentialDegree">The degree of the geopotential model. Default is 60.</param>
        public GeopotentialModelParameters(Stream geopotentialModelStream, ushort geopotentialDegree = 60)
        {
            GeopotentialModelPath = new StreamReader(geopotentialModelStream);
            GeopotentialDegree = geopotentialDegree;
        }

        /// <summary>
        /// Gets the stream reader for the geopotential model.
        /// </summary>
        public StreamReader GeopotentialModelPath { get; }

        /// <summary>
        /// Gets the degree of the geopotential model.
        /// </summary>
        public ushort GeopotentialDegree { get; }
    }
}