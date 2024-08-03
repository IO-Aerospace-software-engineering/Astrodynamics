// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.ComponentModel;

namespace IO.Astrodynamics;

public static class Enumeration
{
    public static string GetDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());

        return Attribute.GetCustomAttribute(field ?? throw new InvalidOperationException("Value unknown"), typeof(DescriptionAttribute)) is not DescriptionAttribute attribute ? value.ToString() : attribute.Description;
    }

    public static T GetValueFromDescription<T>(string description) where T : Enum
    {
        foreach (var field in typeof(T).GetFields())
        {
            if (Attribute.GetCustomAttribute(field,
                    typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                if (attribute.Description.Equals(description, StringComparison.InvariantCultureIgnoreCase))
                    return (T)field.GetValue(null);
            }
            else
            {
                if (field.Name.Equals(description, StringComparison.InvariantCultureIgnoreCase))
                    return (T)field.GetValue(null);
            }
        }

        throw new ArgumentException("Not found.", nameof(description));
    }
}