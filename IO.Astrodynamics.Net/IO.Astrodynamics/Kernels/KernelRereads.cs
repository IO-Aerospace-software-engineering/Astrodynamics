// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.Kernels;

/// <summary>
/// The kernels SPICE reads again, still loaded, after it unloads a file (KEEPER's UNLOAD, #673, measured with CSPICE N0067):
/// the read stops at a kernel deleted since its load and the unload fails.
/// </summary>
[Flags]
internal enum KernelRereads
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>The text kernels, after the unload of a text kernel or a meta-kernel; the pool lacks those after a deleted one.</summary>
    TextKernels = 1,

    /// <summary>The SPKs, opened again after the unload of an SPK (<c>SPKLEF</c>).</summary>
    Spks = 2,

    /// <summary>The CKs, opened again after the unload of a CK (<c>CKLPF</c>).</summary>
    Cks = 4,

    /// <summary>
    /// The binary PCKs, opened again after the unload of a binary PCK (<c>PCKLOF</c>); those loaded before a deleted one then
    /// take precedence over those loaded after it.
    /// </summary>
    Pcks = 8,

    /// <summary>Kernels the library cannot name: a failure that may come from them is never taken as repaired.</summary>
    Unmeasured = 16
}
