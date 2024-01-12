using System;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;

public class GlobalUtils
{
    public static void LogErrorFeatureNotImplemented(string error_message)
    {
        Debug.LogError("Feature not yet implemented:" + error_message);
    }

    public static void LogErrorEnumNotRecognized(string error_message)
    {
        Debug.LogError("Setting not recognized: " + error_message);
    }

    public static string stringValueOf(Enum value)
    {
        FieldInfo fi = value.GetType().GetField(value.ToString());
        DescriptionAttribute[] attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
        if (attributes.Length > 0)
        {
            return attributes[0].Description;
        }
        else
        {
            return value.ToString();
        }
    }

    // given the "description" or string value, get the corresponding enum.
    public static object? enumValueOf(string value, Type enumType)
    {
        string[] names = System.Enum.GetNames(enumType);
        foreach (string name in names)
        {
            if (stringValueOf((Enum)Enum.Parse(enumType, name)).Equals(value))
            {
                return Enum.Parse(enumType, name);
            }
        }

        return null; // not found
    }
}
