using System.Collections.Generic;
using UnityEngine;

using CVoxelyze = System.IntPtr;
using CVX_Material = System.IntPtr;
using CVX_MeshRender = System.IntPtr;
using CVX_Voxel = System.IntPtr;
using UnityEngine.Rendering;
using Unity.Jobs;
using System;
using static SoftVoxelRobot;
using Unity.Mathematics;
using Unity.Collections;
using System.Threading.Tasks;
using System.Collections.Concurrent;

// remember for Voxelyze, the y and z axes are switched compared to Unity's axes
public class SoftVoxelObject
{
    public bool crashed = false;
    int height_offset = 0;
    public const float TIMESTEP = -1f;
    public static Material vertex_color;

    public GameObject gameobject;
    public SoftVoxelMesh mesh;
    public CVoxelyze cpp_voxel_object;
    public Dictionary<RobotVoxel, CVX_Material> voxelyze_materials;

    public RobotVoxel[] robot_voxels;
    CVX_Voxel?[] cvx_voxels;

    const float MPa = 1000000;

    public float recommended_timestep = 0;
    public float last_frame_elapsed_simulation_time = 0;

    Task update_task;

    public int3 dimensions;

    public bool contains_solid_voxels = false;

    public const float BASE_TEMP = 0;
    public const float TEMP_MAX_MAGNITUDE = 15f;
    const float SINUSOID_SPEED = 16f;

    float time = 0;

    public List<(int3, CVX_Voxel)> sensor_voxels;
    public List<(int3, CVX_Voxel)> raycast_sensor_voxels;
    public List<(int3, CVX_Voxel)> motor_voxels;
    public Dictionary<CVX_Voxel, double3> voxel_angular_velocities;
    public Dictionary<CVX_Voxel, double3> voxel_linear_velocities;
    public Dictionary<CVX_Voxel, float> voxel_sinusoid_states;

    ConcurrentDictionary<CVX_Voxel, Queue<double3>> recent_velocities;
    ConcurrentDictionary<CVX_Voxel, Queue<double3>> recent_temps;

    const float lattice_dimension = 0.01f;
    public float scale;

    float fixed_delta_time;

    // for jobs system
    NativeArray<int> diverged;
    NativeArray<int> current_stage;
    NativeArray<int> counter;
    int linksListSize; 
    int voxelsListSize;
    int collisionsListSize;
    int num_threads;


