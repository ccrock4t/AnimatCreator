using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Brain;

// Parallel compute the neural activations for the next time step

//[burstcompile]
public struct ParallelNeuralUpdateCPU : IJobParallelFor
{

    [ReadOnly]
    // buffers holding current state
    public NativeArray<Neuron> current_state_neurons; // 1-to-1 mapping NeuronID --> neuron
    [ReadOnly]
    public NativeArray<Synapse> current_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.

    // buffers holding next state
    [WriteOnly]
    [NativeDisableParallelForRestriction]
    public NativeArray<Neuron> next_state_neurons; // 1-to-1 mapping NeuronID --> neuron

    [NativeDisableParallelForRestriction]
    public NativeArray<Synapse> next_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.


    const bool NORMALIZE_WEIGHT = true;

    const bool CONSTRAIN_WEIGHT = false;
    static float2 CONSTRAIN_WEIGHT_RANGE = new float2(-50, 50);

    const bool CONSTRAIN_BIAS = true;
    static float2 CONSTRAIN_BIAS_RANGE = new float2(-10, 10);


    public void Execute(int i)
    {
        // set the neuron data
        this.next_state_neurons[i] = CalculateNeuronActivation(i,
            this.current_state_neurons, 
            this.current_state_synapses,
            this.next_state_synapses);
    
    }

     
    public static Neuron CalculateNeuronActivation(int i,
        NativeArray<Neuron> current_state_neurons,
        NativeArray<Synapse> current_state_synapses,
        NativeArray<Synapse> next_state_synapses)
    {
        bool Normalize = NORMALIZE_WEIGHT;

        Neuron to_neuron = current_state_neurons[i];
        bool is_sensory_neuron = to_neuron.IsSensory();
        if (is_sensory_neuron) return to_neuron;

        if (!to_neuron.enabled)
        {
            to_neuron.activation = 0;
            return to_neuron;
        }

        to_neuron.real_num_of_synapses = 0; //for metadata

        // sum inputs to the neuron
        float euclidean_norm = 0;
        
        int start_idx = to_neuron.synapse_start_idx;
        int end_idx = (to_neuron.synapse_start_idx + to_neuron.synapse_count);
        float sum = to_neuron.bias;

        for (int j = start_idx; j < end_idx; j++)
        {
            Synapse connection = current_state_synapses[j];
            if (!connection.IsEnabled()) continue;
            int from_idx = connection.from_neuron_idx;
            Neuron from_neuron = current_state_neurons[from_idx];

            float input = connection.weight * from_neuron.activation;
            if (!from_neuron.excitatory) input *= -1;
            sum += input;
            
            to_neuron.real_num_of_synapses++;

            if(Normalize) euclidean_norm += math.pow(connection.weight,2);
        }



        float to_neuron_new_activation;
    
        if (to_neuron.activation_function == Neuron.ActivationFunction.Linear)
        {
            to_neuron_new_activation = to_neuron.LinearSum(sum);
        }
        else if (to_neuron.activation_function == Neuron.ActivationFunction.Sigmoid)
        {
            to_neuron_new_activation = to_neuron.SigmoidSquashSum(sum);
        }
        else if (to_neuron.activation_function == Neuron.ActivationFunction.Tanh)
        {
            to_neuron_new_activation = to_neuron.TanhSquashSum(sum);
        }
        else if (to_neuron.activation_function == Neuron.ActivationFunction.LeakyReLU)
        {
            to_neuron_new_activation = to_neuron.LeakyReLUSum(sum);
        }
        else if (to_neuron.activation_function == Neuron.ActivationFunction.ReLU)
        {
            to_neuron_new_activation = to_neuron.ReLUSum(sum);
        }
        else if (to_neuron.activation_function == Neuron.ActivationFunction.Step)
        {
            to_neuron_new_activation = to_neuron.Step(sum);
        }
        else
        {
            Debug.LogError("error");
            return to_neuron;
        }
        if (float.IsNaN(to_neuron_new_activation) || !float.IsFinite(to_neuron_new_activation))
        {
            to_neuron_new_activation = 0;
        }

        to_neuron.activation = to_neuron_new_activation;


        if (Normalize) euclidean_norm = math.sqrt(euclidean_norm);

        return to_neuron;
    }




}
