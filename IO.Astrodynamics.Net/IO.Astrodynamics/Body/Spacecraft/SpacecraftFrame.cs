// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body.Spacecraft;

/// <summary>
/// The frame of a spacecraft. A kernel-backed spacecraft reads its orientation from SPICE (its frame kernel and CK), at
/// each call, without storing it. A managed spacecraft reads it from its attitude record, which only its propagation
/// fills (and <see cref="AddStateOrientationToICRF"/>): a read never adds an entry (#523).
/// </summary>
/// <remarks>
/// <para>
/// The record holds the rotation from the spacecraft frame to ICRF at each epoch (<c>vectorIcrf =
/// vectorInSpacecraft.Rotate(q)</c>). Between two entries sampled from the same continuous attitude law, the attitude is
/// the spherical linear interpolation of the two (exact, with no linear fallback), the interpolation of a type-3 CK
/// (#529, part 3). Otherwise the change is discrete: the earlier entry holds until the later one (#534).
/// </para>
/// <para>
/// <see cref="WriteOrientation"/> writes the record as SPICE defines a CK: the C-matrix, ICRF to the spacecraft frame
/// (the conjugate of the record's rotation, #529 part 1), in J2000, with the earlier attitude repeated
/// <see cref="DiscreteChangeLead"/> before each discrete change, so that the CK's interpolation of its single interval
/// is the record's attitude everywhere but in that lead.
/// </para>
/// </remarks>
public class SpacecraftFrame : Frame
{
    /// <summary>
    /// The lead before a discrete attitude change at which the CK repeats the earlier attitude: 1 µs, above the double
    /// resolution of the encoded spacecraft clock (about 1.2e-7 s at 65 536 ticks per second 21 years from its start).
    /// </summary>
    internal const double DiscreteChangeLead = 1.0e-6;

    private readonly IDataProvider _dataProvider;
    private readonly List<AttitudeEntry> _record = new();
    private readonly object _recordLock = new();

    public Spacecraft Spacecraft { get; }

    /// <summary>An entry of the attitude record: the rotation to ICRF, the continuous law it samples (null for a discrete one).</summary>
    private readonly record struct AttitudeEntry(StateOrientation Orientation, object Law);

    public SpacecraftFrame(Spacecraft spacecraft) : base(spacecraft.Name + "_FRAME", spacecraft.NaifId * 1000)
    {
        Spacecraft = spacecraft;
        _dataProvider = Configuration.Instance.DataProvider;
    }

    private protected override bool ReadsOrientationCache => false;

    /// <summary>
    /// The rotation from the spacecraft frame to ICRF at <paramref name="date"/>. For a managed spacecraft: the identity of
    /// ICRF before any propagation; the first entry before the record and the last after it; the interpolation of
    /// <see cref="SpacecraftFrame"/>'s remarks inside. The angular velocity follows <see cref="Frame.GetStateOrientationToICRF"/>:
    /// that of ICRF relative to the spacecraft, in the spacecraft frame.
    /// </summary>
    public override StateOrientation GetStateOrientationToICRF(Time date)
    {
        // Kernel-backed spacecraft: SPICE at each call (requires FK+CK kernels loaded), nothing stored
        if (Spacecraft.IsSpiceBacked)
            return _dataProvider.FrameTransformationToICRF(date, this);

        lock (_recordLock)
        {
            if (_record.Count == 0)
            {
                return new StateOrientation(Quaternion.Zero, Vector3.Zero, date, ICRF);
            }

            int index = Find(date);
            if (index >= 0)
            {
                var entry = _record[index].Orientation;
                return WithRate(entry.Rotation, NodeRate(index), date);
            }

            // The entry before the date, ~index - 1, and the one after, ~index; outside the record, the first or the last held
            int next = ~index;
            if (next == 0)
            {
                return At(_record[0].Orientation, date);
            }

            if (next == _record.Count)
            {
                return At(_record[^1].Orientation, date);
            }

            var before = _record[next - 1];
            var after = _record[next];
            double fromBefore = (date - before.Orientation.Epoch).TotalSeconds;
            double span = (after.Orientation.Epoch - before.Orientation.Epoch).TotalSeconds;
            if (Interpolates(before, after) || before.Orientation.Rotation == after.Orientation.Rotation)
            {
                double fraction = fromBefore / span;
                var (rotation, _) = Slerp(before.Orientation.Rotation, after.Orientation.Rotation, fraction, span);
                return WithRate(rotation, NodeRate(next - 1) * (1.0 - fraction) + NodeRate(next) * fraction, date);
            }

            // A discrete change: the earlier entry held; the rate of the CK, from the rate of the earlier record to zero at
            // the record DiscreteChangeLead before the change
            double hold = span - DiscreteChangeLead;
            var rate = fromBefore < hold ? NodeRate(next - 1) * (1.0 - fromBefore / hold) : Vector3.Zero;
            return WithRate(before.Orientation.Rotation, rate, date);
        }
    }

