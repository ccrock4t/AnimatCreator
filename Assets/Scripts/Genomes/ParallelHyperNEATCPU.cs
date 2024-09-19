using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Brain;
using static CPPNGenome;
using static GlobalConfig;
using static SoftVoxelRobot;


//[burstcompile]
public struct ParallelHyperNEATCPU : IJobParallelFor
{
    public MultiLayerNetworkInfo network_info;

    public CPPNGenome.CPPNtype type;

    public int5 dimensions;
    public int3 dimensions3D;

    [NativeDisableParallelForRestriction]
    public NativeArray<Neuron> neurons;

    [NativeDisableParallelForRestriction]
    public NativeArray<Synapse> synapses;

    [NativeDisableParallelForRestriction]
    public NativeArray<RobotVoxel> robot_voxels;

    [ReadOnly]
    public NativeArray<CPPNnodeParallel> CPPN_nodes;

    [ReadOnly]
    public NativeArray<CPPNconnectionParallel> CPPN_connections;

    public int2 CPPN_IO_NODES_INDEXES;

    public const float ALPHA_SCALE = 1f;


    public void Execute(int i)
    {

        int neurons_per_cell = GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL ? this.network_info.GetNumOfNeurons() : 1;
        if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN || robot_voxels.Length == 0)
        {
            //determine brain, or brain+body
            int TOTAL_3D_SUBSTRATE_SIZE = dimensions3D.x * dimensions3D.y * dimensions3D.z;
            //connection from neuron2 to neuron1
            // connections from location (0,0,0) determine neuron and morphology characteristics

            int neuron1_flat_idx = (int)math.floor(i / neurons.Length);
            int neuron2_flat_idx = i - neuron1_flat_idx * neurons.Length;

            int cell1_flat_idx = neuron1_flat_idx / neurons_per_cell;
            int cell2_flat_idx = neuron2_flat_idx / neurons_per_cell;

            int3 cell1_index = GlobalUtils.Index_int3FromFlat(cell1_flat_idx, dimensions3D);
            int3 cell2_index = GlobalUtils.Index_int3FromFlat(cell2_flat_idx, dimensions3D);

            int x1 = cell1_index.x;
            int y1 = cell1_index.y;
            int z1 = cell1_index.z;

            int x2 = cell2_index.x;
            int y2 = cell2_index.y;
            int z2 = cell2_index.z;

            float normalized_x1 = GlobalUtils.NormalizeIndex(x1, dimensions3D.x);
            float normalized_y1 = GlobalUtils.NormalizeIndex(y1, dimensions3D.y);
            float normalized_z1 = GlobalUtils.NormalizeIndex(z1, dimensions3D.z);
            float2 wv1 = GetNeuronCoordinates(neuron1_flat_idx % neurons_per_cell, this.network_info);

            float normalized_w1 = wv1.x;
            float normalized_v1 = wv1.y;

            float normalized_x2 = GlobalUtils.NormalizeIndex(x2, dimensions3D.x);
            float normalized_y2 = GlobalUtils.NormalizeIndex(y2, dimensions3D.y);
            float normalized_z2 = GlobalUtils.NormalizeIndex(z2, dimensions3D.z);
            float2 wv2 = GetNeuronCoordinates(neuron2_flat_idx % neurons_per_cell, this.network_info);
            float normalized_w2 = wv2.x;
            float normalized_v2 = wv2.y;

            CPPNOutput cppn_result = CPPNGenome.QueryCPPN(this.type,
                CPPN_IO_NODES_INDEXES,
                this.CPPN_nodes, this.CPPN_connections,
                normalized_x1,
                normalized_y1,
                normalized_z1,
                normalized_w1,
                normalized_v1,
                normalized_x2,
                normalized_y2,
                normalized_z2,
                normalized_w2,
                normalized_v2
                );


            Synapse synapse = Synapse.GetDefault();

            if (cppn_result.link_expression_output)
            {
                synapse.enabled = true;
            }
            else
            {
                synapse.weight = 0;
                synapse.enabled = false;
            }

            synapse.from_neuron_idx = neuron2_flat_idx;
            synapse.to_neuron_idx = neuron1_flat_idx;

            // determine voxel and neuron characteristics
            if (neuron2_flat_idx == 0)
            {
                RobotVoxel body_material = RobotVoxel.Empty;
                if ((neuron1_flat_idx % neurons_per_cell == 0) && robot_voxels.Length > 0)
                {
                    if (GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL)
                    {

                        body_material = RobotVoxel.Touch_Sensor;

                        if (x1 == 0)
                        {
                            if (y1 > 0 && y1 < this.dimensions3D.y - 1)
                            {
                                if (z1 > 0 && z1 < this.dimensions3D.z - 1)
                                {
                                    body_material = RobotVoxel.Raycast_Sensor;
                                }
                            }
                        }

                        if (x1 == 0 && y1 == 0 && z1 == 0) body_material = RobotVoxel.SineWave_Generator;
                    }
                    else
                    {
                        // determine body
                        bool is_corner = x1 == 0 && z1 == 0
                            || x1 == 0 && z1 == dimensions.z - 1
                            || x1 == dimensions.x - 1 && z1 == 0
                            || x1 == dimensions.x - 1 && z1 == dimensions.z - 1;
                        body_material = cppn_result.robot_voxel_material;
                        bool is_cross_center = (x1 == dimensions.x / 2 && (z1 == dimensions.z - 1 || z1 == 0))
                            || (z1 == dimensions.z / 2 && (x1 == dimensions.x - 1 || x1 == 0));

                        if ((y1 == 1) && !(x1 == 0 || x1 == dimensions.x - 1 || z1 == 0 || z1 == dimensions.z - 1)) body_material = RobotVoxel.Touch_Sensor;
                        else if ((is_cross_center || is_corner) && y1 == dimensions.y / 2 && x1 == 0 && z1 == 0) body_material = RobotVoxel.Raycast_Sensor; // raycast on one corner
                        else if ((is_cross_center || is_corner) && y1 == dimensions.y / 2) body_material = RobotVoxel.Touch_Sensor; // muscles on corners and center
                        // else if ((is_cross_center || is_corner) && y1 == dimensions.y/2) body_material = RobotVoxel.Raycast_Sensor; // raycast on all corners and centers
                        else if (y1 == 0 && !is_corner) body_material = RobotVoxel.Empty;
                        else if (y1 == 0 && is_corner) body_material = RobotVoxel.Touch_Sensor;
                        else if (y1 == 0 || y1 == dimensions.y - 1) body_material = RobotVoxel.Touch_Sensor;
                        else if (z1 == 0 || z1 == dimensions.z - 1) body_material = RobotVoxel.Touch_Sensor;
                        else if (x1 == 0 || x1 == dimensions.x - 1) body_material = RobotVoxel.Touch_Sensor;
                        else body_material = RobotVoxel.Touch_Sensor;

                    }
                    robot_voxels[cell1_flat_idx] = body_material;
                }


                Neuron neuron = Neuron.GetNewNeuron();
                neuron.synapse_start_idx = neuron1_flat_idx * neurons.Length;
                neuron.excitatory = cppn_result.excitatory;
     
                neuron.activation_function = Neuron.ActivationFunction.Tanh;
                
                neuron.activation = 0;
                neuron.synapse_count = neurons.Length;
                neuron.bias = cppn_result.neuron_bias;
                neuron.sigmoid_alpha = math.abs(cppn_result.sigmoid_alpha);
                neuron.enabled = true;
                neuron.activation_function = cppn_result.neuron_activation_function;

                int w1 = 0;
                int v1 = 0;
                if (GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL)
                {
                    if (neuron1_flat_idx % neurons_per_cell < this.network_info.input_layer_size)
                    {
                        neuron.neuron_class = Neuron.NeuronClass.Sensor;
                        w1 = 0;
                        v1 = neuron1_flat_idx % neurons_per_cell;
                    }
                    else if (neuron1_flat_idx % neurons_per_cell < this.network_info.input_layer_size + this.network_info.output_layer_size)
                    {
                        neuron.neuron_class = Neuron.NeuronClass.Motor;
                        w1 = dimensions.w - 1;
                        v1 = (neuron1_flat_idx % neurons_per_cell) - this.network_info.input_layer_size;
                    }
                    else// if ()
                    {
                        neuron.neuron_class = Neuron.NeuronClass.Hidden;
                        int hidden_neuron_num = (neuron1_flat_idx % neurons_per_cell - (this.network_info.input_layer_size + this.network_info.output_layer_size));
                        w1 = (hidden_neuron_num / this.network_info.hidden_layer_size) + 1;
                        v1 = hidden_neuron_num - (w1 - 1) * this.network_info.hidden_layer_size;

                    }
                }
                else
                {
                    Debug.LogError("error");
                }

                if(neuron.neuron_class == Neuron.NeuronClass.Motor)
                {
                    // all motors must be in [-1,1]
                    neuron.activation_function = Neuron.ActivationFunction.Tanh;
                }

                if (neuron.neuron_class != Neuron.NeuronClass.Hidden)
                {
                    neuron.excitatory = true;
                }

                neuron.position_idxs = new(x1, y1, z1, w1, v1);
                neuron.position_normalized = new(normalized_x1, normalized_y1, normalized_z1, normalized_w1, normalized_v1);

                neurons[neuron1_flat_idx] = neuron;
            }

            synapses[i] = synapse;
        }
        else
        {
            Debug.LogError("todo");
        }


    }

    public (int, int) GetNeuronIndices(int i)
    {
        int neuron1_idx = -1;
        int neuron2_idx = -1;
        int j = -1;
        int num_of_input_synapses = this.network_info.GetNumOfInputToHiddenSynapses();
        int num_of_output_synapses = this.network_info.GetNumOfHiddenToOutputSynapses();
        int first_hidden_neuron_idx = GetIndexOfFirstHiddenNeuronInFirstLayer();

        if (i < num_of_input_synapses)
        {
            j = i;
            //synapses between input layer and first hidden layer
            neuron2_idx = j % this.network_info.input_layer_size;
            neuron1_idx = (int)math.floor(j / this.network_info.input_layer_size) + first_hidden_neuron_idx;
        }
        else if (i < num_of_input_synapses + num_of_output_synapses)
        {
            //synapses between last hidden layer and output layer
            j = i - num_of_input_synapses;
            neuron1_idx = (int)math.floor(j / this.network_info.hidden_layer_size) + this.network_info.input_layer_size;
            int last_layer_first_hidden_neuron_idx = GetIndexOfFirstHiddenNeuronInLastLayer();
            neuron2_idx = j % this.network_info.hidden_layer_size + last_layer_first_hidden_neuron_idx;
        }
        else
        {
            // synapses between all the hidden layers
            j = i - num_of_input_synapses - num_of_output_synapses;
            int first_hidden_neuron_idx_in_second_hidden_layer = first_hidden_neuron_idx + this.network_info.hidden_layer_size;
            neuron1_idx = (int)math.floor(j / this.network_info.hidden_layer_size) + first_hidden_neuron_idx_in_second_hidden_layer;
            int neuron2_hidden_layer = (int)math.floor(math.floor(j / this.network_info.hidden_layer_size) / this.network_info.hidden_layer_size);
            int first_hidden_neuron_idx_in_previous_layer = first_hidden_neuron_idx + neuron2_hidden_layer * this.network_info.hidden_layer_size;

            neuron2_idx = j % this.network_info.hidden_layer_size + first_hidden_neuron_idx_in_previous_layer;

        }


        return (neuron1_idx, neuron2_idx);
    }

    public static float2 GetNeuronCoordinates(int neuron_idx, MultiLayerNetworkInfo network_info)
    {
        float layer; // y
        float x;
        if (neuron_idx < network_info.input_layer_size)
        {
            //input layer
            layer = 0;
            int input_node_num = neuron_idx;
            if ((network_info.input_layer_size - 1) == 0) x = 0.5f;
            else x = (float)input_node_num / (network_info.input_layer_size - 1);
        }
        else if (neuron_idx < network_info.input_layer_size + network_info.output_layer_size)
        {
            // output layer
            layer = network_info.GetNumOfLayers() - 1;
            int output_node_num = (neuron_idx - network_info.input_layer_size);
            if ((network_info.output_layer_size - 1) == 0) x = 0.5f;
            else x = (float)output_node_num / (network_info.output_layer_size - 1);
        }
        else
        {
            // hidden layer
            int hidden_node_num = (neuron_idx - network_info.input_layer_size - network_info.output_layer_size);
            int hidden_layer = (int)math.floor((float)hidden_node_num / network_info.hidden_layer_size);
            int hidden_node_num_in_layer = hidden_node_num - hidden_layer * network_info.hidden_layer_size;

            if ((network_info.hidden_layer_size - 1) == 0) x = 0.5f;
            else x = (float)hidden_node_num_in_layer / (network_info.hidden_layer_size - 1);

            layer = hidden_layer + 1;
        }
        float y = layer / (network_info.GetNumOfLayers() - 1);

        return new((y - 0.5f) * 2, (x - 0.5f) * 2);
    }
    public int GetIndexOfFirstHiddenNeuronInFirstLayer()
    {
        return this.network_info.input_layer_size + this.network_info.output_layer_size;
    }
    public int GetIndexOfFirstHiddenNeuronInLastLayer()
    {
        return GetIndexOfFirstHiddenNeuronInFirstLayer() + (this.network_info.num_of_hidden_layers - 1) * this.network_info.hidden_layer_size;
    }

}
