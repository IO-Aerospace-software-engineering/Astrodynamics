using System;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver
{
    public class PlaneAlignmentManeuver : ImpulseManeuver
    {
        /// <summary>
        /// The node the last run locked, true for the ascending node: the last run's value, cleared by the re-arm of the
        /// next run with <see cref="EventCrossingDirection"/> (decision 52 of #668, decided by S. Guillet, 2026-11-23); null
        /// before.
        /// </summary>
        public bool? IsAscendingNode { get; private set; }

        /// <summary>
        /// The relative inclination of the last execution, a side result of the execution. Internal since 11.0.0
        /// (decision 51 of #668, decided by S. Guillet, 2026-11-23): nothing read it, and the re-arm left it.
        /// </summary>
        internal double RelativeInclination { get; private set; }

        private CrossingDirection _crossingDirection = CrossingDirection.Any;

        public PlaneAlignmentManeuver(Time minimumEpoch, TimeSpan maneuverHoldDuration, OrbitalParameters.OrbitalParameters targetOrbit,
            Engine engine) : base(minimumEpoch, maneuverHoldDuration, targetOrbit, engine)
        {
        }

        /// <summary>
        /// The distance to the target plane below which the node is taken by the rate of change of g rather than by its
        /// sign, relative to the distance to the center.
        /// </summary>
        private const double NodeThreshold = 1e-6;

        /// <summary>
        /// g = r · ĥ_target : height above target orbital plane.
        /// Zero at both ascending and descending nodes.
        /// CrossingDirection distinguishes which node triggers the maneuver.
        /// </summary>
        /// <remarks>
        /// Pure (decision 50 of #668, decided by S. Guillet, 2026-11-23): before 11.0.0 its first call locked the node and
        /// the crossing direction (#802). A propagation locks them through its detector, by the rule of
        /// <see cref="NodeAt"/>.
        /// </remarks>
        public override double ComputeEventValue(StateVector localState)
        {
            var targetH = TargetOrbit.SpecificAngularMomentum().NormalizeOrZero();
            return localState.Position.Dot(targetH);
        }

        /// <inheritdoc />
        /// <remarks>On the first evaluation of a run, the node the rule of <see cref="NodeAt"/> gives is locked, with the
        /// crossing direction.</remarks>
        internal override double ComputeEventValueInRun(StateVector localState)
        {
            if (IsAscendingNode == null)
            {
                LockNode(NodeAt(localState));
            }

            return ComputeEventValue(localState);
        }

        /// <summary>
        /// The node a state waits for, true for the ascending node: within the threshold of the target plane, the node of
        /// the rate of change of g (dg/dt = v · ĥ_target positive: ascending); otherwise the next node, that of the sign of g
        /// (above the plane, the descending node).
        /// </summary>
        private bool NodeAt(StateVector localState)
        {
            var targetH = TargetOrbit.SpecificAngularMomentum().NormalizeOrZero();
            double g = localState.Position.Dot(targetH);

            // Ascending node: spacecraft crosses from below to above target plane (g: - → +, NegToPos)
            // Descending node: spacecraft crosses from above to below target plane (g: + → -, PosToNeg)
            double threshold = NodeThreshold * localState.Position.Magnitude();

            if (System.Math.Abs(g) < threshold)
            {
                // At/near the node: use rate of change dg/dt = v · ĥ_target
                // Positive dg/dt → ascending through the plane → ascending node
                // Negative dg/dt → descending through the plane → descending node
                double dgdt = localState.Velocity.Dot(targetH);
                return dgdt > 0;
            }

            // Above target plane: next zero-crossing going negative is descending node
            // Below target plane: next zero-crossing going positive is ascending node
            return !(g > 0);
        }

        /// <inheritdoc />
        /// <remarks>
        /// At the start of a search, the state at the minimum epoch (#569), the node is the one the spacecraft moves toward:
        /// within the threshold of a node it moves away from (g · dg/dt &gt; 0), that node is passed, and the search takes
        /// the next node, that of the sign of g. Otherwise the rule of <see cref="NodeAt"/>, which keeps the node
        /// by dg/dt within the threshold, as a state handed to the maneuver at its node needs.
        /// </remarks>
        internal override double ComputeEventValueAtSearchStart(StateVector localState)
        {
            if (IsAscendingNode == null)
            {
                var targetH = TargetOrbit.SpecificAngularMomentum().NormalizeOrZero();
                double g = localState.Position.Dot(targetH);
                double dgdt = localState.Velocity.Dot(targetH);
                if (System.Math.Abs(g) < NodeThreshold * localState.Position.Magnitude() && g * dgdt > 0.0)
                {
                    LockNode(g < 0.0);
                }
            }

            return ComputeEventValueInRun(localState);
        }

        // The node the maneuver waits for, and the direction of its crossing
        private void LockNode(bool ascending)
        {
            IsAscendingNode = ascending;
            _crossingDirection = ascending ? CrossingDirection.NegativeToPositive : CrossingDirection.PositiveToNegative;
        }

        public override CrossingDirection EventCrossingDirection => _crossingDirection;

        /// <inheritdoc />
        /// <remarks>Pure (decision 50 of #668): the node of the rule of <see cref="NodeAt"/> at the state, never the node a
        /// run locked; at the state of its crossing in a run, the two agree.</remarks>
        protected override Vector3 Execute(StateVector stateVector) => DeltaVAt(stateVector, NodeAt(stateVector)).DeltaV;

        /// <inheritdoc />
        /// <remarks>The node the run locked, and the relative inclination kept.</remarks>
        internal override Vector3 ExecuteInRun(StateVector localState)
        {
            if (IsAscendingNode is null)
            {
                throw new InvalidOperationException("Maneuver point undefined");
            }

            var (deltaV, relativeInclination) = DeltaVAt(localState, IsAscendingNode.Value);
            RelativeInclination = relativeInclination;
            return deltaV;
        }

        // The delta-V at a node, and the relative inclination: the pure core of the execution (decision 50 of #668)
        private (Vector3 DeltaV, double RelativeInclination) DeltaVAt(StateVector stateVector, bool ascendingNode)
        {
            var vel = stateVector.Velocity;
            var pos = stateVector.Position;

            //Project vector
            var projectedVector = vel - (pos * (vel.Dot(pos) / pos.Dot(pos)));

            //Compute relative inclination
            double relativeInclination = System.Math.Acos(System.Math.Cos(stateVector.Inclination()) * System.Math.Cos(TargetOrbit.Inclination()) +
                                                          (System.Math.Sin(stateVector.Inclination()) * System.Math.Sin(TargetOrbit.Inclination())) *
                                                          System.Math.Cos(TargetOrbit.AscendingNode() - stateVector.AscendingNode()));

            double rotationAngle = Constants.HalfPi + relativeInclination * 0.5;

            if (ascendingNode)
            {
                rotationAngle = -rotationAngle;
            }

            //Compute deltaV
            var deltaV = 2.0 * projectedVector.Magnitude() * System.Math.Sin(relativeInclination * 0.5);

            //Compute the quaternion
            var q = Quaternion.FromAxisAngle(pos.NormalizeOrZero(), rotationAngle);

            //Rotate velocity vector
            var rotateVecor = projectedVector.NormalizeOrZero().Rotate(q);

            //Compute delta V vector
            return (rotateVecor * deltaV, relativeInclination);
        }

        /// <summary>
        /// Clears the node the last run locked and its crossing direction, back to <see cref="CrossingDirection.Any"/>
        /// (#784: before 11.0.0 the direction was left, so a re-armed maneuver waited for the crossing of the last run's
        /// node), and the relative inclination.
        /// </summary>
        internal override void Reset()
        {
            IsAscendingNode = null;
            _crossingDirection = CrossingDirection.Any;
            RelativeInclination = 0.0;
            base.Reset();
        }
    }
}