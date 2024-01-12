using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using static Brain;
using static GraphVisualization3D;

public abstract class BrainGenome
{
    /// <summary>
    /// SAVE AND LOAD BRAIN GENOMES
    /// </summary>


    public const int INVALID_NEAT_ID = -9999;
    public const int OUTPUT_TEMP_LAYER_ID = -1;
    public const int SENSORY_LAYER_ID = 0;

    public float brain_update_period;

    // mutate the genome
    public abstract void Mutate();
    public abstract (BrainGenome, BrainGenome) Reproduce(BrainGenome genome_parent2);

    public abstract JobHandle ScheduleDevelopCPUJob();
    public abstract void ScheduleDevelopGPUJob();
    public abstract (NativeArray<Neuron>, NativeArray<Synapse>) DevelopCPU(Dictionary<string, Dictionary<string, int>> neuron_indices);
    public abstract (ComputeBuffer, ComputeBuffer) DevelopGPU(Dictionary<string, Dictionary<string, int>> neuron_indices);
    public abstract BrainGenome Clone();

    public abstract void SaveToDisk();



    public class DevelopmentSynapse
    {
        public float learning_rate;
        public float[] coefficients;

        public DevelopmentSynapse(float[] coefficients = null,
            float learning_rate = 1.0f)
        {
            if (coefficients == null)
            {
                this.coefficients = new float[]
                {
                    0,
                    0,
                    0,
                    0
                };
            }
            else
            {
                this.coefficients = coefficients;
            }

            this.learning_rate = learning_rate;
        }


    }


    public class DevelopmentNeuron : DataElement
    {

        public int threshold;
        public float bias;
        public bool sign; // sign of outputs
        public float adaptation_delta;
        public float decay;
        public float sigmoid_alpha; // larger alpha = steeper slope, easier to activate --- smaller alpha = gradual slope, harder to activate.

        public string extradata = "";

        public DevelopmentNeuron(int threshold,
            float bias,
            bool sign,
            float adaptation_delta,
            float decay,
            float sigmoid_alpha)
        {
            this.threshold = threshold;
            this.bias = bias;
            this.adaptation_delta = adaptation_delta;
            this.decay = decay;
            this.sign = sign;
            this.sigmoid_alpha = sigmoid_alpha;
        }

        public virtual DevelopmentNeuron Clone()
        {
            DevelopmentNeuron clone = new(this.threshold,
                this.bias,
                this.sign,
                this.adaptation_delta,
                this.decay,
                this.sigmoid_alpha);

            clone.extradata = extradata;
            return clone;
        }


    }
}
