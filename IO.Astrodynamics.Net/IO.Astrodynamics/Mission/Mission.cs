

using System;

namespace IO.Astrodynamics.Mission
{
    public class Mission : IEquatable<Mission>
    {
        public Mission(string name) 
        {
            Name = name;
        }

        public string Name { get; }
        
        public bool Equals(Mission other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Mission)obj);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }

        public static bool operator ==(Mission left, Mission right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Mission left, Mission right)
        {
            return !Equals(left, right);
        }
    }
}