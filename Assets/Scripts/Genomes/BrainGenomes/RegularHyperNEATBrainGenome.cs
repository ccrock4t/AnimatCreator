using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor.MemoryProfiler;
using UnityEngine;
using static Brain;
using static Brain.Neuron;
using static BrainGenome;
using static ESHyperNEATBrainGenome;
using static GlobalConfig;
using static HyperNEATBrainGenome;

public class RegularHyperNEATBrainGenome : HyperNEATBrainGenome
{
    // CPU variables
    public NativeArray<Neuron> final_brain_neurons;
    public NativeArray<Synapse> final_brain_synapses;

    // GPU variables
    int main_kernel;
    public static ComputeShader hyperneat_compute_shader_static;
    ComputeShader hyperneat_compute_shader;
    public ComputeBuffer neurons_compute_buffer;
    public ComputeBuffer synapses_compute_buffer;
    public ComputeBuffer CPPN_node_compute_buffer;
    public ComputeBuffer CPPN_connection_compute_buffer;


    public RegularHyperNEATBrainGenome() : base()
    {
        this.substrate_dimensions = new int3(this.num_of_joints, 10, 4);
        this.substrate_dimensions_size = substrate_dimensions.x * substrate_dimensions.y * substrate_dimensions.z;
        this.substrate = new DevelopmentNeuron[this.substrate_dimensions_size];

        this.InsertHexapodSensorimotorNeurons();
    }



    public static RegularHyperNEATBrainGenome CreateTestGenome()
    {
        RegularHyperNEATBrainGenome genome = new();
        
        genome.SetCPPNnodesForIO();
        genome.FinalizeCPPN();

        return genome;

    }

    public override void InsertHexapodSensorimotorNeurons()
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

    public override DevelopmentNeuron ReadFromSubstrate(int x, int y, int z){
        return this.substrate[VoxelUtils.Index_FlatFromint3(x, y, z, this.substrate_dimensions)];
    }


    public override void WriteToSubstrate(int x, int y, int z, DevelopmentNeuron neuron)
    {
        this.substrate[VoxelUtils.Index_FlatFromint3(x, y, z, this.substrate_dimensions)] = neuron;
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

    public void GPU_Setup()
    {
        Debug.LogError("implement to match CPU");
        if (RegularHyperNEATBrainGenome.hyperneat_compute_shader_static == null)
        {
            RegularHyperNEATBrainGenome.hyperneat_compute_shader_static = (ComputeShader)Resources.Load("ParallelHyperNEATGPU");
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








}