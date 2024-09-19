using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

public class BrainGPU : Brain
{


    public static ComputeShader compute_shader_static;
    public ComputeShader compute_shader;
    public int main_kernel;

    public ComputeBuffer current_state_neurons; // 1-to-1 mapping NeuronID --> neuron
    public ComputeBuffer current_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.

    public ComputeBuffer next_state_neurons; // 1-to-1 mapping NeuronID --> neuron
    public ComputeBuffer next_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.

    public Neuron[] current_neurons;
    public Synapse[] current_synapses;

    public static bool RETRIEVE_SYNAPSE_DATA;

    public BrainGPU(NativeArray<Neuron> neurons, NativeArray<Synapse> synapses) : base()
    {
        this.current_state_neurons = new(neurons.Length, Marshal.SizeOf(typeof(Neuron)));
        this.current_state_synapses = new(synapses.Length == 0 ? 1 : synapses.Length, Marshal.SizeOf(typeof(Synapse)));
        this.current_state_neurons.SetData(neurons);
        this.current_state_synapses.SetData(synapses);
        this.InitializeComputeBuffers();

        RETRIEVE_SYNAPSE_DATA = false;

        if (compute_shader_static == null)
        {
            compute_shader_static = (ComputeShader)Resources.Load("ParallelNeuralUpdateGPU");
        }

        compute_shader = (ComputeShader)GameObject.Instantiate(compute_shader_static);

        this.main_kernel = compute_shader.FindKernel("CSMain");

        neurons.Dispose();
        synapses.Dispose();

    }


    public override Neuron GetNeuronCurrentState(int index)
    {
        return this.current_neurons[index];
    }


    public override void SetNeuronCurrentState(int index, Neuron neuron)
    {
        if (this.current_neurons == null) return;
        this.current_neurons[index] = neuron;
    }

    public Synapse GetCurrentSynapse(int index)
    {
        return this.current_synapses[index];
    }

   


    public void SetCurrentSynapse(int index, Synapse synapse)
    {
        if (this.current_synapses == null) return;
        this.current_synapses[index] = synapse;
    }


    public void InitializeComputeBuffers()
    {
        this.next_state_neurons = new(this.current_state_neurons.count, this.current_state_neurons.stride);
        this.next_state_synapses = new(this.current_state_synapses.count, this.current_state_synapses.stride);
        this.current_neurons = new Neuron[this.current_state_neurons.count];
        this.current_synapses = new Synapse[this.current_state_synapses.count];
        this.current_state_neurons.GetData(this.current_neurons);
        this.current_state_synapses.GetData(this.current_synapses);

        SetBuffersOnGPUVariables();
    }

    public void SetBuffersOnGPUVariables()
    {
        compute_shader.SetBuffer(this.main_kernel, "current_state_neurons", this.current_state_neurons);
        compute_shader.SetBuffer(this.main_kernel, "current_state_synapses", this.current_state_synapses);
        compute_shader.SetBuffer(this.main_kernel, "next_state_neurons", this.next_state_neurons);
        compute_shader.SetBuffer(this.main_kernel, "next_state_synapses", this.next_state_synapses);
    }

    public override void ScheduleWorkingCycle()
    {
        //todo set the sensory neurons only
        // then, dispatch the proper number of thread groups

        int remaining_neurons = this.current_state_neurons.count;

        int i = 0;
        int max_neurons_processed_per_dispatch = GlobalConfig.MAX_NUM_OF_THREAD_GROUPS * GlobalConfig.NUM_OF_GPU_THREADS;
        while (remaining_neurons > 0)
        {

            compute_shader.SetInt("index_offset", i * max_neurons_processed_per_dispatch);
            if (remaining_neurons <= max_neurons_processed_per_dispatch)
            {
                compute_shader.Dispatch(this.main_kernel, Mathf.CeilToInt(remaining_neurons / GlobalConfig.NUM_OF_GPU_THREADS), 1, 1);
                remaining_neurons = 0;
                break;
            }
            else
            {
                compute_shader.Dispatch(this.main_kernel, GlobalConfig.MAX_NUM_OF_THREAD_GROUPS, 1, 1);
                remaining_neurons -= max_neurons_processed_per_dispatch;
            }
            i++;
        }


        //move next state to the current state
        ComputeBuffer swap_array = this.current_state_neurons;
        this.current_state_neurons = this.next_state_neurons;
        this.next_state_neurons = swap_array;

        ComputeBuffer swap_synapses = this.current_state_synapses;
        this.current_state_synapses = this.next_state_synapses;
        this.next_state_synapses = swap_synapses;

        SetBuffersOnGPUVariables();
        TransferFromGPUToCPU();
    }

    public void TransferFromGPUToCPU()
    {
        if (this.current_state_neurons == null) return;
        this.current_state_neurons.GetData(this.current_neurons,0,0, this.current_neurons.Length);
        if(RETRIEVE_SYNAPSE_DATA) this.current_state_synapses.GetData(this.current_synapses, 0, 0, this.current_synapses.Length);
    }

    public override void DisposeOfNativeCollections()
    {
        current_state_neurons.Dispose();
        current_state_synapses.Dispose();
        next_state_neurons.Dispose();
        next_state_synapses.Dispose();
    }

    public override int GetNumberOfSynapses()
    {
        if (this.current_state_synapses == null) return 0;
        return this.current_state_synapses.count;
    }

    public override int GetNumberOfNeurons()
    {
        if (this.current_state_neurons == null) return 0;
        return this.current_state_neurons.count;
    }

    public override void SaveToDisk()
    {
        throw new System.NotImplementedException();
    }

}
