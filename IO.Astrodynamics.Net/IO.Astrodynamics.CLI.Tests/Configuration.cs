// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Threading;
using System.Threading.Tasks;

namespace IO.Astrodynamics.CLI.Tests;

public static class Configuration
{
    private static readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

    public static IDisposable Lock()
    {
        _lock.Wait();
        return new Releaser();
    }

    public static async Task<IDisposable> LockAsync()
    {
        await _lock.WaitAsync();
        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _lock.Release();
            }
        }
    }
}
