// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The probe of NamingRuleTests.Violations_ReportANamespaceAndANewOverload (review of lot L0 of #668): a namespace with
// an underscore and an acronym of three capitals, and two overloads of a name that breaks N1.
namespace IO.Astrodynamics.Tests.ApiConventions.Probes.TDM_Probe
{
    public static class TdmReader
    {
        public static double GetTotalISP() => 0.0;

        public static double GetTotalISP(int probeUTCValue) => probeUTCValue;
    }
}
