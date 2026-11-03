// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Kernels;

/// <summary>
/// A managed writer of SPK files of type 9 (Lagrange interpolation, unequal time steps), one segment per arc of states
/// (#540): the native writer, frozen, writes one segment per file, of degree 15. The file is a DAF in the little-endian
/// IEEE format (<c>LTL-IEEE</c>), as the DAF and SPK Required Reading of NAIF define it: a file record, one summary record
/// and one name record for each 25 segments, and one more after a full one, then the data of each segment, with no comment
/// area.
/// </summary>
/// <remarks>
/// <para>
/// A type 9 segment holds its N states (km, km/s), its N epochs (TDB seconds from J2000), the directory of every
/// hundredth epoch, the degree of the interpolating polynomials and N. SPICE reads the segments of a file in reverse
/// order: at an epoch where two arcs meet, the later segment, written last, gives the state. The degree of a segment of N
/// states is min(7, N - 1): a window of 8 states, the managed reader's (choice 15 of #554).
/// </para>
/// <para>
/// The layout of the file record, the FTP validation string and the segment are those of NAIF's DAF Required Reading
/// (daf.req) and SPK Required Reading (spk.req, "Type 9"), to be verified by S. Guillet; the files are checked by CSPICE
/// itself (<c>spkezr</c>, <c>dafus</c>, <c>dafgda</c>) against files that CSPICE's <c>spkw09</c> writes.
/// </para>
/// </remarks>
internal static class SpkWriter
{
    private const int RecordBytes = 1024;
    private const int RecordWords = 128;
    private const int Nd = 2;
    private const int Ni = 6;
    private const int SummaryWords = Nd + (Ni + 1) / 2; // 5
    private const int NameChars = 8 * SummaryWords; // 40
    private const int SummariesPerRecord = (RecordWords - 3) / SummaryWords; // 25
    private const int DirectoryStep = 100;
    private const int CharacterRecordChars = 1000;

    /// <summary>The largest degree of the polynomials: 7, a window of 8 states, the managed reader's.</summary>
    internal const int MaximumDegree = 7;

