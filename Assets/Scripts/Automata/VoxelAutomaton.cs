using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


public struct CellInfo<VoxelType>
{
    public VoxelType current_state;
    public VoxelType next_state;
    public int last_frame_modified;

}

public abstract class VoxelAutomaton<VoxelType> : MonoBehaviour
{
    // CPU
    public NativeArray<CellInfo<VoxelType>> cell_grid; // stores data for each cell, for CPU only



    public int frame = 0; // how many steps has the automaton computed
    public float timer = 0;
    public int3 automaton_dimensions;


    public bool IsOutOfBounds(int3 index)
    {
        return VoxelUtils.IsOutOfBounds(index.x, index.y, index.z, this.automaton_dimensions);
    }

    public bool IsOutOfBounds(int x, int y, int z)
    {
        return VoxelUtils.IsOutOfBounds(x, y, z, this.automaton_dimensions);
    }







    /* Setters and getters for Native Arrays
     * 
     */
    /// <summary>
    ///     Set the current state of a cell. Also flags the cell as modified during this frame.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="state"></param>
    public static void SetCellNextState(NativeArray<CellInfo<VoxelType>> grid_state, int3 automaton_dimensions, int frame, int x, int y, int z, VoxelType state)
    {
        int i = VoxelUtils.Index_FlatFromint3(x, y, z, automaton_dimensions);
        SetCellNextState(grid_state, frame, i, state);
    }

    /// <summary>
    ///     Set the current state of a cell. Also flags the cell as modified during this frame.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="state"></param>
    public static void SetCellNextState(NativeArray<CellInfo<VoxelType>> grid_state, int frame, int i, VoxelType state)
    {
        CellInfo<VoxelType> value = grid_state[i];
        value.next_state = state;
        value.last_frame_modified = frame;
        grid_state[i] = value;
    }

    /// <summary>
    ///     Get the current state of a cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static VoxelType GetCellNextState(NativeArray<CellInfo<VoxelType>> grid_state, int3 automaton_dimensions, int x, int y, int z)
    {
        int i = VoxelUtils.Index_FlatFromint3(x, y, z, automaton_dimensions);
        return GetCellNextState(grid_state, i);
    }

    /// <summary>
    ///     Get the current state of a cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static VoxelType GetCellNextState(NativeArray<CellInfo<VoxelType>> grid_state, int i)
    {
        return grid_state[i].next_state;
    }

    /// <summary>
    ///     Get the current state of a cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static VoxelType GetCellCurrentState(NativeArray<CellInfo<VoxelType>> grid_state, int i)
    {
        return (VoxelType)grid_state[i].current_state;
    }

    /// <summary>
    ///     Get the current state of a cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static VoxelType GetCellCurrentState(NativeArray<CellInfo<VoxelType>> grid_state, int3 automaton_dimensions, int x, int y, int z)
    {
        int i = VoxelUtils.Index_FlatFromint3(x, y, z, automaton_dimensions);
        return GetCellCurrentState(grid_state, i);
    }


    /// <summary>
    ///     Returns a vector 3 where:
    ///         x is the current state
    ///         y is whether this cell was modified this frame
    ///         z is the previous state
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static CellInfo<VoxelType> GetCellInfo(NativeArray<CellInfo<VoxelType>> grid_state, int i)
    {
        return grid_state[i];
    }

    /// <summary>
    ///     Returns a vector 3 where:
    ///         x is the current state
    ///         y is whether this cell was modified this frame
    ///         z is the previous state
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static CellInfo<VoxelType> GetCellInfo(NativeArray<CellInfo<VoxelType>> grid_state, int3 automaton_dimensions, int x, int y, int z)
    {
        return GetCellInfo(grid_state, VoxelUtils.Index_FlatFromint3(x, y, z, automaton_dimensions));
    }


    //=====================

    /// <summary>
    ///     Get the current state of a cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public virtual VoxelType GetCellNextState(int i)
    {
        return GetCellNextState(this.cell_grid, i);
    }

    /// <summary>
    ///     Get the current state of a cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public virtual VoxelType GetCellCurrentState(int i)
    {
        return GetCellCurrentState(this.cell_grid, i);
    }


    /// <summary>
    ///     Returns a vector 3 where:
    ///         x is the current state
    ///         y is whether this cell was modified this frame
    ///         z is the previous state
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public virtual CellInfo<VoxelType> GetCellInfo(int i)
    {
        return GetCellInfo(this.cell_grid, i);
    }

    /// <summary>
    ///     Set the current state of a cell. Also flags the cell as modified during this frame.
    /// </summary>
    /// <param name="i"></param>
    /// <param name="state"></param>
    public virtual void SetCellNextState(int i, VoxelType state)
    {
        SetCellNextState(this.cell_grid, this.frame, i, state);
    }


    //=====================


    /// <summary>
    ///     Get the current state of a cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public virtual VoxelType GetCellNextState(int x, int y, int z)
    {
        int i = VoxelUtils.Index_FlatFromint3(x, y, z, this.automaton_dimensions);
        return GetCellNextState(i);
    }

    /// <summary>
    ///     Get the current state of a cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public virtual VoxelType GetCellCurrentState(int x, int y, int z)
    {
        int i = VoxelUtils.Index_FlatFromint3(x, y, z, this.automaton_dimensions);
        return GetCellCurrentState(i);
    }


    /// <summary>
    ///     Returns a vector 3 where:
    ///         x is the current state
    ///         y is whether this cell was modified this frame
    ///         z is the previous state
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public virtual CellInfo<VoxelType> GetCellInfo(int x, int y, int z)
    {
        return GetCellInfo(this.cell_grid, this.automaton_dimensions, x, y, z);
    }

    /// <summary>
    ///     Set the current state of a cell. Also flags the cell as modified during this frame.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="state"></param>
    public virtual void SetCellNextState(int x, int y, int z, VoxelType state)
    {
        SetCellNextState(this.cell_grid, this.automaton_dimensions, this.frame, x, y, z, state);
    }

    //=====================

    /// <summary>
    ///     Returns a vector 3 where:
    ///         x is the current state
    ///         y is whether this cell was modified this frame
    ///         z is the previous state
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public virtual CellInfo<VoxelType> GetCellInfo(int3 index)
    {
        return GetCellInfo(index.x, index.y, index.z);
    }
}
