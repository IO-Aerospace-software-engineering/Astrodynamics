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
/// vectorInSpacecraft.Rotate(q)</c>), labelled with the spacecraft frame, its source. Between two entries sampled from the
/// same continuous attitude law, the attitude is the spherical linear interpolation of the two (exact, with no linear
/// fallback), the interpolation of a type-3 CK (#529, part 3). Otherwise the change is discrete: the earlier entry holds
/// until the later one (#534). Before a discrete change that follows a continuous law, the propagation samples the law
/// once more, <see cref="DiscreteChangeLead"/> before the change, so that the law is interpolated up to it. Two entries
/// within <see cref="SameEpochTolerance"/> are one epoch: an attitude activated later in the maneuver chain replaces the
/// earlier one, a sample never replaces an activation.
/// </para>
/// <para>
/// <see cref="WriteOrientation"/> writes the record as SPICE defines a CK: the C-matrix, ICRF to the spacecraft frame
/// (the conjugate of the record's rotation, #529 part 1), in J2000, with the earlier attitude repeated
/// <see cref="DiscreteChangeLead"/> before each discrete change (the law's last sample, when the propagation took one
/// there), so that the CK's interpolation of its single interval is the record's attitude everywhere but in that lead.
/// </para>
/// </remarks>
public class SpacecraftFrame : Frame
{
    /// <summary>
    /// The lead before a discrete attitude change at which the CK repeats the earlier attitude: 1 µs. The CK's epochs are
    /// the encoded SCLK of the spacecraft's clock, a double counting 65 536 ticks per second from 1957-01-01
    /// (Templates/ClockTemplate.tsc), whose spacing is 2.4e-7 s up to 2^47 ticks (2025-01-19 03:13 TDB) and 4.8e-7 s from
    /// then to the end of its partition (2^48 ticks, 2093): the lead is 2.1 spacings from 2025-01-19 (4.2 before), so the
    /// records stay strictly increasing.
    /// </summary>
    internal const double DiscreteChangeLead = 1.0e-6;

    /// <summary>
    /// Two entries of the record closer than this, 2 µs (twice <see cref="DiscreteChangeLead"/>), are one epoch: a record
    /// repeated <see cref="DiscreteChangeLead"/> before an entry then follows the previous one by at least
    /// <see cref="DiscreteChangeLead"/>. The propagation itself can sample an output within this of an activation off the TT
    /// grid of the outputs (#501): a minimum epoch given as a TDB label, off it by the change of TDB − TT from the start
    /// (6.0e-7 s at 3 400 s); before #547, an attitude chained after a held one, its hold counted in TDB seconds, 1e-7 to
    /// 2e-7 s from the sample.
    /// </summary>
    internal const double SameEpochTolerance = 2.0 * DiscreteChangeLead;

    private readonly IDataProvider _dataProvider;
    private readonly List<AttitudeEntry> _record = new();

    // No region of this lock calls SPICE: an ephemeris read in this frame rotates by the record under the lock of the SPICE
    // calls (SpiceAPI.ManagedReadFrame, #682), which this order keeps free of a lock inversion
    private readonly object _recordLock = new();

    public Spacecraft Spacecraft { get; }

