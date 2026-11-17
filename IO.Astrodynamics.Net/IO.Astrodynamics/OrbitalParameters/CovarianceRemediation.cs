// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.OrbitalParameters;

/// <summary>
/// The result of <see cref="StateVector.WithRemediatedCovariance"/>: the state whose covariance a propagation of the
/// covariance accepts, whether it was remediated, and the defect that was repaired.
/// </summary>
/// <param name="State">The state to propagate: the state itself when its covariance is accepted as it is, otherwise a
/// new state with the same position, velocity, observer, epoch and frame, and the remediated covariance.</param>
/// <param name="Remediated">True when the covariance was remediated, false when it is returned as it is.</param>
/// <param name="SmallestCorrelationEigenvalue">The smallest eigenvalue of the correlation matrix of the covariance
/// given, over its positive variances (zero when none is): negative when it was remediated, at least −1e-12 of the trace
/// of that matrix otherwise. A remediated covariance's is at least −1e-3 of that trace in the frame of the state or in its
/// RTN frame: <see cref="StateVector.WithRemediatedCovariance"/> refuses a deeper defect (#443).</param>
public sealed record CovarianceRemediation(StateVector State, bool Remediated, double SmallestCorrelationEigenvalue);