    /// <summary>
    /// Writes the bytes of a file built by <see cref="BuildType9"/> to <paramref name="file"/>, replacing it; its directory
    /// is created when it does not exist, as the native writer does.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> or <paramref name="bytes"/> is null.</exception>
    internal static void Write(FileInfo file, byte[] bytes)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        var directory = Path.GetDirectoryName(file.FullName);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(file.FullName, bytes);
    }

    /// <summary>The TDB seconds from J2000 of an epoch, as the file holds it: a double.</summary>
    internal static double Seconds(StateVector state) => state.Epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds;

    /// <summary>
    /// The bytes of the SPK of <paramref name="arcs"/>, each one segment of type 9, checked before anything is written: the
    /// arcs in time order, each of at least two states in strictly increasing epoch order, in SI units, relative to the
    /// centre of their first state; <paramref name="frameCode"/> the SPICE code of their frame (1 for J2000). An
    /// arc whose states are all one TDB second of the file (its epochs within the resolution of the double, 1.2e-7 s in
    /// 2024: an impulse that close to the start or the end of the propagation) cannot be a segment and is left out: SPICE
    /// reads there the neighbouring segment, which starts or ends at that second.
    /// </summary>
    /// <exception cref="ArgumentException">No arc, an arc of fewer than two states, or epochs not strictly increasing in
    /// an arc.</exception>
    internal static byte[] BuildType9(int body, int frameCode, IReadOnlyList<StateVector[]> arcs)
    {
        if (arcs == null || arcs.Count == 0) throw new ArgumentException("No states to write.", nameof(arcs));
        var written = new List<StateVector[]>(arcs.Count);
        for (int k = 0; k < arcs.Count; k++)
        {
            var arc = arcs[k];
            if (arc.Length < 2) throw new ArgumentException($"Arc {k} holds fewer than two states.", nameof(arcs));
            if (Seconds(arc[0]) == Seconds(arc[^1])) continue;
            written.Add(arc);
        }

        if (written.Count == 0) throw new ArgumentException("No states to write.", nameof(arcs));
        arcs = written;

        // A full summary record is followed by an empty one, as the DAF Required Reading has it and CSPICE writes it: its
        // append (dafena) needs it (review of PR 1 of #554, spk-F1)
        int summaryRecords = arcs.Count / SummariesPerRecord + 1;
        int firstDataRecord = 2 + 2 * summaryRecords;
        var data = new List<double>();
        var summaries = new List<(double Start, double End, int Center, int Begin, int Last, string Name)>();
        int nextAddress = (firstDataRecord - 1) * RecordWords + 1;
        for (int k = 0; k < arcs.Count; k++)
        {
            var arc = arcs[k];
            int n = arc.Length;
            var epochs = new double[n];
            for (int i = 0; i < n; i++)
            {
                epochs[i] = Seconds(arc[i]);
                if (i > 0 && epochs[i] <= epochs[i - 1])
                    throw new ArgumentException($"The epochs of arc {k} are not strictly increasing.", nameof(arcs));
            }

            int begin = nextAddress;
            foreach (var sv in arc)
            {
                data.Add(sv.Position.X * 1E-03);
                data.Add(sv.Position.Y * 1E-03);
                data.Add(sv.Position.Z * 1E-03);
                data.Add(sv.Velocity.X * 1E-03);
                data.Add(sv.Velocity.Y * 1E-03);
                data.Add(sv.Velocity.Z * 1E-03);
            }

            data.AddRange(epochs);
            for (int d = 1; d <= (n - 1) / DirectoryStep; d++)
            {
                data.Add(epochs[d * DirectoryStep - 1]);
            }

            data.Add(System.Math.Min(MaximumDegree, n - 1));
            data.Add(n);
            int words = 7 * n + (n - 1) / DirectoryStep + 2;
            nextAddress += words;
            summaries.Add((epochs[0], epochs[^1], arc[0].Observer.NaifId, begin, nextAddress - 1, $"Seg{k + 1}"));
        }

        int dataRecords = (data.Count + RecordWords - 1) / RecordWords;
        var bytes = new byte[(firstDataRecord - 1 + dataRecords) * RecordBytes];
        WriteFileRecord(bytes, 2, 2 + 2 * (summaryRecords - 1), nextAddress);
        for (int r = 0; r < summaryRecords; r++)
        {
            int summaryOffset = (1 + 2 * r) * RecordBytes;
            int nameOffset = summaryOffset + RecordBytes;
            int count = System.Math.Min(SummariesPerRecord, summaries.Count - r * SummariesPerRecord);
            WriteDouble(bytes, summaryOffset, r + 1 < summaryRecords ? 2 + 2 * (r + 1) : 0.0);
            WriteDouble(bytes, summaryOffset + 8, r > 0 ? 2 + 2 * (r - 1) : 0.0);
            WriteDouble(bytes, summaryOffset + 16, count);
            // A character record: 1 000 characters, blank where no name is, the rest of the record null, as CSPICE writes it
            Array.Fill(bytes, (byte)' ', nameOffset, CharacterRecordChars);
            for (int i = 0; i < count; i++)
            {
                var s = summaries[r * SummariesPerRecord + i];
                int o = summaryOffset + 24 + i * SummaryWords * 8;
                WriteDouble(bytes, o, s.Start);
                WriteDouble(bytes, o + 8, s.End);
                WriteInt(bytes, o + 16, body);
                WriteInt(bytes, o + 20, s.Center);
                WriteInt(bytes, o + 24, frameCode);
                WriteInt(bytes, o + 28, 9);
                WriteInt(bytes, o + 32, s.Begin);
                WriteInt(bytes, o + 36, s.Last);
                Encoding.ASCII.GetBytes(s.Name.PadRight(NameChars)).CopyTo(bytes, nameOffset + i * NameChars);
            }
        }

        int dataOffset = (firstDataRecord - 1) * RecordBytes;
        for (int i = 0; i < data.Count; i++)
        {
            WriteDouble(bytes, dataOffset + 8 * i, data[i]);
        }

        return bytes;
    }

    // The file record: identification, ND, NI, internal file name, first and last summary records, first free address,
    // binary format, and the FTP validation string between its null fillers
    private static void WriteFileRecord(byte[] bytes, int forward, int backward, int free)
    {
        Encoding.ASCII.GetBytes("DAF/SPK ").CopyTo(bytes, 0);
        WriteInt(bytes, 8, Nd);
        WriteInt(bytes, 12, Ni);
        Encoding.ASCII.GetBytes("IO.Astrodynamics".PadRight(60)).CopyTo(bytes, 16);
        WriteInt(bytes, 76, forward);
        WriteInt(bytes, 80, backward);
        WriteInt(bytes, 84, free);
        Encoding.ASCII.GetBytes("LTL-IEEE").CopyTo(bytes, 88);
        byte[] ftp =
        [
            (byte)'F', (byte)'T', (byte)'P', (byte)'S', (byte)'T', (byte)'R', (byte)':', 13, (byte)':', 10, (byte)':', 13, 10,
            (byte)':', 13, 0, (byte)':', 129, (byte)':', 16, 206, (byte)':', (byte)'E', (byte)'N', (byte)'D', (byte)'F',
            (byte)'T', (byte)'P'
        ];
        ftp.CopyTo(bytes, 699);
    }

    private static void WriteDouble(byte[] bytes, int offset, double value) =>
        BinaryPrimitives.WriteDoubleLittleEndian(new Span<byte>(bytes, offset, 8), value);

    private static void WriteInt(byte[] bytes, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(bytes, offset, 4), value);
}
