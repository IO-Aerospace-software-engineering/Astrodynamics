namespace IO.Astrodynamics.Coordinates
{
    public readonly record struct AzimuthRange
    {
        public double Start { get; }
        public double End { get; }
        public double Span { get; }

        public AzimuthRange(double start, double end)
        {
            Start = start %Constants.TwoPi;
            if (Start < 0.0)
            {
                Start +=Constants.TwoPi;
            }

            End = end %Constants.TwoPi;
            if (End < 0.0)
            {
                End +=Constants.TwoPi;
            }

            Span = End - Start;
            if (Span < 0.0)
            {
                Span +=Constants.TwoPi;
            }
        }

        public bool IsInRange(double angle)
        {
            var a = angle - Start %Constants.TwoPi;
            if (a < 0.0)
            {
                a +=Constants.TwoPi;
            }

            var end = End;

            if (end < Start)
            {
                end +=Constants.TwoPi;
            }

            end -= Start;

            if (end < 0.0)
            {
                end +=Constants.TwoPi;
            }

            if (a >= 0.0 && a <= end)
            {
                return true;
            }

            return false;
        }

        public bool IsIntersected(in AzimuthRange azimuthRange)
        {
            return IsInRange(azimuthRange.Start) || IsInRange(azimuthRange.End) || azimuthRange.IsInRange(Start) ||
                   azimuthRange.IsInRange(End);
        }
    }
}