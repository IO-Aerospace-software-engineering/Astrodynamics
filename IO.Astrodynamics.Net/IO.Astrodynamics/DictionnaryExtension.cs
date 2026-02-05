using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace IO.Astrodynamics;

public static class DictionnaryExtension
{
    public static TU GetOrAdd<T, TU>(this IDictionary<T, TU> dictionary, in T key, Func<T, TU> add)
    {
        // For ConcurrentDictionary, use its built-in thread-safe GetOrAdd
        if (dictionary is ConcurrentDictionary<T, TU> concurrentDict)
        {
            return concurrentDict.GetOrAdd(key, add);
        }

        // For non-concurrent dictionaries, assume single-threaded access
        if (dictionary.TryGetValue(key, out var value))
        {
            return value;
        }

        var newValue = add(key);
        dictionary[key] = newValue;
        return newValue;
    }
}