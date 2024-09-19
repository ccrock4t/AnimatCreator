// keep this up to date with Brain.Neuron
struct Neuron
{
    int type;
    int activation_function;
    int sign; 
    int neuron_class; // 0 no, 1 sensor, 2 motor
    int firing;
    float voltage;
    float activation;
    int threshold;
    float bias;
    float adaptation_delta;
    float decay_rate_tau;
    float sigmoid_alpha;
    int synapse_start_idx;
    int synapse_count;
    int3 position;
    int real_num_of_synapses;
    

    float SigmoidSquashSum(float sum)
    {
        return 1.0f / (1.0f + exp(-this.sigmoid_alpha * sum));
    }

    float TanhSquashSum(float sum)
    {
        return tanh(this.sigmoid_alpha*sum);
    }

    float ReLUSum(float sum)
    {
        return max(0, this.sigmoid_alpha * sum);
    }

    float LeakyReLUSum(float sum)
    {
        if(sum < 0)
        {
            return this.sigmoid_alpha * sum;
        }
        else
        {
            return this.sigmoid_alpha * sum;
        }
            
    }
};

// keep this up to date with Brain.Synapse
struct Synapse
{
    float weight;
    float learning_rate_r; 
    float coefficient_A;
    float coefficient_B;
    float coefficient_C;
    float coefficient_D;
    int from_neuron_idx;
    int to_neuron_idx;
    int enabled;
};

bool IsNaN(float x)
{
  return !(x < 0.0f || x > 0.0f || x == 0.0f);
}

// keep this up to date with Neuron.NeuronType
#define NEURON_TYPE_PERCEPTRON 0
#define NEURON_TYPE_SPIKING 1 // 8 in a neighborhood minus 1

// keep this up to date with Neuron.NeuronClass
#define NEURON_CLASS_HIDDEN 0
#define NEURON_CLASS_SENSOR 1 
#define NEURON_CLASS_MOTOR 2

// keep this up to date with Neuron.NeuronActivaitonFunction
#define NEURON_ACTIVATIONFUNCTION_SIGMOID 0
#define NEURON_ACTIVATIONFUNCTION_TANH 1
#define NEURON_ACTIVATIONFUNCTION_LEAKYRELU 2
#define NEURON_ACTIVATIONFUNCTION_RELU 3