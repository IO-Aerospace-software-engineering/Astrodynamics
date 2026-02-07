using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Math
{
    public class LegendreTests
    {
        public LegendreTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void AssociatedLegendrePolynomials()
        {
            // Even-m cases (unchanged by CS phase removal)
            var expectedP00 = 1.0;
            var expectedP10 = 0.5;
            var expectedP22 = 3 * (1 - 0.5 * 0.5); // 2.25
            var expectedP44 = 105.0 * ((1.0 - 0.5 * 0.5) * (1.0 - 0.5 * 0.5)); // 59.0625

            Assert.Equal(expectedP00, LegendreFunctions.AssociatedLegendre(0, 0, 0.5));
            Assert.Equal(expectedP10, LegendreFunctions.AssociatedLegendre(1, 0, 0.5));
            Assert.Equal(expectedP22, LegendreFunctions.AssociatedLegendre(2, 2, 0.5));
            Assert.Equal(expectedP44, LegendreFunctions.AssociatedLegendre(4, 4, 0.5), 12);
        }

        [Fact]
        public void AssociatedLegendrePolynomialsOddM()
        {
            // Odd-m cases: without CS phase, these should be positive
            // P(1,1, x) = sqrt(1-x²) for x=0.5 => sqrt(0.75)
            var expectedP11 = System.Math.Sqrt(0.75);
            Assert.Equal(expectedP11, LegendreFunctions.AssociatedLegendre(1, 1, 0.5), 12);

            // P(2,1, x) = 3*x*sqrt(1-x²) for x=0.5 => 3*0.5*sqrt(0.75)
            var expectedP21 = 3.0 * 0.5 * System.Math.Sqrt(0.75);
            Assert.Equal(expectedP21, LegendreFunctions.AssociatedLegendre(2, 1, 0.5), 12);

            // P(3,3, x) = 15*(1-x²)^(3/2) for x=0.5 => 15*(0.75)^1.5
            var expectedP33 = 15.0 * System.Math.Pow(0.75, 1.5);
            Assert.Equal(expectedP33, LegendreFunctions.AssociatedLegendre(3, 3, 0.5), 12);
        }

        [Fact]
        public void NormalizedAssociatedLegendrePolynomials()
        {
            // Geodesy normalization: N_lm = sqrt((2-delta_0m)(2l+1)(l-m)!/(l+m)!)
            // P̄(0,0, theta): norm=sqrt(1*1*1/1)=1; P(0,0,cos(0.5))=1; result=1.0
            var expectedP00 = 1.0;
            Assert.Equal(expectedP00, LegendreFunctions.NormalizedAssociatedLegendre(0, 0, 0.5), 12);

            // P̄(1,0, theta): norm=sqrt(1*3*1/1)=sqrt(3); P(1,0,cos(0.5))=cos(0.5)
            var expectedP10 = System.Math.Sqrt(3.0) * System.Math.Cos(0.5);
            Assert.Equal(expectedP10, LegendreFunctions.NormalizedAssociatedLegendre(1, 0, 0.5), 12);

            // P̄(2,2, theta): norm=sqrt(2*5*0!/4!)=sqrt(10/24)=sqrt(5/12)
            // P(2,2,cos(0.5))=3*sin²(0.5)
            var expectedP22 = System.Math.Sqrt(5.0 / 12.0) * 3.0 * System.Math.Pow(System.Math.Sin(0.5), 2.0);
            Assert.Equal(expectedP22, LegendreFunctions.NormalizedAssociatedLegendre(2, 2, 0.5), 12);
        }

        [Fact]
        public void NormalizedAssociatedLegendreOddM()
        {
            // P̄(1,1, theta): norm=sqrt(2*3*0!/2!)=sqrt(3); P(1,1,cos(0.5))=sin(0.5)
            var expectedP11 = System.Math.Sqrt(3.0) * System.Math.Sin(0.5);
            Assert.Equal(expectedP11, LegendreFunctions.NormalizedAssociatedLegendre(1, 1, 0.5), 12);

            // P̄(2,1, theta): norm=sqrt(2*5*1!/3!)=sqrt(10/6)=sqrt(5/3)
            // P(2,1,cos(0.5))=3*cos(0.5)*sin(0.5)
            var expectedP21 = System.Math.Sqrt(5.0 / 3.0) * 3.0 * System.Math.Cos(0.5) * System.Math.Sin(0.5);
            Assert.Equal(expectedP21, LegendreFunctions.NormalizedAssociatedLegendre(2, 1, 0.5), 12);
        }

        [Fact]
        public void LegendreTableMatchesIndividualFunctions()
        {
            // Verify the table computation matches individual NormalizedAssociatedLegendre calls
            int maxDegree = 6;
            double theta = 0.8; // colatitude
            double phi = System.Math.PI / 2.0 - theta; // geocentric latitude
            double sinPhi = System.Math.Sin(phi);
            double cosPhi = System.Math.Cos(phi);

            var (P, dP) = LegendreFunctions.AllocateLegendreTable(maxDegree);
            LegendreFunctions.ComputeGeodesyNormalizedLegendreTable(maxDegree, sinPhi, cosPhi, P, dP);

            // The table computes P̄_nm(sinφ) where φ is latitude,
            // which equals P̄_nm(cos(θ)) where θ is colatitude.
            // NormalizedAssociatedLegendre(l, m, θ) computes P̄_lm at colatitude θ.
            for (int n = 0; n <= maxDegree; n++)
            {
                for (int m = 0; m <= n; m++)
                {
                    double expected = LegendreFunctions.NormalizedAssociatedLegendre(n, m, theta);
                    Assert.Equal(expected, P[n][m], 8);
                }
            }
        }

        [Fact]
        public void LegendreDerivativeNumericalValidation()
        {
            // Verify derivatives against numerical finite differences
            int maxDegree = 4;
            double phi = 0.3; // geocentric latitude
            double sinPhi = System.Math.Sin(phi);
            double cosPhi = System.Math.Cos(phi);

            var (P, dP) = LegendreFunctions.AllocateLegendreTable(maxDegree);
            LegendreFunctions.ComputeGeodesyNormalizedLegendreTable(maxDegree, sinPhi, cosPhi, P, dP);

            // Numerical derivative via central differences
            double h = 1e-7;
            var (Pp, _) = LegendreFunctions.AllocateLegendreTable(maxDegree);
            var (Pm, _) = LegendreFunctions.AllocateLegendreTable(maxDegree);
            var (dPp, _) = LegendreFunctions.AllocateLegendreTable(maxDegree);
            var (dPm, _) = LegendreFunctions.AllocateLegendreTable(maxDegree);

            LegendreFunctions.ComputeGeodesyNormalizedLegendreTable(maxDegree, System.Math.Sin(phi + h),
                System.Math.Cos(phi + h), Pp, dPp);
            LegendreFunctions.ComputeGeodesyNormalizedLegendreTable(maxDegree, System.Math.Sin(phi - h),
                System.Math.Cos(phi - h), Pm, dPm);

            for (int n = 1; n <= maxDegree; n++)
            {
                for (int m = 0; m <= n; m++)
                {
                    double numericalDeriv = (Pp[n][m] - Pm[n][m]) / (2.0 * h);
                    Assert.Equal(numericalDeriv, dP[n][m], 4);
                }
            }
        }

        [Fact]
        public void LegendreSouthernHemisphere()
        {
            // Verify that P̄_nm(-sinφ) has correct sign for odd-degree terms
            // For odd (n-m), P̄_nm(-sinφ) = -P̄_nm(sinφ) (antisymmetric)
            // For even (n-m), P̄_nm(-sinφ) = P̄_nm(sinφ) (symmetric)
            int maxDegree = 6;
            double phi = 0.5; // 0.5 radians latitude
            double sinPhi = System.Math.Sin(phi);
            double cosPhi = System.Math.Cos(phi);

            var (Pn, dPn) = LegendreFunctions.AllocateLegendreTable(maxDegree);
            LegendreFunctions.ComputeGeodesyNormalizedLegendreTable(maxDegree, sinPhi, cosPhi, Pn, dPn);

            var (Ps, dPs) = LegendreFunctions.AllocateLegendreTable(maxDegree);
            LegendreFunctions.ComputeGeodesyNormalizedLegendreTable(maxDegree, -sinPhi, cosPhi, Ps, dPs);

            for (int n = 0; n <= maxDegree; n++)
            {
                for (int m = 0; m <= n; m++)
                {
                    if ((n - m) % 2 == 0)
                    {
                        // Even (n-m): symmetric
                        Assert.Equal(Pn[n][m], Ps[n][m], 10);
                    }
                    else
                    {
                        // Odd (n-m): antisymmetric
                        Assert.Equal(Pn[n][m], -Ps[n][m], 10);
                    }
                }
            }
        }
    }
}
