// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Surface;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The surface of the kernel writers, lot L5c of #668 (R14, decision 78 of #668, decided by S. Guillet, 2026-11-23): the
/// six text kernel writers are synchronous and named without <c>Async</c> (<see cref="Clock.Write"/>,
/// <see cref="SpacecraftFrame.Write"/>, <see cref="Instrument.WriteFrame"/>, <see cref="Instrument.WriteKernel"/>,
/// <see cref="Site.WriteFrame"/>, <see cref="SiteFrame.Write"/>), and the constructor <c>SpacecraftFrame(Spacecraft)</c>
/// and <c>SpacecraftFrame.WriteOrientation(FileInfo, Spacecraft)</c> are internal (#799(d)): a spacecraft builds its frame,
/// and <see cref="Spacecraft.WriteOrientation"/> writes its CK. The reference is the decided rule.
/// </summary>
public class KernelWriterSurfaceTests
{
    private const BindingFlags Everything = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [Theory]
    [InlineData(typeof(Clock), "Write")]
    [InlineData(typeof(SpacecraftFrame), "Write")]
    [InlineData(typeof(Instrument), "WriteFrame")]
    [InlineData(typeof(Instrument), "WriteKernel")]
    [InlineData(typeof(CircularInstrument), "WriteKernel")]
    [InlineData(typeof(RectangularInstrument), "WriteKernel")]
    [InlineData(typeof(EllipticalInstrument), "WriteKernel")]
    [InlineData(typeof(Site), "WriteFrame")]
    [InlineData(typeof(SiteFrame), "Write")]
    public void TextKernelWriter_IsSynchronous_AndNamedWithoutAsync(Type type, string name)
    {
        var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, [typeof(FileInfo)]);
        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
        Assert.Null(type.GetMethod(name + "Async", Everything));
    }

    [Fact]
    public void SpacecraftFrame_ConstructorAndWriteOrientation_AreInternal()
    {
        var type = typeof(SpacecraftFrame);
        Assert.DoesNotContain(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
            c => c.GetParameters().Select(p => p.ParameterType).SequenceEqual([typeof(Spacecraft)]));
        var constructor = type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, [typeof(Spacecraft)]);
        Assert.NotNull(constructor);
        Assert.True(constructor.IsAssembly);

        Assert.Null(type.GetMethod("WriteOrientation", BindingFlags.Public | BindingFlags.Instance, [typeof(FileInfo), typeof(Spacecraft)]));
        var writeOrientation = type.GetMethod("WriteOrientation", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(FileInfo), typeof(Spacecraft)]);
        Assert.NotNull(writeOrientation);
        Assert.True(writeOrientation.IsAssembly);
    }

    [Fact]
    public void Spacecraft_HasNoPrivateFrameWriter()
    {
        // The private Spacecraft.WriteFrameAsync was never called; it is removed, the frame kernel is SpacecraftFrame.Write's
        Assert.Null(typeof(Spacecraft).GetMethod("WriteFrameAsync", Everything));
        Assert.Null(typeof(Spacecraft).GetMethod("WriteFrame", Everything));
    }
}
