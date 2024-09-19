using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public abstract class Brain
{
    public const string save_file_extension = ".Brain";

    public struct MultiLayerNetworkInfo
    {
        public int input_layer_size;
        public int hidden_layer_size;
        public int output_layer_size;
        public int num_of_hidden_layers;

        public MultiLayerNetworkInfo(int input_layer_size, int hidden_layer_size, int output_layer_size, int num_of_hidden_layers)
        {
            this.input_layer_size = input_layer_size;
            this.hidden_layer_size = hidden_layer_size;
            this.output_layer_size = output_layer_size;
            this.num_of_hidden_layers = num_of_hidden_layers;
        }

        // number of neurons in the whole network
        public int GetNumOfNeurons()
        {
            return this.input_layer_size + this.hidden_layer_size * this.num_of_hidden_layers + this.output_layer_size;
        }

        public int GetNumOfSynapses()
        {
            int num_synapses = 0;
            num_synapses += (this.input_layer_size * this.hidden_layer_size); // between input and first hidden layer
            num_synapses += (this.hidden_layer_size * this.hidden_layer_size) * (this.num_of_hidden_layers - 1); // between hidden layers
            num_synapses += (this.output_layer_size * this.hidden_layer_size); // between last hidden layer and the output
            return num_synapses;
        }

        public int GetNumOfLayers()
        {
            return this.num_of_hidden_layers + 2;
        }

        public int GetFirstInputNeuronIdx()
        {
            return 0;
        }
        public int GetFirstOutputNeuronIdx()
        {
            return this.input_layer_size;
        }

        public int GetFirstHiddenNeuronIdx()
        {
            return this.input_layer_size + this.output_layer_size;
        }

        public int GetNumOfInputToHiddenSynapses()
        {
            return this.input_layer_size * this.hidden_layer_size;
        }

        public int GetNumOfHiddenToOutputSynapses()
        {
            return this.hidden_layer_size * this.output_layer_size;
        }
    }

    public Brain()
    {
    }

    public abstract void ScheduleWorkingCycle();

    public abstract void DisposeOfNativeCollections();

    public abstract Neuron GetNeuronCurrentState(int index);
    public abstract void SetNeuronCurrentState(int index, Neuron neuron);

    public abstract int GetNumberOfNeurons();

    public abstract int GetNumberOfSynapses();

    public abstract void SaveToDisk();


    [System.Serializable]
    public struct Synapse
    {

        public float weight; // the activation value multiplier


        // evolvable parameters


        // info
        public int from_neuron_idx; // neuron index this connection is coming from
        public int to_neuron_idx; // neuron index this connection is coming from
        public bool enabled;

        public Synapse(bool enabled)
        {
            this.weight = 0;
            this.enabled = enabled;
            this.from_neuron_idx = 0;
            this.to_neuron_idx = 0;
        }

        public static Synapse GetDefault()
        {
            return new Synapse(true);
        }

        public bool IsEnabled()
        {
            return this.enabled;
        }
    }

    [System.Serializable]
    public struct Neuron
    {

        public enum NeuronClass : int
        {
            Hidden,
            Sensor,
            Motor
        }

        public enum ActivationFunction : int
        {
            Linear,
            Sigmoid,
            Tanh,
            LeakyReLU,
            ReLU,
            Step
        }


        // === static
        public ActivationFunction activation_function;
        public NeuronClass neuron_class; // 0 no, 1 sensor, 2 motor

        public bool excitatory; 

        // === dynamic

        // perceptron
        public float activation; //sigmoid output in (0,1)

        // === evolvable parameters
        public float bias;  // bias

        public float sigmoid_alpha; // larger alpha = steeper slope, easier to activate --- smaller alpha = gradual slope, harder to activate.

        // metadata
        public int synapse_start_idx;
        public int synapse_count;
        public int5 position_idxs;

        public int real_num_of_synapses;

        public bool enabled;
        public float5 position_normalized;

        public static Neuron GetNewNeuron()
        {
            Neuron neuron = new();
            neuron.activation_function = ActivationFunction.Tanh;

            neuron.bias = 0;
            neuron.activation = 0.0f;
            neuron.neuron_class = NeuronClass.Hidden;

            neuron.synapse_count = 0;
            neuron.synapse_start_idx = -1;
            neuron.position_idxs = new(int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue);
            neuron.position_normalized = new(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);

            neuron.real_num_of_synapses = 0;
            neuron.enabled = true;
            neuron.excitatory = true;
            neuron.sigmoid_alpha = 1;
            return neuron;
        }

        public bool IsSensory()
        {
            return this.neuron_class == NeuronClass.Sensor;
        }
        public float LinearSum(float sum)
        {
            return sigmoid_alpha*sum;
        }

        /// <summary>
        ///         Perceptron squash with a sigmoid function
        /// </summary>
        /// <param name="sum"></param>
        /// <returns></returns>
        public float SigmoidSquashSum(float sum)
        {
            return 1.0f / (1.0f + math.exp(-sigmoid_alpha * sum));
        }

        public float TanhSquashSum(float sum)
        {
            return math.tanh(sigmoid_alpha * sum);
        }

        public float ReLUSum(float sum)
        {
            return math.max(0, sum);
        }

        public float LeakyReLUSum(float sum)
        {
            if(sum < 0)
            {
                return sigmoid_alpha * sum;
            }
            else
            {
                return sum;
            }
            
        }

        public float Step(float sum)
        {
            if (sum <= 0)
            {
                return 0;
            }
            else
            {
                return 1;
            }

        }


    }
}
