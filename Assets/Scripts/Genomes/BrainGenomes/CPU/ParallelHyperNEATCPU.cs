using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Brain;
using static HyperNEATBrainGenome;


[BurstCompile]
public struct ParallelHyperNEATCPU : IJobParallelFor
{
    public int SUBSTRATE_SIZE_X;
    public int SUBSTRATE_SIZE_Y;
    public int SUBSTRATE_SIZE_Z;
    public int NUM_OF_NODES;
    public int OUTPUT_NODES_START_IDX;
    public int OUTPUT_NODES_END_IDX;
    public float CONNECTION_PRUNING_CUTOFF;

    [NativeDisableParallelForRestriction]
    public NativeArray<Neuron> neurons;
    [NativeDisableParallelForRestriction]
    public NativeArray<Synapse> synapses;

    [ReadOnly]
    public NativeArray<CPPNnodeParallel> CPPN_nodes;
    [NativeDisableParallelForRestriction]
    public NativeArray<float> CPPN_nodes_outputs;
    [ReadOnly]
    public NativeArray<CPPNconnectionParallel> CPPN_connections;



    public void Execute(int index)
    {
 
        Evaluate(index);
    }



    int Index_FlatFromVector3(int x, int y, int z)
    {
        return x + SUBSTRATE_SIZE_X * y + SUBSTRATE_SIZE_X * SUBSTRATE_SIZE_Y * z;
    }


    int3 Index_int3FromFlat(int i)
    {
        int x = i % SUBSTRATE_SIZE_X;
        int y = (int)math.floor(i / SUBSTRATE_SIZE_X) % SUBSTRATE_SIZE_Y;
        int z = (int)math.floor(i / (SUBSTRATE_SIZE_X * SUBSTRATE_SIZE_Y));
        return new int3(x, y, z);
    }

    int TOTAL_SUBSTRATE_SIZE()
    {
        return SUBSTRATE_SIZE_X * SUBSTRATE_SIZE_Y * SUBSTRATE_SIZE_Z;
    }


    float EvaluateCPPNNode(CPPNnodeParallel node, int node_idx_offset)
    {
        float sum = 0;
        for (int i = node.input_connection_start_idx; i < node.input_connection_start_idx + node.number_of_input_connections; i++)
        {
            CPPNconnectionParallel connection = CPPN_connections[i];
            float input_value = CPPN_nodes_outputs[node_idx_offset + connection.from_idx];
            sum += connection.weight * input_value;
        }
        return CPPNnode.EvaluateCPPNFunction((CPPNFunction)node.function, sum);
    }

    CPPNOutputArray EvaluateCPPN(int i, int x1, int y1, int z1, int x2, int y2, int z2)
    {
        CPPNOutputArray output_array = CPPNOutputArray.GetNewDefault();

        int node_idx_offset = i * NUM_OF_NODES;
        int k;
        float result;

        // do inputs
        for (k = 0; k < OUTPUT_NODES_START_IDX; k++)
        {

            if (k == 0)
            {
                result = 1;
            }
            else if (k == 1)
            {
                result = (((float)x1 / (float)SUBSTRATE_SIZE_X) - 0.5f) * 2; ;
            }
            else if (k == 2)
            {
                result = (((float)y1 / (float)SUBSTRATE_SIZE_Y) - 0.5f) * 2;
            }
            else if (k == 3)
            {
                result = (((float)z1 / (float)SUBSTRATE_SIZE_Z) - 0.5f) * 2;
            }
            else if (k == 4)
            {
                result = (((float)x2 / (float)SUBSTRATE_SIZE_X) - 0.5f) * 2;
            }
            else if (k == 5)
            {
                result = (((float)y2 / (float)SUBSTRATE_SIZE_Y) - 0.5f) * 2;
            }
            else// if (k == 6)
            {
                result = (((float)z2 / (float)SUBSTRATE_SIZE_Z) - 0.5f) * 2;
            }

            CPPN_nodes_outputs[node_idx_offset + k] = result;
        }


 


        // do hidden nodes
        for (k = OUTPUT_NODES_END_IDX; k < NUM_OF_NODES; k++)
        {
            CPPN_nodes_outputs[node_idx_offset + k] = EvaluateCPPNNode(CPPN_nodes[k], node_idx_offset); ;
        }

        // do outputs
        int m = 0;
        for (k = OUTPUT_NODES_START_IDX; k < OUTPUT_NODES_END_IDX; k++)
        {
            result = EvaluateCPPNNode(CPPN_nodes[k], node_idx_offset);

            if (m == 0)
            {
                output_array.initial_weight = result;
            }
            else if (m == 1)
            {
                output_array.learning_rate = result;
            }
            else if (m == 2)
            {
                output_array.hebb_ABCD_coefficients[0] = ABCD_multiplier * result;
            }
            else if (m == 3)
            {
                output_array.hebb_ABCD_coefficients[1] = ABCD_multiplier * result;
            }
            else if (m == 4)
            {
                output_array.hebb_ABCD_coefficients[2] = ABCD_multiplier * result;
            }
            else if (m == 5)
            {
                output_array.hebb_ABCD_coefficients[3] = ABCD_multiplier * result;
            }
            else if (m == 6)
            {
                output_array.bias = result;
            }
            else if (m == 7)
            {
                output_array.sigmoid_alpha = multiplier * math.abs(result);
            }
            else if (m == 8)
            {
                

                if (result >= -1 && result <= -0.33)
                {
                    output_array.activation_function = Neuron.NeuronActivationFunction.Sigmoid;
                   
                }
                else if (result >= -0.33 && result <= 0.33) {
                    output_array.activation_function = Neuron.NeuronActivationFunction.Tanh;
                }
                else //   result > 0.25
                {
                    output_array.activation_function = Neuron.NeuronActivationFunction.LeakyReLU;
                }
            }
            else if (m == 9)
            {
                output_array.sign = result < 0 ? -1 : 1;
            }
            else if (m == 10)
            {
                output_array.enabled = result > 0 ? true : false;
            }
            else
            {
                Debug.LogError("Error");
            }
            m++;
        }

        return output_array;
    }


