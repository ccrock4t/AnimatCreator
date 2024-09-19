using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Brain;
using static GlobalConfig;
using static SoftVoxelRobot;
using Debug = UnityEngine.Debug;
using CVX_Voxel = System.IntPtr;
using static CPPNGenome;
using static LinearAndNEATGenome.NEATGenome;

/// <summary>
///     Script attached to neural network agent. Coordinates neural network and the body.
/// </summary>
public class Animat : MonoBehaviour
{
    public object genome;
    public CPPNGenome unified_CPPN_genome;
    public BrainBodyDualGenome dual_CPPN_genome;
    public LinearAndNEATGenome linear_and_neat_genome;
    public Brain brain;
    public SoftVoxelRobot body;

    public Transform animat_creator_food_block;

    public bool initialized = false;

    public float3 birthplace;
    public float3 birthplace_forward_vector;
    public float original_distance_from_food;
    public float3 food_position;


    //score
    public int number_of_food_eaten;
    public int times_reproduced;

    public List<BehaviorCharacterizationDatapoint> behavior_characterization;

    public float score_multiplier;

    public int MAX_LIFESPAN = 30;
    public float lifespan; // in seconds

    public static float ENERGY_IN_A_FOOD;
    public static float MAX_ENERGY;
    public float energy_remaining;
    public float MOTOR_ENERGY_SPEND_SCALE_FACTOR = 1;

    public float brain_update_period;

    public List<ArticulationBody> listab;

    public const float MAX_VISION_DISTANCE = 15f;


    public Dictionary<int, float> motor_idx_to_new_activation;

    public MultiLayerNetworkInfo network_info;
    int5 dimensions;
    public int3 dimensions3D;

    NativeArray<RobotVoxel> robot_voxels;
    bool body_created = false;

    public Task develop_task = null;

    public struct BehaviorCharacterizationDatapoint
    {
        public float3 offset_from_birthplace;
        public int number_of_food_eaten;
    }

    public Color? override_color = null;

