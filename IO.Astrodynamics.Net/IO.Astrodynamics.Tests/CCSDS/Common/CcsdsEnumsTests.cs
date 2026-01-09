// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using IO.Astrodynamics.CCSDS.Common.Enums;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.Common
{
    public class CcsdsEnumsTests
    {
        [Theory]
        [InlineData(CcsdsTimeSystem.UTC, "UTC")]
        [InlineData(CcsdsTimeSystem.TAI, "TAI")]
        [InlineData(CcsdsTimeSystem.TDB, "TDB")]
        [InlineData(CcsdsTimeSystem.GPS, "GPS")]
        [InlineData(CcsdsTimeSystem.TT, "TT")]
        [InlineData(CcsdsTimeSystem.UT1, "UT1")]
        public void CcsdsTimeSystem_HasCorrectDescriptions(CcsdsTimeSystem timeSystem, string expectedDescription)
        {
            var description = GetEnumDescription(timeSystem);
            Assert.Equal(expectedDescription, description);
        }

        [Fact]
        public void CcsdsTimeSystem_ContainsAllExpectedValues()
        {
            var values = Enum.GetValues<CcsdsTimeSystem>();

            Assert.Contains(CcsdsTimeSystem.UTC, values);
            Assert.Contains(CcsdsTimeSystem.TAI, values);
            Assert.Contains(CcsdsTimeSystem.TDB, values);
            Assert.Contains(CcsdsTimeSystem.GPS, values);
            Assert.Contains(CcsdsTimeSystem.TT, values);
            Assert.Contains(CcsdsTimeSystem.TCB, values);
            Assert.Contains(CcsdsTimeSystem.TCG, values);
            Assert.Contains(CcsdsTimeSystem.GMST, values);
        }

        [Theory]
        [InlineData(CcsdsReferenceFrame.ICRF, "ICRF")]
        [InlineData(CcsdsReferenceFrame.TEME, "TEME")]
        [InlineData(CcsdsReferenceFrame.EME2000, "EME2000")]
        [InlineData(CcsdsReferenceFrame.ITRF93, "ITRF93")]
        [InlineData(CcsdsReferenceFrame.RTN, "RTN")]
        [InlineData(CcsdsReferenceFrame.TNW, "TNW")]
        public void CcsdsReferenceFrame_HasCorrectDescriptions(CcsdsReferenceFrame frame, string expectedDescription)
        {
            var description = GetEnumDescription(frame);
            Assert.Equal(expectedDescription, description);
        }

        [Fact]
        public void CcsdsReferenceFrame_ContainsAllExpectedValues()
        {
            var values = Enum.GetValues<CcsdsReferenceFrame>();

            Assert.Contains(CcsdsReferenceFrame.ICRF, values);
            Assert.Contains(CcsdsReferenceFrame.GCRF, values);
            Assert.Contains(CcsdsReferenceFrame.TEME, values);
            Assert.Contains(CcsdsReferenceFrame.EME2000, values);
            Assert.Contains(CcsdsReferenceFrame.ITRF, values);
            Assert.Contains(CcsdsReferenceFrame.RTN, values);
            Assert.Contains(CcsdsReferenceFrame.TNW, values);
            Assert.Contains(CcsdsReferenceFrame.Custom, values);
        }

        [Theory]
        [InlineData(MeanElementTheory.SGP4, "SGP4")]
        [InlineData(MeanElementTheory.SGP4XP, "SGP4-XP")]
        [InlineData(MeanElementTheory.DSST, "DSST")]
        [InlineData(MeanElementTheory.USM, "USM")]
        public void MeanElementTheory_HasCorrectDescriptions(MeanElementTheory theory, string expectedDescription)
        {
            var description = GetEnumDescription(theory);
            Assert.Equal(expectedDescription, description);
        }

        [Theory]
        [InlineData(ObjectType.Payload, "PAYLOAD")]
        [InlineData(ObjectType.RocketBody, "ROCKET BODY")]
        [InlineData(ObjectType.Debris, "DEBRIS")]
        [InlineData(ObjectType.Unknown, "UNKNOWN")]
        [InlineData(ObjectType.Other, "OTHER")]
        public void ObjectType_HasCorrectDescriptions(ObjectType objectType, string expectedDescription)
        {
            var description = GetEnumDescription(objectType);
            Assert.Equal(expectedDescription, description);
        }

        [Fact]
        public void AllEnumsHaveDescriptionAttributes()
        {
            AssertAllValuesHaveDescription<CcsdsTimeSystem>();
            AssertAllValuesHaveDescription<CcsdsReferenceFrame>();
            AssertAllValuesHaveDescription<MeanElementTheory>();
            AssertAllValuesHaveDescription<ObjectType>();
        }

        private static string GetEnumDescription<T>(T value) where T : Enum
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? value.ToString();
        }

        private static void AssertAllValuesHaveDescription<T>() where T : struct, Enum
        {
            foreach (var value in Enum.GetValues<T>())
            {
                var field = typeof(T).GetField(value.ToString());
                var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
                Assert.NotNull(attribute);
                Assert.False(string.IsNullOrEmpty(attribute.Description),
                    $"Enum value {typeof(T).Name}.{value} has empty description");
            }
        }
    }
}
