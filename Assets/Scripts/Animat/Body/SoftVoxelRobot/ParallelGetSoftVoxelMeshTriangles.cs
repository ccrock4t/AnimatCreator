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

////[burstcompile]
// for each quad
public struct ParallelGetSoftVoxelMeshTriangles : IJobParallelFor
{
    public Mesh.MeshData mesh_data;

    [NativeDisableUnsafePtrRestriction]
    public CVoxelyze voxel_mesh;


    public void Execute(int quad_start_index)
    {
        int tri_idx = 6 * quad_start_index;
        NativeArray<int> output_triangles = mesh_data.GetIndexData<int>();
        //tri1
        int v1 = VoxelyzeEngine.GetNextQuadValue(voxel_mesh, 4 * quad_start_index + 0);
        int v2 = VoxelyzeEngine.GetNextQuadValue(voxel_mesh, 4 * quad_start_index + 1);
        int v3 = VoxelyzeEngine.GetNextQuadValue(voxel_mesh, 4 * quad_start_index + 2);

        //tri2
        int v4 = VoxelyzeEngine.GetNextQuadValue(voxel_mesh, 4 * quad_start_index + 2);
        int v5 = VoxelyzeEngine.GetNextQuadValue(voxel_mesh, 4 * quad_start_index + 3);
        int v6 = VoxelyzeEngine.GetNextQuadValue(voxel_mesh, 4 * quad_start_index + 0);

        output_triangles[tri_idx] = v1;
        output_triangles[tri_idx + 1] = v2;
        output_triangles[tri_idx + 2] = v3;
        output_triangles[tri_idx + 3] = v4;
        output_triangles[tri_idx + 4] = v5;
        output_triangles[tri_idx + 5] = v6;
    }
}
