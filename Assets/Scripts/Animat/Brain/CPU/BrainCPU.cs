using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class BrainCPU : Brain
{
    public NativeArray<Neuron> current_state_neurons; // 1-to-1 mapping NeuronID --> neuron
    public NativeArray<Synapse> current_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.

    public NativeArray<Neuron> next_state_neurons; // 1-to-1 mapping NeuronID --> neuron
    public NativeArray<Synapse> next_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.



    public BrainCPU(NativeArray<Neuron> neurons, NativeArray<Synapse> synapses)
    { 
        // prune disabled synapses, to save system resources 
        int[] neuron_idx_to_incoming_synapse_count = new int[neurons.Length];
        List<Synapse> synapses_pruned_list = new();
        for (int i = 0; i < synapses.Length; i++)
        {
            Synapse synapse = synapses[i];
            synapses_pruned_list.Add(synapse);
            neuron_idx_to_incoming_synapse_count[synapse.to_neuron_idx]++;
        }

        synapses.Dispose();
        NativeArray<Synapse> synapses_pruned = new NativeArray<Synapse>(synapses_pruned_list.ToArray(), Allocator.Persistent);

       // update the neuron info to account for the pruned synapses
        int synapse_start_idx = 0;
        for (int i = 0; i < neurons.Length; i++)
        {
            Neuron neuron = neurons[i];
            int synapse_count = neuron_idx_to_incoming_synapse_count[i];
            neuron.synapse_count = synapse_count;
            neuron.synapse_start_idx = synapse_start_idx;
            synapse_start_idx += synapse_count;
            neurons[i] = neuron;
        }
        synapses = synapses_pruned;

        this.current_state_neurons = neurons;
        this.current_state_synapses = synapses;
        this.next_state_neurons = new(neurons, Allocator.Persistent);
        this.next_state_synapses = new(synapses, Allocator.Persistent);
    }




    public void SwapCurrentAndNextStates()
    {
        //move next state to the current state, to get the motor activations
        NativeArray<Neuron> swap_array = this.current_state_neurons;
        this.current_state_neurons = this.next_state_neurons;
        this.next_state_neurons = swap_array;

        NativeArray<Synapse> swap_synapses = this.current_state_synapses;
        this.current_state_synapses = this.next_state_synapses;
        this.next_state_synapses = swap_synapses;
    }

    public JobHandle update_job_handle;
    public override void ScheduleWorkingCycle()
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

    public override void SaveToDisk()
    {
        string[] existing_saves = Directory.GetFiles(path: GlobalConfig.save_file_path, searchPattern: GlobalConfig.save_file_base_name + "*" + save_file_extension);
        int num_files = existing_saves.Length;
        string full_path = GlobalConfig.save_file_path + GlobalConfig.save_file_base_name + num_files.ToString() + save_file_extension;
        Debug.Log("Saving brain to disk: " + full_path);
        StreamWriter data_file;
        data_file = new(path: full_path, append: false);


        BinaryFormatter formatter = new BinaryFormatter();
        object[] objects_to_save = new object[] { this.current_state_neurons.ToArray(), this.current_state_synapses.ToArray() };
        formatter.Serialize(data_file.BaseStream, objects_to_save);
        data_file.Close();
    }

    public static (NativeArray<Neuron>, NativeArray<Synapse>) LoadFromDisk(string filename="")
    {
     
        if (filename == "")
        {
            string[] existing_saves = Directory.GetFiles(path: GlobalConfig.save_file_path, searchPattern: GlobalConfig.save_file_base_name + "*" + save_file_extension);
            int num_files = existing_saves.Length-1;
            filename = GlobalConfig.save_file_base_name + num_files.ToString();
        }


        Neuron[] neuron_array = null;
        Synapse[] synapse_array = null;

        BinaryFormatter formatter = new BinaryFormatter();
        string full_path = GlobalConfig.save_file_path + filename + save_file_extension;
        // loading
        using (FileStream fs = File.Open(full_path, FileMode.Open))
        {
            object obj = formatter.Deserialize(fs);
            var newlist = (object[])obj;
            for(int i=0; i < newlist.Length; i++) 
            {
                if (i == 0)
                {
                    neuron_array = (Neuron[])newlist[i];
                }
                else if(i == 1)
                {
                    synapse_array = (Synapse[])newlist[i];
                }
                else
                {
                    Debug.LogWarning("ERROR LOADING BRAIN");
                }
                
            }
        }

        NativeArray<Neuron> native_neuron_array = new(neuron_array.Length, Allocator.Persistent);
        NativeArray<Synapse> native_synapse_array = new(synapse_array.Length, Allocator.Persistent);

        for(int i = 0; i < neuron_array.Length; i++)
        {
            native_neuron_array[i] = neuron_array[i];
        }

        for (int i = 0; i < synapse_array.Length; i++)
        {
            native_synapse_array[i] = synapse_array[i];
        }

        return (native_neuron_array, native_synapse_array);

    }

    public override Neuron GetNeuronCurrentState(int index)
    {
        return this.current_state_neurons[index];
    }

    public override void SetNeuronCurrentState(int index, Neuron neuron)
    {
        this.current_state_neurons[index] = neuron;
    }
}