    public void Initialize(object genome)
    {
        if (genome is CPPNGenome)
        {
            this.unified_CPPN_genome = (CPPNGenome)genome;

        }
        else if (genome is BrainBodyDualGenome)
        {
            this.dual_CPPN_genome = (BrainBodyDualGenome)genome;
        }
        else if (genome is LinearAndNEATGenome)
        {
            this.linear_and_neat_genome = (LinearAndNEATGenome)genome;
        }
        else
        {
            Debug.LogError("Invalid genome type.");
            return;
        }

        this.genome = genome;

        // set vars
        this.brain_update_period = GlobalConfig.ANIMAT_BRAIN_UPDATE_PERIOD;
        this.motor_idx_to_new_activation = new();
        this.behavior_characterization = new();
        this.number_of_food_eaten = 0;
        this.times_reproduced = 0;


        // read the genome
        this.dimensions = GlobalConfig.ANIMAT_SUBSTRATE_DIMENSIONS;
        this.dimensions3D = new(this.dimensions.x, this.dimensions.y, this.dimensions.z);


        int5 substrate_dimensions = this.dimensions;
        int substrate_size = substrate_dimensions.x * substrate_dimensions.y * substrate_dimensions.z * substrate_dimensions.w * substrate_dimensions.v;
        int substrate_size3D = substrate_dimensions.x * substrate_dimensions.y * substrate_dimensions.z;
        if (ENERGY_IN_A_FOOD == 0)
        {
            ENERGY_IN_A_FOOD = 20 * substrate_size;
            MAX_ENERGY = 5 * ENERGY_IN_A_FOOD;
        }
        this.energy_remaining = ENERGY_IN_A_FOOD;

        network_info = new(GlobalConfig.NUM_OF_SENSOR_NEURONS,
            GlobalConfig.NUM_OF_HIDDEN_NEURONS_PER_LAYER,
            GlobalConfig.NUM_OF_MOTOR_NEURONS,
            GlobalConfig.NUM_OF_HIDDEN_LAYERS);

        // body 
        this.robot_voxels = new(length: substrate_size3D, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        // brain
        int num_of_neurons = -1;
        int num_of_synapses = -1;
        if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN)
        {
            int neurons_per_voxel = GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL ? network_info.GetNumOfNeurons() : 1;
            num_of_neurons = substrate_size3D * neurons_per_voxel;
            num_of_synapses = num_of_neurons * num_of_neurons;
        }
        else if (GlobalConfig.GENOME_METHOD == GenomeMethod.LinearGenomeandNEAT)
        {
            num_of_neurons = this.linear_and_neat_genome.brain_genome.nodes.Count;
            num_of_synapses = this.linear_and_neat_genome.brain_genome.connections.Count;
        }
        else
        {
            Debug.LogError("error");
        }

        NativeArray<Neuron> neurons = new(length: num_of_neurons, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        NativeArray<Synapse> synapses = new(length: num_of_synapses, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);


        //develop the brain and body in a separate thread
        this.develop_task = Task.Run(() =>
        {
            if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN)
            {
                ParallelHyperNEATCPU job = new()
                {
                    network_info = network_info,
                    dimensions = this.dimensions,
                    dimensions3D = this.dimensions3D,
                    neurons = neurons,
                    synapses = synapses,
                    robot_voxels = robot_voxels,

                    CPPN_nodes = this.unified_CPPN_genome.CPPN_nodes,
                    CPPN_connections = this.unified_CPPN_genome.CPPN_connections,

                    CPPN_IO_NODES_INDEXES = this.unified_CPPN_genome.CPPN_IO_IDXS
                };
                job.Run(synapses.Length);
            }
            else if (GlobalConfig.GENOME_METHOD == GenomeMethod.LinearGenomeandNEAT)
            {
                // ===== Transcribe brain genome to a brain
                // neurons. get neuron ID to idx (for brainviewer, allows checking for correctness)
                Dictionary<int, int> nodeID_to_idx = new();
                int i = 0;
                foreach (NEATNode node in this.linear_and_neat_genome.brain_genome.nodes)
                {
                    nodeID_to_idx[node.ID] = i;
                    i++;
                }

                // connections. First group them together
                Dictionary<int, List<NEATConnection>> nodeID_to_connections = new();
                foreach (NEATConnection connection in this.linear_and_neat_genome.brain_genome.connections)
                {
                    if (!nodeID_to_connections.ContainsKey(connection.toID))
                    {
                        nodeID_to_connections[connection.toID] = new();
                    }
                    nodeID_to_connections[connection.toID].Add(connection);
                }

                // connections. Next place them in the array
                Dictionary<int, int> nodeID_to_startIdx = new();
                i = 0;
                foreach (KeyValuePair<int, List<NEATConnection>> node_connections in nodeID_to_connections)
                {
                    int nodeID = node_connections.Key;
                    nodeID_to_startIdx[nodeID] = i;
                    foreach (NEATConnection connection in node_connections.Value)
                    {
                        Synapse synapse = Synapse.GetDefault();
                        synapse.from_neuron_idx = nodeID_to_idx[connection.fromID];
                        synapse.to_neuron_idx = nodeID_to_idx[connection.toID];

                        synapse.enabled = connection.enabled;
                        if (connection.toID != nodeID)
                        {
                            Debug.LogError("error");
                        }
                        synapse.weight = connection.weight;
                        synapses[i] = synapse;
                        i++;
                    }

                }

                // neurons. Set their synapse start idx and count
                i = 0;
                Dictionary<(int3, Neuron.NeuronClass), int> num_of_neuron_type_placed = new();
                foreach (NEATNode node in this.linear_and_neat_genome.brain_genome.nodes)
                {
                    Neuron neuron = Neuron.GetNewNeuron();
                    neuron.bias = node.bias;
                    neuron.neuron_class = node.type;
                    int layer_num;
                    if (neuron.neuron_class == Neuron.NeuronClass.Sensor)
                    {
                        layer_num = 0;
                    }
                    else if (neuron.neuron_class == Neuron.NeuronClass.Motor)
                    {
                        layer_num = 2;
                    }
                    else
                    { //hidden
                        layer_num = 1;
                    }

                    int w = 0;
                    if (neuron.neuron_class != Neuron.NeuronClass.Hidden)
                    {
                        (int3, Neuron.NeuronClass) key = ((int3)node.coords, neuron.neuron_class);
                        if (!num_of_neuron_type_placed.ContainsKey(key)) num_of_neuron_type_placed[key] = 0;
                        w = num_of_neuron_type_placed[key];
                        num_of_neuron_type_placed[key] += 1;
                    }
                    if (neuron.neuron_class != Neuron.NeuronClass.Hidden) neuron.position_idxs = new((int)node.coords.x, (int)node.coords.y, (int)node.coords.z, w, layer_num);
                    neuron.position_normalized = new((float)node.coords.x / dimensions3D.x,
                        (float)node.coords.y / dimensions3D.y,
                        (float)node.coords.z / dimensions3D.z,
                        w / 2f,
                        (layer_num - 1) / 2f);

                    int synapse_start_idx;
                    int synapse_count;
                    if (nodeID_to_startIdx.ContainsKey(node.ID))
                    {
                        synapse_start_idx = nodeID_to_startIdx[node.ID];
                        synapse_count = nodeID_to_connections[node.ID].Count;
                    }
                    else
                    {
                        synapse_start_idx = 0;
                        synapse_count = 0;
                    }
                    neuron.synapse_start_idx = synapse_start_idx;
                    neuron.synapse_count = synapse_count;
                    neurons[nodeID_to_idx[node.ID]] = neuron;
                }

                // ===== Transcribe body genome to a body
                i = 0;
                foreach (RobotVoxel voxel in this.linear_and_neat_genome.body_genome)
                {
                    this.robot_voxels[i] = voxel;
                    i++;
                }
            }
            else
            {
                Debug.LogError("error not implemented");
            }

            // === create brain object

            Brain brain;
            if (GlobalConfig.brain_processing_method == GlobalConfig.ProcessingMethod.CPU)
            {
                brain = new BrainCPU(neurons, synapses);
            }
            else if (GlobalConfig.brain_processing_method == GlobalConfig.ProcessingMethod.GPU)
            {
                brain = new BrainGPU(neurons, synapses);
            }
            else
            {
                GlobalUtils.LogErrorEnumNotRecognized("NOT SUPPORTED");
            }

            this.brain = brain;

            // body will be created on the main thread later, since it requires creating a Unity GameObject
        });

        // develop the brain
        this.SetLifespan(UnityEngine.Random.Range(MAX_LIFESPAN - 5, MAX_LIFESPAN));
        // this.initialized = true;
    }