    public SoftVoxelObject(NativeArray<RobotVoxel> robot_voxels_nativearray, int3 dimensions, Color? override_mesh_color = null)
    {

        this.cpp_voxel_object = VoxelyzeEngine.CreateCVoxelyze(lattice_dimension);
        this.scale = (1f / lattice_dimension) / 10f;
        VoxelyzeEngine.SetGravity(this.cpp_voxel_object, 1.5f);

        if (vertex_color == null) vertex_color = (Material)Resources.Load("Materials/Vertex Color");

        this.fixed_delta_time = Time.fixedDeltaTime;

        this.dimensions = dimensions;
        this.voxelyze_materials = new();
        this.motor_voxels = new();
        this.sensor_voxels = new();
        this.raycast_sensor_voxels = new();
        this.voxel_angular_velocities = new();
        this.voxel_linear_velocities = new();
        this.voxel_sinusoid_states = new();
        this.robot_voxels = robot_voxels_nativearray.ToArray();
        

        float density = MPa;
        float poisson_ratio = 0.35f;
        float soft_material_modulus = 5 * MPa;
        float medium_material_modulus = 50 * MPa;
        float hard_material_modulus = 50 * MPa;
        Color brown_color = new Color(100 / 255f, 65 / 255f, 23 / 255f);
      
        this.voxelyze_materials[RobotVoxel.Touch_Sensor]
            = VoxelyzeEngine.AddNewMaterial(this.cpp_voxel_object, medium_material_modulus, density, poisson_ratio, Color.magenta);
        this.voxelyze_materials[RobotVoxel.Raycast_Sensor]
            = VoxelyzeEngine.AddNewMaterial(this.cpp_voxel_object, medium_material_modulus, density, poisson_ratio, Color.black);
        this.voxelyze_materials[RobotVoxel.Mouth]
             = VoxelyzeEngine.AddNewMaterial(this.cpp_voxel_object, medium_material_modulus, density, poisson_ratio, Color.red);
        this.voxelyze_materials[RobotVoxel.SineWave_Generator]
            = VoxelyzeEngine.AddNewMaterial(this.cpp_voxel_object, medium_material_modulus, density, poisson_ratio, Color.cyan);

        float CTE = 0.01f; // how much the voxel can expand
        float static_friction = 1f;
        float kinetic_friction = 10f;
        foreach (KeyValuePair<RobotVoxel, CVoxelyze> pair in this.voxelyze_materials)
        {
            CVoxelyze voxelyze_material = pair.Value;
            VoxelyzeEngine.SetInternalDamping(voxelyze_material, 1);
            VoxelyzeEngine.SetGlobalDamping(voxelyze_material, 0.01f);
            VoxelyzeEngine.SetCollisionDamping(voxelyze_material, 0.8f);
            VoxelyzeEngine.SetFriction(voxelyze_material, static_friction, kinetic_friction);

            VoxelyzeEngine.SetCoefficientOfThermalExpansion(voxelyze_material, CTE);
        }
        //VoxelyzeEngine.SetCoefficientOfThermalExpansion(this.voxelyze_materials[RobotVoxel.Voluntary_Muscle], CTE);
        // pre-processing
        int lowest_voxel_height = -1;
        for (int i = 0; i < robot_voxels.Length; i++)
        { 
            RobotVoxel material_enum = robot_voxels[i];
            if (material_enum == RobotVoxel.Empty) continue;

            // remove disconnected voxels
            if (!IsVoxelConnectedToNeighbor(i, robot_voxels, dimensions))
            {
                robot_voxels[i] = RobotVoxel.Empty;
                continue;
            }

            // record lowest voxel height, so we can spawn the object on the ground
            this.contains_solid_voxels = true;
            int3 coords = GlobalUtils.Index_int3FromFlat(i, dimensions);
            if (lowest_voxel_height == -1 || coords.y < lowest_voxel_height)
            {
                lowest_voxel_height = coords.y;
            }
        }

        // spawn the voxels, if there are any
        this.cvx_voxels = new CVX_Voxel?[robot_voxels.Length];
        if (contains_solid_voxels)
        {
            for (int i = 0; i < robot_voxels.Length; i++)
            {
                RobotVoxel material_enum = robot_voxels[i];
                int3 coords = GlobalUtils.Index_int3FromFlat(i, dimensions);
                if (material_enum == RobotVoxel.Empty)
                {
                    // can't add a voxel that doesn't exist, so skip this
                    this.cvx_voxels[i] = null;
                    continue;
                }
                CVX_Material voxelyze_material = this.voxelyze_materials[material_enum];
                
                CVX_Voxel cvx_voxel = AddVoxel(voxelyze_material, coords.x, coords.y - lowest_voxel_height, coords.z);
                this.cvx_voxels[i] = cvx_voxel;

                // this voxel can be controlled by the brain
                this.motor_voxels.Add((coords, cvx_voxel));

                // this voxel can be sensed by the brain
                this.sensor_voxels.Add((coords, cvx_voxel));
   
                if (material_enum == RobotVoxel.Raycast_Sensor || material_enum == RobotVoxel.Mouth)
                {
                    this.raycast_sensor_voxels.Add((coords, cvx_voxel));
                }
               
                this.voxel_angular_velocities[cvx_voxel] = double3.zero;
                this.voxel_linear_velocities[cvx_voxel] = double3.zero;
            }
        }


       
        VoxelyzeEngine.EnableFloor(this.cpp_voxel_object);
        // VoxelyzeEngine.EnableCollisions(this.cpp_voxel_object);

        // now create mesh and gameobject;
        this.mesh = new SoftVoxelMesh(this.cpp_voxel_object, override_mesh_color);
        this.gameobject = new GameObject("SoftVoxelObject");
        MeshRenderer mr = this.gameobject.AddComponent<MeshRenderer>();
        mr.material = vertex_color;
        MeshFilter filter = this.gameobject.AddComponent<MeshFilter>();
        filter.mesh = this.mesh.unity_mesh;

        SetAmbientTemperature(BASE_TEMP);

        this.linksListSize = VoxelyzeEngine.GetLinksListSize(this.cpp_voxel_object);
        this.voxelsListSize = VoxelyzeEngine.GetVoxelsListSize(this.cpp_voxel_object);
        this.collisionsListSize = VoxelyzeEngine.GetCollisionsListSize(this.cpp_voxel_object);
        this.num_threads = math.max(math.max(this.linksListSize, this.voxelsListSize), this.collisionsListSize);
        diverged = new(1, Allocator.Persistent);
        counter = new(1, Allocator.Persistent);
        current_stage = new(1, Allocator.Persistent);
        diverged[0] = 0;

        this.recent_velocities = new();
        this.recent_temps = new();
    }

