using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Brain;
using static GlobalConfig;
using static UnityEngine.Random;

public class HyperNEATBrainGenome : BrainGenome
{
    const bool INITIALIZE_FULLY_CONNECT_CPPN = true;
    const bool ALLOW_MULTIPLE_MUTATIONS = false;
    const bool STACK_MUTATIONS = false;
    const bool ALLOW_RECURRENT_CONNECTIONS = false;
    const bool INCLUDE_EXTRA_CPPN_FUNCTIONS = false;

    public const float ADD_CONNECTION_MUTATION_RATE = 0.12f;
    public const float ADD_NODE_MUTATION_RATE = 0.08f;
    public const float DISABLE_CONNECTION_MUTATION_RATE = 0.2f;
    public const float BRAIN_UPDATE_PERIOD_MUTATION_RATE = 0.00f;
    public const float BRAIN_UPDATE_PERIOD_MUTATION_INCREMENT = 0.005f;
    public const float CHANCE_TO_MUTATE_EACH_CONNECTION = 0.85f;
    public const float CHANGE_NODE_FUNCTION_MUTATION_RATE = 0.1f;
    public const float WEIGHT_MUTATE_INCREMENT = 0.025f;

    bool BFS_for_CPPN_layers = false; // true for BFS, false for DFS

    public int3 substrate_dimensions;
    public int substrate_dimensions_size;

    public List<CPPNnode> cppn_nodes;
    public List<CPPNconnection> cppn_connections;
    public List<List<CPPNnode>> layers;

    public DevelopmentNeuron[] substrate;

    CPPNnode bias_input_node;
    CPPNnode x1_input_node;
    CPPNnode y1_input_node;
    CPPNnode z1_input_node;
    CPPNnode x2_input_node;
    CPPNnode y2_input_node;
    CPPNnode z2_input_node;


    CPPNnode initial_weight_output_node;
    CPPNnode LR_output_node;
    CPPNnode A_output_node;
    CPPNnode B_output_node;
    CPPNnode C_output_node;
    CPPNnode D_output_node;
    CPPNnode bias_output_node;
    CPPNnode sigmoid_alpha_output_node;
    CPPNnode activation_function_output_node;
    CPPNnode sign_output_node;
    CPPNnode LEO_connection_enabled_node;

    static int2 sensorimotor_idxs;


    // CPU variables
    NativeArray<Neuron> final_brain_neurons;
    NativeArray<Synapse> final_brain_synapses;

    // GPU variables
    int main_kernel;
    static ComputeShader hyperneat_compute_shader_static;
    ComputeShader hyperneat_compute_shader;
    public ComputeBuffer neurons_compute_buffer;
    public ComputeBuffer synapses_compute_buffer;
    public ComputeBuffer CPPN_node_compute_buffer;
    public ComputeBuffer CPPN_connection_compute_buffer;


    public enum CPPNFunction
    {
        [Description("LIN")] Linear,
        [Description("SIG")] Sigmoid,
        [Description("GAU")] Gaussian,
        [Description("SIN")] Sine,
        [Description("ABS")] Abs,
        [Description("STP")] Step,
        [Description("RLU")] ReLU,
        [Description("SQ")] Square,
        [Description("TANH")] HyperTangent,


        [Description("COS")] Cosine,


        [Description("SQR")] SquareRoot,

        [Description("TAN")] Tangent,

        [Description("COSH")] HyperCosine,
        [Description("SINH")] HyperSine,
        
        [Description("CUB")] Cube,
        
    }

    int num_of_joints;
    public HyperNEATBrainGenome()
    {
        this.num_of_joints = GlobalConfig.creature_to_use == Creature.Hexapod ? 21 : 14; // hexapod or quadruped
        this.brain_update_period = GlobalConfig.ANIMAT_BRAIN_UPDATE_PERIOD;
        this.substrate_dimensions = new int3(this.num_of_joints, 10, 4);
        this.substrate_dimensions_size = substrate_dimensions.x * substrate_dimensions.y * substrate_dimensions.z;
        this.substrate = new DevelopmentNeuron[this.substrate_dimensions_size];

        this.cppn_nodes = new();
        this.cppn_connections = new();
        this.layers = new();

        this.InsertHexapodSensorimotorNeurons();

    }

