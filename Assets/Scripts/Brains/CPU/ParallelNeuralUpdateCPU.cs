using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using static Brain;

// Parallel compute the neural activations for the next time step

[BurstCompile]
public struct ParallelNeuralUpdateCPU : IJobParallelFor
{

    [ReadOnly]
    // buffers holding current state
    public NativeArray<Neuron> current_state_neurons; // 1-to-1 mapping NeuronID --> neuron
    [ReadOnly]
    public NativeArray<Synapse> current_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.

    // buffers holding next state
    [NativeDisableParallelForRestriction]
    public NativeArray<Neuron> next_state_neurons; // 1-to-1 mapping NeuronID --> neuron
    [NativeDisableParallelForRestriction]
    public NativeArray<Synapse> next_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.

    public void Execute(int i)
    {
        /*
         * First do the Firing calculation 
         */
        Neuron neuron = this.current_state_neurons[i];

        neuron.real_num_of_synapses = 0; //for metadata

        //math.E(-1.0f / (neuron.adaptation_delta));

        // neuron voltage decay
        neuron.DecayVoltage();

        // sum inputs in the current
        int start_idx = (neuron.neuron_class == Neuron.NeuronClass.Sensor) ? 0 : neuron.synapse_start_idx;
        int end_idx = (neuron.neuron_class == Neuron.NeuronClass.Sensor) ? 0 : (neuron.synapse_start_idx + neuron.synapse_count);
        float sum = 0.0f;
        sum += neuron.bias;
        for (int j = start_idx; j < end_idx; j++)
        {
            Synapse connection = this.current_state_synapses[j];
            if (!connection.enabled) continue;
            int from_id = connection.from_neuron_idx;
            Neuron from_neuron = this.current_state_neurons[from_id];


            int from_sign = from_neuron.sign;
            if (from_neuron.type == Neuron.NeuronType.Spiking)
            {
                if (from_neuron.firing == 0) continue;
                sum += connection.weight * from_sign;
            }
            else //if(from_neuron.type == Neuron.NeuronType.Perceptron)
            {
                sum += connection.weight * from_sign * from_neuron.activation;
            }
            neuron.real_num_of_synapses++;
        }

 
        if (neuron.type == Neuron.NeuronType.Spiking)
        {
            neuron.voltage += sum;
            if (neuron.voltage > neuron.threshold)
            {
                // fire if the voltage exceeds threshold
                neuron.firing = 1;
            }
            else
            {
                neuron.firing = 0;
            }
        }
        else //if(next_state_neuron.type == Neuron.NeuronType.Perceptron)
        {
            if (neuron.neuron_class != Neuron.NeuronClass.Sensor)
            {
                if (neuron.activation_function == Neuron.NeuronActivationFunction.Sigmoid)
                {
                    neuron.activation = neuron.SigmoidSquashSum(sum);
                }
                else if (neuron.activation_function == Neuron.NeuronActivationFunction.Tanh)
                {
                    neuron.activation = neuron.TanhSquashSum(sum);
                }
                else if (neuron.activation_function == Neuron.NeuronActivationFunction.LeakyReLU)
                {
                    neuron.activation = neuron.LeakyReLUSum(sum);
                }
                else if (neuron.activation_function == Neuron.NeuronActivationFunction.ReLU)
                {
                    neuron.activation = neuron.ReLUSum(sum);
                }
                else
                {
                    Debug.LogError("error");
                }
                if (!float.IsFinite(neuron.activation))
                {
                    neuron.activation = 0;
                }
            }
        }


        // set the data
        this.next_state_neurons[i] = neuron;

        /*
         * Second do the Hebbian learning calculation 
         */
 
        float delta_weight;
        for (int j = start_idx; j < end_idx; j++)
        {
            Synapse connection = this.current_state_synapses[j];
            if (connection.enabled)
            {
                int from_idx = connection.from_neuron_idx;
                Neuron from_neuron = this.current_state_neurons[from_idx];
                float presynaptic_firing;
                float postsynaptic_firing;
                if (from_neuron.type == Neuron.NeuronType.Spiking)
                {
                    presynaptic_firing = from_neuron.firing == 1 ? 1 : 0;
                    postsynaptic_firing = this.current_state_neurons[i].firing == 1 ? 1 : 0;
                }
                else
                {
                    presynaptic_firing = from_neuron.activation;
                    postsynaptic_firing = this.current_state_neurons[i].activation;
                }

                delta_weight = connection.learning_rate_r * (connection.coefficient_A * presynaptic_firing * postsynaptic_firing
                    + connection.coefficient_B * presynaptic_firing
                    + connection.coefficient_C * postsynaptic_firing
                    + connection.coefficient_D);
                connection.weight += delta_weight;
            }

            if (!float.IsFinite(connection.weight))
            {
                connection.weight = 0;
            }

            // constrain weight in [-1,1]
   /*         if(connection.weight > 1)
            {
                connection.weight = 1;
            }else if(connection.weight < -1)
            {
                connection.weight = -1;
            }*/

            this.next_state_synapses[j] = connection; // set the data
        }


    
    }


}