    // A rotation to ICRF with the rate of the spacecraft relative to ICRF in ICRF; the convention of the frames is that of
    // ICRF relative to the spacecraft, in the spacecraft frame
    private static StateOrientation WithRate(in Quaternion rotation, in Vector3 rate, in Time date) =>
        new(rotation, rate.Inverse().Rotate(rotation.Conjugate()), date, ICRF);

    // The rate of the interpolation over the interval (i - 1, i): zero for a hold or a discrete change
    private Vector3 IntervalRate(int i)
    {
        if (i <= 0 || i >= _record.Count || !Interpolates(_record[i - 1], _record[i]))
        {
            return Vector3.Zero;
        }

        double span = (_record[i].Orientation.Epoch - _record[i - 1].Orientation.Epoch).TotalSeconds;
        return Slerp(_record[i - 1].Orientation.Rotation, _record[i].Orientation.Rotation, 1.0, span).Rate;
    }

    // The angular velocity written with entry i (that of ckw03_c, the spacecraft relative to J2000 in J2000): the mean of the
    // rates of the interpolated intervals on either side, zero when neither is
    private Vector3 NodeRate(int i)
    {
        bool hasBefore = i > 0 && Interpolates(_record[i - 1], _record[i]);
        bool hasAfter = i + 1 < _record.Count && Interpolates(_record[i], _record[i + 1]);
        if (hasBefore && hasAfter) return (IntervalRate(i) + IntervalRate(i + 1)) * 0.5;
        if (hasBefore) return IntervalRate(i);
        if (hasAfter) return IntervalRate(i + 1);
        return Vector3.Zero;
    }

    // A held entry at another epoch: the same rotation, no angular velocity
    private static StateOrientation At(StateOrientation entry, in Time date) => new(entry.Rotation, Vector3.Zero, date, ICRF);

    private static bool Interpolates(in AttitudeEntry before, in AttitudeEntry after) => after.Law != null && ReferenceEquals(before.Law, after.Law);

    /// <summary>
    /// The exact spherical linear interpolation from <paramref name="q0"/> to <paramref name="q1"/> at
    /// <paramref name="fraction"/>, by the axis and angle of q1 q0⁻¹ (the interpolation of a type-3 CK), and the constant
    /// angular velocity of the spacecraft relative to ICRF over <paramref name="span"/> seconds, in ICRF.
    /// </summary>
    internal static (Quaternion Rotation, Vector3 Rate) Slerp(in Quaternion q0, in Quaternion q1, double fraction, double span)
    {
        var delta = (q1 * q0.Conjugate()).Normalize();
        if (delta.W < 0.0)
        {
            delta = delta * -1.0;
        }

        double sinHalf = delta.VectorPart.Magnitude();
        if (sinHalf == 0.0)
        {
            return (q0, Vector3.Zero);
        }

        double angle = 2.0 * System.Math.Atan2(sinHalf, delta.W);
        var axis = delta.VectorPart / sinHalf;
        var rotation = (new Quaternion(axis, angle * fraction) * q0).Normalize();
        return (rotation, axis * (angle / span));
    }

    // The index of the entry at the date, or the complement of the index of the first entry after it
    private int Find(in Time date)
    {
        int lo = 0, hi = _record.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int c = _record[mid].Orientation.Epoch.CompareTo(date);
            if (c == 0) return mid;
            if (c < 0) lo = mid + 1;
            else hi = mid - 1;
        }

