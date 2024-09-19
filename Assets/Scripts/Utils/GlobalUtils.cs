using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Unity.Mathematics;
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


    ///
    /// Index converters
    ///

   public static float NormalizeIndex(int index, int dimension_length)
   {
        return (((float)index / (float)dimension_length) - 0.5f) * 2;
   }

    public static int5 Index_int5FromFlat(int i, int5 automaton_dimensions)
    {
        // from https://stackoverflow.com/questions/29142417/4d-position-from-1d-index
        int x = i % automaton_dimensions.x;
        int y = ((i - x) / automaton_dimensions.x) % automaton_dimensions.y;
        int z = ((i - y * automaton_dimensions.x - x) / (automaton_dimensions.x * automaton_dimensions.y)) % automaton_dimensions.z;
        int w = ((i - z * automaton_dimensions.y * automaton_dimensions.x - y * automaton_dimensions.x - x) / (automaton_dimensions.x * automaton_dimensions.y * automaton_dimensions.z)) % automaton_dimensions.w;
        int v = ((i - w * automaton_dimensions.z * automaton_dimensions.y * automaton_dimensions.x - z * automaton_dimensions.y * automaton_dimensions.x - y * automaton_dimensions.x - x) / (automaton_dimensions.x * automaton_dimensions.y * automaton_dimensions.z * automaton_dimensions.w)) % automaton_dimensions.v;
        return new(x, y, z, w, v);
    }


    public static int Index_FlatFromint5(int5 coords, int5 dimensions)
    {
        return Index_FlatFromint5(coords.x, coords.y, coords.z, coords.w, coords.v, dimensions);
    }

    public static int Index_FlatFromint5(int x, int y, int z, int w, int v, int5 dimensions)
    {
        return x + dimensions.x * y + dimensions.x * dimensions.y * z + dimensions.x * dimensions.y * dimensions.z * w + dimensions.x * dimensions.y * dimensions.z * dimensions.w + v;
    }

    public static int4 Index_int4FromFlat(int i, int4 automaton_dimensions)
    {
        // from https://stackoverflow.com/questions/29142417/4d-position-from-1d-index
        int x = i % automaton_dimensions.x;
        int y = ((i - x) / automaton_dimensions.x) % automaton_dimensions.y;
        int z = ((i - y * automaton_dimensions.x - x) / (automaton_dimensions.x * automaton_dimensions.y)) % automaton_dimensions.z;
        int w = ((i - z * automaton_dimensions.y * automaton_dimensions.x - y * automaton_dimensions.x - x) / (automaton_dimensions.x * automaton_dimensions.y * automaton_dimensions.z)) % automaton_dimensions.w;
        return new(x, y, z, w);
    }


    public static int Index_FlatFromint4(int4 coords, int4 dimensions)
    {
        return Index_FlatFromint4(coords.x, coords.y, coords.z, coords.w, dimensions);
    }

    public static int Index_FlatFromint4(int x, int y, int z, int w, int4 dimensions)
    {
        return x + dimensions.x * y + dimensions.x * dimensions.y * z + dimensions.x * dimensions.y * dimensions.z * w;
    }


    public static int3 Index_int3FromFlat(int i, int3 automaton_dimensions)
    {
        int x = i % automaton_dimensions.x;
        int y = Mathf.FloorToInt(i / automaton_dimensions.x) % automaton_dimensions.y;
        int z = Mathf.FloorToInt(i / (automaton_dimensions.x * automaton_dimensions.y));
        return new int3(x, y, z);
    }


    public static int Index_FlatFromint3(int3 coords, int3 dimensions)
    {
        return Index_FlatFromint3(coords.x, coords.y, coords.z, dimensions);
    }

    public static int Index_FlatFromint3(int x, int y, int z, int3 dimensions)
    {
        return x + dimensions.x * y + dimensions.x * dimensions.y * z;
    }

    public static bool IsOutOfBounds(int3 index, int3 dimensions)
    {
        return IsOutOfBounds(index.x, index.y, index.z, dimensions);
    }

    public static bool IsOutOfBounds(int x, int y, int z, int3 dimensions)
    {
        return (x < 0 || y < 0 || z < 0 || x >= dimensions.x || y >= dimensions.y || z >= dimensions.z);
    }

    public static bool IsOutOfBounds(int4 index, int4 dimensions)
    {
        return IsOutOfBounds(index.x, index.y, index.z, index.w, dimensions);
    }

    public static bool IsOutOfBounds(int x, int y, int z, int w, int4 dimensions)
    {
        return (x < 0 || y < 0 || z < 0 || w < 0 || x >= dimensions.x || y >= dimensions.y || z >= dimensions.z || w >= dimensions.w);
    }

    public static int Index_FlatFromint2(int x, int y, int2 substrate_dimensions)
    {
        return x + substrate_dimensions.x * y;
    }

    public static int2 Index_int2FromFlat(int i, int2 substrate_dimensions)
    {
        int x = i % substrate_dimensions.x;
        int y = (int)math.floor(i / substrate_dimensions.x) % substrate_dimensions.y;
        return new int2(x, y);
    }

    public static class SyntaxUtils
    {
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
}
