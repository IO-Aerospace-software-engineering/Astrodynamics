using Xunit;

namespace IO.Astrodynamics.Tests.Mission
{
    public class MissionTests
    {
        [Fact]
        public void Create()
        {
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("Mission1");
            Assert.Equal("Mission1", mission.Name);
        }

        [Fact]
        public void Equality()
        {
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("Mission1");
            Astrodynamics.Mission.Mission mission1 = new Astrodynamics.Mission.Mission("Mission1");
            Astrodynamics.Mission.Mission mission2 = new Astrodynamics.Mission.Mission("Mission2");
            Assert.False(mission2.Equals((object)mission));
            Assert.Equal(mission, mission1);
            Assert.False(mission.Equals(null));
            Assert.True(mission.Equals(mission));
            Assert.True(mission.Equals((object)mission));
            Assert.True(mission.Equals((object)mission1));
            Assert.False(mission.Equals((object)null));
            Assert.False(mission.Equals("null"));
            Assert.True(mission == mission1);
            Assert.True(mission != mission2);
        }
    }
}