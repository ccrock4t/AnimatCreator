using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class BrainCPU : Brain
{
    public NativeArray<Neuron> current_state_neurons; // 1-to-1 mapping NeuronID --> neuron
    public NativeArray<Synapse> current_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.

    public NativeArray<Neuron> next_state_neurons; // 1-to-1 mapping NeuronID --> neuron
    public NativeArray<Synapse> next_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.


    public BrainCPU(BrainGenome genome) : base(genome)
    {

    }

    public override void DevelopFromGenome() 
    {
        this.Develop(this.genome);
    }

    public void ScheduleBrainUpdateJob()
    {
        ParallelNeuralUpdateCPU job = new()
        {
            current_state_neurons = this.current_state_neurons,
            current_state_synapses = this.current_state_synapses,
            next_state_neurons = this.next_state_neurons,
            next_state_synapses = this.next_state_synapses
        };
        update_job_handle = job.Schedule(this.next_state_neurons.Length, 128);
    }

    public JobHandle update_job_handle;
    public override void DoWorkingCycle()
    {
        ScheduleBrainUpdateJob();


        //move next state to the current state
        NativeArray<Neuron> swap_array = this.current_state_neurons;
        this.current_state_neurons = this.next_state_neurons;
        this.next_state_neurons = swap_array;

        NativeArray<Synapse> swap_synapses = this.current_state_synapses;
        this.current_state_synapses = this.next_state_synapses;
        this.next_state_synapses = swap_synapses;


    }

    

    // Update is called once per frame
    void Update()
    {
        
    }

    public override void DisposeOfNativeCollections()
    {
        this.update_job_handle.Complete();
        this.current_state_neurons.Dispose();
        this.current_state_synapses.Dispose();
        this.next_state_neurons.Dispose();
        this.next_state_synapses.Dispose();
    }

    public override int GetNumberOfSynapses()
    {
        return this.current_state_synapses.Length;
    }

    public override int GetNumberOfNeurons()
    {
        return this.current_state_neurons.Length;
    }
}