    double max_angular_acceleration = 0;
    double max_linear_acceleration = 0;
    double max_strain = 0;

    public float3 GetVoxelAngularVelocity(CVX_Voxel voxel)
    {
        double3 result = VoxelyzeEngine.GetVoxelAngularVelocity(voxel);
        return (float3)result;
    }

    // this should be called once per step
    const int NUM_OF_VELOCITIES_TO_STORE = 5;
    const int NUM_OF_TEMPS_TO_STORE = 1;
    public void UpdateVoxelLinearVelocityCache(CVX_Voxel voxel)
    {
        float3 velocity = (float3)VoxelyzeEngine.GetVoxelVelocity(voxel);
        if (!this.recent_velocities.ContainsKey(voxel)) this.recent_velocities[voxel] = new();
        this.recent_velocities[voxel].Enqueue(velocity);
        if(this.recent_velocities[voxel].Count > NUM_OF_VELOCITIES_TO_STORE)
        {
            this.recent_velocities[voxel].Dequeue();
        }
    }

    public void UpdateVoxelTemperatureCache(CVX_Voxel voxel)
    {
        if (!this.recent_temps.ContainsKey(voxel)) this.recent_temps[voxel] = new();
        double[] temp = new double[3];
        temp[0] = 0;
        temp[1] = 0;
        temp[2] = 0;
        VoxelyzeEngine.GetVoxelTemperature(voxel, temp);
        double3 result = new();
        result.x = temp[0];
        result.y = temp[1];
        result.z = temp[2];

        this.recent_temps[voxel].Enqueue(result);
        if (this.recent_temps[voxel].Count > NUM_OF_TEMPS_TO_STORE)
        {
            this.recent_temps[voxel].Dequeue();
        }
    }

    public float3 GetAverageTemp(CVX_Voxel voxel)
    {
        double3 result = double3.zero;
        foreach (double3 temperature in this.recent_temps[voxel])
        {
            result += temperature;
        }
        result /= this.recent_temps[voxel].Count;
        return (float3)result;
    }


    public float3 GetAverageVoxelLinearVelocity(CVX_Voxel voxel)
    {
        double3 result = double3.zero;
        foreach(double3 velocity in this.recent_velocities[voxel])
        {
            result += velocity;
        }
        result /= this.recent_velocities[voxel].Count;
        return (float3)result;
    }
    

    public float3 GetAverageVoxelLinearVelocityNthDifference(CVX_Voxel voxel)
    {
        List<double3> nth_difference = new(this.recent_velocities[voxel]);

        while(nth_difference.Count > 1)
        {
            List<double3> new_nth_difference = new();
            for(int i=1; i< nth_difference.Count; i++)
            {
                new_nth_difference.Add(nth_difference[i] - nth_difference[i - 1]);
            }
            nth_difference = new_nth_difference;
        }

        return (float3)nth_difference[0];
    }

    public float3 GetVoxelAngularAccelerationAndCache(CVX_Voxel voxel)
    {
        double3 w2 = VoxelyzeEngine.GetVoxelAngularVelocity(voxel);
        double3 w1 = this.voxel_angular_velocities[voxel];
        double3 result = (float3)(w2 - w1) / GlobalConfig.ANIMAT_BRAIN_UPDATE_PERIOD;
        this.voxel_angular_velocities[voxel] = w2;
        max_angular_acceleration = math.max(max_angular_acceleration, math.abs(result.x));
        max_angular_acceleration = math.max(max_angular_acceleration, math.abs(result.y));
        max_angular_acceleration = math.max(max_angular_acceleration, math.abs(result.z));
        if (max_angular_acceleration > 0) result /= max_angular_acceleration;
        return (float3)result;
    }