    /// <summary>
    /// An entry of the attitude record: the rotation to ICRF, the continuous law it samples (null for a discrete one), and
    /// whether it is the sample of that law <see cref="DiscreteChangeLead"/> before the discrete change that follows it.
    /// </summary>
    private readonly record struct AttitudeEntry(StateOrientation Orientation, object Law, bool IsLead = false);

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
                return new StateOrientation(Quaternion.Identity, Vector3.Zero, date, this);
            }

            int index = Find(date);
            if (index >= 0)
            {
                return EntryAt(index);
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

    // Entry i as the reads return it: its rotation, with the angular velocity of the CK at its record (NodeRate), in the
    // convention of the frames
    private StateOrientation EntryAt(int i) => WithRate(_record[i].Orientation.Rotation, NodeRate(i), _record[i].Orientation.Epoch);

    // A rotation to ICRF with the rate of the spacecraft relative to ICRF in ICRF; the convention of the frames is that of
    // ICRF relative to the spacecraft, in the spacecraft frame, the source of the rotation and its label
    private StateOrientation WithRate(in Quaternion rotation, in Vector3 rate, in Time date) =>
        new(rotation, rate.Inverse().Rotate(rotation.Conjugate()), date, this);

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
    private StateOrientation At(StateOrientation entry, in Time date) => new(entry.Rotation, Vector3.Zero, date, this);

    private static bool Interpolates(in AttitudeEntry before, in AttitudeEntry after) => after.Law != null && ReferenceEquals(before.Law, after.Law);

    /// <summary>
    /// The exact spherical linear interpolation from <paramref name="q0"/> to <paramref name="q1"/> at
    /// <paramref name="fraction"/>, by the axis and angle of q1 q0⁻¹ (the interpolation of a type-3 CK), and the constant
    /// angular velocity of the spacecraft relative to ICRF over <paramref name="span"/> seconds, in ICRF.
    /// </summary>
    internal static (Quaternion Rotation, Vector3 Rate) Slerp(in Quaternion q0, in Quaternion q1, double fraction, double span)
    {
        var (rotation, axis, angle) = Quaternion.SlerpByAxisAngle(q0, q1, fraction);
        return angle == 0.0 ? (rotation, Vector3.Zero) : (rotation, axis * (angle / span));
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
    /// <param name="replace">Whether it replaces the entries at the same epoch, within <see cref="SameEpochTolerance"/>:
    /// an attitude activated later in the maneuver chain does, and is kept at its own epoch; a sample never replaces an
    /// activation, and is dropped.</param>
    /// <returns>Whether the entry was recorded.</returns>
    internal bool Record(StateOrientation orientation, object law, bool replace)
    {
        if (orientation == null) throw new ArgumentNullException(nameof(orientation));
        return Insert(new AttitudeEntry(ToEntry(orientation), law), replace);
    }

    // Inserts an entry under the rules of the shared epoch (Record)
    private bool Insert(in AttitudeEntry entry, bool replace)
    {
        lock (_recordLock)
        {
            var (first, end) = Near(entry.Orientation.Epoch);
            if (end > first)
            {
                if (!replace)
                {
                    return false;
                }

                _record.RemoveRange(first, end - first);

                // A lead whose change was replaced is an ordinary sample of its law, at least SameEpochTolerance before the entry
                if (first > 0 && _record[first - 1].IsLead)
                {
                    _record[first - 1] = _record[first - 1] with { IsLead = false };
                }
            }

            _record.Insert(first, entry);
            return true;
        }
    }

    /// <summary>
    /// The epochs of the discrete changes that follow a sample of <paramref name="law"/> by at least
    /// <see cref="SameEpochTolerance"/> plus <see cref="DiscreteChangeLead"/>: where the law holds its last sample over a
    /// partial step unless it is sampled once more before the change (<see cref="RecordLead"/>).
    /// </summary>
    internal Time[] ChangesAfterSamplesOf(object law)
    {
        if (law == null) throw new ArgumentNullException(nameof(law));
        lock (_recordLock)
        {
            var changes = new List<Time>();
            for (int i = 1; i < _record.Count; i++)
            {
                if (LeadFits(i, law))
                {
                    changes.Add(_record[i].Orientation.Epoch);
                }
            }

            return changes.ToArray();
        }
    }

    // Whether entry i is a discrete change after a sample of the law far enough before it for a lead
    private bool LeadFits(int i, object law)
    {
        var before = _record[i - 1];
        return ReferenceEquals(before.Law, law) && !before.IsLead && !Interpolates(before, _record[i]) &&
               (_record[i].Orientation.Epoch - before.Orientation.Epoch).TotalSeconds >= SameEpochTolerance + DiscreteChangeLead;
    }

    /// <summary>
    /// Records <paramref name="orientation"/>, the sample of <paramref name="law"/> on the state before the discrete change at
    /// <paramref name="change"/>, at <paramref name="change"/> less <see cref="DiscreteChangeLead"/>: the interpolation of
    /// the law then runs up to the change, and that sample is the record the CK repeats before it (#529 part 3, #534).
    /// </summary>
    /// <returns>Whether the sample was recorded: the change is in the record, after a sample of the law
    /// (<see cref="ChangesAfterSamplesOf"/>).</returns>
    internal bool RecordLead(StateOrientation orientation, object law, in Time change)
    {
        if (orientation == null) throw new ArgumentNullException(nameof(orientation));
        var entry = new AttitudeEntry(ToEntry(orientation) with { Epoch = change.AddSeconds(-DiscreteChangeLead) }, law, true);
        lock (_recordLock)
        {
            int index = Find(change);
            if (index <= 0 || !LeadFits(index, law))
            {
                return false;
            }

            _record.Insert(index, entry);
            return true;
        }
    }

    // The range [First, End) of the entries within SameEpochTolerance of the date: First is also where an entry at the date
    // goes once they are removed
    private (int First, int End) Near(in Time date)
    {
        var from = date.AddSeconds(-SameEpochTolerance);
        var to = date.AddSeconds(SameEpochTolerance);
        int first = LowerBound(from, strict: true);
        int end = LowerBound(to, strict: false);
        return (first, System.Math.Max(first, end));
    }

    // The index of the first entry after the date (strict), or at or after it
    private int LowerBound(in Time date, bool strict)
    {
        int lo = 0, hi = _record.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            int c = _record[mid].Orientation.Epoch.CompareTo(date);
            if (c < 0 || (strict && c == 0)) lo = mid + 1;
            else hi = mid;
        }

        return lo;
    }

    /// <summary>The latest entry held until <paramref name="end"/>, recorded there unless an entry is, within
    /// <see cref="SameEpochTolerance"/>.</summary>
    internal void RecordEnd(in Time end)
    {
        lock (_recordLock)
        {
            if (_record.Count == 0)
            {
                return;
            }

            var latest = _record[^1];
            if (latest.Orientation.Epoch > end.AddSeconds(-SameEpochTolerance))
            {
                return;
            }

            // The same rotation, sampled from the same law: the interpolation between the two is the hold
            _record.Add(new AttitudeEntry(new StateOrientation(latest.Orientation.Rotation, Vector3.Zero, end, this), latest.Law));
        }
    }

    // An attitude of the propagation, the rotation from the spacecraft frame to an inertial frame labelled with that frame
    // (Attitude.StateOrientation), as an entry: the rotation to ICRF (the rotation between two inertial frames is
    // constant), labelled with the spacecraft frame, its source (choice 9 of #537). The entry keeps no angular velocity:
    // every read gives that of the CK's model (NodeRate, choice 8)
    private StateOrientation ToEntry(StateOrientation orientation)
    {
        if (orientation.ReferenceFrame == ICRF)
        {
            return new StateOrientation(orientation.Rotation, Vector3.Zero, orientation.Epoch, this);
        }

        var toIcrf = orientation.ReferenceFrame.ToFrame(ICRF, orientation.Epoch);
        return new StateOrientation((toIcrf.Rotation * orientation.Rotation).Normalize(), Vector3.Zero, orientation.Epoch, this);
    }

    /// <summary>
    /// Adds an orientation to the attitude record of a managed spacecraft, as a discrete attitude: held until the next
    /// entry. It replaces the entries at the same epoch, within 2 µs.
    /// </summary>
    /// <param name="stateOrientation">The rotation from the spacecraft frame to ICRF, labelled with the spacecraft frame,
    /// its source, as <see cref="Frame.GetStateOrientationToICRF"/> returns it. Its angular velocity is not kept: the reads
    /// give the record's (<see cref="GetStateOrientationToICRF"/>), zero at a discrete entry.</param>
    /// <exception cref="ArgumentException">The orientation is labelled with another frame.</exception>
    public override bool AddStateOrientationToICRF(StateOrientation stateOrientation)
    {
        if (stateOrientation == null) throw new ArgumentNullException(nameof(stateOrientation));
        if (stateOrientation.ReferenceFrame != this)
            throw new ArgumentException(
                $"The orientation of {Spacecraft.Name} to ICRF is labelled with its frame, {Name}, the source of the rotation, not {stateOrientation.ReferenceFrame?.Name}.",
                nameof(stateOrientation));
        return Insert(new AttitudeEntry(stateOrientation with { AngularVelocity = Vector3.Zero }, null), true);
    }

    /// <summary>
    /// Whether the attitude record of a managed spacecraft holds an entry: false for a spacecraft never propagated, with no
    /// orientation added (<see cref="AddStateOrientationToICRF"/>), built from a TLE or propagated by the TLE propagator,
    /// whose reads give the identity of ICRF; always true for a kernel-backed spacecraft, which reads its CK (#682).
    /// </summary>
    internal bool HasAttitude
    {
        get
        {
            if (Spacecraft.IsSpiceBacked)
                return true;
            lock (_recordLock)
            {
                return _record.Count > 0;
            }
        }
    }

    /// <summary>The last entry of the attitude record of a managed spacecraft, null before any propagation: as
    /// <see cref="GetStateOrientationToICRF"/> returns it at its epoch, the angular velocity included.</summary>
    public override StateOrientation GetLatestStateOrientationToICRF()
    {
        lock (_recordLock)
        {
            return _record.Count == 0 ? null : EntryAt(_record.Count - 1);
        }
    }

    /// <summary>The attitude record of a managed spacecraft, in epoch order: rotations to ICRF labelled with the spacecraft
    /// frame, their source, each as <see cref="GetStateOrientationToICRF"/> returns it at its epoch, with the angular
    /// velocity of the CK at its record in the convention of the frames (zero at a discrete entry).</summary>
    public override IEnumerable<StateOrientation> GetStateOrientationsToICRF()
    {
        lock (_recordLock)
        {
            var entries = new StateOrientation[_record.Count];
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = EntryAt(i);
            }

            return entries;
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
                // A lead is the CK's record before its change, DiscreteChangeLead earlier
                bool afterLead = i > 0 && _record[i - 1].IsLead;
                if (i > 0 && !afterLead)
                {
                    double span = (entry.Epoch - _record[i - 1].Orientation.Epoch).TotalSeconds;
                    if (span < SameEpochTolerance)
                        throw new InvalidOperationException(
                            $"The attitudes of {Spacecraft.Name} at {_record[i - 1].Orientation.Epoch} and {entry.Epoch} are {span:E3} s apart: a CK needs them at least {SameEpochTolerance:E1} s apart.");
                }

                if (i > 0 && !afterLead && !Interpolates(_record[i - 1], _record[i]) && _record[i - 1].Orientation.Rotation != entry.Rotation)
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
