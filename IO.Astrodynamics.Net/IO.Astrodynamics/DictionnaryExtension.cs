using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics;

public static class DictionnaryExtension
{
    private static readonly object _lock = new object();
    
    public static TU GetOrAdd<T,TU>(this IDictionary<T, TU> dictionary, in T key, Func<T,TU> add)
    {
        // For ConcurrentDictionary, use its built-in thread-safe GetOrAdd
        if (dictionary is ConcurrentDictionary<T, TU> concurrentDict)
        {
            return concurrentDict.GetOrAdd(key, add);
        }
        
        // For other dictionaries, use locking to ensure thread-safety
        lock (_lock)
        {
            if (dictionary.TryGetValue(key, out var value))
            {
                return value;
            }
            var newValue = add(key);
            dictionary[key] = newValue;
            return newValue;
        }
    }
}