    void Evaluate(int i)
    {
        int neuron1_flat_idx = (int)math.floor(i / TOTAL_SUBSTRATE_SIZE());
        int neuron2_flat_idx = i - neuron1_flat_idx * TOTAL_SUBSTRATE_SIZE();

        int3 neuron1_index = Index_int3FromFlat(neuron1_flat_idx);
        Neuron neuron1 = neurons[neuron1_flat_idx];
        int x1 = neuron1_index.x;
        int y1 = neuron1_index.y;
        int z1 = neuron1_index.z;

        int3 neuron2_index = Index_int3FromFlat(neuron2_flat_idx);
        int x2 = neuron2_index.x;
        int y2 = neuron2_index.y;
        int z2 = neuron2_index.z;


        CPPNOutputArray CPPN_outputs = EvaluateCPPN(i, x2, y2, z2, x1, y1, z1); //from neuron2 to neuron1

        bool neuron1_is_sensor = (z1 == 0);
        bool neuron1_is_motor = (z1 == SUBSTRATE_SIZE_Z-1);
        float initial_weight = CPPN_outputs.initial_weight;
        float learning_rate = CPPN_outputs.learning_rate;


        Synapse synapse = new();
        synapse.from_neuron_idx = neuron2_flat_idx;
        synapse.to_neuron_idx = neuron1_flat_idx;

        if (!CPPN_outputs.enabled || neuron1_is_sensor)
        {
            synapse.learning_rate_r = 0;
            synapse.coefficient_A = 0;
            synapse.coefficient_B = 0;
            synapse.coefficient_C = 0;
            synapse.coefficient_D = 0;
            synapse.weight = 0;
            synapse.enabled = false;
        }
        else
        {
            synapse.learning_rate_r = learning_rate;
            synapse.coefficient_A = CPPN_outputs.hebb_ABCD_coefficients[0];
            synapse.coefficient_B = CPPN_outputs.hebb_ABCD_coefficients[1];
            synapse.coefficient_C = CPPN_outputs.hebb_ABCD_coefficients[2];
            synapse.coefficient_D = CPPN_outputs.hebb_ABCD_coefficients[3];
            synapse.weight = initial_weight;
            synapse.enabled = true;
        }
        synapses[i] = synapse;

        if (x2 == 0 && y2 == 0 && z2 == 0)
        {
            neuron1.type = Neuron.NeuronType.Perceptron;
            neuron1.activation_function = (neuron1_is_sensor || neuron1_is_motor) ? Neuron.NeuronActivationFunction.Tanh : CPPN_outputs.activation_function;

            neuron1.sign = CPPN_outputs.sign;

            neuron1.bias = CPPN_outputs.bias;
            neuron1.sigmoid_alpha = CPPN_outputs.sigmoid_alpha;


            neuron1.synapse_start_idx = neuron1_flat_idx * TOTAL_SUBSTRATE_SIZE();
            neuron1.synapse_count = TOTAL_SUBSTRATE_SIZE();
            neuron1.position = new int3(x1, y1, z1);
            neurons[neuron1_flat_idx] = neuron1;
        }

        
    }



}
