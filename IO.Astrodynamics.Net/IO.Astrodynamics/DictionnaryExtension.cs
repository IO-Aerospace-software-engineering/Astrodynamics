using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics;

public static class DictionnaryExtension
{
    public static TU GetOrAdd<T,TU>(this IDictionary<T, TU> dictionary, in T key,Func<T,TU> add)
    {
        if(dictionary.TryGetValue(key, out var value))
        {
            return value;
        }
        var newValue = add(key);
        dictionary[key] = newValue;
        return newValue;
    }
}