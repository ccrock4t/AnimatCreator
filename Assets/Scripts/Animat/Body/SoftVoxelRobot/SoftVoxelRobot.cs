using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static Brain;
using static CPPNGenome;

public class SoftVoxelRobot : AnimatBody
{
    public SoftVoxelObject soft_voxel_object;
    public MeshCollider mesh_collider;

    public enum RobotVoxel
    {
        Empty,
        Touch_Sensor,
        Raycast_Sensor,
        Mouth,
        SineWave_Generator,
    }


    public SoftVoxelRobot() : base()
    {
        
    }

    public void Initialize(NativeArray<RobotVoxel> robot_voxels, Color? override_color)
    {
        this.soft_voxel_object = new(robot_voxels,
            new int3(GlobalConfig.ANIMAT_SUBSTRATE_DIMENSIONS.x, GlobalConfig.ANIMAT_SUBSTRATE_DIMENSIONS.y, GlobalConfig.ANIMAT_SUBSTRATE_DIMENSIONS.z), 
            override_color);
        this.soft_voxel_object.gameobject.transform.parent = this.transform;
        this.mesh_collider = this.soft_voxel_object.gameobject.AddComponent<MeshCollider>();
        this.mesh_collider.convex = true;
        this.soft_voxel_object.gameobject.transform.localPosition = Vector3.zero;
        this.soft_voxel_object.gameobject.layer = AnimatArena.ANIMAT_GAMEOBJECT_LAYER;
        this.transform.localScale = Vector3.one * this.soft_voxel_object.scale;
    }

    public void DoTimestep()
    {
        this.soft_voxel_object.DoTimestep();
        this.mesh_collider.sharedMesh = this.soft_voxel_object.mesh.unity_mesh;
    }


    public override float3 GetCenterOfMass()
    {
        return this.soft_voxel_object.GetCenterOfMass();
    }

    public override void ResetPositionsAndVelocities()
    {
        return;
    }

    public override void SetColorToColorful()
    {
        return;
    }

    public override void SetColorToStone()
    {
        return;
    }

    public override void Teleport(Vector3 position, Quaternion rotation)
    {
        this.transform.localPosition = position;
        this.transform.rotation = rotation;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDestroy()
    {
        this.soft_voxel_object.OnDestroy();
    }
}
