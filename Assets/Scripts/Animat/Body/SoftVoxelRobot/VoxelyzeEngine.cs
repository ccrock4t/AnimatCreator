using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

using CVoxelyze = System.IntPtr;
using CVX_Material = System.IntPtr;
using CVX_MeshRender = System.IntPtr;
using CVX_Voxel = System.IntPtr;
using CPP_OBJECT = System.IntPtr;
using System.Drawing;
using Color = UnityEngine.Color;
using static UnityEngine.Networking.UnityWebRequest;
using Unity.Mathematics;

public class VoxelyzeEngine : MonoBehaviour
{
    const string DLL_path = "Voxelyze";

    [DllImport(DLL_path)]
    public static extern CVoxelyze CreateCVoxelyze(float voxel_size);

    [DllImport(DLL_path)]
    public static extern void SetAmbientTemperature(CVoxelyze voxel_object, float temperature);

    [DllImport(DLL_path)]
    public static extern bool IsVoxelTouchingFloor(CVX_Voxel voxel);

    [DllImport(DLL_path)]
    public static extern float GetVoxelFloorPenetration(CVX_Voxel voxel);

    [DllImport(DLL_path)]
    public static extern void SetVoxelTemperature(CVX_Voxel voxel_object, float temperature);

    [DllImport(DLL_path)]
    public static extern void SetVoxelTemperatureXdirection(CVX_Voxel voxel_object, float temperature);

    [DllImport(DLL_path)]
    public static extern void SetVoxelTemperatureYdirection(CVX_Voxel voxel_object, float temperature);

    [DllImport(DLL_path)]
    public static extern void SetVoxelTemperatureZdirection(CVX_Voxel voxel_object, float temperature);

    [DllImport(DLL_path)]
    public static extern void GetVoxelTemperature(CVX_Voxel voxel_object, double[] result);

    [DllImport(DLL_path)]
    public static extern void GetVoxelRotation(CVX_Voxel voxel, double[] result);

    [DllImport(DLL_path)]
    public static extern void AddForceToVoxel(CVX_Voxel voxel, float forceX, float forceY, float forceZ);

    [DllImport(DLL_path)]
    public static extern void SetInternalDamping(CVX_Material material, float damping);

    [DllImport(DLL_path)]
    public static extern void SetGlobalDamping(CVX_Material material, float damping);

    [DllImport(DLL_path)]
    public static extern void SetCollisionDamping(CVX_Material material, float damping);

    [DllImport(DLL_path)]
    public static extern void SetCoefficientOfThermalExpansion(CVX_Material material, float CTE);

    [DllImport(DLL_path)]
    public static extern void SetFriction(CVX_Material material, float static_friction, float kinetic_friction);

    [DllImport(DLL_path)]
    public static extern void SetGravity(CVoxelyze voxel_object, float g);

    [DllImport(DLL_path)]
    public static extern void EnableFloor(CVoxelyze voxel_object);

    [DllImport(DLL_path)]
    public static extern void EnableCollisions(CVoxelyze voxel_object);

    public static CVX_Material AddNewMaterial(CVoxelyze voxel_object, float elastic_modulus, float density, float poissons_ratio, Color color) {
        return AddMaterial(voxel_object, elastic_modulus, density, poissons_ratio, color.r * 255f, color.g * 255f, color.b * 255f);
    }
        
    [DllImport(DLL_path)]
    private static extern CVX_Material AddMaterial(CVoxelyze voxel_object, float elastic_modulus, float poissons_ratio, float density, float color_r, float color_g, float color_b);

    [DllImport(DLL_path)]
    public static extern CVX_Voxel SetVoxelMaterial(CVoxelyze voxel_object, CVX_Material material, int x, int y, int z);

    [DllImport(DLL_path)]
    public static extern void SetVoxelFixedAll(CVX_Voxel voxel);

    [DllImport(DLL_path)]
    public static extern void SetVoxelForce(CVX_Voxel voxel, float forceX, float forceY, float forceZ);

