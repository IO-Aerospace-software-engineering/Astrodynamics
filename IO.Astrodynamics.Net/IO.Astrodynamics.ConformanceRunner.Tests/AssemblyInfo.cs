// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using Xunit;

// The SPICE pool is process-global: a class that loads a case's kernels (CaseKernels) must not run beside another that
// computes with the runner's set. An ITRF93 state computed on the wrong Earth binary PCK is 3.3e-5 m off.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
