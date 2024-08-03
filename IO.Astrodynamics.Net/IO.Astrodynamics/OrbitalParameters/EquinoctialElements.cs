using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.OrbitalParameters
{
    public class EquinoctialElements : OrbitalParameters, IEquatable<EquinoctialElements>
    {
        private double? _argumentOfPeriapsis;
        private double? _ascendingNode;
        private double? _eccentricAnomaly;
        private double? _eccentricity;
        private double? _inclination;
        private double? _meanAnomaly;
        private double? _semisMajorAxis;
        private double? _trueAnomaly;


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="p">P coefficient</param>
        /// <param name="f">F Coefficient</param>
        /// <param name="g">G Coefficient</param>
        /// <param name="h">H Coefficient</param>
        /// <param name="k">K Coefficient</param>
        /// <param name="l0">True longitude</param>
        /// <param name="observer">Center of motion</param>
        /// <param name="epoch">Epoch</param>
        /// <param name="frame">Reference frame</param>
        /// <returns></returns>
        public EquinoctialElements(double p, double f, double g, double h, double k, double l0, ILocalizable observer, DateTime epoch, Frame frame) : base(observer,
            epoch, frame)
        {
            P = p;
            F = f;
            G = g;
            H = h;
            K = k;
            L0 = l0;
        }

        public double P { get; }
        public double F { get; }
        public double G { get; }
        public double H { get; }
        public double K { get; }
        public double L0 { get; }

        public override double ArgumentOfPeriapsis()
        {
            _argumentOfPeriapsis ??= System.Math.Atan2(G * H - F * K, F * H + G * K);
            return _argumentOfPeriapsis.Value;
        }

        public override double AscendingNode()
        {
            _ascendingNode ??= System.Math.Atan(K / H);
            return _ascendingNode.Value;
        }

        public override double EccentricAnomaly()
        {
            if (_eccentricAnomaly.HasValue)
            {
                return _eccentricAnomaly.Value;
            }

            double v = TrueAnomaly();
            double e = Eccentricity();
            _eccentricAnomaly = 2 * System.Math.Atan((System.Math.Tan(v / 2.0)) / System.Math.Sqrt((1 + e) / (1 - e)));
            return _eccentricAnomaly.Value;
        }

        public override double Eccentricity()
        {
            _eccentricity ??= System.Math.Sqrt(F * F + G * G);
            return _eccentricity.Value;
        }

        public override double Inclination()
        {
            _inclination ??= System.Math.Atan2(2 * System.Math.Sqrt(H * H + K * K), 1 - H * H - K * K);
            return _inclination.Value;
        }

        public override double MeanAnomaly()
        {
            _meanAnomaly ??= EccentricAnomaly() - Eccentricity() * System.Math.Sin(EccentricAnomaly());
            return _meanAnomaly.Value;
        }

        public override double SemiMajorAxis()
        {
            _semisMajorAxis ??= P / (1 - F * F - G * G);
            return _semisMajorAxis.Value;
        }

        public override double TrueAnomaly()
        {
            _trueAnomaly ??= L0 - (AscendingNode() + ArgumentOfPeriapsis());
            return _trueAnomaly.Value;
        }

        public override EquinoctialElements ToEquinoctial()
        {
            return this;
        }

        public bool Equals(EquinoctialElements other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return P.Equals(other.P) && F.Equals(other.F) && G.Equals(other.G) && H.Equals(other.H) && K.Equals(other.K) && L0.Equals(other.L0) &&
                   Observer.NaifId == other.Observer.NaifId && Epoch == other.Epoch && Frame == other.Frame;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EquinoctialElements)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), P, F, G, H, K, L0);
        }

        public static bool operator ==(EquinoctialElements left, EquinoctialElements right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(EquinoctialElements left, EquinoctialElements right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return $"Epoch : {Epoch.ToFormattedString()} P : {P} F : {F} G : {G} H : {H} K {K} L0 : {L0} Frame : {Frame.Name}" ;
        }
    }
}