        return ~lo;
    }

    /// <summary>
    /// Records an attitude of the propagation.
    /// </summary>
    /// <param name="orientation">The rotation from the spacecraft frame to its reference frame, an inertial frame,
    /// converted to ICRF.</param>
    /// <param name="law">The continuous attitude law it samples, or null for a discrete attitude (an attitude executed once,
    /// an impulse's, the initial one).</param>
    /// <param name="replace">Whether it replaces an entry at the same epoch: an attitude activated later in the maneuver
    /// chain does; a sample never replaces an activation.</param>
    /// <returns>Whether the entry was recorded.</returns>
    internal bool Record(StateOrientation orientation, object law, bool replace)
    {
        if (orientation == null) throw new ArgumentNullException(nameof(orientation));
        var entry = new AttitudeEntry(ToIcrf(orientation), law);
        lock (_recordLock)
        {
            int index = Find(entry.Orientation.Epoch);
            if (index >= 0)
            {
                if (!replace)
                {
                    return false;
                }

                _record[index] = entry;
                return true;
            }

            _record.Insert(~index, entry);
            return true;
        }
    }

    /// <summary>The latest entry held until <paramref name="end"/>, recorded there unless an entry is.</summary>
    internal void RecordEnd(in Time end)
    {
        lock (_recordLock)
        {
            if (_record.Count == 0)
            {
                return;
            }

            var latest = _record[^1];
            if (latest.Orientation.Epoch >= end)
            {
                return;
            }

            // The same rotation, sampled from the same law: the interpolation between the two is the hold
            _record.Add(new AttitudeEntry(new StateOrientation(latest.Orientation.Rotation, Vector3.Zero, end, ICRF), latest.Law));
        }
    }

    // An orientation relative to an inertial frame, made relative to ICRF; the rotation between two inertial frames is constant
    private static StateOrientation ToIcrf(StateOrientation orientation)
    {
        if (orientation.ReferenceFrame == ICRF)
        {
            return orientation;
        }

        var toIcrf = orientation.ReferenceFrame.ToFrame(ICRF, orientation.Epoch);
        return new StateOrientation((toIcrf.Rotation * orientation.Rotation).Normalize(), orientation.AngularVelocity.Rotate(toIcrf.Rotation), orientation.Epoch, ICRF);
    }

    /// <summary>
    /// Adds an orientation to the attitude record of a managed spacecraft, as a discrete attitude: held until the next
    /// entry. It replaces an entry at the same epoch.
    /// </summary>
    public override bool AddStateOrientationToICRF(StateOrientation stateOrientation)
    {
        return Record(stateOrientation, null, true);
    }

    public override StateOrientation GetLatestStateOrientationToICRF()
    {
        lock (_recordLock)
        {
            return _record.Count == 0 ? null : _record[^1].Orientation;
        }
    }

    /// <summary>The attitude record of a managed spacecraft, in epoch order (rotations to ICRF).</summary>
    public override IEnumerable<StateOrientation> GetStateOrientationsToICRF()
    {
        lock (_recordLock)
        {
            return _record.Select(x => x.Orientation).ToArray();
        }
    }

    public override void ClearStateOrientations()
    {
        lock (_recordLock)
        {
            _record.Clear();
        }
    }

    public async Task WriteAsync(FileInfo outputFile)
    {
        await using var stream = this.GetType().Assembly.GetManifestResourceStream("IO.Astrodynamics.Templates.FrameTemplate.tf");
        using StreamReader sr = new StreamReader(stream ?? throw new InvalidOperationException());
        var templateData = await sr.ReadToEndAsync();
        var data = templateData.Replace("{framename}", Name.ToUpper()).Replace("{frameid}", Id.ToString()).Replace("{spacecraftid}", Spacecraft.NaifId.ToString())
            .Replace("{spacecraftname}", Spacecraft.Name.ToUpper());
        await using var sw = new StreamWriter(outputFile.FullName);
        await sw.WriteAsync(data);
    }

    /// <summary>
    /// The records of the CK of the attitude record (<see cref="SpacecraftFrame"/>'s remarks): the C-matrix (ICRF to the
    /// spacecraft frame) in J2000, the earlier attitude repeated <see cref="DiscreteChangeLead"/> before each discrete
    /// change, and the angular velocity of the spacecraft relative to J2000 in J2000 (the convention of <c>ckw03_c</c>):
    /// at each record, the mean of the rates of the interpolated intervals on either side.
    /// </summary>
    internal StateOrientation[] CkRecords()
    {
        lock (_recordLock)
        {
            if (Spacecraft.IsSpiceBacked)
                throw new InvalidOperationException("Cannot write the orientation of a kernel-backed spacecraft.");
            if (_record.Count == 0)
                throw new InvalidOperationException($"Spacecraft {Spacecraft.Name} has no attitude to write: it has not been propagated.");

            var records = new List<StateOrientation>(_record.Count + 8);
            for (int i = 0; i < _record.Count; i++)
            {
                var entry = _record[i].Orientation;
                if (i > 0)
                {
                    double span = (entry.Epoch - _record[i - 1].Orientation.Epoch).TotalSeconds;
                    if (span < 2.0 * DiscreteChangeLead)
                        throw new InvalidOperationException(
                            $"The attitudes of {Spacecraft.Name} at {_record[i - 1].Orientation.Epoch} and {entry.Epoch} are {span:E3} s apart: a CK needs them at least {2.0 * DiscreteChangeLead:E1} s apart.");
                }

                if (i > 0 && !Interpolates(_record[i - 1], _record[i]) && _record[i - 1].Orientation.Rotation != entry.Rotation)
                {
                    var held = _record[i - 1].Orientation;
                    records.Add(new StateOrientation(held.Rotation.Conjugate(), Vector3.Zero, entry.Epoch.AddSeconds(-DiscreteChangeLead), ICRF));
                }

                records.Add(new StateOrientation(entry.Rotation.Conjugate(), NodeRate(i), entry.Epoch, ICRF));
            }

            return records.ToArray();
        }
    }

    /// <summary>
    /// Writes the attitude record of a managed spacecraft as a type-3 CK (<see cref="CkRecords"/>). The spacecraft clock
    /// kernel must be loaded.
    /// </summary>
    public void WriteOrientation(FileInfo outputFile, Spacecraft spacecraft)
    {
        SpiceAPI.Instance.WriteOrientation(outputFile, spacecraft.NaifId, CkRecords());
    }
}
