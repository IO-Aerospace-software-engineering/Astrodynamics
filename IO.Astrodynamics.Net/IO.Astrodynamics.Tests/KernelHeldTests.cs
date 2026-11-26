// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// Whether SPICE holds a file (internal <see cref="SpiceApi.HoldsKernelFile"/>, the native <c>KernelHeldProxy</c>; decision
/// 102 of #668, decided by S. Guillet, 2026-11-25): read from KEEPER's list by the rule of the writers' unload, unloading
/// nothing. The registry of <see cref="SpiceApi"/> lists a meta-kernel as one entry and cannot tell the files it names; the
/// query does.
/// </summary>
public sealed class KernelHeldTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("kernel-held-");

    public KernelHeldTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    public void Dispose() => _root.Delete(true);

    private FileInfo TextKernel(string name, string variable)
    {
        var file = new FileInfo(Path.Combine(_root.FullName, name));
        File.WriteAllText(file.FullName, $"KPL/PCK\n\\begindata\n{variable} = 1\n\\begintext\n");
        return file;
    }

    // A meta-kernel naming the file by its absolute path, in strings of 60 characters at most (a string value of a text
    // kernel holds 80 characters at most), each but the last ended by the continuation marker '+'
    private FileInfo MetaKernel(string name, FileInfo member)
    {
        var path = member.FullName;
        var value = new StringBuilder();
        for (int i = 0; i < path.Length; i += 60)
        {
            value.Append('\'').Append(path.Substring(i, System.Math.Min(60, path.Length - i))).Append(i + 60 < path.Length ? "+'\n" : "'");
        }

        var file = new FileInfo(Path.Combine(_root.FullName, name));
        File.WriteAllText(file.FullName, $"KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( {value} )\n\\begintext\n");
        return file;
    }

    [Fact]
    public void HoldsKernelFile_OfAFileALoadedMetaKernelNames_IsTrue_AndOfAFileSpiceDoesNotHold_IsFalse()
    {
        // Arrange: a meta-kernel naming one text kernel, beside a text kernel nobody loads
        var member = TextKernel("member.tk", "IOA_102_MEMBER");
        var other = TextKernel("other.tk", "IOA_102_OTHER");
        var metaKernel = MetaKernel("user.tm", member);
        Assert.False(SpiceApi.Instance.HoldsKernelFile(member));

        SpiceApi.Instance.LoadKernels(metaKernel);
        try
        {
            // The registry lists the meta-kernel only
            var listed = SpiceApi.Instance.GetLoadedKernels().Select(x => x.FullName).ToArray();
            Assert.Contains(metaKernel.FullName, listed);
            Assert.DoesNotContain(member.FullName, listed);

            // Act and assert: SPICE holds the member and the meta-kernel, not the other file nor a missing one
            Assert.True(SpiceApi.Instance.HoldsKernelFile(member));
            Assert.True(SpiceApi.Instance.HoldsKernelFile(metaKernel));
            Assert.False(SpiceApi.Instance.HoldsKernelFile(other));
            Assert.False(SpiceApi.Instance.HoldsKernelFile(new FileInfo(Path.Combine(_root.FullName, "missing.bsp"))));

            // The query unloads nothing: the member is still held, and the registry still lists the meta-kernel (other classes
            // load and unload theirs meanwhile, so the whole list is not compared)
            Assert.True(SpiceApi.Instance.HoldsKernelFile(member));
            Assert.Contains(metaKernel.FullName, SpiceApi.Instance.GetLoadedKernels().Select(x => x.FullName));
        }
        finally
        {
            SpiceApi.Instance.UnloadKernels(metaKernel);
        }

        Assert.False(SpiceApi.Instance.HoldsKernelFile(member));
        Assert.False(SpiceApi.Instance.HoldsKernelFile(metaKernel));
    }

    [Fact]
    public void HoldsKernelFile_OfAFileLoadedDirectly_IsTrueUntilItIsUnloaded()
    {
        var file = TextKernel("direct.tk", "IOA_102_DIRECT");
        SpiceApi.Instance.LoadKernels(file);
        try
        {
            Assert.True(SpiceApi.Instance.HoldsKernelFile(file));
        }
        finally
        {
            SpiceApi.Instance.UnloadKernels(file);
        }

        Assert.False(SpiceApi.Instance.HoldsKernelFile(file));
    }

    [Fact]
    public void HoldsKernelFile_OfNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SpiceApi.Instance.HoldsKernelFile(null));
    }
}
