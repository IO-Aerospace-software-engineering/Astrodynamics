// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Reflection;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.Common
{
    public class CcsdsCenterNamesTests
    {
        public CcsdsCenterNamesTests()
        {
            TestKernels.EnsureSolarSystem();
        }

        [Fact]
        public void EveryEntry_IsTheSpiceNameOfItsId_AndCoversPlanetsAndMoons()
        {
            // The name SPICE gives each id (bodc2n through GetCelestialBodyInfo), independent of the managed table
            foreach (var (name, naifId) in CcsdsCenterNames.CanonicalNames)
            {
                Assert.Equal(name, SpiceAPI.Instance.GetCelestialBodyInfo(naifId).Name, ignoreCase: true);
                Assert.True(CcsdsCenterNames.TryGetNaifId(name, out var id));
                Assert.Equal(naifId, id);
            }

            // Every planet and moon of the library, the barycenters and the Sun are in the table
            var planetsAndMoons = typeof(PlanetsAndMoons).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(NaifObject))
                .Select(f => (NaifObject)f.GetValue(null))
                .ToArray();
            Assert.Equal(161, planetsAndMoons.Length);
            foreach (var body in planetsAndMoons)
            {
                Assert.True(CcsdsCenterNames.TryGetNaifId(body.Name, out var id), body.Name);
                Assert.Equal(body.NaifId, id);
            }

            for (int naifId = 0; naifId <= 10; naifId++)
            {
                Assert.Contains(CcsdsCenterNames.CanonicalNames, e => e.NaifId == naifId);
            }

            Assert.Equal(172, CcsdsCenterNames.CanonicalNames.Count);
            Assert.Equal(CcsdsCenterNames.CanonicalNames.Count, CcsdsCenterNames.CanonicalNames.Select(e => e.NaifId).Distinct().Count());
        }

        // NAIF's built-in synonyms (NAIF IDs Required Reading, "Barycenters"), measured through bodn2c_ of CSPICE N0067
        [Theory]
        [InlineData("SSB", 0)]
        [InlineData("SOLAR_SYSTEM_BARYCENTER", 0)]
        [InlineData("MERCURY_BARYCENTER", 1)]
        [InlineData("VENUS_BARYCENTER", 2)]
        [InlineData("EARTH_BARYCENTER", 3)]
        [InlineData("EMB", 3)]
        [InlineData("EARTH MOON BARYCENTER", 3)]
        [InlineData("EARTH-MOON BARYCENTER", 3)]
        [InlineData("MARS_BARYCENTER", 4)]
        [InlineData("JUPITER_BARYCENTER", 5)]
        [InlineData("SATURN_BARYCENTER", 6)]
        [InlineData("URANUS_BARYCENTER", 7)]
        [InlineData("NEPTUNE_BARYCENTER", 8)]
        [InlineData("PLUTO_BARYCENTER", 9)]
        [InlineData("\tearth moon barycenter ", 3)]
        public void Synonym_MapsToNaifId(string name, int naifId)
        {
            Assert.True(CcsdsCenterNames.TryGetNaifId(name, out var id));
            Assert.Equal(naifId, id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("DSS-13")]
        [InlineData("GEOCENTER")]
        [InlineData("PLUTO BARYCENTER X")]
        public void UnknownName_IsNotFound(string name)
        {
            Assert.False(CcsdsCenterNames.TryGetNaifId(name, out _));
        }
    }
}