    internal float3 GetVoxelLinearAccelerationAndCache(CVoxelyze voxel)
    {
        double3 v2 = VoxelyzeEngine.GetVoxelVelocity(voxel);
        double3 v1 = this.voxel_linear_velocities[voxel];
        double3 result = (float3)(v2 - v1) / GlobalConfig.ANIMAT_BRAIN_UPDATE_PERIOD;
        this.voxel_linear_velocities[voxel] = v2;
        max_linear_acceleration = math.max(max_linear_acceleration, math.abs(result.x));
        max_linear_acceleration = math.max(max_linear_acceleration, math.abs(result.y));
        max_linear_acceleration = math.max(max_linear_acceleration, math.abs(result.z));
        if (max_linear_acceleration > 0) result /= max_linear_acceleration;
        return (float3)result;
    }

    internal float3 GetVoxelStrainNormalizedAndCache(CVoxelyze cvx_voxel)
    {
        double3 result = VoxelyzeEngine.GetVoxelStrain(cvx_voxel);
        max_strain = math.max(max_strain, math.abs(result.x));
        max_strain = math.max(max_strain, math.abs(result.y));
        max_strain = math.max(max_strain, math.abs(result.z));
        if (max_strain > 0) result /= max_strain;
        return (float3)result;
    }


    public void AddForce()
    {
        foreach (CVX_Voxel? voxel in this.cvx_voxels)
        {
            if(voxel != null)
            {
                VoxelyzeEngine.AddForceToVoxel((CVX_Voxel)voxel, 0, 0.75f, 0.75f);
            }
            
        }
    }


    // activation in [-1,1]
    public void SinusoidalMovementFromNeuronActivation(CVX_Voxel voxel, float activation)
    {
        if (!this.voxel_sinusoid_states.ContainsKey(voxel)) this.voxel_sinusoid_states[voxel] = 0;
        this.voxel_sinusoid_states[voxel] += activation;
        float new_temp = TEMP_MAX_MAGNITUDE * math.sin(SINUSOID_SPEED * this.voxel_sinusoid_states[voxel]);
        SetVoxelTemperature(voxel, new_temp);
    }


    // activation in [-1,1]
    public void SetVoxelTemperatureFromNeuronActivation(CVX_Voxel voxel, float activation)
    {
        SetVoxelTemperature(voxel, activation * TEMP_MAX_MAGNITUDE);
    }

    // activation in [-1,1]
    public void SetVoxelTemperatureXFromNeuronActivation(CVX_Voxel voxel, float activation)
    {
        VoxelyzeEngine.SetVoxelTemperatureXdirection(voxel, activation * TEMP_MAX_MAGNITUDE);
    }

    // activation in [-1,1]
    public void SetVoxelTemperatureYFromNeuronActivation(CVX_Voxel voxel, float activation)
    {
        // Z in Unity is Y in voxelyze
        VoxelyzeEngine.SetVoxelTemperatureYdirection(voxel, activation * TEMP_MAX_MAGNITUDE);
    }


    // activation in [-1,1]
    public void SetVoxelTemperatureZFromNeuronActivation(CVX_Voxel voxel, float activation)
    {
        // Z in Unity is Y in voxelyze
        VoxelyzeEngine.SetVoxelTemperatureZdirection(voxel, activation * TEMP_MAX_MAGNITUDE);
    }

    // temp in 
    public void SetVoxelTemperature(CVX_Voxel voxel, float temperature)
    {
        VoxelyzeEngine.SetVoxelTemperature(voxel, temperature);
    }

    public static int3[] neighbor_offsets = new int3[]
    {
            new int3(0, 0, 1),
            new int3(0, 1, 0),
            new int3(1, 0, 0),
            new int3(0, 0, -1),
            new int3(0, -1, 0),
            new int3(-1, 0, 0),
    };



    public static bool IsVoxelConnectedToNeighbor(int i, RobotVoxel[] voxels, int3 dimensions)
    {
        int3 coords = GlobalUtils.Index_int3FromFlat(i, dimensions);

        foreach(int3 offset in neighbor_offsets) {
            int3 new_coords = coords + offset;
            if (GlobalUtils.IsOutOfBounds(new_coords, dimensions)) continue;
            int flat = GlobalUtils.Index_FlatFromint3(new_coords, dimensions);
            if (voxels[flat] != RobotVoxel.Empty) return true;
        }

        return false;
    }

