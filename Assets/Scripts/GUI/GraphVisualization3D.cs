using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Brain;
using static BrainGenomeTree;

public class GraphVisualization3D
{
    public Dictionary<DataElement, Vector3Int> data_to_position;
    public Dictionary<Vector3Int, DataElement> position_to_data;

    public List<Vector3> positions_list;

    public int maxX, maxY, maxZ;


    public GraphVisualization3D()
    {
        Initialize();
    }

    public GraphVisualization3D(DataElement ancestor)
    {
        Initialize();

        this.UpdateDataPosition(ancestor, Vector3Int.zero);
    }

    public void Initialize()
    {
        this.data_to_position = new();
        this.position_to_data = new();
        this.positions_list = new();

        this.maxX = 0;
        this.maxY = 0;
        this.maxZ = 0;
    }

    public abstract class DataElement
    {
        public bool gui_toggle;
    }

    public void UpdateDataPosition(DataElement data, Vector3Int new_position)
    {
        this.data_to_position[data] = new_position;
        this.position_to_data[new_position] = data;
    }

    /// <summary>
    /// insert a new node to the right of this one
    /// </summary>
    public void InsertRight(DataElement left, DataElement right)
    {
        Vector3Int left_position = this.data_to_position[left];
        Vector3Int to_right_position = left_position + new Vector3Int(1, 0, 0);
        TransferNeighborsToRight(to_right_position);
        UpdateDataPosition(right, to_right_position);
        
    }

    /// <summary>
    /// Note: the old position is not guaranteed to be overwritten
    /// </summary>
    /// <param name="position"></param>
    public void TransferNeighborsToRight(Vector3Int position)
    {
        if (!this.position_to_data.ContainsKey(position)) return;
        Vector3Int to_right_position = position + new Vector3Int(1, 0, 0);
        TransferNeighborsToRight(to_right_position); // transfer the right nodes first
        DataElement data = this.position_to_data[position];
        UpdateDataPosition(data, to_right_position);
        maxX = Mathf.Max(to_right_position.x, maxX);
    }

    /// <summary>
    /// insert a new node to the up of this one
    /// </summary>
    public void InsertUp(DataElement bottom, DataElement up)
    {
        Vector3Int bottom_position = this.data_to_position[bottom];
        Vector3Int to_up_position = bottom_position + new Vector3Int(0, 1, 0);
        TransferNeighborsToUp(to_up_position);
        UpdateDataPosition(up, to_up_position);

    }

    public void TransferNeighborsToUp(Vector3Int position)
    {
        if (!this.position_to_data.ContainsKey(position)) return;
        Vector3Int to_up_position = position + new Vector3Int(0, 1, 0);
        TransferNeighborsToUp(to_up_position); // transfer the right nodes first
        DataElement data = this.position_to_data[position];
        UpdateDataPosition(data, to_up_position);
        maxY = Mathf.Max(to_up_position.y, maxY);
    }

    /// <summary>
    /// insert a new node to the forward of this one
    /// </summary>
    public void InsertForward(DataElement backward, DataElement forward)
    {
        forward.gui_toggle = !forward.gui_toggle;
        Vector3Int backward_position = this.data_to_position[backward];
        Vector3Int to_forward_position = backward_position + new Vector3Int(0, 0, 1);
        TransferNeighborsToForward(to_forward_position);
        UpdateDataPosition(forward, to_forward_position);
    }

    public void TransferNeighborsToForward(Vector3Int position)
    {
        if (!this.position_to_data.ContainsKey(position)) return;
        Vector3Int to_forward_position = position + new Vector3Int(0, 0, 1);
        TransferNeighborsToForward(to_forward_position); // transfer the right nodes first
        DataElement data = this.position_to_data[position];
        UpdateDataPosition(data, to_forward_position);
        maxZ = Mathf.Max(to_forward_position.z, maxZ);
    }

    /// <summary>
    /// insert a new node to the right of this one, then up, and alternate each time it is called
    /// </summary>
    public void InsertRightUpAlternating(DataElement dataA, DataElement dataB)
    {
        if (dataA.gui_toggle)
        {
            InsertRight(dataA, dataB);
        }
        else
        {
            InsertUp(dataA, dataB);
        }

        dataA.gui_toggle = !dataA.gui_toggle;

    }





}