    public void GPU_Setup()
    {
        Debug.LogError("implement to match CPU");
        if (HyperNEATBrainGenome.hyperneat_compute_shader_static == null)
        {
            HyperNEATBrainGenome.hyperneat_compute_shader_static = (ComputeShader)Resources.Load("ParallelHyperNEATGPU");
        }

        // set vars
        hyperneat_compute_shader = (ComputeShader)GameObject.Instantiate(hyperneat_compute_shader_static);
        hyperneat_compute_shader.SetInt("SUBSTRATE_SIZE_X", this.substrate_dimensions.x);
        hyperneat_compute_shader.SetInt("SUBSTRATE_SIZE_Y", this.substrate_dimensions.y);
        hyperneat_compute_shader.SetInt("SUBSTRATE_SIZE_Z", this.substrate_dimensions.z);
        hyperneat_compute_shader.SetInt("NUM_OF_NODES", this.cppn_nodes.Count);
        hyperneat_compute_shader.SetInt("OUTPUT_NODES_START_IDX", sensorimotor_idxs.x);
        hyperneat_compute_shader.SetInt("OUTPUT_NODES_END_IDX", sensorimotor_idxs.y);
        
        // create brain buffers
        neurons_compute_buffer = new(count: substrate_dimensions_size, stride: Marshal.SizeOf(typeof(Neuron)));
        synapses_compute_buffer = new(count: substrate_dimensions_size * substrate_dimensions_size, stride: Marshal.SizeOf(typeof(Synapse)));

        
        this.main_kernel = hyperneat_compute_shader.FindKernel("CSMain");
        hyperneat_compute_shader.SetBuffer(this.main_kernel, "neurons", this.neurons_compute_buffer);
        hyperneat_compute_shader.SetBuffer(this.main_kernel, "synapses", this.synapses_compute_buffer);

        // convert CPPN into a Parallel version 
        this.CPPN_node_compute_buffer = new(count: this.cppn_nodes.Count * substrate_dimensions_size * substrate_dimensions_size, stride: Marshal.SizeOf(typeof(CPPNnodeParallel)));

        Debug.LogError("todo: create buffer of floats for CPPN output size substrate_dimensions_size * substrate_dimensions_size, since CPPN nodes is now too small");
        (CPPNnodeParallel[] node_buffer_write_array, CPPNconnectionParallel[] connection_buffer_write_array) = ConvertCPPNToParallel();
        this.CPPN_connection_compute_buffer = new(count: connection_buffer_write_array.Length, stride: Marshal.SizeOf(typeof(CPPNconnectionParallel)));

        CPPN_node_compute_buffer.SetData(node_buffer_write_array);
        CPPN_connection_compute_buffer.SetData(connection_buffer_write_array);
        hyperneat_compute_shader.SetBuffer(this.main_kernel, "CPPN_nodes", CPPN_node_compute_buffer);
        hyperneat_compute_shader.SetBuffer(this.main_kernel, "CPPN_connections", CPPN_connection_compute_buffer);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>(_,_,total number of inputs in all neurons)</returns>
    public (CPPNnodeParallel[], CPPNconnectionParallel[]) ConvertCPPNToParallel()
    {
        CPPNnodeParallel[] parallel_nodes = new CPPNnodeParallel[this.cppn_nodes.Count]; // CPPN_node_compute_buffer.BeginWrite<CPPNnodeGPU>(0, CPPN_node_compute_buffer.count);
        Dictionary<CPPNnode, int> node_to_buffer_idx = new();
        int i = 0;
        int total_num_of_inputs = 0;

        for (int j = 0; j < this.layers.Count; j++)
        {
            List<CPPNnode> layer = this.layers[j];
            foreach (CPPNnode node in layer)
            {
                CPPNnodeParallel gpu_node = new();
                gpu_node.input_connection_start_idx = total_num_of_inputs;
                gpu_node.function = (int)node.function;

                int number_of_inputs = 0;
                foreach ((CPPNnode input_node, CPPNconnection input_connection) in node.inputs)
                {
                    if (!input_connection.enabled) continue;
                    if (!ALLOW_RECURRENT_CONNECTIONS && input_node.layer >= j) continue;
                    if (input_node.layer == INVALID_NEAT_ID) continue;
                    number_of_inputs++;
                }

                gpu_node.number_of_input_connections = number_of_inputs;

                parallel_nodes[i] = gpu_node;
                node_to_buffer_idx[node] = i;

                total_num_of_inputs += number_of_inputs;
                i++;
            }

        }
        // CPPN_node_compute_buffer.EndWrite<CPPNnodeGPU>(CPPN_node_compute_buffer.count);

        
        CPPNconnectionParallel[] parallel_connections = new CPPNconnectionParallel[total_num_of_inputs];// CPPN_connection_compute_buffer.BeginWrite<CPPNconnectionGPU>(0, CPPN_connection_compute_buffer.count);
        i = 0;
        for (int j = 0; j < this.layers.Count; j++)
        {
            List<CPPNnode> layer = this.layers[j];
            foreach (CPPNnode node in layer)
            {
                foreach ((CPPNnode input_node, CPPNconnection input_connection) in node.inputs)
                {
                    if (!input_connection.enabled) continue;
                    if (!ALLOW_RECURRENT_CONNECTIONS && input_node.layer >= j) continue;
                    if (input_node.layer == INVALID_NEAT_ID) continue;
                    CPPNconnectionParallel gpu_connection = new();
                    gpu_connection.from_idx = node_to_buffer_idx[input_node];
                    gpu_connection.weight = input_connection.weight;
                    parallel_connections[i] = gpu_connection;
                    i++;
                }
            }
        }


        //CPPN_connection_compute_buffer.EndWrite<CPPNconnectionGPU>(CPPN_connection_compute_buffer.count);

        return (parallel_nodes, parallel_connections);
    }

   



    public void SetCPPNnodesForIO()
    {
        sensorimotor_idxs = new();
        this.bias_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(bias_input_node);
        this.x1_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(x1_input_node);
        this.y1_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(y1_input_node);
        this.z1_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(z1_input_node);
        this.x2_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(x2_input_node);
        this.y2_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(y2_input_node);
        this.z2_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(z2_input_node);
        sensorimotor_idxs.x = this.cppn_nodes.Count; // sensor idx end

        CPPNFunction GetInitialOutputFunction()
        {
            return GetRandomCPPNfunction();
            return CPPNFunction.Linear;
        }
        
        this.initial_weight_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 0
        this.cppn_nodes.Add(initial_weight_output_node);
        this.LR_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 1
        this.cppn_nodes.Add(LR_output_node);
        this.A_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 2
        this.cppn_nodes.Add(A_output_node);
        this.B_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 3
        this.cppn_nodes.Add(B_output_node);
        this.C_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 4
        this.cppn_nodes.Add(C_output_node);
        this.D_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 5
        this.cppn_nodes.Add(D_output_node);
        this.bias_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 6
        this.cppn_nodes.Add(bias_output_node);
        this.sigmoid_alpha_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 7
        this.cppn_nodes.Add(sigmoid_alpha_output_node);
        this.activation_function_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 8
        this.cppn_nodes.Add(activation_function_output_node);
        this.sign_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 9
        this.cppn_nodes.Add(sign_output_node);
        this.LEO_connection_enabled_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 10
        this.cppn_nodes.Add(LEO_connection_enabled_node);
        sensorimotor_idxs.y = this.cppn_nodes.Count; // motor idx end


        if (INITIALIZE_FULLY_CONNECT_CPPN)
        {
            for (int i = 0; i < sensorimotor_idxs.x; i++)
            {
                for (int j = sensorimotor_idxs.x; j < sensorimotor_idxs.y; j++)
                {
                    int rnd = UnityEngine.Random.Range(0, 2);
                    if (rnd == 0) continue;

                    CPPNconnection new_connection = new(from_ID: this.cppn_nodes[i].ID, to_ID: this.cppn_nodes[j].ID, weight: GetRandomInitialCPPNWeight(), ID: this.cppn_connections.Count);
                    this.cppn_connections.Add(new_connection);
                }
            }
        }

        if (NEXT_NEURON_ID == BrainGenome.INVALID_NEAT_ID) NEXT_NEURON_ID = this.cppn_nodes.Count;
        if (NEXT_SYNAPSE_ID == BrainGenome.INVALID_NEAT_ID) NEXT_SYNAPSE_ID = this.cppn_connections.Count;

        for (int j = 0; j < 25; j++)
        {
            AddNewRandomNode();
        }
    }

    public void InsertNeuron(int3 coords, string extradata)
    {
        DevelopmentNeuron neuron = GetDefaultDevNeuron();
        neuron.extradata = extradata;
        WriteToSubstrate(coords.x, coords.y, coords.z, neuron);
    }

    public DevelopmentNeuron ReadFromSubstrate(int x, int y, int z)
    {
        return this.substrate[VoxelUtils.Index_FlatFromint3(x, y, z, this.substrate_dimensions)];
    }

    public void WriteToSubstrate(int x, int y, int z, DevelopmentNeuron neuron)
    {
        this.substrate[VoxelUtils.Index_FlatFromint3(x, y, z, this.substrate_dimensions)] = neuron;
    }

    public void InsertHexapodSensorimotorNeurons()
    {

        // insert sensory neurons
        for (int x = 0; x < this.num_of_joints; x++) // joints in hexapod
        {
            string joint_key = Animat.GetSensorimotorJointKey(x);

            int3 coords = new int3(x, 0, 0);
            // 10 for the sensor
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_LLL");
            coords.y++;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_LLR");
            coords.y++;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_LR");
            coords.y++;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_RL");
            coords.y++;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_RRL");
            coords.y++;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_RRR");
            coords.y++;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_LL");
            coords.y++;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_LR");
            coords.y++;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_RL");
            coords.y++;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_RR");
        }


        // insert motor neurons
        for (int x = 0; x < this.num_of_joints; x++) 
        {
            string joint_key = Animat.GetSensorimotorJointKey(x);

            // 3 for the motor
            int3 coords = new int3(x, 0, this.substrate_dimensions.z - 1);
            this.InsertNeuron(coords, extradata: "MOTORLAYER_" + joint_key + "_LL");
            coords.y += 4;
            this.InsertNeuron(coords, extradata: "MOTORLAYER_" + joint_key + "_LR");
            coords.y += 4;
            this.InsertNeuron(coords, extradata: "MOTORLAYER_" + joint_key + "_R");

        }

    }

    public override BrainGenome Clone()
    {
        HyperNEATBrainGenome genome = new();

        foreach(CPPNnode n in this.cppn_nodes)
        {
            genome.cppn_nodes.Add(n.Clone());
        }
        foreach (CPPNconnection c in this.cppn_connections)
        {
            genome.cppn_connections.Add(c.Clone());
        }
        genome.FinalizeCPPN();

        return genome;


    }

    public static HyperNEATBrainGenome CreateTestGenome()
    {
        HyperNEATBrainGenome genome = new();
        genome.SetCPPNnodesForIO();
        genome.FinalizeCPPN();

        return genome;
        
    }

    public static CPPNFunction GetRandomCPPNfunction()
    {
        System.Random sysrnd = new();
        if(INCLUDE_EXTRA_CPPN_FUNCTIONS) return (CPPNFunction)sysrnd.Next(0, Enum.GetNames(typeof(CPPNFunction)).Length);
        else return (CPPNFunction)sysrnd.Next(0, (int)CPPNFunction.Cosine);
    }

    public void ChangeRandomCPPNNodeFunction()
    {
        CPPNnode node = this.cppn_nodes[UnityEngine.Random.Range(sensorimotor_idxs.x,this.cppn_nodes.Count)];
        node.function = GetRandomCPPNfunction();
    }

    public void DisableRandomCPPNConnection()
    {
        if (this.cppn_connections.Count == 0) return;
        CPPNconnection connection = this.cppn_connections[UnityEngine.Random.Range(0, this.cppn_connections.Count)];
        connection.enabled = false;
    }

    public override (ComputeBuffer, ComputeBuffer) DevelopGPU(Dictionary<string, Dictionary<string, int>> neuron_indices)
    {
        // ComputeBuffer 
        GPU_Setup();

       
        int remaining_items = this.synapses_compute_buffer.count;

        int i = 0;
        int max_items_processed_per_dispatch = GlobalConfig.MAX_NUM_OF_THREAD_GROUPS * GlobalConfig.NUM_OF_GPU_THREADS;
        while (remaining_items > 0)
        {
            hyperneat_compute_shader.SetInt("index_offset", i * max_items_processed_per_dispatch);
            if (remaining_items <= max_items_processed_per_dispatch)
            {
                hyperneat_compute_shader.Dispatch(this.main_kernel, Mathf.CeilToInt(remaining_items / GlobalConfig.NUM_OF_GPU_THREADS), 1, 1);
                remaining_items = 0;
                break;
            }
            else
            {
                hyperneat_compute_shader.Dispatch(this.main_kernel, GlobalConfig.MAX_NUM_OF_THREAD_GROUPS, 1, 1);
                remaining_items -= max_items_processed_per_dispatch;
            }
            i++;
        }

        LabelNeurons(neuron_indices);


        return (neurons_compute_buffer, synapses_compute_buffer);
    }

    public void LabelNeurons(Dictionary<string, Dictionary<string, int>> neuron_indices)
    {
        Neuron[] gpu_neuron_array = null;
        if (GlobalConfig.brain_genome_development_processing_method == ProcessingMethod.GPU)
        {
            gpu_neuron_array = new Neuron[this.neurons_compute_buffer.count];
            this.neurons_compute_buffer.GetData(gpu_neuron_array);
        }
        

        int[] arr = new[] { 0, 0, 0 };

        string[] strings;
        string neuron_type;
        string sensor_type;
        for (int z = 0; z < substrate_dimensions.z; z++)
        {
            for (int y = 0; y < substrate_dimensions.y; y++)
            {
                for (int x = 0; x < substrate_dimensions.x; x++)
                {
                    int3 cell_idx = new int3(x, y, z);
                    int cell_idx_flat = VoxelUtils.Index_FlatFromint3(cell_idx, substrate_dimensions);

                    DevelopmentNeuron cell = this.substrate[cell_idx_flat];

                    Neuron neuron;
                    if (GlobalConfig.brain_genome_development_processing_method == ProcessingMethod.CPU)
                    {
                        neuron = this.final_brain_neurons[cell_idx_flat];
                    }
                    else if (GlobalConfig.brain_genome_development_processing_method == ProcessingMethod.GPU)
                    {
                        neuron = gpu_neuron_array[cell_idx_flat];
                    }
                    else
                    {
                        Debug.LogError("error");
                        return;
                    }

                    neuron.position = cell_idx;

                    

                    if (cell != null && cell.extradata != "")
                    {

                        strings = cell.extradata.Split("_");
                        neuron_type = strings[strings.Length - 1];
                        sensor_type = strings[strings.Length - 2];

                        if (sensor_type != "TOUCHSENSE" && sensor_type != "ROTATESENSE")
                        {
                            arr[2] += 3;
                            // no sensor type
                            // this is a motor (output) neuron, so turn it into a perceptron
                            neuron.type = Neuron.NeuronType.Perceptron;
                            // connect to motor interface

                            int tree_idx = -1;
                            if (neuron_type == "LL")
                            {
                                tree_idx = 0;
                            }
                            else if (neuron_type == "LR")
                            {
                                tree_idx = 1;
                            }
                            else if (neuron_type == "R")
                            {
                                tree_idx = 2;
                            }
                            else
                            {
                                Debug.LogError("ERROR " + neuron_type);
                            }
                            neuron_indices[Brain.MOTOR_NEURON_KEY][cell.extradata[0..^neuron_type.Length] + tree_idx] = cell_idx_flat;
                            neuron.neuron_class = Neuron.NeuronClass.Motor;
                        }
                        else
                        {
                            // this is a sensory (input) neuron, so turn it into a perceptron
                            neuron.type = Neuron.NeuronType.Perceptron;

                            // connect to sensory interface
                            int tree_idx = -1;

                            if (sensor_type == "TOUCHSENSE")
                            {
                                if (neuron_type == "LLL")
                                {
                                    tree_idx = 0; // TOP
                                }
                                else if (neuron_type == "LLR")
                                {
                                    tree_idx = 1; // BOT
                                }
                                else if (neuron_type == "LR")
                                {
                                    tree_idx = 2; // LEFT
                                }
                                else if (neuron_type == "RL")
                                {
                                    tree_idx = 3; // RIGHT
                                }
                                else if (neuron_type == "RRL")
                                {
                                    tree_idx = 4; // FRONT
                                }
                                else if (neuron_type == "RRR")
                                {
                                    tree_idx = 5; // BACK
                                }
                                else
                                {
                                    Debug.LogError("ERROR " + neuron_type);
                                }
                            }
                            else if (sensor_type == "ROTATESENSE")
                            {
                                if (neuron_type == "LL")
                                {
                                    tree_idx = 6; // W
                                }
                                else if (neuron_type == "LR")
                                {
                                    tree_idx = 7; // X
                                }
                                else if (neuron_type == "RL")
                                {
                                    tree_idx = 8; // Y
                                }
                                else if (neuron_type == "RR")
                                {
                                    tree_idx = 9; // Z
                                }
                                else
                                {
                                    Debug.LogError("ERROR " + neuron_type);
                                }
                            }
                            else if (sensor_type == "")
                            {
                                Debug.LogError("ERROR: No Sensor type");
                            }
                            else
                            {
                                Debug.LogError("ERROR " + neuron_type + " for sensor type " + sensor_type);
                            }

                            string key = "";
                            for (int m = 0; m < strings.Length - 2; m++)
                            {
                                key += strings[m] + "_";
                            }
                            key += tree_idx;

                            neuron_indices[Brain.SENSORY_NEURON_KEY][key] = cell_idx_flat;

                            neuron.neuron_class = Neuron.NeuronClass.Sensor;
                        }

                    }

                  // 

                    if (GlobalConfig.brain_genome_development_processing_method == ProcessingMethod.CPU)
                    {
                        this.final_brain_neurons[cell_idx_flat] = neuron;
                    }
                    else if (GlobalConfig.brain_genome_development_processing_method == ProcessingMethod.GPU)
                    {
                        gpu_neuron_array[cell_idx_flat] = neuron;
                    }
                    else
                    {
                        Debug.LogError("error");
                    }

                }
            }
        }

        if (GlobalConfig.brain_genome_development_processing_method == ProcessingMethod.GPU)
        {
            this.neurons_compute_buffer.SetData(gpu_neuron_array);
        }
    }


    NativeArray<float> CPPN_nodes_outputs_native;
    NativeArray<CPPNnodeParallel> CPPN_nodes_native;
    NativeArray<CPPNconnectionParallel> CPPN_connections_native;
    public override JobHandle ScheduleDevelopCPUJob()
    {
        this.final_brain_neurons = new(length: this.substrate_dimensions_size, Allocator.Persistent);
        this.final_brain_synapses = new(length: this.substrate_dimensions_size * this.substrate_dimensions_size, Allocator.Persistent);

        (CPPNnodeParallel[] CPPN_nodes, CPPNconnectionParallel[] CPPN_connections) = ConvertCPPNToParallel();

        this.CPPN_nodes_outputs_native = new NativeArray<float>(CPPN_nodes.Length * this.final_brain_synapses.Length, allocator: Allocator.TempJob);
        this.CPPN_nodes_native = new NativeArray<CPPNnodeParallel>(CPPN_nodes, allocator: Allocator.TempJob);
        this.CPPN_connections_native = new NativeArray<CPPNconnectionParallel>(CPPN_connections, allocator: Allocator.TempJob);


        ParallelHyperNEATCPU job = new()
        {
            SUBSTRATE_SIZE_X = this.substrate_dimensions.x,
            SUBSTRATE_SIZE_Y = this.substrate_dimensions.y,
            SUBSTRATE_SIZE_Z = this.substrate_dimensions.z,
            NUM_OF_NODES = this.cppn_nodes.Count,
            OUTPUT_NODES_START_IDX = sensorimotor_idxs.x,
            OUTPUT_NODES_END_IDX = sensorimotor_idxs.y,

            neurons = this.final_brain_neurons,
            synapses = final_brain_synapses,

            CPPN_nodes = this.CPPN_nodes_native,
            CPPN_nodes_outputs = this.CPPN_nodes_outputs_native,
            CPPN_connections = this.CPPN_connections_native
        };
        JobHandle develop_job_handle = job.Schedule(this.final_brain_synapses.Length, final_brain_synapses.Length);


        return develop_job_handle;
    }

    public override (NativeArray<Brain.Neuron>, NativeArray<Brain.Synapse>) DevelopCPU(Dictionary<string, Dictionary<string, int>> neuron_indices)
    {
        this.CPPN_nodes_outputs_native.Dispose();
        this.CPPN_nodes_native.Dispose();
        this.CPPN_connections_native.Dispose();



        LabelNeurons(neuron_indices);

       // Debug.Log("brain has " + final_brain_synapses.Count + " synapse ");


        return (this.final_brain_neurons, this.final_brain_synapses);

    }

    public DevelopmentNeuron GetDefaultDevNeuron()
    {
        return new(threshold: 1, bias: 0, sign: true, adaptation_delta: 1, decay: 1, sigmoid_alpha: 1);
    }

    public override BrainGenome LoadFromDisk()
    {
        throw new System.NotImplementedException();
    }

    public override void Mutate()
    {
        bool should_mutate;
        float rnd;
        // first, mutate synapse parameters
        foreach (CPPNconnection connection in this.cppn_connections)
        {
            should_mutate = Range(0f, 1f) < CHANCE_TO_MUTATE_EACH_CONNECTION;
            if (!should_mutate) continue;
            rnd = Range(0f, 1f);


            if (rnd < 0.97)
            {
                connection.weight += Range(0, 2) == 0 ? WEIGHT_MUTATE_INCREMENT : -WEIGHT_MUTATE_INCREMENT;
            }
            else if (rnd < 0.98)
            {
                connection.weight /= 2;
            }
            else if (rnd < 0.99)
            {
                connection.weight *= 2;
            }
            else
            {
                connection.weight *= -1;
            }

            // constrain weights
            //connection.weight = math.max(connection.weight, -1);
            //connection.weight = math.min(connection.weight, 1);

        }


        /*        // then, mutate neuron parameters
                for (int i = sensorimotor_idxs.x; i < this.cppn_nodes.Count; i++)
                {
                    CPPNnode node = this.cppn_nodes[i];
                    should_mutate = Range(0f, 1f) < CHANCE_TO_MUTATE_NODE;
                    if (!should_mutate) continue;
                    //rnd = Range(0f, 1f);

                    node.function = GetRandomCPPNfunction();

                }*/



        
        int mutation_type;

        if (ALLOW_MULTIPLE_MUTATIONS)
        {
            mutation_type = 0;
        }
        else
        {
            float mutation_type_rnd = Range(0f,1f);
            if(mutation_type_rnd < 0.06)
            {
                mutation_type = 0;
            }else if(mutation_type_rnd < 0.12)
            {
                mutation_type = 1;
            }
            else if (mutation_type_rnd < 0.18)
            {
                mutation_type = 2;
            }
            else if (mutation_type_rnd < 0.24)
            {
                mutation_type = 3;
            }
            else
            {
                mutation_type = -1;
            }


        }

        switch (mutation_type)
        {
            case 0:
                // MUTATE NODE?
                should_mutate = Range(0f, 1f) < CHANGE_NODE_FUNCTION_MUTATION_RATE;
                while (!ALLOW_MULTIPLE_MUTATIONS || should_mutate)
                {
                    ChangeRandomCPPNNodeFunction();

                    if (!ALLOW_MULTIPLE_MUTATIONS) break;
                    should_mutate = STACK_MUTATIONS ? Range(0f, 1f) < CHANGE_NODE_FUNCTION_MUTATION_RATE : false;
                }
                if (!ALLOW_MULTIPLE_MUTATIONS) break;
                else goto case 1;
            case 1:
                // ADD CONNECTION?
                should_mutate = Range(0f, 1f) < ADD_CONNECTION_MUTATION_RATE;
                while (!ALLOW_MULTIPLE_MUTATIONS || should_mutate)
                {
                    AddRandomConnection();

                    if (!ALLOW_MULTIPLE_MUTATIONS) break;
                    should_mutate = STACK_MUTATIONS ? Range(0f, 1f) < ADD_CONNECTION_MUTATION_RATE : false;
                }
                if (!ALLOW_MULTIPLE_MUTATIONS) break;
                else goto case 2;
            case 2:
                // ADD NODE?
                if(this.cppn_connections.Count > 0)
                {
                    should_mutate = (Range(0f, 1f) < ADD_NODE_MUTATION_RATE);
                    while (!ALLOW_MULTIPLE_MUTATIONS || should_mutate)
                    {
                        AddNewRandomNode();

                        if (!ALLOW_MULTIPLE_MUTATIONS) break;
                        should_mutate = STACK_MUTATIONS ? Range(0f, 1f) < ADD_NODE_MUTATION_RATE : false;
                    }
                }

                if (!ALLOW_MULTIPLE_MUTATIONS) break;
                else goto case 3;
            case 3:
                // DISABLE CONNECTION?
                should_mutate = Range(0f, 1f) < DISABLE_CONNECTION_MUTATION_RATE;
                while (!ALLOW_MULTIPLE_MUTATIONS || should_mutate)
                {
                    DisableRandomCPPNConnection();

                    if (!ALLOW_MULTIPLE_MUTATIONS) break;
                    should_mutate = STACK_MUTATIONS ? Range(0f, 1f) < DISABLE_CONNECTION_MUTATION_RATE : false;
                }
                if (!ALLOW_MULTIPLE_MUTATIONS) break;
                else goto default;
            default:
                break;
        }

        



/*        // Mutate Brain Update speed
        should_mutate = Range(0f, 1f) < BRAIN_UPDATE_PERIOD_MUTATION_RATE;
        while (should_mutate)
        {
            this.brain_update_period += Range(0, 2) == 0 ? BRAIN_UPDATE_PERIOD_MUTATION_INCREMENT : -BRAIN_UPDATE_PERIOD_MUTATION_INCREMENT;
            this.brain_update_period = Mathf.Max(this.brain_update_period, 0.001f);
        }*/
        
        this.FinalizeCPPN();
    }

    public void AddNewRandomNode()
    {
        if (this.cppn_connections.Count == 0) return;
        int attempts = 0;
        CPPNconnection random_connection = this.cppn_connections[Range(0, this.cppn_connections.Count)];
        while (!random_connection.enabled && attempts < 1000)
        {
            random_connection = this.cppn_connections[Range(0, this.cppn_connections.Count)];
        }
        if (attempts >= 1000) return;
        random_connection.enabled = false;
        CPPNnode new_node = new(ID: GetNextGlobalCPPNNeuronID(), function: GetRandomCPPNfunction());
        CPPNconnection new_connectionA = new(ID: GetNextGlobalCPPNSynapseID(), weight: 1, from_ID: random_connection.from_node_ID, to_ID: new_node.ID);
        CPPNconnection new_connectionB = new(ID: GetNextGlobalCPPNSynapseID(), weight: random_connection.weight, from_ID: new_node.ID, to_ID: random_connection.to_node_ID);
        this.cppn_connections.Add(new_connectionA);
        this.cppn_connections.Add(new_connectionB);
        this.cppn_nodes.Add(new_node);
    }

    public void AddRandomConnection()
    {
        int num_of_outputs = (sensorimotor_idxs.y - sensorimotor_idxs.x);
        int from_idx = Range(0, this.cppn_nodes.Count - num_of_outputs);
        if(from_idx >= sensorimotor_idxs.x && from_idx < sensorimotor_idxs.y)
        {
            // its an output, cant connect from an output
            from_idx += num_of_outputs;
        }
        if (from_idx >= this.cppn_nodes.Count) from_idx = Range(0, sensorimotor_idxs.x);
        int to_idx = Range(sensorimotor_idxs.x, this.cppn_nodes.Count);
        CPPNnode from_neuron = (CPPNnode)this.cppn_nodes[from_idx];
        CPPNnode to_neuron = (CPPNnode)this.cppn_nodes[to_idx];

        if (!ALLOW_RECURRENT_CONNECTIONS)
        {
            int attempts = 0;
            // try to find another connection if the randomly generated one is recurrent
            while (to_neuron.layer <= from_neuron.layer && attempts < 100) 
            {
                from_idx = Range(0, this.cppn_nodes.Count);
                to_idx = Range(0, this.cppn_nodes.Count);
                from_neuron = (CPPNnode)this.cppn_nodes[from_idx];
                to_neuron = (CPPNnode)this.cppn_nodes[to_idx];
                attempts++;
            }

             if (to_neuron.layer <= from_neuron.layer) return;
        }
            

        CPPNconnection new_connection = new(from_ID: from_neuron.ID, to_ID: to_neuron.ID, weight: GetRandomInitialCPPNWeight(), ID: GetNextGlobalCPPNSynapseID());
        this.cppn_connections.Add(new_connection);
    }

    public float GetRandomInitialCPPNWeight()
    {
        return Range(-0.1f, 0.1f);
    }

    public override (BrainGenome, BrainGenome) Reproduce(BrainGenome genome_parent2)
    {
        HyperNEATBrainGenome parent1 = this;
        HyperNEATBrainGenome parent2 = (HyperNEATBrainGenome)genome_parent2;
        HyperNEATBrainGenome offspring1 = new();
        HyperNEATBrainGenome offspring2 = new();

        float[] update_period = new float[]{ parent1.brain_update_period, parent2.brain_update_period };
        int rnd = Range(0, 2);
        offspring1.brain_update_period = update_period[rnd];
        offspring2.brain_update_period = update_period[1 - rnd];

        int i = 0, j = 0;
        while (i < parent1.cppn_nodes.Count || j < parent2.cppn_nodes.Count)
        {
            CPPNnode neuron1 = null;
            if (i < parent1.cppn_nodes.Count)
            {
                neuron1 = (CPPNnode)parent1.cppn_nodes[i];
            }

            CPPNnode neuron2 = null;
            if (j < parent2.cppn_nodes.Count)
            {
                neuron2 = (CPPNnode)parent2.cppn_nodes[j];
            }

            if (neuron1 != null && neuron2 != null)
            {
                if (neuron1.ID < neuron2.ID)
                {
                    neuron2 = null;
                } else if (neuron1.ID > neuron2.ID)
                {
                    neuron1 = null;
                }
            }

            if (neuron1 != null && neuron2 != null)
            {
                rnd = Range(0, 2);
                CPPNnode[] nodes = { neuron1, neuron2 };
                offspring1.cppn_nodes.Add(nodes[rnd].Clone());
                offspring2.cppn_nodes.Add(nodes[1-rnd].Clone());
                i++;
                j++;
            }
            else if (neuron1 != null && neuron2 == null)
            {
                offspring1.cppn_nodes.Add(neuron1.Clone());
                offspring2.cppn_nodes.Add(neuron1.Clone());
                i++;
            }
            else if (neuron1 == null && neuron2 != null)
            {
                offspring1.cppn_nodes.Add(neuron2.Clone());
                offspring2.cppn_nodes.Add(neuron2.Clone());
                j++;
            }



        }

        i = 0;
        j = 0;
        while (i < parent1.cppn_connections.Count || j < parent2.cppn_connections.Count)
        {
            CPPNconnection connection1 = null;
            if (i < parent1.cppn_connections.Count)
            {
                connection1 = (CPPNconnection)parent1.cppn_connections[i];
            }

            CPPNconnection connection2 = null;
            if (j < parent2.cppn_connections.Count)
            {
                connection2 = (CPPNconnection)parent2.cppn_connections[j];
            }

            if (connection1 != null && connection2 != null)
            {
                if (connection1.ID < connection2.ID)
                {
                    connection2 = null;
                }
                else if (connection1.ID > connection2.ID)
                {
                    connection1 = null;
                }
            }



            if (connection1 != null && connection2 != null)
            {
                rnd = Range(0, 2);
                CPPNconnection[] connections = new CPPNconnection[] { connection1, connection2 };
                offspring1.cppn_connections.Add(connections[rnd].Clone());
                offspring2.cppn_connections.Add(connections[1-rnd].Clone());
                i++;
                j++;
            }
            else if (connection1 != null && connection2 == null)
            {
                offspring1.cppn_connections.Add(connection1.Clone());
                offspring2.cppn_connections.Add(connection1.Clone());
                i++;
            }
            else if (connection1 == null && connection2 != null)
            {
                offspring1.cppn_connections.Add(connection2.Clone());
                offspring2.cppn_connections.Add(connection2.Clone());
                j++;
            }

            if ((connection1 != null && !connection1.enabled) || (connection2 != null && !connection2.enabled))
            {
                offspring1.cppn_connections[^1].enabled = Range(0f, 1.0f) < 0.75f ? false : true;
                offspring2.cppn_connections[^1].enabled = Range(0f, 1.0f) < 0.75f ? false : true;
            }


        }

        offspring1.FinalizeCPPN();
        offspring2.FinalizeCPPN();

        return (offspring1, offspring2);
    }

    public override void SaveToDisk()
    {
        throw new System.NotImplementedException();
    }

    // call this function when all CPPN nodes and connections are set

    public void FinalizeCPPN()
    {
        // reset nodes
        foreach(List<CPPNnode> layer in this.layers)
        {
            layer.Clear();
        }
        this.layers.Clear();

        Dictionary<int, int> ID_to_idx = new();
        int j = 0;
        foreach (CPPNnode n in this.cppn_nodes)
        {
            if (ID_to_idx.ContainsKey(n.ID))
            {
                Debug.LogError("Duplicate nodes");
            }
            ID_to_idx[n.ID] = j;
            n.inputs.Clear();
            n.outputs.Clear();
            n.layer = INVALID_NEAT_ID;
            j++;
        }


        // add nodes to input/output lists for easy access
        foreach (CPPNconnection c in this.cppn_connections)
        {
           //if (!c.enabled) continue;
            int to_idx = ID_to_idx[c.to_node_ID];
            CPPNnode to_node = this.cppn_nodes[to_idx];
            int from_idx = ID_to_idx[c.from_node_ID];
            CPPNnode from_node = this.cppn_nodes[from_idx];
            to_node.inputs.Add((from_node, c));
            from_node.outputs.Add((to_node, c));
        }


        // sort nodes into layers.
        Stack<CPPNnode> nodes_to_explore = new();
        for(int i=0; i < sensorimotor_idxs.x; i++)
        {
            this.cppn_nodes[i].layer = SENSORY_LAYER_ID; // sensor layer
            nodes_to_explore.Push(this.cppn_nodes[i]);
        }

        for (int i = sensorimotor_idxs.x; i < sensorimotor_idxs.y; i++)
        {
            this.cppn_nodes[i].layer = OUTPUT_TEMP_LAYER_ID; // motor layer
        }

        int max_hidden_layer = 0;

        if (BFS_for_CPPN_layers)
        {
            // breadth first search, produces the shortest path to each node
            while (nodes_to_explore.Count != 0)
            {
                // be careful of this loop... its doing graph traversal in a while loop.
                CPPNnode node = nodes_to_explore.Pop();
                // without recursive connections, the output layer is the max layer
                foreach ((CPPNnode, CPPNconnection) c in node.outputs)
                {
                    CPPNnode output_node = c.Item1;
                    if (output_node.layer == OUTPUT_TEMP_LAYER_ID || output_node.layer == SENSORY_LAYER_ID || output_node == node) continue;
                    if (output_node.layer == INVALID_NEAT_ID) //|| (output_node.layer < node.layer + 1))
                    {
                        output_node.layer = node.layer + 1;
                        max_hidden_layer = Math.Max(max_hidden_layer, output_node.layer);
                        if (!nodes_to_explore.Contains(output_node)) nodes_to_explore.Push(output_node);
                    }

                }
            }
        }
        else
        {
            // depth first search, produces the longest path to each node
            Dictionary<CPPNnode, bool> visited = new(); // to prevent infinite loops
            void Explore(CPPNnode node)
            {
                visited[node] = true;
                foreach ((CPPNnode, CPPNconnection) c in node.outputs)
                {
                    CPPNnode output_node = c.Item1;
                    if (output_node.layer == OUTPUT_TEMP_LAYER_ID || output_node.layer == SENSORY_LAYER_ID || output_node == node) continue;
                    bool output_node_not_visited = !visited.ContainsKey(output_node) || !visited[output_node];
                    if (output_node.layer == INVALID_NEAT_ID || (output_node.layer < node.layer + 1 && output_node_not_visited))
                    {
                        output_node.layer = node.layer + 1; 
                        max_hidden_layer = Math.Max(max_hidden_layer, output_node.layer);
                       
                        Explore(output_node);
                        
                        //if (!nodes_to_explore.Contains(output_node) && output_node != node) nodes_to_explore.Push(output_node);
                    }

                }
                visited[node] = false;
            }

            while (nodes_to_explore.Count != 0)
            {
                CPPNnode node = nodes_to_explore.Pop();
                Explore(node);       
            }
        }


        int output_layer = max_hidden_layer + 1;
        for (int i = sensorimotor_idxs.x; i < sensorimotor_idxs.y; i++)
        {
            this.cppn_nodes[i].layer = output_layer; // motor layer
        }


        //
 
        for (int i = 0; i <= output_layer; i++)
        {
            this.layers.Add(new List<CPPNnode>());
        }
  


        foreach (CPPNnode n in this.cppn_nodes)
        {
            if (n.layer == INVALID_NEAT_ID) continue;
            this.layers[n.layer].Add(n);
        }

        //Debug.Log("CPPN Size " + this.cppn_nodes.Count + " with " + this.cppn_connections.Count + " connections");
    }


    public struct CPPNconnectionParallel
    {
        public int from_idx;
        public float weight;
    }

    public struct CPPNnodeParallel
    {
        public int function;
        public int number_of_input_connections;
        public int input_connection_start_idx;
    }

    public class CPPNnode
    {
        public List<(CPPNnode, CPPNconnection)> inputs;
        public List<(CPPNnode, CPPNconnection)> outputs;


        public int ID;
        public CPPNFunction function;
        public bool sensorimotor;

        public int layer;

        public CPPNnode(int ID,
            CPPNFunction function)
        {
            this.function = function;
            this.ID = ID;
            this.inputs = new();
            this.outputs = new();
        }

        public CPPNnode Clone()
        {
            return new(this.ID,
                this.function);
        }

        public static float EvaluateCPPNFunction(CPPNFunction function, float sum)
        {
            float activation;
            switch (function)
            {
                case CPPNFunction.Linear:
                    // pass through
                    activation = sum;
                    break;
                case CPPNFunction.Sigmoid:
                    activation = 1.0f / (1.0f + math.exp(-1 * sum));
                    break;
                case CPPNFunction.Gaussian:
                    activation = math.exp(-(sum * sum));
                    break;
                case CPPNFunction.Sine:
                    activation = math.sin(sum);
                    break;
                case CPPNFunction.Abs:
                    activation = math.abs(sum);
                    break;
                case CPPNFunction.Step:
                    if(sum > 0)
                    {
                        activation = 1;
                    }else// if(sum < 0)
                    {
                        activation = 0;
                    }
                    break;
                case CPPNFunction.ReLU:
                    activation = sum > 0 ? sum : 0;
                    break;
                case CPPNFunction.Cube:
                    activation = (sum * sum * sum);
                    break;
                case CPPNFunction.Square:
                    activation = (sum * sum);
                    break;
                case CPPNFunction.Cosine:
                    activation = math.cos(sum);
                    break;
                case CPPNFunction.Tangent:
                    activation = math.tan(sum);
                    break;
                case CPPNFunction.HyperSine:
                    activation = math.sinh(sum);
                    break;
                case CPPNFunction.HyperCosine:
                    activation = math.cosh(sum);
                    break;
                case CPPNFunction.HyperTangent:
                    activation = math.tanh(sum);
                    break;
                case CPPNFunction.SquareRoot:
                    activation = math.sqrt(math.abs(sum));
                    break;
                default:
                    Debug.LogError("Not recognized.");
                    activation = -1;
                    break;
            }

            if (float.IsNaN(activation)) return 0.0f;
            else return activation;
        }

    }

  

    public class CPPNconnection
    {
        public int ID;
        public float weight;
        public int from_node_ID;
        public int to_node_ID;
        public bool enabled;

        public CPPNconnection(int ID,
            float weight,
            int from_ID,
            int to_ID,
            bool enabled=true)
        {
            this.ID = ID;
            this.weight = weight;
            this.from_node_ID = from_ID;
            this.to_node_ID = to_ID;
            this.enabled = enabled;
        }
        public CPPNconnection Clone()
        {
            return new(this.ID,
                this.weight,
                this.from_node_ID,
                this.to_node_ID,
                this.enabled);
        }
    }

    static int NEXT_SYNAPSE_ID = BrainGenome.INVALID_NEAT_ID;
    static int NEXT_NEURON_ID = BrainGenome.INVALID_NEAT_ID;
    static int GetNextGlobalCPPNSynapseID()
    {
        int ID = NEXT_SYNAPSE_ID;
        NEXT_SYNAPSE_ID++;
        return ID;
    }
    static int GetNextGlobalCPPNNeuronID()
    {
        int ID = NEXT_NEURON_ID;
        NEXT_NEURON_ID++;
        return ID;
    }

    public override void ScheduleDevelopGPUJob()
    {
        throw new NotImplementedException();
    }
}