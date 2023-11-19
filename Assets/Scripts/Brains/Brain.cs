using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static Brain;
using static BrainGenome;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

public abstract class Brain
{


    public Dictionary<string, Dictionary<string, int>> neuron_indices; // SENSORY/MOTOR --> { LEG/BODY_segment# --> neuron indices }
    public const string SENSORY_NEURON_KEY = "SENSORY";
    public const string MOTOR_NEURON_KEY = "MOTOR";

    public List<DevelopmentNeuron> developed_brain;


    public BrainGenome genome;



    public Brain(BrainGenome genome)
    {
        // store genome
        this.genome = genome;

        // initialize memory
        neuron_indices = new();
        neuron_indices[Brain.SENSORY_NEURON_KEY] = new();
        neuron_indices[Brain.MOTOR_NEURON_KEY] = new();
    }

    public abstract void DoWorkingCycle();
    public abstract void DevelopFromGenome();

    public abstract void DisposeOfNativeCollections();

    public abstract int GetNumberOfNeurons();

    public abstract int GetNumberOfSynapses();


    public void ScheduleDevelopJob(BrainGenome genome)
    {
        if (GlobalConfig.brain_genome_development_processing_method == GlobalConfig.ProcessingMethod.CPU)
        {
             genome.ScheduleDevelopCPUJob();
        }
        else if (GlobalConfig.brain_genome_development_processing_method == GlobalConfig.ProcessingMethod.GPU)
        {
            GlobalUtils.LogErrorFeatureNotImplemented("todo -- schedule GPU jobs into a batch");
            return;   
        }

    }


    public void Develop(BrainGenome genome)
    {
        if(GlobalConfig.brain_genome_development_processing_method == GlobalConfig.ProcessingMethod.CPU)
        {
            (NativeArray<Neuron> neurons, NativeArray<Synapse> synapses) = genome.DevelopCPU(neuron_indices);

            if (this is BrainCPU)
            {
                // todo return nativearrays in DevelopCPU, so we don't have to do ToArray().
                ((BrainCPU)this).current_state_neurons = neurons;
                ((BrainCPU)this).current_state_synapses = synapses;
                ((BrainCPU)this).next_state_neurons = new(((BrainCPU)this).current_state_neurons.Length, Allocator.Persistent);
                ((BrainCPU)this).next_state_synapses = new(((BrainCPU)this).current_state_synapses.Length, Allocator.Persistent);
            }
            else if(this is BrainGPU)
            {
                ((BrainGPU)this).current_state_neurons = new(neurons.Length, Marshal.SizeOf(typeof(Neuron)));
                ((BrainGPU)this).current_state_synapses = new(synapses.Length, Marshal.SizeOf(typeof(Synapse)));
                ((BrainGPU)this).current_state_neurons.SetData(neurons);
                ((BrainGPU)this).current_state_synapses.SetData(synapses);
                ((BrainGPU)this).InitializeComputeBuffers();
            }
            else
            {
                GlobalUtils.LogErrorEnumNotRecognized("Brain type");
            }
        }else if(GlobalConfig.brain_genome_development_processing_method == GlobalConfig.ProcessingMethod.GPU)
        {
            if(GlobalConfig.brain_genome_method != GlobalConfig.BrainGenomeMethod.HyperNEAT)
            {
                GlobalUtils.LogErrorFeatureNotImplemented("Developing genomes on GPU is only supported for HyperNEAT genomes.");
                return;
            }
            (ComputeBuffer neurons, ComputeBuffer synapses) = genome.DevelopGPU(neuron_indices);

            if (this is BrainCPU)
            {
                Neuron[] cpu_neurons = new Neuron[neurons.count];
                neurons.GetData(cpu_neurons, 0, 0, neurons.count);
                NativeArray<Neuron> cpu_neurons_native_array = new(cpu_neurons, Allocator.Persistent);

                Synapse[] cpu_synapses = new Synapse[synapses.count];
                synapses.GetData(cpu_synapses, 0, 0, synapses.count);
                NativeArray<Synapse> cpu_synapses_native_array = new(cpu_synapses, Allocator.Persistent);

                ((BrainCPU)this).current_state_neurons = cpu_neurons_native_array;
                ((BrainCPU)this).current_state_synapses = cpu_synapses_native_array;
                ((BrainCPU)this).next_state_neurons = new(((BrainCPU)this).current_state_neurons.Length, Allocator.Persistent);
                ((BrainCPU)this).next_state_synapses = new(((BrainCPU)this).current_state_synapses.Length, Allocator.Persistent);
            }
            else if(this is BrainGPU)
            {
                ((BrainGPU)this).current_state_neurons = neurons;
                ((BrainGPU)this).current_state_synapses = synapses;
                ((BrainGPU)this).InitializeComputeBuffers();
            }
            else
            {
                GlobalUtils.LogErrorEnumNotRecognized("Brain type");
            }
        }

    }