    public void Update()
    {
        if (!body_created)
        {
            if (this.IsGenomeTranscribed())
            {
                // === create body
                GameObject body_GO = new GameObject("Body");
                body_GO.transform.parent = this.transform;
                body_GO.transform.localPosition = Vector3.zero;

                this.body = body_GO.AddComponent<SoftVoxelRobot>();
                this.body.Initialize(robot_voxels, override_color);
                this.body.transform.parent = this.transform;
                this.body.Teleport(Vector3.zero, Quaternion.identity);

                // flag as fully initialized
                this.initialized = true;
                body_created = true;

                robot_voxels.Dispose();
            }
        }
    }

    public void SetLifespan(int lifespan_in_seconds)
    {
        this.lifespan = lifespan_in_seconds;
    }

    void OnApplicationQuit()
    {
        // manually dispose of all unmanaged memory
        DiposeOfAllocatedMemory();
    }


    public void DiposeOfAllocatedMemory()
    {
        if (this.develop_task != null)
        {
            this.develop_task.Wait();
            this.develop_task.Dispose();
        }

        if (this.brain != null) this.brain.DisposeOfNativeCollections();
        if (this.unified_CPPN_genome != null) this.unified_CPPN_genome.DisposeOfNativeCollections();
        if (this.dual_CPPN_genome.brain_genome != null) this.dual_CPPN_genome.brain_genome.DisposeOfNativeCollections();
        if (this.dual_CPPN_genome.body_genome != null) this.dual_CPPN_genome.body_genome.DisposeOfNativeCollections();

    }


    bool birthed = false;
    float operate_brain_timer = 0;
    float behavior_characterization_timer = 0;
    float BEHAVIOR_CHARACTERIZATION_RECORD_PERIOD = 1;
    public void DoFixedUpdate()
    {
        if (!this.initialized)
        {
            return;
        }
        else
        {
            if (!this.birthed)
            {
                this.body.SetColorToColorful();

                this.birthplace = GetCenterOfMass();
                this.birthplace_forward_vector = Vector3.right;
                this.original_distance_from_food = Vector3.Distance(this.food_position, GetCenterOfMass());

                this.birthed = true;
            }
        }

        behavior_characterization_timer -= Time.fixedDeltaTime;
        if (behavior_characterization_timer < 0)
        {
            this.RecordBehaviorCharacterizationSnapshot();
            behavior_characterization_timer = BEHAVIOR_CHARACTERIZATION_RECORD_PERIOD;
        }

        operate_brain_timer -= Time.fixedDeltaTime;
        if (operate_brain_timer < 0)
        {
            this.OperateAnimatOneCycle();
            operate_brain_timer = this.brain_update_period;
        }

        this.body.DoTimestep();
        this.lifespan -= this.body.soft_voxel_object.last_frame_elapsed_simulation_time;
    }

    public void OperateAnimatOneCycle()
    {
        if (!this.initialized) return;

        if (GlobalConfig.brain_processing_method == ProcessingMethod.CPU && !((BrainCPU)this.brain).update_job_handle.Equals(null))
        {
            ((BrainCPU)this.brain).update_job_handle.Complete();
            ((BrainCPU)this.brain).SwapCurrentAndNextStates();
        }

        this.SpendEnergy();

        if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN)
        {
            this.MotorEffectCPPN();
            this.SenseCPPN();
        }
        else if (GlobalConfig.GENOME_METHOD == GenomeMethod.LinearGenomeandNEAT)
        {
            this.MotorEffectLinearGenome();
            this.SenseLinearGenome();
        }



