// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics;

public static class ArrayBuilder
{
    public static T[] ArrayOf<T>(int count) where T : new()
    {
        var arr = new T[count];
        for (var i = 0; i < count; i++) arr[i] = new T();

        return arr;
    }
}