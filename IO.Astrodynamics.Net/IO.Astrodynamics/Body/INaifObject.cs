namespace IO.Astrodynamics.Body
{
    public interface INaifObject
    {
        int NaifId { get; }
        string Name { get; }
    }
}