    [System.Serializable]
    public struct Synapse
    {

        public float weight; // the activation value multiplier


        // evolvable parameters
        public float learning_rate_r;  // the learning rate
        public float coefficient_A;  // correlated activation coefficient
        public float coefficient_B;  // pre-synaptic activation coefficient
        public float coefficient_C; // post-synaptic activation coefficient
        public float coefficient_D; // a connection bias

        // info
        public int from_neuron_idx { get; set; } // neuron index this connection is coming from
        public int to_neuron_idx { get; set; } // neuron index this connection is coming from
        public bool enabled;

        public Synapse(float learning_rate,
            int from_neuron_idx,
            int to_neuron_idx,
            float[] coefficients)
        {
            this.learning_rate_r = learning_rate;
            this.coefficient_A = coefficients[0];
            this.coefficient_B = coefficients[1];
            this.coefficient_C = coefficients[2];
            this.coefficient_D = coefficients[3];



            this.weight = 0;
            this.enabled = true;

            this.from_neuron_idx = from_neuron_idx;
            this.to_neuron_idx = to_neuron_idx;
        }
    }

    [System.Serializable]
    public struct Neuron
    {
        public enum NeuronType : int
        {
            Perceptron,
            Spiking
        }

        public enum NeuronClass : int
        {
            Hidden,
            Sensor,
            Motor
        }

        public enum NeuronActivationFunction : int
        {
            Sigmoid,
            Tanh,
            LeakyReLU
        }


        // === static
        public NeuronType type;
        public NeuronActivationFunction activation_function;
        public int sign; // sign of outputs (false == -1, true == 1)
        public NeuronClass neuron_class; // 0 no, 1 sensor, 2 motor



        // === dynamic

        // spiking
        public int firing; //0 or 1
        public float voltage; // accumulated voltage in the neuron

        // perceptron
        public float activation; //sigmoid output in (0,1)

        // === evolvable parameters

        public int threshold; // V_t
        public float bias;  // bias
        public float adaptation_delta; // delta
        public float decay_rate_tau; // tau
        public float sigmoid_alpha; // larger alpha = steeper slope, easier to activate --- smaller alpha = gradual slope, harder to activate.

        // metadata
        public int synapse_start_idx;
        public int synapse_count;
        public int3 position;

        public int real_num_of_synapses;

        public Neuron(int threshold,
            float bias,
            float adaptation_delta,
            float decay_rate_tau,
            float sigmoid_alpha,
            bool sign,
            NeuronType type = NeuronType.Perceptron,
            NeuronActivationFunction activation_function = NeuronActivationFunction.Sigmoid)
        {
            this.type = type;
            this.activation_function = activation_function;
            this.threshold = threshold;
            this.bias = bias;
            this.adaptation_delta = adaptation_delta;
            this.decay_rate_tau = decay_rate_tau;
            this.sigmoid_alpha = sigmoid_alpha;

            this.firing = 0;
            this.voltage = 0.0f;
            this.activation = 0.0f;
            this.sign = sign ? 1 : -1;
            this.neuron_class = NeuronClass.Hidden;

            this.synapse_count = 0;
            this.synapse_start_idx = -1;
            this.position = new int3(-1, -1, -1);

            this.real_num_of_synapses = 0;
        }

        public void DecayVoltage()
        {
            this.voltage *= this.decay_rate_tau;
        }

        /// <summary>
        ///         Perceptron squash with a sigmoid function
        /// </summary>
        /// <param name="sum"></param>
        /// <returns></returns>
        public float SigmoidSquashSum(float sum)
        {
            return 1.0f / (1.0f + math.exp(-this.sigmoid_alpha * sum));
        }

        public float TanhSquashSum(float sum)
        {
            return math.tanh(this.sigmoid_alpha*sum);
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


    }



}
