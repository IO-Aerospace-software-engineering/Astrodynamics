// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IO.Astrodynamics.Physics;
using Xunit;

namespace IO.Astrodynamics.Tests.Physics;

/// <summary>
/// <see cref="GeopotentialModelReader"/> on the rows of <c>EGM2008_to70_TideFree</c> written with LF, CRLF or CR line
/// breaks, with or without a final line break, and with blank lines (#502): every coefficient is the one its row holds,
/// whatever the platform. The expected values are decoded from the text of each row.
/// </summary>
public class GeopotentialModelReaderTests
{
    private static readonly string ModelPath = Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree");

    [Theory]
    [InlineData("\n", "")]
    [InlineData("\r\n", "")]
    [InlineData("\r", "")]
    [InlineData("\n", "\n")]
    [InlineData("\r\n", "\r\n")]
    [InlineData("\r", "\r")]
    [InlineData("\n", "\n\n")]
    public void LineBreaks(string lineBreak, string end)
    {
        // Arrange: the rows joined by the line break, followed by the end of the file
        var rows = Rows();

        // Act
        var reader = Read(string.Join(lineBreak, rows) + end);

        // Assert
        AssertRows(reader, rows);
    }

    [Fact]
    public void BlankLines()
    {
        // Arrange: an empty line and a line of spaces between two rows, and a blank line before the first one
        var rows = Rows();
        var text = "\n" + string.Join("\n", rows.Take(10)) + "\n\n   \n" + string.Join("\n", rows.Skip(10)) + "\n";

        // Act
        var reader = Read(text);

        // Assert
        AssertRows(reader, rows);
    }

    private static string[] Rows()
    {
        var rows = File.ReadAllText(ModelPath).Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Every (n, m) of degrees 2 to 70: 3 + 4 + ... + 71 rows
        Assert.Equal(2553, rows.Length);
        return rows;
    }

    private static GeopotentialModelReader Read(string text)
    {
        return new GeopotentialModelReader(new StreamReader(new MemoryStream(Encoding.ASCII.GetBytes(text))));
    }

    private static void AssertRows(GeopotentialModelReader reader, string[] rows)
    {
        foreach (var row in rows)
        {
            var fields = row.Replace('D', 'E').Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var n = ushort.Parse(fields[0], CultureInfo.InvariantCulture);
            var m = ushort.Parse(fields[1], CultureInfo.InvariantCulture);
            var expected = new GeopotentialCoefficient(n, m, double.Parse(fields[2], CultureInfo.InvariantCulture), double.Parse(fields[3], CultureInfo.InvariantCulture),
                double.Parse(fields[4], CultureInfo.InvariantCulture), double.Parse(fields[5], CultureInfo.InvariantCulture));
            Assert.Equal(expected, reader.ReadCoefficient(n, m));
        }
    }
}
