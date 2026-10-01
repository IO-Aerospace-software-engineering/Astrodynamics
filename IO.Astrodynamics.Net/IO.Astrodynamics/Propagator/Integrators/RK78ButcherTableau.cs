// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Propagator.Integrators;

/// <summary>
/// Prince-Dormand 7(8) Butcher tableau coefficients.
/// 13 stages, 8th-order solution with 7th-order embedded formula for error estimation.
/// Reference: Prince, P.J. and Dormand, J.R., "High Order Embedded Runge-Kutta Formulae", 1981.
/// Coefficients transcribed from GMAT R2025a PrinceDormand78.cpp (Apache 2.0, NASA/GSFC).
/// </summary>
internal static class RK78ButcherTableau
{
    public const int Stages = 13;

    /// <summary>c_i: stage time fractions. c[s] is the fraction of the step at which stage s is evaluated.</summary>
    public static readonly double[] C =
    {
        0.0,
        1.0 / 18.0,
        1.0 / 12.0,
        1.0 / 8.0,
        5.0 / 16.0,
        3.0 / 8.0,
        59.0 / 400.0,
        93.0 / 200.0,
        5490023248.0 / 9719169821.0,
        13.0 / 20.0,
        1201146811.0 / 1299019798.0,
        1.0,
        1.0
    };

    /// <summary>
    /// a_ij: lower-triangular RK coefficient matrix (jagged array).
    /// A[s] has exactly s+1 entries for s=0..12, where the last entry is unused padding
    /// to allow uniform indexing. Only entries j=0..s-1 are used in the sum.
    /// </summary>
    public static readonly double[][] A =
    {
        // Stage 0: no predecessors
        new[] { 0.0 },

        // Stage 1
        new[] { 1.0 / 18.0, 0.0 },

        // Stage 2
        new[] { 1.0 / 48.0, 1.0 / 16.0, 0.0 },

        // Stage 3
        new[] { 1.0 / 32.0, 0.0, 3.0 / 32.0, 0.0 },

        // Stage 4
        new[] { 5.0 / 16.0, 0.0, -75.0 / 64.0, 75.0 / 64.0, 0.0 },

        // Stage 5
        new[] { 3.0 / 80.0, 0.0, 0.0, 3.0 / 16.0, 3.0 / 20.0, 0.0 },

        // Stage 6
        new[]
        {
            29443841.0 / 614563906.0, 0.0, 0.0,
            77736538.0 / 692538347.0, -28693883.0 / 1125000000.0,
            23124283.0 / 1800000000.0, 0.0
        },

        // Stage 7
        new[]
        {
            16016141.0 / 946692911.0, 0.0, 0.0,
            61564180.0 / 158732637.0, 22789713.0 / 633445777.0,
            545815736.0 / 2771057229.0, -180193667.0 / 1043307555.0, 0.0
        },

        // Stage 8
        new[]
        {
            39632708.0 / 573591083.0, 0.0, 0.0,
            -433636366.0 / 683701615.0, -421739975.0 / 2616292301.0,
            100302831.0 / 723423059.0, 790204164.0 / 839813087.0,
            800635310.0 / 3783071287.0, 0.0
        },

        // Stage 9
        new[]
        {
            246121993.0 / 1340847787.0, 0.0, 0.0,
            -37695042795.0 / 15268766246.0, -309121744.0 / 1061227803.0,
            -12992083.0 / 490766935.0, 6005943493.0 / 2108947869.0,
            393006217.0 / 1396673457.0, 123872331.0 / 1001029789.0, 0.0
        },

        // Stage 10
        new[]
        {
            -1028468189.0 / 846180014.0, 0.0, 0.0,
            8478235783.0 / 508512852.0, 1311729495.0 / 1432422823.0,
            -10304129995.0 / 1701304382.0, -48777925059.0 / 3047939560.0,
            15336726248.0 / 1032824649.0, -45442868181.0 / 3398467696.0,
            3065993473.0 / 597172653.0, 0.0
        },

        // Stage 11
        new[]
        {
            185892177.0 / 718116043.0, 0.0, 0.0,
            -3185094517.0 / 667107341.0, -477755414.0 / 1098053517.0,
            -703635378.0 / 230739211.0, 5731566787.0 / 1027545527.0,
            5232866602.0 / 850066563.0, -4093664535.0 / 808688257.0,
            3962137247.0 / 1805957418.0, 65686358.0 / 487910083.0, 0.0
        },

        // Stage 12
        new[]
        {
            403863854.0 / 491063109.0, 0.0, 0.0,
            -5068492393.0 / 434740067.0, -411421997.0 / 543043805.0,
            652783627.0 / 914296604.0, 11173962825.0 / 925320556.0,
            -13158990841.0 / 6184727034.0, 3936647629.0 / 1978049680.0,
            -160528059.0 / 685178525.0, 248638103.0 / 1413531060.0,
            0.0, 0.0
        }
    };

    /// <summary>b_j: 8th-order solution weights.</summary>
    public static readonly double[] B =
    {
        14005451.0 / 335480064.0,
        0.0,
        0.0,
        0.0,
        0.0,
        -59238493.0 / 1068277825.0,
        181606767.0 / 758867731.0,
        561292985.0 / 797845732.0,
        -1041891430.0 / 1371343529.0,
        760417239.0 / 1151165299.0,
        118820643.0 / 751138087.0,
        -528747749.0 / 2220607170.0,
        1.0 / 4.0
    };

    /// <summary>bhat_j: 7th-order embedded weights for error estimation.</summary>
    public static readonly double[] BHat =
    {
        13451932.0 / 455176623.0,
        0.0,
        0.0,
        0.0,
        0.0,
        -808719846.0 / 976000145.0,
        1757004468.0 / 5645159321.0,
        656045339.0 / 265891186.0,
        -3867574721.0 / 1518517206.0,
        465885868.0 / 322736535.0,
        53011238.0 / 667516719.0,
        2.0 / 45.0,
        0.0
    };

    /// <summary>e_j = b_j - bhat_j: error estimation coefficients (precomputed).</summary>
    public static readonly double[] E = ComputeErrorCoefficients();

    private static double[] ComputeErrorCoefficients()
    {
        var e = new double[Stages];
        for (int i = 0; i < Stages; i++)
            e[i] = B[i] - BHat[i];
        return e;
    }
}
