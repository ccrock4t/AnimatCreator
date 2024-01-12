using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Brain;
using static GlobalConfig;
using Random = UnityEngine.Random;

public class NEATBrainGenome : BrainGenome
{
    public List<DevelopmentNeuron> neuron_genome;
    public List<NEATDevelopmentSynapse> connection_genome;


    public int AVG_NODE_MUTATIONS_PER_MUTATE = 10; // between 1 and infinity, how many nodes to mutate on average
    public float AVG_CONNECTION_MUTATIONS_PER_MUTATE = 0.9f; // between 1 and infinity, how many nodes to mutate on average
    public float ADD_CONNECTION_MUTATION_RATE = 0.0f; // between 1 and infinity, how many nodes to mutate on average
    public float ADD_NODE_MUTATION_RATE = 0.0f; // between 1 and infinity, how many nodes to mutate on average
    public float CHANCE_TO_MUTATE_CONNECTION = 1.0f; // between 1 and infinity, how many nodes to mutate on average
    public float CHANCE_TO_MUTATE_NODE = 0.5f; // between 1 and infinity, how many nodes to mutate on average
    public float NODE_MUTATION_RATE_MUTATION_RATE = 0.1f;
    public float HEBB_INCREMENT = 0.05f;

    int num_of_joints;

    public NEATBrainGenome()
    {
        this.num_of_joints = GlobalConfig.creature_to_use == Creature.Hexapod ? 21 : 14; // hexapod or quadruped
        this.neuron_genome = new();
        this.connection_genome = new();
    }

    public static NEATBrainGenome CreateTestGenome()
    {
        NEATBrainGenome genome = new();
        genome.InsertHexapodSensorimotorNeurons();
        return genome;
    }

    public void InsertNeuron(string extradata = "", int id = -1)
    {
        if (id == -1) id = GetNextGlobalNeuronID();
        NEATDevelopmentNeuron neuron = new(ID: id);
        neuron.extradata = extradata;
        this.neuron_genome.Add(neuron);
    }

    public void InsertConnection(int from, int to, int id = -1)
    {
        if (id == -1) id = GetNextGlobalSynapseID();
        NEATDevelopmentSynapse connection = new(from, to, ID: id);
        this.connection_genome.Add(connection);
    }

