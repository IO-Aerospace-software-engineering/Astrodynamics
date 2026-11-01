// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Result of a segment integration, containing the propagation segment
/// and optionally an event that terminated the segment early.
/// </summary>
public record struct IntegrationResult(
    PropagationSegment Segment,
    EventInfo? Event);

/// <summary>
/// Information about an event detected during integration; <see cref="EventTime"/> is in seconds of the time argument
/// of the propagation from the base epoch of the segment: TT seconds about the Earth, TDB seconds about any other central
/// body (#501).
/// </summary>
public record struct EventInfo(
    int DetectorIndex,
    double EventTime,
    Vector3 EventPosition,
    Vector3 EventVelocity);
