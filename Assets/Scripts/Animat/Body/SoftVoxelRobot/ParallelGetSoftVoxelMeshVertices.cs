using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

using CVoxelyze = System.IntPtr;
using CVX_Material = System.IntPtr;
using CVX_MeshRender = System.IntPtr;
using CVX_Voxel = System.IntPtr;
using CPP_OBJECT = System.IntPtr;
using System;
using Unity.Collections;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
// for each vertex
public struct ParallelGetSoftVoxelMeshVertices : IJobParallelFor
{
    public Mesh.MeshData mesh_data;

    [NativeDisableUnsafePtrRestriction]
    public CVoxelyze voxel_mesh;

    public void Execute(int vertex_start_idx)
    {
        NativeArray<Vector3> output_vertices = mesh_data.GetVertexData<Vector3>();
        float vx = VoxelyzeEngine.GetNextVertexValue(voxel_mesh, 3*vertex_start_idx + 0);
        float vy = VoxelyzeEngine.GetNextVertexValue(voxel_mesh, 3 * vertex_start_idx + 1);
        float vz = VoxelyzeEngine.GetNextVertexValue(voxel_mesh, 3 * vertex_start_idx + 2);
        Vector3 v = new(vx, vy, vz);
        output_vertices[vertex_start_idx] = v;
    }
}