    public void InsertHexapodSensorimotorNeurons()
    {
        // insert sensory neurons
        for (int i = 0; i < this.num_of_joints; i++) 
        {
            string joint_key = Animat.GetSensorimotorJointKey(i);


            // 10 for the sensor
            this.InsertNeuron(extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_LLL", id: this.neuron_genome.Count);
            this.InsertNeuron(extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_LLR", id: this.neuron_genome.Count);
            this.InsertNeuron(extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_LR", id: this.neuron_genome.Count);
            this.InsertNeuron(extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_RL", id: this.neuron_genome.Count);
            this.InsertNeuron(extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_RRL", id: this.neuron_genome.Count);
            this.InsertNeuron(extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_RRR", id: this.neuron_genome.Count);
            this.InsertNeuron(extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_LL", id: this.neuron_genome.Count);
            this.InsertNeuron(extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_LR", id: this.neuron_genome.Count);
            this.InsertNeuron(extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_RL", id: this.neuron_genome.Count);
            this.InsertNeuron(extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_RR", id: this.neuron_genome.Count);




        }

        int motor_neuron_start_idx = this.neuron_genome.Count;

        // insert motor neurons
        for (int i = 0; i < this.num_of_joints; i++) 
        {
            string joint_key = Animat.GetSensorimotorJointKey(i);

            // 3 for the motor
            this.InsertNeuron(extradata: "MOTORLAYER_" + joint_key + "_LL", id: this.neuron_genome.Count);
            this.InsertNeuron(extradata: "MOTORLAYER_" + joint_key + "_LR", id: this.neuron_genome.Count);
            this.InsertNeuron(extradata: "MOTORLAYER_" + joint_key + "_R", id: this.neuron_genome.Count);

        }

        int hidden_neuron_start_idx = this.neuron_genome.Count;

        // insert hidden neurons
        for (int i = 0; i <= 128; i++) 
        {
            this.InsertNeuron(id: this.neuron_genome.Count);
        }



/*        for (int i = 0; i <= 12000; i++)
        {
            AddRandomConnection();
        }*/

        // insert full connections


        for (int i = 0; i < motor_neuron_start_idx; i++)  // from sensors
        {
            for (int k = hidden_neuron_start_idx; k < this.neuron_genome.Count; k++) // to hidden
            {
                InsertConnection(i, k, this.connection_genome.Count);
            }
        }

        for (int i = hidden_neuron_start_idx; i < this.neuron_genome.Count; i++)  // from hidden
        {
            for (int k = motor_neuron_start_idx; k < hidden_neuron_start_idx; k++) // to motor
            {
                InsertConnection(i, k, this.connection_genome.Count);
            }
        }



        if(NEXT_NEURON_ID == BrainGenome.INVALID_NEAT_ID) NEXT_NEURON_ID = this.neuron_genome.Count;
        if (NEXT_SYNAPSE_ID == BrainGenome.INVALID_NEAT_ID) NEXT_SYNAPSE_ID = this.connection_genome.Count;

    }

    public override BrainGenome Clone()
    {
        NEATBrainGenome clone = new NEATBrainGenome();
        foreach(NEATDevelopmentNeuron neuron in this.neuron_genome)
        {
            NEATDevelopmentNeuron neuron_clone = (NEATDevelopmentNeuron)neuron.Clone();
            clone.neuron_genome.Add(neuron_clone);
        }


        foreach (NEATDevelopmentSynapse connection in this.connection_genome)
        {
            NEATDevelopmentSynapse connection_clone = connection.Clone();
            clone.connection_genome.Add(connection_clone);
        }

        return clone;
    }

    public override (NativeArray<Brain.Neuron>, NativeArray<Brain.Synapse>) DevelopCPU(Dictionary<string, Dictionary<string, int>> neuron_indices)
    {


        List<DevelopmentNeuron> developed_brain = this.neuron_genome;

        List<Neuron> final_brain_neurons = new();
        List<Synapse> final_brain_synapses = new();

        Dictionary<int, List<Synapse>> neuron_idx_to_synapse = new();
        Dictionary<int, int> neuronID_to_idx = new ();



        string[] strings;
        string neuron_type;
        string sensor_type;

        int[] position_array = new[] { 0 , 0, 0 };

        // first turn all the cells into neurons
        for (int i = 0; i < developed_brain.Count; i++)
        {
            NEATDevelopmentNeuron cell = (NEATDevelopmentNeuron)developed_brain[i];

            Neuron neuron = new Neuron(threshold: cell.threshold,
                bias: cell.bias,
                adaptation_delta: cell.adaptation_delta,
                decay_rate_tau: cell.decay,
                sign: cell.sign,
                sigmoid_alpha: cell.sigmoid_alpha);

            neuronID_to_idx[cell.ID] = i;

            if (cell.extradata != "")
            {
   
                strings = cell.extradata.Split("_");
                neuron_type = strings[strings.Length - 1];
                sensor_type = strings[strings.Length - 2];

                if (sensor_type != "TOUCHSENSE" && sensor_type != "ROTATESENSE")
                {
                    neuron.position = new int3(position_array[2], Random.Range(0, 20), 10);
                    position_array[2] += 3;
                    // no sensor type
                    // this is a motor (output) neuron, so turn it into a perceptron
                    neuron.type = Neuron.NeuronType.Perceptron;
                    // connect to motor interface

                    int neuron_idx = -1;
                    if (neuron_type == "LL")
                    {
                        neuron_idx = 0;
                    }
                    else if (neuron_type == "LR")
                    {
                        neuron_idx = 1;
                    }
                    else if (neuron_type == "R")
                    {
                        neuron_idx = 2;
                    }
                    else
                    {
                        Debug.LogError("ERROR " + neuron_type);
                    }
                    neuron_indices[Brain.MOTOR_NEURON_KEY][cell.extradata[0..^neuron_type.Length] + neuron_idx] = i;
                    neuron.neuron_class = Neuron.NeuronClass.Motor;
                }
                else
                {
                    neuron.position = new int3(position_array[1]++, Random.Range(0,20), 0);
                    // this is a sensory (input) neuron, so turn it into a perceptron
                    neuron.type = Neuron.NeuronType.Perceptron;

                    // connect to sensory interface
                    int neuron_idx = -1;

                    if (sensor_type == "TOUCHSENSE")
                    {
                        if (neuron_type == "LLL")
                        {
                            neuron_idx = 0; // TOP
                        }
                        else if (neuron_type == "LLR")
                        {
                            neuron_idx = 1; // BOT
                        }
                        else if (neuron_type == "LR")
                        {
                            neuron_idx = 2; // LEFT
                        }
                        else if (neuron_type == "RL")
                        {
                            neuron_idx = 3; // RIGHT
                        }
                        else if (neuron_type == "RRL")
                        {
                            neuron_idx = 4; // FRONT
                        }
                        else if (neuron_type == "RRR")
                        {
                            neuron_idx = 5; // BACK
                        }
                        else
                        {
                            Debug.LogError("ERROR " + neuron_type);
                        }
                    }
                    else if (sensor_type == "ROTATESENSE")
                    {
                        if (neuron_type == "LL")
                        {
                            neuron_idx = 6; // W
                        }
                        else if (neuron_type == "LR")
                        {
                            neuron_idx = 7; // X
                        }
                        else if (neuron_type == "RL")
                        {
                            neuron_idx = 8; // Y
                        }
                        else if (neuron_type == "RR")
                        {
                            neuron_idx = 9; // Z
                        }
                        else
                        {
                            Debug.LogError("ERROR " + neuron_type);
                        }
                    }
                    else if (sensor_type == "")
                    {
                        Debug.LogError("ERROR: No Sensor type");
                    }
                    else
                    {
                        Debug.LogError("ERROR " + neuron_type + " for sensor type " + sensor_type);
                    }

                    string key = "";
                    for (int k = 0; k < strings.Length - 2; k++)
                    {
                        key += strings[k] + "_";
                    }
                    key += neuron_idx;

                    neuron_indices[Brain.SENSORY_NEURON_KEY][key] = i;

                    neuron.neuron_class = Neuron.NeuronClass.Sensor;
                }

            }
            else
            {
                neuron.position = new int3(position_array[0]++, Random.Range(0, 20), 5);
            }


            final_brain_neurons.Add(neuron);


        }

        // and all connections
        foreach (NEATDevelopmentSynapse c in this.connection_genome)
        {
            int from_neuron_idx = neuronID_to_idx[c.from_ID];
            int to_neuron_idx = neuronID_to_idx[c.to_ID];

            List<Synapse> synapses;
            if (neuron_idx_to_synapse.ContainsKey(to_neuron_idx))
            {
                synapses = neuron_idx_to_synapse[to_neuron_idx];
            }
            else
            {
                synapses = new();
                neuron_idx_to_synapse[to_neuron_idx] = synapses;
            }
            

            Synapse connection = new(learning_rate: c.learning_rate,
                        from_neuron_idx: from_neuron_idx,
                        to_neuron_idx: to_neuron_idx,
                        coefficients: c.coefficients);
            synapses.Add(connection);
        }

        
        int synapse_idx = 0;
        for (int i = 0; i < developed_brain.Count; i++)
        {
            Neuron neuron = final_brain_neurons[i];
            List<Synapse> synapses;
            if (neuron_idx_to_synapse.ContainsKey(i))
            {
                synapses = neuron_idx_to_synapse[i];
            }
            else
            {
                synapses = new();
            }
            neuron.synapse_count = synapses.Count;

            neuron.synapse_start_idx = synapse_idx;
            final_brain_neurons[i] = neuron;

            synapse_idx += synapses.Count;

            final_brain_synapses.AddRange(synapses);
        }

        return (final_brain_neurons.ToNativeArray<Neuron>(Allocator.Persistent), final_brain_synapses.ToNativeArray<Synapse>(Allocator.Persistent));
    }


    public override void Mutate()
    {
        bool should_mutate;
        float rnd;
        // first, mutate synapse parameters
        foreach (NEATDevelopmentSynapse connection in this.connection_genome)
        {
            should_mutate = Random.Range(0f, 1f) < CHANCE_TO_MUTATE_CONNECTION;
            if (!should_mutate) continue;
            rnd = Random.Range(0f, 1f);

            int parameter = UnityEngine.Random.Range(0, 5);

            if (rnd < 0.97)
            {
                

                if(parameter <= 3) {
                    connection.coefficients[parameter] += UnityEngine.Random.Range(0, 2) == 0 ? HEBB_INCREMENT : -HEBB_INCREMENT;
                }
                else
                {
                    connection.learning_rate += UnityEngine.Random.Range(0, 2) == 0 ? HEBB_INCREMENT : -HEBB_INCREMENT;
                }

                /*   for (int i = 0; i < connection.coefficients.Length; i++)
                 {




                                   // bound in [-1,1]
                                     connection.coefficients[i] = Mathf.Max(connection.coefficients[i], -1f);
                                     connection.coefficients[i] = Mathf.Min(connection.coefficients[i], 1f);
                    } */


            }
            else if (rnd < 0.98)
            {
                if (parameter <= 3)
                {
                    connection.coefficients[parameter] *= UnityEngine.Random.Range(0, 2) == 0 ? 0.5f : 2.0f;
                }
                else
                {
                    connection.learning_rate *= UnityEngine.Random.Range(0, 2) == 0 ? 0.5f : 2.0f;
                }
            }
            else if (rnd < 0.99)
            {
                if (parameter <= 3)
                {
                    connection.coefficients[parameter] *= -1;
                }
                else
                {
                    connection.learning_rate *= -1;
                }
            }
            else
            {
                connection.enabled = false;
            }
           
        }

        // then, mutate neuron parameters
        foreach (NEATDevelopmentNeuron neuron in this.neuron_genome)
        {
            should_mutate = Random.Range(0f, 1f) < CHANCE_TO_MUTATE_NODE;
            if (!should_mutate) continue;
            rnd = Random.Range(0f, 1f);
            int parameter = UnityEngine.Random.Range(0, 2);
            if (rnd < 0.99)
            {
                if(parameter == 0)
                {
                    neuron.sigmoid_alpha += UnityEngine.Random.Range(0, 2) == 0 ? HEBB_INCREMENT : -HEBB_INCREMENT;
                }
                else if(parameter == 1)
                {
                    neuron.bias += UnityEngine.Random.Range(0, 2) == 0 ? HEBB_INCREMENT : -HEBB_INCREMENT;
                }
                
                
            }else //if(rnd < 0.98)
            {
                
                if (parameter == 0)
                {
                    neuron.sigmoid_alpha *= UnityEngine.Random.Range(0, 2) == 0 ? 0.5f : 2.0f;
                }
                else if (parameter == 1)
                {
                    neuron.bias *= UnityEngine.Random.Range(0, 2) == 0 ? 0.5f : 2.0f;
                }
            }
/*            else
            {
                //neuron.sign = UnityEngine.Random.Range(0, 2) == 0;
                if (parameter == 0)
                {
                    neuron.sigmoid_alpha *= -1;
                }
                else if (parameter == 1)
                {
                    neuron.bias *= -1;
                }
            }*/
            
            
        }


       // ADD CONNECTION?
        should_mutate = Random.Range(0f, 1f) < ADD_CONNECTION_MUTATION_RATE;
        if (should_mutate)
        {
            AddRandomConnection();
        }

        // ADD NODE?
        should_mutate = Random.Range(0f, 1f) < ADD_NODE_MUTATION_RATE;
        if (should_mutate && this.connection_genome.Count > 0)
        {
            NEATDevelopmentSynapse random_connection = this.connection_genome[Random.Range(0, this.connection_genome.Count)];
            random_connection.enabled = false;
            NEATDevelopmentNeuron new_neuron = new(ID: GetNextGlobalNeuronID());
            NEATDevelopmentSynapse new_connectionA = new(from_neuron: random_connection.from_ID, to_neuron: new_neuron.ID, ID: GetNextGlobalSynapseID());
            NEATDevelopmentSynapse new_connectionB = random_connection.Clone();
            new_connectionB.from_ID = new_neuron.ID;
            new_connectionB.to_ID = random_connection.to_ID;
            new_connectionB.ID = GetNextGlobalSynapseID();
            this.connection_genome.Add(new_connectionA);
            this.connection_genome.Add(new_connectionB);
            this.neuron_genome.Add(new_neuron);
        }
    }

    public void AddRandomConnection()
    {
        NEATDevelopmentNeuron from_neuron = (NEATDevelopmentNeuron)this.neuron_genome[Random.Range(0, this.neuron_genome.Count)];
        NEATDevelopmentNeuron to_neuron = (NEATDevelopmentNeuron)this.neuron_genome[Random.Range(0, this.neuron_genome.Count)];
        NEATDevelopmentSynapse new_connection = new(from_neuron: from_neuron.ID, to_neuron: to_neuron.ID, ID: GetNextGlobalSynapseID());
        this.connection_genome.Add(new_connection);
    }

    public override (BrainGenome, BrainGenome) Reproduce(BrainGenome genome_parent2)
    {
        NEATBrainGenome parent1 = this;
        NEATBrainGenome parent2 = (NEATBrainGenome)genome_parent2;
        NEATBrainGenome offspring1 = new();
        NEATBrainGenome offspring2 = new();



        int i = 0, j = 0;
        while (i < parent1.neuron_genome.Count || j < parent2.neuron_genome.Count)
        {
            NEATDevelopmentNeuron neuron1 = null;
            if (i < parent1.neuron_genome.Count)
            {
                neuron1 = (NEATDevelopmentNeuron)parent1.neuron_genome[i];
            }

            NEATDevelopmentNeuron neuron2 = null;
            if (j < parent2.neuron_genome.Count)
            {
                neuron2 = (NEATDevelopmentNeuron)parent2.neuron_genome[j];
            }

            if (neuron1 != null && neuron2 != null)
            {
                if(neuron1.ID < neuron2.ID)
                {
                    neuron2 = null;
                }else if (neuron1.ID > neuron2.ID)
                {
                    neuron1 = null;
                }
            }

            if (neuron1 != null && neuron2 != null)
            {
                int rnd = Random.Range(0, 2);
                NEATDevelopmentNeuron neuron_merged = (NEATDevelopmentNeuron)neuron1.Clone();
                neuron_merged.bias = (neuron1.bias + neuron2.bias)/ 2;
                neuron_merged.sigmoid_alpha = (neuron1.sigmoid_alpha + neuron2.sigmoid_alpha) / 2;
                offspring1.neuron_genome.Add(neuron_merged);
                offspring2.neuron_genome.Add(neuron_merged.Clone());
                i++;
                j++;
            }
            else if (neuron1 != null && neuron2 == null)
            {
                offspring1.neuron_genome.Add(neuron1.Clone());
                offspring2.neuron_genome.Add(neuron1.Clone());
                i++;
            }
            else if (neuron1 == null && neuron2 != null)
            {
                offspring1.neuron_genome.Add(neuron2.Clone());
                offspring2.neuron_genome.Add(neuron2.Clone());
                j++;
            }


      
        }

        i = 0;
        j = 0;
        while (i < parent1.connection_genome.Count || j < parent2.connection_genome.Count)
        {
            NEATDevelopmentSynapse connection1 = null;
            if (i < parent1.connection_genome.Count)
            {
                connection1 = (NEATDevelopmentSynapse)parent1.connection_genome[i];
            }

            NEATDevelopmentSynapse connection2 = null;
            if (j < parent2.connection_genome.Count)
            {
                connection2 = (NEATDevelopmentSynapse)parent2.connection_genome[j];
            }

            if (connection1 != null && connection2 != null)
            {
                if (connection1.ID < connection2.ID)
                {
                    connection2 = null;
                }
                else if (connection1.ID > connection2.ID)
                {
                    connection1 = null;
                }
            }



            if (connection1 != null && connection2 != null)
            {
                int rnd = Random.Range(0, 2);
                NEATDevelopmentSynapse[] connections = new NEATDevelopmentSynapse[] { connection1, connection2 };
           
                
                /*        connection_merged.coefficients[0] = (connection1.coefficients[0] + connection2.coefficients[0]) / 2f;
                        connection_merged.coefficients[1] = (connection1.coefficients[1] + connection2.coefficients[1]) / 2f;
                        connection_merged.coefficients[2] = (connection1.coefficients[2] + connection2.coefficients[2]) / 2f;
                        connection_merged.coefficients[3] = (connection1.coefficients[3] + connection2.coefficients[3]) / 2f;*/
                //connection_merged.learning_rate = (connection1.learning_rate + connection2.learning_rate) / 2f;
                offspring1.connection_genome.Add(connections[rnd].Clone());
                offspring2.connection_genome.Add(connections[1-rnd].Clone());
                i++;
                j++;
            }
            else if (connection1 != null && connection2 == null)
            {
                offspring1.connection_genome.Add(connection1.Clone());
                offspring2.connection_genome.Add(connection1.Clone());
                i++;
            }
            else if (connection1 == null && connection2 != null)
            {
                offspring1.connection_genome.Add(connection2.Clone());
                offspring2.connection_genome.Add(connection2.Clone());
                j++;
            }

            if ((connection1 != null && !connection1.enabled) || (connection2 != null && !connection2.enabled))
            {
                offspring1.connection_genome[^1].enabled = Random.Range(0f,1.0f) < 0.75f ? false : true;
                offspring2.connection_genome[^1].enabled = Random.Range(0f,1.0f) < 0.75f ? false : true;
            }


        }

        return (offspring1, offspring2);
    }

    public override void SaveToDisk()
    {
        throw new System.NotImplementedException();
    }

    public static BrainGenome LoadFromDisk(string filename = "")
    {
        throw new System.NotImplementedException();
    }

    public class NEATDevelopmentSynapse : DevelopmentSynapse
    {

        public int ID;
        public int from_ID;
        public int to_ID;
        public bool enabled;

        public NEATDevelopmentSynapse(int from_neuron,
            int to_neuron,
            int ID,
            float[] coefficients = null, 
            float learning_rate = 0,
            bool enabled = true) : base(coefficients, learning_rate)
        {

            this.ID = ID;
            
            this.from_ID = from_neuron;
            this.to_ID = to_neuron;
            this.enabled = enabled;

            if(coefficients == null)
            {
                for(int i = 0; i < this.coefficients.Length; i++)
                {
                    this.coefficients[0] = Random.Range(-.1F, .1F);
                    this.coefficients[1] = Random.Range(-.1F, .1F);
                    this.coefficients[2] = Random.Range(-.1F, .1F);
                    this.coefficients[3] = Random.Range(-.1F, .1F);
                }
               
            }
        }



        public NEATDevelopmentSynapse Clone()
        {
            NEATDevelopmentSynapse clone = new(this.from_ID,
                this.to_ID,
                ID: this.ID,
                (float[])this.coefficients.Clone(),
                this.learning_rate,
                this.enabled);

            return clone;
        }
    }

    public class NEATDevelopmentNeuron : DevelopmentNeuron
    {

        public int ID;

        public NEATDevelopmentNeuron(int ID, 
            int threshold=1,
            float bias = 0,
            bool sign=true,
            float adaptation_delta=1,
            float decay=1,
            float sigmoid_alpha=1
            ) : base(threshold, bias, sign, adaptation_delta, decay, sigmoid_alpha)
        {
    
           this.ID = ID;
        }



        public override DevelopmentNeuron Clone()
        {
            NEATDevelopmentNeuron clone = new(this.ID,
                this.threshold,
                this.bias,
                this.sign,
                this.adaptation_delta,
                this.decay,
                this.sigmoid_alpha);

            clone.extradata = extradata;
            return clone;
        }


    }
    static int NEXT_SYNAPSE_ID = BrainGenome.INVALID_NEAT_ID;
    static int NEXT_NEURON_ID = BrainGenome.INVALID_NEAT_ID;
    int GetNextGlobalSynapseID()
    {
        int ID = NEXT_SYNAPSE_ID;
        NEXT_SYNAPSE_ID++;
        return ID;
    }

    int GetNextGlobalNeuronID()
    {
        int ID = NEXT_NEURON_ID;
        NEXT_NEURON_ID++;
        return ID;
    }

    public override (ComputeBuffer, ComputeBuffer) DevelopGPU(Dictionary<string, Dictionary<string, int>> neuron_indices)
    {
        throw new System.NotImplementedException();
    }

    public override JobHandle ScheduleDevelopCPUJob()
    {
        return new JobHandle();
    }

    public override void ScheduleDevelopGPUJob()
    {
        throw new System.NotImplementedException();
    }
}
