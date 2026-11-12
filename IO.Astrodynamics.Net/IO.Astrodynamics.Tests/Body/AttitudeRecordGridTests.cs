using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// Part 1 of the cases of <see cref="AttitudeRecordTests"/>: those whose index modulo <see cref="AttitudeRecordTests.Parts"/>
/// is 1, checked by <see cref="AttitudeRecordTests.Check"/>.
/// </summary>
/// <remarks>
/// xUnit runs the tests of a class one after the other: the cases of one class ran from 21 s to 841 s of the Linux CI run
/// 43256478324 (2026-11-12), the critical path; split over four classes, they run in parallel (see
/// <see cref="AttitudeRecordTests.Parts"/>). The same test names, cases, assertions, limits and categories.
/// </remarks>
public class AttitudeRecordGridPart1Tests
{
    private readonly ITestOutputHelper _output;

    public AttitudeRecordGridPart1Tests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    [Theory]
    [MemberData(nameof(AttitudeRecordTests.Worst), 1, MemberType = typeof(AttitudeRecordTests))]
    public void WorstCase_MatchesSpice(string key) => AttitudeRecordTests.Check(key, _output);

    [Theory]
    [Trait("Category", "Validation")]
    [MemberData(nameof(AttitudeRecordTests.Grid), 1, MemberType = typeof(AttitudeRecordTests))]
    public void Case_MatchesSpice(string key) => AttitudeRecordTests.Check(key, _output);
}

/// <summary>
/// Part 2 of the cases of <see cref="AttitudeRecordTests"/>: those whose index modulo <see cref="AttitudeRecordTests.Parts"/>
/// is 2, checked by <see cref="AttitudeRecordTests.Check"/>.
/// </summary>
/// <remarks>See <see cref="AttitudeRecordGridPart1Tests"/>.</remarks>
public class AttitudeRecordGridPart2Tests
{
    private readonly ITestOutputHelper _output;

    public AttitudeRecordGridPart2Tests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    [Theory]
    [MemberData(nameof(AttitudeRecordTests.Worst), 2, MemberType = typeof(AttitudeRecordTests))]
    public void WorstCase_MatchesSpice(string key) => AttitudeRecordTests.Check(key, _output);

    [Theory]
    [Trait("Category", "Validation")]
    [MemberData(nameof(AttitudeRecordTests.Grid), 2, MemberType = typeof(AttitudeRecordTests))]
    public void Case_MatchesSpice(string key) => AttitudeRecordTests.Check(key, _output);
}

/// <summary>
/// Part 3 of the cases of <see cref="AttitudeRecordTests"/>: those whose index modulo <see cref="AttitudeRecordTests.Parts"/>
/// is 3, checked by <see cref="AttitudeRecordTests.Check"/>.
/// </summary>
/// <remarks>See <see cref="AttitudeRecordGridPart1Tests"/>.</remarks>
public class AttitudeRecordGridPart3Tests
{
    private readonly ITestOutputHelper _output;

    public AttitudeRecordGridPart3Tests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    [Theory]
    [MemberData(nameof(AttitudeRecordTests.Worst), 3, MemberType = typeof(AttitudeRecordTests))]
    public void WorstCase_MatchesSpice(string key) => AttitudeRecordTests.Check(key, _output);

    [Theory]
    [Trait("Category", "Validation")]
    [MemberData(nameof(AttitudeRecordTests.Grid), 3, MemberType = typeof(AttitudeRecordTests))]
    public void Case_MatchesSpice(string key) => AttitudeRecordTests.Check(key, _output);
}
