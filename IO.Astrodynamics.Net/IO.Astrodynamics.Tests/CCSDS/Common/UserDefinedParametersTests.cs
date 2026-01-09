// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.Common;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.Common
{
    public class UserDefinedParametersTests
    {
        [Fact]
        public void UserDefinedParameter_Constructor_Succeeds()
        {
            var param = new UserDefinedParameter("MISSION_NAME", "ISS");

            Assert.Equal("MISSION_NAME", param.Name);
            Assert.Equal("ISS", param.Value);
        }

        [Fact]
        public void UserDefinedParameter_WithNullName_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new UserDefinedParameter(null, "value"));
        }

        [Fact]
        public void UserDefinedParameter_WithEmptyName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new UserDefinedParameter("", "value"));
        }

        [Fact]
        public void UserDefinedParameter_WithWhitespaceName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new UserDefinedParameter("   ", "value"));
        }

        [Fact]
        public void UserDefinedParameter_WithNullValue_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new UserDefinedParameter("name", null));
        }

        [Fact]
        public void UserDefinedParameter_Equality_Works()
        {
            var param1 = new UserDefinedParameter("NAME", "VALUE");
            var param2 = new UserDefinedParameter("NAME", "VALUE");
            var param3 = new UserDefinedParameter("NAME", "OTHER");

            Assert.Equal(param1, param2);
            Assert.NotEqual(param1, param3);
            Assert.True(param1 == param2);
            Assert.True(param1 != param3);
        }

        [Fact]
        public void UserDefinedParameter_GetHashCode_ConsistentWithEquality()
        {
            var param1 = new UserDefinedParameter("NAME", "VALUE");
            var param2 = new UserDefinedParameter("NAME", "VALUE");

            Assert.Equal(param1.GetHashCode(), param2.GetHashCode());
        }

        [Fact]
        public void UserDefinedParameter_ToString_ReturnsNameEqualsValue()
        {
            var param = new UserDefinedParameter("MISSION", "APOLLO");

            Assert.Equal("MISSION=APOLLO", param.ToString());
        }

        [Fact]
        public void UserDefinedParameters_Constructor_WithNoParameters_CreatesEmpty()
        {
            var udp = new UserDefinedParameters();

            Assert.Empty(udp.Parameters);
            Assert.Empty(udp.Comments);
            Assert.False(udp.HasParameters);
        }

        [Fact]
        public void UserDefinedParameters_Constructor_WithParameters_Succeeds()
        {
            var parameters = new List<UserDefinedParameter>
            {
                new UserDefinedParameter("PARAM1", "VALUE1"),
                new UserDefinedParameter("PARAM2", "VALUE2")
            };
            var comments = new List<string> { "Test comment" };

            var udp = new UserDefinedParameters(parameters, comments);

            Assert.Equal(2, udp.Parameters.Count);
            Assert.Single(udp.Comments);
            Assert.True(udp.HasParameters);
        }

        [Fact]
        public void UserDefinedParameters_GetValue_ReturnsCorrectValue()
        {
            var parameters = new List<UserDefinedParameter>
            {
                new UserDefinedParameter("MISSION_NAME", "ISS"),
                new UserDefinedParameter("OPERATOR", "NASA")
            };
            var udp = new UserDefinedParameters(parameters);

            Assert.Equal("ISS", udp.GetValue("MISSION_NAME"));
            Assert.Equal("NASA", udp.GetValue("OPERATOR"));
        }

        [Fact]
        public void UserDefinedParameters_GetValue_CaseInsensitive()
        {
            var parameters = new List<UserDefinedParameter>
            {
                new UserDefinedParameter("MISSION_NAME", "ISS")
            };
            var udp = new UserDefinedParameters(parameters);

            Assert.Equal("ISS", udp.GetValue("mission_name"));
            Assert.Equal("ISS", udp.GetValue("Mission_Name"));
        }

        [Fact]
        public void UserDefinedParameters_GetValue_NotFound_ReturnsNull()
        {
            var udp = new UserDefinedParameters();

            Assert.Null(udp.GetValue("NONEXISTENT"));
        }

        [Fact]
        public void UserDefinedParameters_ToString_ReturnsCount()
        {
            var parameters = new List<UserDefinedParameter>
            {
                new UserDefinedParameter("P1", "V1"),
                new UserDefinedParameter("P2", "V2")
            };
            var udp = new UserDefinedParameters(parameters);

            Assert.Contains("Count=2", udp.ToString());
        }
    }
}