    public static bool DoNextTimestep(CVoxelyze obj, float timestep = -1f)
    {
        return DoTimestep(obj, timestep);
    }

    [DllImport(DLL_path)]
    private static extern bool DoTimestep(CVoxelyze obj, float timestep);

    [DllImport(DLL_path)]
    public static extern float GetRecommendedTimestep(CVoxelyze obj);

    [DllImport(DLL_path)]
    public static extern CVX_MeshRender CreateMesh(CVoxelyze cvx_object);

    [DllImport(DLL_path)]
    public static extern void UpdateMesh(CVX_MeshRender mesh);

    [DllImport(DLL_path)]
    private static extern void GetVoxelyzeCenterOfMass(CVoxelyze obj, double[] result);

    public static double3 GetVoxelyzeCenterOfMass(CVoxelyze obj)
    {
        double[] result = new double[3];
        GetVoxelyzeCenterOfMass(obj, result);
        return new(result[0], result[1], result[2]);
    }

    [DllImport(DLL_path)]
    private static extern void GetVoxelCenterOfMass(CVX_Voxel voxel, double[] result);

    public static double3 GetVoxelCenterOfMass(CVX_Voxel voxel)
    {
        double[] result = new double[3];
        GetVoxelCenterOfMass(voxel, result);
        return new(result[0], result[1], result[2]);
    }

    [DllImport(DLL_path)]
    private static extern void GetVoxelVelocity(CVX_Voxel voxel, double[] result);

    public static double3 GetVoxelVelocity(CVX_Voxel voxel)
    {
        double[] result = new double[3];
        GetVoxelVelocity(voxel, result);
        return new(result[0], result[1], result[2]);
    }

    [DllImport(DLL_path)]
    private static extern void GetVoxelAngularVelocity(CVX_Voxel voxel, double[] result);

    public static double3 GetVoxelAngularVelocity(CVX_Voxel voxel)
    {
        double[] result = new double[3];
        GetVoxelAngularVelocity(voxel,result);
        return new(result[0], result[1], result[2]);
    }

    [DllImport(DLL_path)]
    private static extern void GetVoxelStrain(CVX_Voxel voxel, double[] result);

    public static double3 GetVoxelStrain(CVX_Voxel voxel)
    {
        double[] result = new double[3];
        GetVoxelStrain(voxel, result);
        return new(result[0], result[1], result[2]);
    }

    [DllImport(DLL_path)]
    public static extern int GetMeshNumberOfVertices(CVX_MeshRender mesh);

    [DllImport(DLL_path)]
    public static extern int GetMeshNumberOfQuads(CVX_MeshRender mesh);

    [DllImport(DLL_path)]
    public static extern float GetNextVertexValue(CVX_MeshRender mesh, int index);

    [DllImport(DLL_path)]
    public static extern int GetNextQuadValue(CVX_MeshRender mesh, int index);

    [DllImport(DLL_path)]
    public static extern float GetNextQuadColorValue(CVX_MeshRender mesh, int index);

    [DllImport(DLL_path)]
    public static extern void DestroyNativeObject(CPP_OBJECT obj);

    [DllImport(DLL_path)]
    public static extern void SaveVoxelObjectToDisk(CVoxelyze obj);

    [DllImport(DLL_path)]
    public static extern void LoadVoxelFileFromDisk(CVoxelyze obj);

    [DllImport(DLL_path)]
    public static extern int GetLinksListSize(CVoxelyze voxelyze_obj);

    [DllImport(DLL_path)]
    public static extern int GetVoxelsListSize(CVoxelyze voxelyze_obj);

    [DllImport(DLL_path)]
    public static extern int GetCollisionsListSize(CVoxelyze voxelyze_obj);

    [DllImport(DLL_path)]
    public static extern int DoTimeStepInUnityJob(CVoxelyze voxelyze_obj, float dt, int stage, int i);
}
