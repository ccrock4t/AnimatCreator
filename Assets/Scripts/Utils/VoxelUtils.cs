using Unity.Mathematics;
using UnityEngine;

public class VoxelUtils : MonoBehaviour
{


    ///
    /// Index converters
    ///

    public static int3 Index_int3FromFlat(int i, int3 automaton_dimensions)
    {
        int x = i % automaton_dimensions.x;
        int y = Mathf.FloorToInt(i / automaton_dimensions.x) % automaton_dimensions.y;
        int z = Mathf.FloorToInt(i / (automaton_dimensions.x * automaton_dimensions.y));
        return new int3(x, y, z);
    }


    public static int Index_FlatFromint3(int3 coords, int3 automaton_dimensions)
    {
        return Index_FlatFromint3(coords.x, coords.y, coords.z, automaton_dimensions);
    }

    public static int Index_FlatFromint3(int x, int y, int z, int3 automaton_dimensions)
    {
        return x + automaton_dimensions.x * y + automaton_dimensions.x * automaton_dimensions.y * z;
    }

    public static bool IsOutOfBounds(int3 index, int3 automaton_dimensions)
    {
        return IsOutOfBounds(index.x, index.y, index.z, automaton_dimensions);
    }

    public static bool IsOutOfBounds(int x, int y, int z, int3 automaton_dimensions)
    {
        return (x < 0 || y < 0 || z < 0 || x >= automaton_dimensions.x || y >= automaton_dimensions.y || z >= automaton_dimensions.z);
    }
}
