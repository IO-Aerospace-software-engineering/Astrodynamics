namespace IO.Astrodynamics.Body
{
    /// <summary>
    /// Represents an interface for NAIF objects.
    /// </summary>
    public interface INaifObject
    {
        /// <summary>
        /// Gets the NAIF ID of the object.
        /// </summary>
        int NaifId { get; }

        /// <summary>
        /// Gets the name of the object.
        /// </summary>
        string Name { get; }
    }
}