    public float3 GetCenterOfMass()
    {
        if (!contains_solid_voxels) return float3.zero;
        double[] cpp_result = new double[3];
        double3 result = VoxelyzeEngine.GetVoxelyzeCenterOfMass(this.cpp_voxel_object);
        return (float3)result * this.scale;
    }

    // give input in [-1,1]
    public void SetAmbientTemperature(float temp)
    {
       VoxelyzeEngine.SetAmbientTemperature(this.cpp_voxel_object, temp);
    }


    public void DoTimestep()
    {
        if (crashed)
        {
            Debug.LogError("Can't do timestep; Voxelyze crashed.");
            return;
        }
        if (update_task != null) update_task.Wait();
        this.mesh.UpdateMesh();

        update_task = Task.Run(() =>
        {
            float elapsed_simulation_time = 0;
            int iterations = 0;
            while (elapsed_simulation_time < this.fixed_delta_time)
            {
                if (iterations >= GlobalConfig.MAX_VOXELYZE_ITERATIONS) break;
                this.recommended_timestep = VoxelyzeEngine.GetRecommendedTimestep(this.cpp_voxel_object);
                elapsed_simulation_time += this.recommended_timestep;
                if (float.IsNaN(time) || float.IsInfinity(time)) time = 0;
                if (!VoxelyzeEngine.DoNextTimestep(this.cpp_voxel_object, this.recommended_timestep)) crashed = true;
                if (crashed) break;
                iterations++;
            }
            if (iterations > GlobalConfig.MAX_VOXELYZE_ITERATIONS) Debug.LogWarning("Capped out Voxelyze iterations.");
            time += elapsed_simulation_time;
            last_frame_elapsed_simulation_time = elapsed_simulation_time;
        });


    }


    CVX_Voxel AddVoxel(CVX_Material pMaterial, int x, int y, int z)
    {
        CVX_Voxel voxel = VoxelyzeEngine.SetVoxelMaterial(this.cpp_voxel_object, pMaterial, x, y + height_offset, z); //Voxel at index x=0, y=0. z=0
        return voxel;
    }

    public void OnDestroy()
    {
        this.counter.Dispose();
        this.current_stage.Dispose();
        this.diverged.Dispose();
    }

    public Quaternion GetVoxelRotation(CVX_Voxel cvx_voxel)
    {
        double[] result = new double[4];
        VoxelyzeEngine.GetVoxelRotation(cvx_voxel, result);
        return new Quaternion((float)result[1], (float)result[2], (float)result[3], (float)result[0]);
    }



    public class SoftVoxelMesh
    {
        int num_of_vertex_coordinates;
        int num_of_quad_vertex_idxs;
        int num_of_vertices = 0;
        int num_of_quads;
        int num_of_triangle_idxs;
        public CVX_MeshRender voxel_mesh;
        public Mesh unity_mesh;
        Color[] colors;

        public SoftVoxelMesh(CVoxelyze voxel_object, Color? override_mesh_color)
        {
            this.voxel_mesh = VoxelyzeEngine.CreateMesh(voxel_object);

            this.num_of_vertex_coordinates = VoxelyzeEngine.GetMeshNumberOfVertices(voxel_mesh);

            this.num_of_quad_vertex_idxs = VoxelyzeEngine.GetMeshNumberOfQuads(voxel_mesh);

            this.num_of_vertices = this.num_of_vertex_coordinates / 3; // 3 coordinates per vertex
            this.num_of_quads = this.num_of_quad_vertex_idxs / 4; // 4 vertices per quad
            this.num_of_triangle_idxs = num_of_quads * 2 * 3; // 2 triangles per quad, 3 vertices per triangle

            this.unity_mesh = new Mesh();

            if (override_mesh_color == null)
            {
                this.colors = GetVertexColors();
            }
            else
            {
                colors = new Color[this.num_of_vertices];
                for (int i = 0; i < this.num_of_vertices; i++)
                {
                    colors[i] = (Color)override_mesh_color;
                }
            }

            this.UpdateMesh();
        }

