namespace IO.Astrodynamics.OrbitalParameters.TLE;

/// <summary>
/// TLE classification levels according to US space surveillance standards
/// </summary>
public enum Classification : byte
{
    /// <summary>
    /// Unclassified
    /// </summary>
    Unclassified = (byte)'U',
    
    /// <summary>
    /// Classified
    /// </summary>
    Classified = (byte)'C',
    
    /// <summary>
    /// Secret
    /// </summary>
    Secret = (byte)'S'
}