        this.brain.ScheduleWorkingCycle();
    }


    public bool IsGenomeTranscribed()
    {
        if (this.develop_task == null) return false;
        if (this.develop_task.IsFaulted)
        {
            Debug.LogError("ERROR TASK FAULTED");
            return false;
        }
        return this.develop_task.IsCompleted; // return if task is completed
    }

    public (Vector3, Vector3) GetRaycastPositionAndDirection(Vector3 transform_position, CVX_Voxel cvx_voxel, int3 coords)
    {
        double3 voxel_relative_position = VoxelyzeEngine.GetVoxelCenterOfMass(cvx_voxel);
        Quaternion voxel_rotation = this.body.soft_voxel_object.GetVoxelRotation(cvx_voxel);
        int num_of_raycasts = 0;
        Vector3 direction = Vector3.zero;
        for (int i = 0; i < SoftVoxelObject.neighbor_offsets.Length; i++)
        {
            int3 offset = SoftVoxelObject.neighbor_offsets[i];

            int3 neighbor_coords = coords + offset;
            if (!GlobalUtils.IsOutOfBounds(neighbor_coords, this.body.soft_voxel_object.dimensions))
            {
                // if the neighbor is not out of bounds, we have to check for a blocking voxel
                int neighbor_idx = GlobalUtils.Index_FlatFromint3(neighbor_coords, this.body.soft_voxel_object.dimensions);
                RobotVoxel neighbor_voxel = this.body.soft_voxel_object.robot_voxels[neighbor_idx];
                if (neighbor_voxel != RobotVoxel.Empty) continue; // continue to next face if this face is blocked
            }

            if (i == 0)
            {
                direction = (float3)offset;
            }
            else
            {
                direction = (float3)offset + (float3)direction;
            }
            num_of_raycasts++;
        }
        direction /= num_of_raycasts;
        direction = voxel_rotation * direction;
        direction = Vector3.Normalize(direction);

        Vector3 voxel_global_position = transform_position + (new Vector3((float)voxel_relative_position.x, (float)voxel_relative_position.y, (float)voxel_relative_position.z)) * this.body.soft_voxel_object.scale;

        return (voxel_global_position, direction);
    }

    public Vector3 GetVoxelCenterOfMass(Vector3 transform_position, CVX_Voxel cvx_voxel)
    {
        double3 voxel_relative_position = VoxelyzeEngine.GetVoxelCenterOfMass(cvx_voxel);
        return transform_position + (new Vector3((float)voxel_relative_position.x, (float)voxel_relative_position.y, (float)voxel_relative_position.z)) * this.body.soft_voxel_object.scale;
    }

    bool draw_sensor_raycasts = true;
    const float sine_speed = 3;

    public void SenseLinearGenome()
    {
        Vector3 transform_position = transform.position;
        if (!this.initialized) return;
        float current_time = Time.time;
        // random
        uint seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        // first do most of the sensors
        // foreach (KeyValuePair<int3, CVX_Voxel> cvx_voxel_pair in this.body.soft_voxel_object.sensor_voxels)
        Parallel.ForEach(this.body.soft_voxel_object.sensor_voxels, (cvx_voxel_pair, loop_state, loop_index) =>
        {
            int3 coords = cvx_voxel_pair.Item1;
            CVX_Voxel cvx_voxel = cvx_voxel_pair.Item2;
            int cell_flat_idx = GlobalUtils.Index_FlatFromint3(coords, this.dimensions3D);
            RobotVoxel robot_voxel = this.body.soft_voxel_object.robot_voxels[cell_flat_idx];
            this.body.soft_voxel_object.UpdateVoxelLinearVelocityCache(cvx_voxel);
            this.body.soft_voxel_object.UpdateVoxelTemperatureCache(cvx_voxel);
            float3 voxel_linear_velocity = this.body.soft_voxel_object.GetAverageVoxelLinearVelocity(cvx_voxel);
            float3 voxel_linear_velocity_nth_diff = this.body.soft_voxel_object.GetAverageVoxelLinearVelocityNthDifference(cvx_voxel);

            float3 voxel_strain = this.body.soft_voxel_object.GetVoxelStrainNormalizedAndCache(cvx_voxel);

            bool is_touching_ground = VoxelyzeEngine.GetVoxelFloorPenetration(cvx_voxel) > 0;

            float3 temp = this.body.soft_voxel_object.GetAverageTemp(cvx_voxel);


            // populate all the sensor neurons for this cell
            int k = 0;
            int sensory_neuron_idx = LinearAndNEATGenome.NEATGenome.NUM_OF_SENSOR_NEURONS * cell_flat_idx + k;
            Neuron sensory_neuron = this.brain.GetNeuronCurrentState(sensory_neuron_idx);
            if (sensory_neuron.neuron_class != Neuron.NeuronClass.Sensor) Debug.LogError("error");
            if (robot_voxel == RobotVoxel.Touch_Sensor)
            {
                // touch
                sensory_neuron.activation = is_touching_ground ? 1 : 0;
            }
            else if (robot_voxel == RobotVoxel.Raycast_Sensor || robot_voxel == RobotVoxel.Mouth)
            {
                // raycast
            }
            else if (robot_voxel == RobotVoxel.SineWave_Generator)
            {
                // sine wave driving function
                sensory_neuron.activation = math.sin(sine_speed * 2 * math.PI * current_time);

            }
            else
            {
                Debug.LogError("error. " + robot_voxel);
            }

            this.brain.SetNeuronCurrentState(sensory_neuron_idx, sensory_neuron);

        });

        //now do raycasting sensors
        int num_raycasts_per_voxel = 5;

        NativeArray<RaycastCommand> raycast_commands = new(this.body.soft_voxel_object.raycast_sensor_voxels.Count * num_raycasts_per_voxel, Allocator.TempJob);
        NativeArray<RaycastHit> raycast_results = new(raycast_commands.Length, Allocator.TempJob);

  
        int raycast_food_and_obstacle_layerMask = (1 << AnimatArena.FOOD_GAMEOBJECT_LAYER)
            | (1 << AnimatArena.OBSTACLE_GAMEOBJECT_LAYER);


        // queue up the raycasts
        Parallel.ForEach(this.body.soft_voxel_object.raycast_sensor_voxels, (cvx_voxel_pair, loop_state, loop_index) =>
        {
            int3 coords = cvx_voxel_pair.Item1;
            CVX_Voxel cvx_voxel = cvx_voxel_pair.Item2;
            Quaternion voxel_rotation = this.body.soft_voxel_object.GetVoxelRotation(cvx_voxel);
            (Vector3 position, Vector3 direction) = GetRaycastPositionAndDirection(transform_position, cvx_voxel, coords);
            QueryParameters query_params = QueryParameters.Default;

            // set layermark
            query_params.layerMask = raycast_food_and_obstacle_layerMask;


            RaycastCommand raycast_command = new RaycastCommand(position, direction, query_params, MAX_VISION_DISTANCE);
            raycast_commands[(int)loop_index * num_raycasts_per_voxel] = raycast_command;

            int idx = 1;
            // create raycast command
            float degrees_offset = 2;
            for (int i = 0; i < 2; i++)
            {
                Vector3 axis;
                if (i == 0) axis = Vector3.up;
                else axis = Vector3.left;
                for (int j = -1; j < 2; j += 2)
                {
                    Vector3 rotated_axis = Vector3.Cross(direction.normalized, Quaternion.Inverse(voxel_rotation) * (j * axis));
                    Vector3 rotated_direction = Quaternion.AngleAxis(degrees_offset, rotated_axis) * direction.normalized;

                    raycast_command = new RaycastCommand(position, rotated_direction, query_params, MAX_VISION_DISTANCE);
                    raycast_commands[(int)loop_index * num_raycasts_per_voxel + idx] = raycast_command;
                    idx++;
                }
            }



        });

        // Execute the batch of raycasts in parallel
        JobHandle handle = RaycastCommand.ScheduleBatch(raycast_commands, raycast_results, 1, 1, default(JobHandle));
        handle.Complete();


        // handle results
        int loop_index = 0;
        foreach (var cvx_voxel_pair in this.body.soft_voxel_object.raycast_sensor_voxels)
        {
            int3 coords = cvx_voxel_pair.Item1;
            CVX_Voxel cvx_voxel = cvx_voxel_pair.Item2;
            int cell_flat_idx = GlobalUtils.Index_FlatFromint3(coords, this.dimensions3D);
            float3 voxel_linear_velocity = this.body.soft_voxel_object.GetAverageVoxelLinearVelocity(cvx_voxel);
            RobotVoxel robot_voxel = this.body.soft_voxel_object.robot_voxels[cell_flat_idx];

            GameObject food_hit_GO = null;
            GameObject animat_hit_GO = null;
            float max_food_activation = 0;
            float max_obstacle_activation = 0;
            float max_animat_activation = 0;
            for (int i = 0; i < num_raycasts_per_voxel; i++)
            {
                RaycastHit raycast_hit = raycast_results[(int)loop_index * num_raycasts_per_voxel + i];
                RaycastCommand raycast_cmd = raycast_commands[(int)loop_index * num_raycasts_per_voxel + i];

                if (raycast_hit.distance != 0)
                {
   
                    float closeness = (MAX_VISION_DISTANCE - raycast_hit.distance) / MAX_VISION_DISTANCE;
    ;
                    if (raycast_hit.transform.gameObject.layer == AnimatArena.FOOD_GAMEOBJECT_LAYER)
                    {
                        // food was hit
                        if (closeness > max_food_activation)
                        {
                            max_food_activation = closeness;
                            food_hit_GO = raycast_hit.transform.gameObject;
                        }
                    }
                    else if (raycast_hit.transform.gameObject.layer == AnimatArena.OBSTACLE_GAMEOBJECT_LAYER)
                    {
                        // obstacle was hit
                        max_obstacle_activation = math.max(closeness, max_obstacle_activation);
                    }
                    else if (raycast_hit.transform.gameObject.layer == AnimatArena.ANIMAT_GAMEOBJECT_LAYER)
                    {
                        // animat was hit
                        if (closeness > max_animat_activation)
                        {
                            max_animat_activation = closeness;
                            animat_hit_GO = raycast_hit.transform.gameObject;
                        }
                    }
                    else
                    {
                        Debug.LogError("error");
                    }

                    if (draw_sensor_raycasts)
                    {
                        Debug.DrawRay(raycast_cmd.from, raycast_cmd.direction * raycast_hit.distance, new Color(0, closeness, 0), this.brain_update_period);
                    }
                }
                else
                {
                    // no hit
                    if (draw_sensor_raycasts)
                    {
                        Debug.DrawRay(raycast_cmd.from, raycast_cmd.direction * MAX_VISION_DISTANCE, Color.red, this.brain_update_period);
                    }
                }


            }


            if (max_food_activation > 0.98f)
            {
                this.number_of_food_eaten += 1;
                this.ResetEnergyAndLifespan();
                AnimatArena.GetInstance().ChangeBlockPosition(food_hit_GO);
                break;
            }

            bool enough_energy_to_mate = this.energy_remaining > ENERGY_IN_A_FOOD;


            for (int k = 0; k < LinearAndNEATGenome.NEATGenome.NUM_OF_SENSOR_NEURONS; k++)
            {
                int raycast_sensor_neuron_idx = LinearAndNEATGenome.NEATGenome.NUM_OF_SENSOR_NEURONS * cell_flat_idx + k;
                if (robot_voxel == RobotVoxel.Raycast_Sensor)
                {
                    Neuron sensor_neuron = this.brain.GetNeuronCurrentState(raycast_sensor_neuron_idx);
                    if (sensor_neuron.neuron_class != Neuron.NeuronClass.Sensor) Debug.LogError("error");
                    if (k == 0) sensor_neuron.activation = max_food_activation;
                    if (k == 1) sensor_neuron.activation = max_obstacle_activation;
                    this.brain.SetNeuronCurrentState(raycast_sensor_neuron_idx, sensor_neuron);
                }
            }

    

            loop_index++;
        }


        raycast_results.Dispose();
        raycast_commands.Dispose();

    }

    public void SenseCPPN()
    {
        Vector3 transform_position = transform.position;
        if (!this.initialized) return;
        float current_time = Time.time;
        // random
        uint seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        // first do most of the sensors
        int neurons_per_cell = GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL ? this.network_info.GetNumOfNeurons() : 1;
        Parallel.ForEach(this.body.soft_voxel_object.sensor_voxels, (cvx_voxel_pair, loop_state, loop_index) =>
         {
             int3 coords = cvx_voxel_pair.Item1;
             CVX_Voxel cvx_voxel = cvx_voxel_pair.Item2;
             int cell_flat_idx = GlobalUtils.Index_FlatFromint3(coords, this.dimensions3D);
             RobotVoxel robot_voxel = this.body.soft_voxel_object.robot_voxels[cell_flat_idx];

             this.body.soft_voxel_object.UpdateVoxelLinearVelocityCache(cvx_voxel);
             this.body.soft_voxel_object.UpdateVoxelTemperatureCache(cvx_voxel);
             float3 voxel_linear_velocity = this.body.soft_voxel_object.GetAverageVoxelLinearVelocity(cvx_voxel);
             float3 voxel_linear_velocity_nth_diff = this.body.soft_voxel_object.GetAverageVoxelLinearVelocityNthDifference(cvx_voxel);

             bool is_touching_ground = VoxelyzeEngine.GetVoxelFloorPenetration(cvx_voxel) > 0;

             float3 temp = this.body.soft_voxel_object.GetAverageTemp(cvx_voxel);


             // populate all the sensor neurons for this cell
             int v = 0;
            int sensory_neuron_idx = cell_flat_idx * neurons_per_cell + v;
            Neuron sensory_neuron = this.brain.GetNeuronCurrentState(sensory_neuron_idx);
            if (sensory_neuron.neuron_class != Neuron.NeuronClass.Sensor) Debug.LogError("error");
            if (robot_voxel == RobotVoxel.Touch_Sensor)
            {
                // touch
                sensory_neuron.activation = is_touching_ground ? 1 : 0;
            }
            else if (robot_voxel == RobotVoxel.Raycast_Sensor)
            {
                // raycast food
            }
            else if (robot_voxel == RobotVoxel.SineWave_Generator)
            {
                // sine wave driving function
                sensory_neuron.activation = math.sin(2 * math.PI * current_time);
            }

            this.brain.SetNeuronCurrentState(sensory_neuron_idx, sensory_neuron);
    



         });

        //now do raycasting sensors
        int num_raycasts_per_voxel = 5;

        NativeArray<RaycastCommand> raycast_commands = new(this.body.soft_voxel_object.raycast_sensor_voxels.Count * num_raycasts_per_voxel, Allocator.TempJob);
        NativeArray<RaycastHit> raycast_results = new(raycast_commands.Length, Allocator.TempJob);
        int raycast_food_and_obstacle_layerMask = (1 << AnimatArena.FOOD_GAMEOBJECT_LAYER)
            | (1 << AnimatArena.OBSTACLE_GAMEOBJECT_LAYER);


        // queue up the raycasts
        Parallel.ForEach(this.body.soft_voxel_object.raycast_sensor_voxels, (cvx_voxel_pair, loop_state, loop_index) =>
        {
            int3 coords = cvx_voxel_pair.Item1;
            CVX_Voxel cvx_voxel = cvx_voxel_pair.Item2;
            Quaternion voxel_rotation = this.body.soft_voxel_object.GetVoxelRotation(cvx_voxel);
            (Vector3 position, Vector3 direction) = GetRaycastPositionAndDirection(transform_position, cvx_voxel, coords);
            QueryParameters query_params = QueryParameters.Default;

            // set layermark
            query_params.layerMask = raycast_food_and_obstacle_layerMask;


            RaycastCommand raycast_command = new RaycastCommand(position, direction, query_params, MAX_VISION_DISTANCE);
            raycast_commands[(int)loop_index * num_raycasts_per_voxel] = raycast_command;

            int idx = 1;
            // create raycast command
            float degrees_offset = 2;
            for (int i = 0; i < 2; i++)
            {
                Vector3 axis;
                if (i == 0) axis = Vector3.up;
                else axis = Vector3.left;
                for (int j = -1; j < 2; j += 2)
                {
                    Vector3 rotated_axis = Vector3.Cross(direction.normalized, Quaternion.Inverse(voxel_rotation) * (j * axis));
                    Vector3 rotated_direction = Quaternion.AngleAxis(degrees_offset, rotated_axis) * direction.normalized;

                    raycast_command = new RaycastCommand(position, rotated_direction, query_params, MAX_VISION_DISTANCE);
                    raycast_commands[(int)loop_index * num_raycasts_per_voxel + idx] = raycast_command;
                    idx++;
                }
            }



        });

        // Execute the batch of raycasts in parallel
        JobHandle handle = RaycastCommand.ScheduleBatch(raycast_commands, raycast_results, 1, 1, default(JobHandle));
        handle.Complete();


        // handle results
        int loop_index = 0;
        foreach (var cvx_voxel_pair in this.body.soft_voxel_object.raycast_sensor_voxels)
        {
            int3 coords = cvx_voxel_pair.Item1;
            CVX_Voxel cvx_voxel = cvx_voxel_pair.Item2;
            int cell_idx = GlobalUtils.Index_FlatFromint3(coords, this.dimensions3D);
            float3 voxel_linear_velocity = this.body.soft_voxel_object.GetAverageVoxelLinearVelocity(cvx_voxel);
            RobotVoxel robot_voxel = this.body.soft_voxel_object.robot_voxels[cell_idx];

            GameObject food_hit_GO = null;
            GameObject animat_hit_GO = null;
            float max_food_activation = 0;
            float max_obstacle_activation = 0;
            float max_animat_activation = 0;
            for (int i = 0; i < num_raycasts_per_voxel; i++)
            {
                RaycastHit raycast_hit = raycast_results[(int)loop_index * num_raycasts_per_voxel + i];
                RaycastCommand raycast_cmd = raycast_commands[(int)loop_index * num_raycasts_per_voxel + i];

                if (raycast_hit.distance != 0)
                {
                    float closeness = (MAX_VISION_DISTANCE - raycast_hit.distance) / MAX_VISION_DISTANCE;
                    if (raycast_hit.transform.gameObject.layer == AnimatArena.FOOD_GAMEOBJECT_LAYER)
                    {
                        // food was hit
                        if (closeness > max_food_activation)
                        {
                            max_food_activation = closeness;
                            food_hit_GO = raycast_hit.transform.gameObject;
                        }
                    }
                    else if (raycast_hit.transform.gameObject.layer == AnimatArena.OBSTACLE_GAMEOBJECT_LAYER)
                    {
                        // obstacle was hit
                        max_obstacle_activation = math.max(closeness, max_obstacle_activation);
                    }
                    else if (raycast_hit.transform.gameObject.layer == AnimatArena.ANIMAT_GAMEOBJECT_LAYER)
                    {
                        // animat was hit
                        if (closeness > max_animat_activation)
                        {
                            max_animat_activation = closeness;
                            animat_hit_GO = raycast_hit.transform.gameObject;
                        }
                    }
                    else
                    {
                        Debug.LogError("error");
                    }

                    if (draw_sensor_raycasts)
                    {
                        Debug.DrawRay(raycast_cmd.from, raycast_cmd.direction * raycast_hit.distance, new Color(0, closeness, 0), this.brain_update_period);
                    }
                }
                else
                {
                    // no hit
                    if (draw_sensor_raycasts)
                    {
                        Debug.DrawRay(raycast_cmd.from, raycast_cmd.direction * MAX_VISION_DISTANCE, Color.red, this.brain_update_period);
                    }
                }


            }


            if (max_food_activation > 0.98f)
            {
                this.number_of_food_eaten += 1;
                this.ResetEnergyAndLifespan();
                AnimatArena.GetInstance().ChangeBlockPosition(food_hit_GO);
                break;
            }

            bool enough_energy_to_mate = this.energy_remaining > ENERGY_IN_A_FOOD;
            for (int v = 0; v < GlobalConfig.NUM_OF_SENSOR_NEURONS; v++)
            {
                int sensory_neuron_idx = cell_idx * neurons_per_cell + v;
                Neuron food_neuron = this.brain.GetNeuronCurrentState(sensory_neuron_idx);
                if (v == 0) food_neuron.activation = max_food_activation;
                if (v == 1) food_neuron.activation = max_obstacle_activation;
                this.brain.SetNeuronCurrentState(sensory_neuron_idx, food_neuron);
            }


            loop_index++;
        }
 

        raycast_results.Dispose();
        raycast_commands.Dispose();

    }

    public void SpendEnergy()
    {
    }


    // set the actuators according to the motor neuron's current activation

    public void MotorEffectCPPN()
    {
        if (!this.initialized) return;

        foreach ((int3, CVX_Voxel) cvx_voxel in this.body.soft_voxel_object.motor_voxels)
        {
            int3 coords = cvx_voxel.Item1;

            int cell_idx = GlobalUtils.Index_FlatFromint3(coords, this.dimensions3D);
            RobotVoxel robot_voxel = this.body.soft_voxel_object.robot_voxels[cell_idx];

            int motor_neuron_idx;
            if (GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL)
            {
                motor_neuron_idx = cell_idx * network_info.GetNumOfNeurons() + network_info.input_layer_size;
            }
            else
            {
                motor_neuron_idx = cell_idx;
            }

            Neuron motor_neuron_X = this.brain.GetNeuronCurrentState(motor_neuron_idx);
            Neuron motor_neuron_Y = this.brain.GetNeuronCurrentState(motor_neuron_idx + 1);
            Neuron motor_neuron_Z = this.brain.GetNeuronCurrentState(motor_neuron_idx + 2);
            if (motor_neuron_X.neuron_class != Neuron.NeuronClass.Motor) Debug.LogError("error");
            if (motor_neuron_Y.neuron_class != Neuron.NeuronClass.Motor) Debug.LogError("error");
            if (motor_neuron_Z.neuron_class != Neuron.NeuronClass.Motor) Debug.LogError("error");
            float activationX = motor_neuron_X.activation;
            float activationY = motor_neuron_Y.activation;
            float activationZ = motor_neuron_Z.activation;

            if (motor_neuron_X.activation_function == Neuron.ActivationFunction.Sigmoid)
            {
                // from [0,1] to [-1,1]
                activationX = activationX * 2 - 1;
                activationY = activationY * 2 - 1;
                activationZ = activationZ * 2 - 1;
            }


            float energy_spent;
            if (float.IsNaN(activationX) || float.IsInfinity(activationX))
            {
                Debug.LogWarning("Got NaN for motor activation");
            }

            // neuron activation for regular muscle
            this.body.soft_voxel_object.SetVoxelTemperatureXFromNeuronActivation(cvx_voxel.Item2, activationX);
            this.body.soft_voxel_object.SetVoxelTemperatureYFromNeuronActivation(cvx_voxel.Item2, activationY);
            this.body.soft_voxel_object.SetVoxelTemperatureZFromNeuronActivation(cvx_voxel.Item2, activationZ);


        }
    }


    public void MotorEffectLinearGenome()
    {
        if (!this.initialized) return;

        foreach ((int3, CVX_Voxel) cvx_voxel in this.body.soft_voxel_object.motor_voxels)
        {
            int3 coords = cvx_voxel.Item1;

            int cell_idx = GlobalUtils.Index_FlatFromint3(coords, this.dimensions3D);
            RobotVoxel robot_voxel = this.body.soft_voxel_object.robot_voxels[cell_idx];


            int motor_neuron_idx = LinearAndNEATGenome.NEATGenome.NUM_OF_MOTOR_NEURONS * cell_idx
                + LinearAndNEATGenome.NEATGenome.NUM_OF_SENSOR_NEURONS * dimensions3D.x * dimensions.y * dimensions.z;


            Neuron[] motor_neurons = new Neuron[LinearAndNEATGenome.NEATGenome.NUM_OF_MOTOR_NEURONS];
            float[] motor_activations = new float[motor_neurons.Length];
            for (int i = 0; i < motor_neurons.Length; i++)
            {
                motor_neurons[i] = this.brain.GetNeuronCurrentState(motor_neuron_idx + i);
                if (motor_neurons[i].neuron_class != Neuron.NeuronClass.Motor) Debug.LogError("error");
                motor_activations[i] = motor_neurons[i].activation;
                if (float.IsNaN(motor_activations[i]) || float.IsInfinity(motor_activations[i]))
                {
                    Debug.LogWarning("Got NaN for motor activation");
                }
                if (motor_neurons[i].activation_function == Neuron.ActivationFunction.Sigmoid)
                {
                    // from [0,1] to [-1,1]
                    motor_activations[i] = motor_activations[i] * 2 - 1;
                }
                // cap motor activation in [-1,1]
                motor_activations[i] = math.min(motor_activations[i], 1);
                motor_activations[i] = math.max(motor_activations[i], -1);


            }


            // neuron activation for regular muscle
            for (int i = 0; i < motor_activations.Length; i++)
            {
                if (motor_activations.Length > 1)
                {
                    // theres multiple activations so they can go on different axes
                    if (i == 0) this.body.soft_voxel_object.SetVoxelTemperatureXFromNeuronActivation(cvx_voxel.Item2, motor_activations[i]);
                    if (i == 1) this.body.soft_voxel_object.SetVoxelTemperatureYFromNeuronActivation(cvx_voxel.Item2, motor_activations[i]);
                    if (i == 2) this.body.soft_voxel_object.SetVoxelTemperatureZFromNeuronActivation(cvx_voxel.Item2, motor_activations[i]);
                }
                else
                {
                    // theres only 1 activation so contract the whole voxel
                    this.body.soft_voxel_object.SetVoxelTemperatureFromNeuronActivation(cvx_voxel.Item2, motor_activations[i]);
                }



            }


        }
    }



    public float3 GetCenterOfMass()
    {
        return (float3)this.transform.position + this.body.GetCenterOfMass();
    }

    public void RecordBehaviorCharacterizationSnapshot()
    {
        BehaviorCharacterizationDatapoint datapoint = new();

        float3 current_position = GetCenterOfMass();
        float x = math.sign(current_position.x - birthplace.x) * math.pow(current_position.x - this.birthplace.x, 2);
        float y = math.sign(current_position.y - birthplace.y) * math.pow(current_position.y - this.birthplace.y, 2);
        float z = math.sign(current_position.z - birthplace.z) * math.pow(current_position.z - this.birthplace.z, 2);

        datapoint.offset_from_birthplace = new float3(x, y, z);

        datapoint.number_of_food_eaten = this.number_of_food_eaten;

        this.behavior_characterization.Add(datapoint);
    }

    public void Kill()
    {
        this.body.SetColorToStone();
        this.DiposeOfAllocatedMemory();
        Destroy(this.gameObject);
    }

    public float GetDisplacementFromBirthplace()
    {
        return Vector2.Distance(this.birthplace.xz, this.GetCenterOfMass().xz) * this.body.soft_voxel_object.scale / 10f;
    }

    public void ResetEnergyAndLifespan()
    {
        this.energy_remaining += ENERGY_IN_A_FOOD;
        this.energy_remaining = math.min(this.energy_remaining, MAX_ENERGY);
        this.lifespan = MAX_LIFESPAN;
    }
}