        public void OverrideMeshColor(Color color)
        {
            colors = new Color[this.num_of_vertices];
            for (int i = 0; i < this.num_of_vertices; i++)
            {
                colors[i] = (Color)color;
            }
        }

        // call DLL to get vertex colors
        public Color[] GetVertexColors()
        {
            List<int> quad_vertex_idxs = new();
            for (int i = 0; i < this.num_of_quad_vertex_idxs; i++)
            {
                int quad_vertex_idx = VoxelyzeEngine.GetNextQuadValue(voxel_mesh, i);
                quad_vertex_idxs.Add(quad_vertex_idx);
            }

            Color[] quad_colors = new Color[this.num_of_quads];
            for (int i = 0; i < this.num_of_quads; i++)
            {

                float quad_color_r = VoxelyzeEngine.GetNextQuadColorValue(voxel_mesh, 3 * i);
                float quad_color_g = VoxelyzeEngine.GetNextQuadColorValue(voxel_mesh, 3 * i + 1);
                float quad_color_b = VoxelyzeEngine.GetNextQuadColorValue(voxel_mesh, 3 * i + 2);
                quad_colors[i] = new(quad_color_r, quad_color_g, quad_color_b);
            }


            // loop over quad colors, and determine the color sets of their vertices
            Dictionary<int, List<Color>> vertexIdx_to_color = new();

            int quad_idx = 0;
            for (int i = 0; i < quad_vertex_idxs.Count; i += 4)
            {
                Color quad_color = quad_colors[quad_idx];

                int v1 = quad_vertex_idxs[i + 0];
                int v2 = quad_vertex_idxs[i + 1];
                int v3 = quad_vertex_idxs[i + 2];
                int v4 = quad_vertex_idxs[i + 3];

                int[] vert_idxs = new int[] { v1, v2, v3, v4 };

                foreach (int vert_idx in vert_idxs)
                {
                    if (!vertexIdx_to_color.ContainsKey(vert_idx))
                    {
                        vertexIdx_to_color[vert_idx] = new();

                    }
                    vertexIdx_to_color[vert_idx].Add(quad_color);
                }
                quad_idx++;
            }

            Color[] vertex_colors = new Color[this.num_of_vertices];
            // loop over vertices and set colors by averaging color sets
            for (int i = 0; i < this.num_of_vertices; i++)
            {
                Color average_color = new(0, 0, 0, 0);
                foreach (Color color in vertexIdx_to_color[i])
                {
                    average_color += color;
                }
                average_color /= vertexIdx_to_color[i].Count;
                vertex_colors[i] = average_color;
                vertex_colors[i].a = 1;
            }

            return vertex_colors;
        }

        // call DLL to update mesh
        public void UpdateMesh()
        {
            VoxelyzeEngine.UpdateMesh(this.voxel_mesh);
            Mesh.MeshDataArray mesh_data_array = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData mesh_data = mesh_data_array[0];
            mesh_data.SetIndexBufferParams(this.num_of_triangle_idxs, IndexFormat.UInt32);
            mesh_data.SetVertexBufferParams(this.num_of_vertices,
                new VertexAttributeDescriptor(VertexAttribute.Position));

            // get vertices
            ParallelGetSoftVoxelMeshVertices job_vertices = new()
            {
                mesh_data = mesh_data,
                voxel_mesh = this.voxel_mesh
            };
            JobHandle job_vertices_handle = job_vertices.Schedule(this.num_of_vertices, 256);

            // get triangles
            ParallelGetSoftVoxelMeshTriangles job_triangles = new()
            {
                mesh_data = mesh_data,
                voxel_mesh = this.voxel_mesh
            };
            JobHandle job_triangles_handle = job_triangles.Schedule(this.num_of_quads, 256);

            // complete the jobs
            job_vertices_handle.Complete();
            job_triangles_handle.Complete();

            //finalize mesh

            mesh_data.subMeshCount = 1;
            mesh_data.SetSubMesh(0, new SubMeshDescriptor(0, mesh_data.GetIndexData<UInt32>().Length));

            Mesh.ApplyAndDisposeWritableMeshData(mesh_data_array, this.unity_mesh);
            this.unity_mesh.RecalculateBounds();
            this.unity_mesh.colors = this.colors;
        }
    }
}
