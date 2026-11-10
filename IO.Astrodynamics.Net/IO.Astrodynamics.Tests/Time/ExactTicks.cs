using System;
using System.Numerics;

namespace IO.Astrodynamics.Tests.Time;

/// <summary>
/// The tick count nearest the exact value of a double times a number of ticks per unit, ties to even, in exact integer
/// arithmetic: the double is m 2^e exactly, so the product is m k 2^e, rounded by its remainder against half of 2^-e.
/// The reference of the rounding of seconds to ticks (#625), independent of the floating-point path of the library.
/// </summary>
internal static class ExactTicks
{
    internal static long Nearest(double value, long ticksPerUnit = TimeSpan.TicksPerSecond)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        int exponent = (int)((bits >> 52) & 0x7FF);
        long mantissa = bits & 0xFFFFFFFFFFFFFL;
        if (exponent == 0x7FF)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (exponent == 0)
            exponent = 1;
        else
            mantissa |= 1L << 52;

        exponent -= 1075;
        BigInteger product = new BigInteger(mantissa) * ticksPerUnit;
        BigInteger result;
        if (exponent >= 0)
        {
            result = product << exponent;
        }
        else
        {
            int shift = -exponent;
            result = product >> shift;
            BigInteger remainder = product - (result << shift);
            BigInteger half = BigInteger.One << (shift - 1);
            if (remainder > half || (remainder == half && !result.IsEven))
                result += 1;
        }

        return (long)(bits < 0 ? -result : result);
    }
}
