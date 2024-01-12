using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Brain;
using static GlobalConfig;
using static HyperNEATBrainGenome;
using static UnityEngine.Random;

public abstract class HyperNEATBrainGenome : BrainGenome
{
    const bool INITIALIZE_FULLY_CONNECT_CPPN = true;
    const bool ALLOW_MULTIPLE_MUTATIONS = false;
    const bool STACK_MUTATIONS = false;
    const bool ALLOW_RECURRENT_CONNECTIONS = false;
    const bool INCLUDE_EXTRA_CPPN_FUNCTIONS = false;


    const float INITIAL_HIDDEN_CPPN_NODES = 25;

    public const float ADD_CONNECTION_MUTATION_RATE = 0.12f;
    public const float ADD_NODE_MUTATION_RATE = 0.08f;
    public const float DISABLE_CONNECTION_MUTATION_RATE = 0.2f;
    public const float BRAIN_UPDATE_PERIOD_MUTATION_RATE = 0.00f;
    public const float BRAIN_UPDATE_PERIOD_MUTATION_INCREMENT = 0.005f;
    public const float CHANCE_TO_MUTATE_EACH_CONNECTION = 0.85f;
    public const float CHANGE_NODE_FUNCTION_MUTATION_RATE = 0.1f;
    public const float WEIGHT_MUTATE_INCREMENT = 0.025f;

    // cppn output multipliers
    public const float multiplier = 0.1f;
    public const float ABCD_multiplier = 0.5f;//0.05f;

    bool BFS_for_CPPN_layers = false; // true for BFS, false for DFS

    public int3 substrate_dimensions;
    public int substrate_dimensions_size;

    public int num_of_joints;

    public List<CPPNnode> cppn_nodes;
    public List<CPPNconnection> cppn_connections;
    public List<List<CPPNnode>> layers;
    public CPPNnodeParallel[] CPPN_nodes;
    public CPPNconnectionParallel[] CPPN_connections;

    public DevelopmentNeuron[] substrate;

    CPPNnode bias_input_node;
    CPPNnode x1_input_node;
    CPPNnode y1_input_node;
    CPPNnode z1_input_node;
    CPPNnode x2_input_node;
    CPPNnode y2_input_node;
    CPPNnode z2_input_node;


    CPPNnode initial_weight_output_node;
    CPPNnode LR_output_node;
    CPPNnode A_output_node;
    CPPNnode B_output_node;
    CPPNnode C_output_node;
    CPPNnode D_output_node;
    CPPNnode bias_output_node;
    CPPNnode sigmoid_alpha_output_node;
    CPPNnode activation_function_output_node;
    CPPNnode sign_output_node;
    CPPNnode LEO_connection_enabled_node;

    public static int2 sensorimotor_idxs;




    public enum CPPNFunction
    {
        [Description("LIN")] Linear,
        [Description("SIG")] Sigmoid,
        [Description("GAU")] Gaussian,
        [Description("SIN")] Sine,
        [Description("ABS")] Abs,
        [Description("STP")] Step,
        [Description("RLU")] ReLU,
        [Description("SQ")] Square,
        [Description("TANH")] HyperTangent,


        [Description("COS")] Cosine,


        [Description("SQR")] SquareRoot,

        [Description("TAN")] Tangent,

        [Description("COSH")] HyperCosine,
        [Description("SINH")] HyperSine,
        
        [Description("CUB")] Cube,
        
    }


    public HyperNEATBrainGenome()
    {
        this.num_of_joints = GlobalConfig.creature_to_use == Creature.Hexapod ? 21 : 14; // hexapod or quadruped
        this.brain_update_period = GlobalConfig.ANIMAT_BRAIN_UPDATE_PERIOD;

        this.cppn_nodes = new();
        this.cppn_connections = new();
        this.layers = new();
    }

   

    /// <summary>
    /// 
    /// </summary>
    /// <returns>(_,_,total number of inputs in all neurons)</returns>
    public (CPPNnodeParallel[], CPPNconnectionParallel[]) ConvertCPPNToParallel()
    {
        CPPNnodeParallel[] parallel_nodes = new CPPNnodeParallel[this.cppn_nodes.Count]; // CPPN_node_compute_buffer.BeginWrite<CPPNnodeGPU>(0, CPPN_node_compute_buffer.count);
        Dictionary<CPPNnode, int> node_to_buffer_idx = new();
        int i = 0;
        int total_num_of_inputs = 0;

        for (int j = 0; j < this.layers.Count; j++)
        {
            List<CPPNnode> layer = this.layers[j];
            foreach (CPPNnode node in layer)
            {
                CPPNnodeParallel gpu_node = new();
                gpu_node.input_connection_start_idx = total_num_of_inputs;
                gpu_node.function = (int)node.function;

                int number_of_inputs = 0;
                foreach ((CPPNnode input_node, CPPNconnection input_connection) in node.inputs)
                {
                    if (!input_connection.enabled) continue;
                    if (!ALLOW_RECURRENT_CONNECTIONS && input_node.layer >= j) continue;
                    if (input_node.layer == INVALID_NEAT_ID) continue;
                    number_of_inputs++;
                }

                gpu_node.number_of_input_connections = number_of_inputs;

                parallel_nodes[i] = gpu_node;
                node_to_buffer_idx[node] = i;

                total_num_of_inputs += number_of_inputs;
                i++;
            }

        }
        // CPPN_node_compute_buffer.EndWrite<CPPNnodeGPU>(CPPN_node_compute_buffer.count);

        
        CPPNconnectionParallel[] parallel_connections = new CPPNconnectionParallel[total_num_of_inputs];// CPPN_connection_compute_buffer.BeginWrite<CPPNconnectionGPU>(0, CPPN_connection_compute_buffer.count);
        i = 0;
        for (int j = 0; j < this.layers.Count; j++)
        {
            List<CPPNnode> layer = this.layers[j];
            foreach (CPPNnode node in layer)
            {
                foreach ((CPPNnode input_node, CPPNconnection input_connection) in node.inputs)
                {
                    if (!input_connection.enabled) continue;
                    if (!ALLOW_RECURRENT_CONNECTIONS && input_node.layer >= j) continue;
                    if (input_node.layer == INVALID_NEAT_ID) continue;
                    CPPNconnectionParallel gpu_connection = new();
                    gpu_connection.from_idx = node_to_buffer_idx[input_node];
                    gpu_connection.weight = input_connection.weight;
                    parallel_connections[i] = gpu_connection;
                    i++;
                }
            }
        }


        //CPPN_connection_compute_buffer.EndWrite<CPPNconnectionGPU>(CPPN_connection_compute_buffer.count);

        return (parallel_nodes, parallel_connections);
    }

   



    public void SetCPPNnodesForIO()
    {
        sensorimotor_idxs = new();
        this.bias_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(bias_input_node);
        this.x1_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(x1_input_node);
        this.y1_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(y1_input_node);
        this.z1_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(z1_input_node);
        this.x2_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(x2_input_node);
        this.y2_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(y2_input_node);
        this.z2_input_node = new(ID: this.cppn_nodes.Count, CPPNFunction.Linear);
        this.cppn_nodes.Add(z2_input_node);
        sensorimotor_idxs.x = this.cppn_nodes.Count; // sensor idx end

        CPPNFunction GetInitialOutputFunction()
        {
            return GetRandomCPPNfunction();
            return CPPNFunction.Linear;
        }
        
        this.initial_weight_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 0
        this.cppn_nodes.Add(initial_weight_output_node);
        this.LR_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 1
        this.cppn_nodes.Add(LR_output_node);
        this.A_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 2
        this.cppn_nodes.Add(A_output_node);
        this.B_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 3
        this.cppn_nodes.Add(B_output_node);
        this.C_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 4
        this.cppn_nodes.Add(C_output_node);
        this.D_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 5
        this.cppn_nodes.Add(D_output_node);
        this.bias_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 6
        this.cppn_nodes.Add(bias_output_node);
        this.sigmoid_alpha_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 7
        this.cppn_nodes.Add(sigmoid_alpha_output_node);
        this.activation_function_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 8
        this.cppn_nodes.Add(activation_function_output_node);
        this.sign_output_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 9
        this.cppn_nodes.Add(sign_output_node);
        this.LEO_connection_enabled_node = new(ID: this.cppn_nodes.Count, GetInitialOutputFunction()); // 10
        this.cppn_nodes.Add(LEO_connection_enabled_node);
        sensorimotor_idxs.y = this.cppn_nodes.Count; // motor idx end


        if (INITIALIZE_FULLY_CONNECT_CPPN)
        {
            for (int i = 0; i < sensorimotor_idxs.x; i++)
            {
                for (int j = sensorimotor_idxs.x; j < sensorimotor_idxs.y; j++)
                {
                    int rnd = UnityEngine.Random.Range(0, 2);
                    if (rnd == 0) continue;

                    CPPNconnection new_connection = new(from_ID: this.cppn_nodes[i].ID, to_ID: this.cppn_nodes[j].ID, weight: GetRandomInitialCPPNWeight(), ID: this.cppn_connections.Count);
                    this.cppn_connections.Add(new_connection);
                }
            }
        }

        if (NEXT_NEURON_ID == BrainGenome.INVALID_NEAT_ID) NEXT_NEURON_ID = this.cppn_nodes.Count;
        if (NEXT_SYNAPSE_ID == BrainGenome.INVALID_NEAT_ID) NEXT_SYNAPSE_ID = this.cppn_connections.Count;

        for (int j = 0; j < INITIAL_HIDDEN_CPPN_NODES; j++)
        {
            AddNewRandomNode();
        }
    }

    public void InsertNeuron(int3 coords, string extradata)
    {
        DevelopmentNeuron neuron = GetDefaultDevNeuron();
        neuron.extradata = extradata;
        WriteToSubstrate(coords.x, coords.y, coords.z, neuron);
    }

    public abstract DevelopmentNeuron ReadFromSubstrate(int x, int y, int z);

    public abstract void WriteToSubstrate(int x, int y, int z, DevelopmentNeuron neuron);


    public abstract void InsertHexapodSensorimotorNeurons();

    public override BrainGenome Clone()
    {
        HyperNEATBrainGenome genome;
        if (this is RegularHyperNEATBrainGenome)
        {
            genome = new RegularHyperNEATBrainGenome();

        }
        else if(this is ESHyperNEATBrainGenome)
        {
            genome = new ESHyperNEATBrainGenome();
        }
        else
        {
            Debug.LogError("error");
            return null;
        }
       

        foreach(CPPNnode n in this.cppn_nodes)
        {
            genome.cppn_nodes.Add(n.Clone());
        }
        foreach (CPPNconnection c in this.cppn_connections)
        {
            genome.cppn_connections.Add(c.Clone());
        }
        genome.FinalizeCPPN();

        return genome;


    }



    public static CPPNFunction GetRandomCPPNfunction()
    {
        System.Random sysrnd = new();
        if(INCLUDE_EXTRA_CPPN_FUNCTIONS) return (CPPNFunction)sysrnd.Next(0, Enum.GetNames(typeof(CPPNFunction)).Length);
        else return (CPPNFunction)sysrnd.Next(0, (int)CPPNFunction.Cosine);
    }

    public void ChangeRandomCPPNNodeFunction()
    {
        CPPNnode node = this.cppn_nodes[UnityEngine.Random.Range(sensorimotor_idxs.x,this.cppn_nodes.Count)];
        node.function = GetRandomCPPNfunction();
    }

    public void DisableRandomCPPNConnection()
    {
        if (this.cppn_connections.Count == 0) return;
        CPPNconnection connection = this.cppn_connections[UnityEngine.Random.Range(0, this.cppn_connections.Count)];
        connection.enabled = false;
    }





    public DevelopmentNeuron GetDefaultDevNeuron()
    {
        return new(threshold: 1, bias: 0, sign: true, adaptation_delta: 1, decay: 1, sigmoid_alpha: 1);
    }


    public override void Mutate()
    {
        bool should_mutate;
        float rnd;
        // first, mutate synapse parameters
        foreach (CPPNconnection connection in this.cppn_connections)
        {
            should_mutate = Range(0f, 1f) < CHANCE_TO_MUTATE_EACH_CONNECTION;
            if (!should_mutate) continue;
            rnd = Range(0f, 1f);


            if (rnd < 0.97)
            {
                connection.weight += Range(0, 2) == 0 ? WEIGHT_MUTATE_INCREMENT : -WEIGHT_MUTATE_INCREMENT;
            }
            else if (rnd < 0.98)
            {
                connection.weight /= 2;
            }
            else if (rnd < 0.99)
            {
                connection.weight *= 2;
            }
            else
            {
                connection.weight *= -1;
            }

            // constrain weights
            //connection.weight = math.max(connection.weight, -1);
            //connection.weight = math.min(connection.weight, 1);

        }


        /*        // then, mutate neuron parameters
                for (int i = sensorimotor_idxs.x; i < this.cppn_nodes.Count; i++)
                {
                    CPPNnode node = this.cppn_nodes[i];
                    should_mutate = Range(0f, 1f) < CHANCE_TO_MUTATE_NODE;
                    if (!should_mutate) continue;
                    //rnd = Range(0f, 1f);

                    node.function = GetRandomCPPNfunction();

                }*/



        
        int mutation_type;

        if (ALLOW_MULTIPLE_MUTATIONS)
        {
            mutation_type = 0;
        }
        else
        {
            float mutation_type_rnd = Range(0f,1f);
            if(mutation_type_rnd < 0.06)
            {
                mutation_type = 0;
            }else if(mutation_type_rnd < 0.12)
            {
                mutation_type = 1;
            }
            else if (mutation_type_rnd < 0.18)
            {
                mutation_type = 2;
            }
            else if (mutation_type_rnd < 0.24)
            {
                mutation_type = 3;
            }
            else
            {
                mutation_type = -1;
            }


        }

        switch (mutation_type)
        {
            case 0:
                // MUTATE NODE?
                should_mutate = Range(0f, 1f) < CHANGE_NODE_FUNCTION_MUTATION_RATE;
                while (!ALLOW_MULTIPLE_MUTATIONS || should_mutate)
                {
                    ChangeRandomCPPNNodeFunction();

                    if (!ALLOW_MULTIPLE_MUTATIONS) break;
                    should_mutate = STACK_MUTATIONS ? Range(0f, 1f) < CHANGE_NODE_FUNCTION_MUTATION_RATE : false;
                }
                if (!ALLOW_MULTIPLE_MUTATIONS) break;
                else goto case 1;
            case 1:
                // ADD CONNECTION?
                should_mutate = Range(0f, 1f) < ADD_CONNECTION_MUTATION_RATE;
                while (!ALLOW_MULTIPLE_MUTATIONS || should_mutate)
                {
                    AddRandomConnection();

                    if (!ALLOW_MULTIPLE_MUTATIONS) break;
                    should_mutate = STACK_MUTATIONS ? Range(0f, 1f) < ADD_CONNECTION_MUTATION_RATE : false;
                }
                if (!ALLOW_MULTIPLE_MUTATIONS) break;
                else goto case 2;
            case 2:
                // ADD NODE?
                if(this.cppn_connections.Count > 0)
                {
                    should_mutate = (Range(0f, 1f) < ADD_NODE_MUTATION_RATE);
                    while (!ALLOW_MULTIPLE_MUTATIONS || should_mutate)
                    {
                        AddNewRandomNode();

                        if (!ALLOW_MULTIPLE_MUTATIONS) break;
                        should_mutate = STACK_MUTATIONS ? Range(0f, 1f) < ADD_NODE_MUTATION_RATE : false;
                    }
                }

                if (!ALLOW_MULTIPLE_MUTATIONS) break;
                else goto case 3;
            case 3:
                // DISABLE CONNECTION?
                should_mutate = Range(0f, 1f) < DISABLE_CONNECTION_MUTATION_RATE;
                while (!ALLOW_MULTIPLE_MUTATIONS || should_mutate)
                {
                    DisableRandomCPPNConnection();

                    if (!ALLOW_MULTIPLE_MUTATIONS) break;
                    should_mutate = STACK_MUTATIONS ? Range(0f, 1f) < DISABLE_CONNECTION_MUTATION_RATE : false;
                }
                if (!ALLOW_MULTIPLE_MUTATIONS) break;
                else goto default;
            default:
                break;
        }

        



/*        // Mutate Brain Update speed
        should_mutate = Range(0f, 1f) < BRAIN_UPDATE_PERIOD_MUTATION_RATE;
        while (should_mutate)
        {
            this.brain_update_period += Range(0, 2) == 0 ? BRAIN_UPDATE_PERIOD_MUTATION_INCREMENT : -BRAIN_UPDATE_PERIOD_MUTATION_INCREMENT;
            this.brain_update_period = Mathf.Max(this.brain_update_period, 0.001f);
        }*/
        
        this.FinalizeCPPN();
    }

    public void AddNewRandomNode()
    {
        if (this.cppn_connections.Count == 0) return;
        int attempts = 0;
        CPPNconnection random_connection = this.cppn_connections[Range(0, this.cppn_connections.Count)];
        while (!random_connection.enabled && attempts < 1000)
        {
            random_connection = this.cppn_connections[Range(0, this.cppn_connections.Count)];
        }
        if (attempts >= 1000) return;
        random_connection.enabled = false;
        CPPNnode new_node = new(ID: GetNextGlobalCPPNNeuronID(), function: GetRandomCPPNfunction());
        CPPNconnection new_connectionA = new(ID: GetNextGlobalCPPNSynapseID(), weight: 1, from_ID: random_connection.from_node_ID, to_ID: new_node.ID);
        CPPNconnection new_connectionB = new(ID: GetNextGlobalCPPNSynapseID(), weight: random_connection.weight, from_ID: new_node.ID, to_ID: random_connection.to_node_ID);
        this.cppn_connections.Add(new_connectionA);
        this.cppn_connections.Add(new_connectionB);
        this.cppn_nodes.Add(new_node);
    }

    public void AddRandomConnection()
    {
        int num_of_outputs = (sensorimotor_idxs.y - sensorimotor_idxs.x);
        int from_idx = Range(0, this.cppn_nodes.Count - num_of_outputs);
        if(from_idx >= sensorimotor_idxs.x && from_idx < sensorimotor_idxs.y)
        {
            // its an output, cant connect from an output
            from_idx += num_of_outputs;
        }
        if (from_idx >= this.cppn_nodes.Count) from_idx = Range(0, sensorimotor_idxs.x);
        int to_idx = Range(sensorimotor_idxs.x, this.cppn_nodes.Count);
        CPPNnode from_neuron = (CPPNnode)this.cppn_nodes[from_idx];
        CPPNnode to_neuron = (CPPNnode)this.cppn_nodes[to_idx];

        if (!ALLOW_RECURRENT_CONNECTIONS)
        {
            int attempts = 0;
            // try to find another connection if the randomly generated one is recurrent
            while (to_neuron.layer <= from_neuron.layer && attempts < 100) 
            {
                from_idx = Range(0, this.cppn_nodes.Count);
                to_idx = Range(0, this.cppn_nodes.Count);
                from_neuron = (CPPNnode)this.cppn_nodes[from_idx];
                to_neuron = (CPPNnode)this.cppn_nodes[to_idx];
                attempts++;
            }

             if (to_neuron.layer <= from_neuron.layer) return;
        }
            

        CPPNconnection new_connection = new(from_ID: from_neuron.ID, to_ID: to_neuron.ID, weight: GetRandomInitialCPPNWeight(), ID: GetNextGlobalCPPNSynapseID());
        this.cppn_connections.Add(new_connection);
    }

    public float GetRandomInitialCPPNWeight()
    {
        return Range(-0.5f, 0.5f);
    }

    public override (BrainGenome, BrainGenome) Reproduce(BrainGenome genome_parent2)
    {
        HyperNEATBrainGenome parent1;
        HyperNEATBrainGenome parent2;
        HyperNEATBrainGenome offspring1;
        HyperNEATBrainGenome offspring2;

        if (this is RegularHyperNEATBrainGenome)
        {
            parent1 = (RegularHyperNEATBrainGenome)this;
            parent2 = (RegularHyperNEATBrainGenome)genome_parent2;
            offspring1 = new RegularHyperNEATBrainGenome();
            offspring2 = new RegularHyperNEATBrainGenome();
        }
        else if(this is ESHyperNEATBrainGenome)
        {
            parent1 = (ESHyperNEATBrainGenome)this;
            parent2 = (ESHyperNEATBrainGenome)genome_parent2;
            offspring1 = new ESHyperNEATBrainGenome();
            offspring2 = new ESHyperNEATBrainGenome();
        }
        else
        {
            Debug.LogError("error");
            return (null, null);
        }


        float[] update_period = new float[] { parent1.brain_update_period, parent2.brain_update_period };
        int rnd = Range(0, 2);
        offspring1.brain_update_period = update_period[rnd];
        offspring2.brain_update_period = update_period[1 - rnd];

        int i = 0, j = 0;
        while (i < parent1.cppn_nodes.Count || j < parent2.cppn_nodes.Count)
        {
            CPPNnode neuron1 = null;
            if (i < parent1.cppn_nodes.Count)
            {
                neuron1 = (CPPNnode)parent1.cppn_nodes[i];
            }

            CPPNnode neuron2 = null;
            if (j < parent2.cppn_nodes.Count)
            {
                neuron2 = (CPPNnode)parent2.cppn_nodes[j];
            }

            if (neuron1 != null && neuron2 != null)
            {
                if (neuron1.ID < neuron2.ID)
                {
                    neuron2 = null;
                }
                else if (neuron1.ID > neuron2.ID)
                {
                    neuron1 = null;
                }
            }

            if (neuron1 != null && neuron2 != null)
            {
                rnd = Range(0, 2);
                CPPNnode[] nodes = { neuron1, neuron2 };
                offspring1.cppn_nodes.Add(nodes[rnd].Clone());
                offspring2.cppn_nodes.Add(nodes[1 - rnd].Clone());
                i++;
                j++;
            }
            else if (neuron1 != null && neuron2 == null)
            {
                offspring1.cppn_nodes.Add(neuron1.Clone());
                offspring2.cppn_nodes.Add(neuron1.Clone());
                i++;
            }
            else if (neuron1 == null && neuron2 != null)
            {
                offspring1.cppn_nodes.Add(neuron2.Clone());
                offspring2.cppn_nodes.Add(neuron2.Clone());
                j++;
            }



        }

        i = 0;
        j = 0;
        while (i < parent1.cppn_connections.Count || j < parent2.cppn_connections.Count)
        {
            CPPNconnection connection1 = null;
            if (i < parent1.cppn_connections.Count)
            {
                connection1 = (CPPNconnection)parent1.cppn_connections[i];
            }

            CPPNconnection connection2 = null;
            if (j < parent2.cppn_connections.Count)
            {
                connection2 = (CPPNconnection)parent2.cppn_connections[j];
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
                rnd = Range(0, 2);
                CPPNconnection[] connections = new CPPNconnection[] { connection1, connection2 };
                offspring1.cppn_connections.Add(connections[rnd].Clone());
                offspring2.cppn_connections.Add(connections[1 - rnd].Clone());
                i++;
                j++;
            }
            else if (connection1 != null && connection2 == null)
            {
                offspring1.cppn_connections.Add(connection1.Clone());
                offspring2.cppn_connections.Add(connection1.Clone());
                i++;
            }
            else if (connection1 == null && connection2 != null)
            {
                offspring1.cppn_connections.Add(connection2.Clone());
                offspring2.cppn_connections.Add(connection2.Clone());
                j++;
            }

            if ((connection1 != null && !connection1.enabled) || (connection2 != null && !connection2.enabled))
            {
                offspring1.cppn_connections[^1].enabled = Range(0f, 1.0f) < 0.75f ? false : true;
                offspring2.cppn_connections[^1].enabled = Range(0f, 1.0f) < 0.75f ? false : true;
            }


        }

        offspring1.FinalizeCPPN();
        offspring2.FinalizeCPPN();

        return (offspring1, offspring2);
    }



    public const string save_file_extension = ".HyperNEATBrainGenome";
    public override void SaveToDisk()
    {
        string[] existing_saves = Directory.GetFiles(path: GlobalConfig.save_file_path, searchPattern: GlobalConfig.save_file_base_name + "*" + save_file_extension);
        int num_files = existing_saves.Length;
        string full_path = GlobalConfig.save_file_path + GlobalConfig.save_file_base_name + num_files.ToString() + save_file_extension;
        Debug.Log("Saving brain genome to disk: " + full_path);
        StreamWriter data_file;
        data_file = new(path: full_path, append: false);


        BinaryFormatter formatter = new BinaryFormatter();
        object[] objects_to_save = new object[] { CPPN_nodes, CPPN_connections };
        formatter.Serialize(data_file.BaseStream, objects_to_save);
        data_file.Close();
    }

    public static HyperNEATBrainGenome LoadFromDisk(string filename = "")
    {

        if (filename == "")
        {
            string[] existing_saves = Directory.GetFiles(path: GlobalConfig.save_file_path, searchPattern: GlobalConfig.save_file_base_name + "*" + save_file_extension);
            int num_files = existing_saves.Length - 1;
            filename = GlobalConfig.save_file_base_name + num_files.ToString();
        }


        CPPNnodeParallel[] CPPN_nodes = null;
        CPPNconnectionParallel[] CPPN_connections = null;

        BinaryFormatter formatter = new BinaryFormatter();
        string full_path = GlobalConfig.save_file_path + filename + save_file_extension;
        // loading
        using (FileStream fs = File.Open(full_path, FileMode.Open))
        {
            object obj = formatter.Deserialize(fs);
            // = new object[] { this.current_state_neurons.ToArray(), this.current_state_synapses.ToArray() };
            var newlist = (object[])obj;
            for (int i = 0; i < newlist.Length; i++)
            {
                if (i == 0)
                {
                    CPPN_nodes = (CPPNnodeParallel[])newlist[i];
                }
                else if (i == 1)
                {
                    CPPN_connections = (CPPNconnectionParallel[])newlist[i];
                }
                else
                {
                    Debug.LogWarning("ERROR LOADING BRAIN");
                }

            }
        }

        RegularHyperNEATBrainGenome genome = new();
        JobHandle handle = genome.ScheduleDevelopCPUJob();
        handle.Complete();

        return genome;
    }

    // call this function when all CPPN nodes and connections are set

    public void FinalizeCPPN()
    {
        // reset nodes
        foreach(List<CPPNnode> layer in this.layers)
        {
            layer.Clear();
        }
        this.layers.Clear();

        Dictionary<int, int> ID_to_idx = new();
        int j = 0;
        foreach (CPPNnode n in this.cppn_nodes)
        {
            if (ID_to_idx.ContainsKey(n.ID))
            {
                Debug.LogError("Duplicate nodes");
            }
            ID_to_idx[n.ID] = j;
            n.inputs.Clear();
            n.outputs.Clear();
            n.layer = INVALID_NEAT_ID;
            j++;
        }


        // add nodes to input/output lists for easy access
        foreach (CPPNconnection c in this.cppn_connections)
        {
           //if (!c.enabled) continue;
            int to_idx = ID_to_idx[c.to_node_ID];
            CPPNnode to_node = this.cppn_nodes[to_idx];
            int from_idx = ID_to_idx[c.from_node_ID];
            CPPNnode from_node = this.cppn_nodes[from_idx];
            to_node.inputs.Add((from_node, c));
            from_node.outputs.Add((to_node, c));
        }


        // sort nodes into layers.
        Stack<CPPNnode> nodes_to_explore = new();
        for(int i=0; i < sensorimotor_idxs.x; i++)
        {
            this.cppn_nodes[i].layer = SENSORY_LAYER_ID; // sensor layer
            nodes_to_explore.Push(this.cppn_nodes[i]);
        }

        for (int i = sensorimotor_idxs.x; i < sensorimotor_idxs.y; i++)
        {
            this.cppn_nodes[i].layer = OUTPUT_TEMP_LAYER_ID; // motor layer
        }

        int max_hidden_layer = 0;

        if (BFS_for_CPPN_layers)
        {
            // breadth first search, produces the shortest path to each node
            while (nodes_to_explore.Count != 0)
            {
                // be careful of this loop... its doing graph traversal in a while loop.
                CPPNnode node = nodes_to_explore.Pop();
                // without recursive connections, the output layer is the max layer
                foreach ((CPPNnode, CPPNconnection) c in node.outputs)
                {
                    CPPNnode output_node = c.Item1;
                    if (output_node.layer == OUTPUT_TEMP_LAYER_ID || output_node.layer == SENSORY_LAYER_ID || output_node == node) continue;
                    if (output_node.layer == INVALID_NEAT_ID) //|| (output_node.layer < node.layer + 1))
                    {
                        output_node.layer = node.layer + 1;
                        max_hidden_layer = Math.Max(max_hidden_layer, output_node.layer);
                        if (!nodes_to_explore.Contains(output_node)) nodes_to_explore.Push(output_node);
                    }

                }
            }
        }
        else
        {
            // depth first search, produces the longest path to each node
            Dictionary<CPPNnode, bool> visited = new(); // to prevent infinite loops
            void Explore(CPPNnode node)
            {
                visited[node] = true;
                foreach ((CPPNnode, CPPNconnection) c in node.outputs)
                {
                    CPPNnode output_node = c.Item1;
                    if (output_node.layer == OUTPUT_TEMP_LAYER_ID || output_node.layer == SENSORY_LAYER_ID || output_node == node) continue;
                    bool output_node_not_visited = !visited.ContainsKey(output_node) || !visited[output_node];
                    if (output_node.layer == INVALID_NEAT_ID || (output_node.layer < node.layer + 1 && output_node_not_visited))
                    {
                        output_node.layer = node.layer + 1; 
                        max_hidden_layer = Math.Max(max_hidden_layer, output_node.layer);
                       
                        Explore(output_node);
                        
                        //if (!nodes_to_explore.Contains(output_node) && output_node != node) nodes_to_explore.Push(output_node);
                    }

                }
                visited[node] = false;
            }

            while (nodes_to_explore.Count != 0)
            {
                CPPNnode node = nodes_to_explore.Pop();
                Explore(node);       
            }
        }


        int output_layer = max_hidden_layer + 1;
        for (int i = sensorimotor_idxs.x; i < sensorimotor_idxs.y; i++)
        {
            this.cppn_nodes[i].layer = output_layer; // motor layer
        }


        //
 
        for (int i = 0; i <= output_layer; i++)
        {
            this.layers.Add(new List<CPPNnode>());
        }
  


        foreach (CPPNnode n in this.cppn_nodes)
        {
            if (n.layer == INVALID_NEAT_ID) continue;
            this.layers[n.layer].Add(n);
        }


        // convert to GPU
        (this.CPPN_nodes, this.CPPN_connections) = ConvertCPPNToParallel();

        //Debug.Log("CPPN Size " + this.cppn_nodes.Count + " with " + this.cppn_connections.Count + " connections");
    }

    [Serializable]
    public struct CPPNconnectionParallel
    {
        public int from_idx;
        public float weight;
    }

    [Serializable]
    public struct CPPNnodeParallel
    {
        public int function;
        public int number_of_input_connections;
        public int input_connection_start_idx;
    }


    public struct CPPNOutputArray
    {
        public float initial_weight;
        public float learning_rate;
        public float4 hebb_ABCD_coefficients;
        public float bias;
        public int sign;
        public float sigmoid_alpha;
        public Neuron.NeuronActivationFunction activation_function;
        public bool enabled;

        public CPPNOutputArray(float initial_weight,
            float learning_rate,
            float4 hebb_ABCD_coefficients,
            float bias,
            int sign,
            float sigmoid_alpha,
            Neuron.NeuronActivationFunction activation_function,
            bool enabled)
        {
            this.initial_weight = initial_weight;
            this.learning_rate = learning_rate;
            this.hebb_ABCD_coefficients = hebb_ABCD_coefficients;
            this.bias = bias;
            this.sign = sign;
            this.sigmoid_alpha = sigmoid_alpha;
            this.activation_function = activation_function;
            this.enabled = enabled;
    }

        public static CPPNOutputArray GetNewDefault()
        {
            return new CPPNOutputArray(0, 0, new float4(0, 0, 0, 0), 0, 0, 0, Neuron.NeuronActivationFunction.Sigmoid, false);
        }

        public static float Distance(CPPNOutputArray a, CPPNOutputArray b)
        {
            float result = 0;
            //result += math.pow(a.initial_weight - b.initial_weight, 2);
            result += math.pow(a.learning_rate - b.learning_rate, 2);
            for(int i = 0; i < 4; i++)
            {
                result += math.pow(a.hebb_ABCD_coefficients[i] - b.hebb_ABCD_coefficients[i], 2);
            }
            // result += math.pow(a.bias - b.bias, 2);
            //result += math.pow(a.sign - b.sign, 2);
            //result += math.pow(a.sigmoid_alpha - b.sigmoid_alpha, 2);
            return result;
        }
    };

    public class CPPNnode
    {
        public List<(CPPNnode, CPPNconnection)> inputs;
        public List<(CPPNnode, CPPNconnection)> outputs;


        public int ID;
        public CPPNFunction function;
        public bool sensorimotor;

        public int layer;

        public CPPNnode(int ID,
            CPPNFunction function)
        {
            this.function = function;
            this.ID = ID;
            this.inputs = new();
            this.outputs = new();
        }

        public CPPNnode Clone()
        {
            return new(this.ID,
                this.function);
        }

        public static float EvaluateCPPNFunction(CPPNFunction function, float sum)
        {
            float activation;
            switch (function)
            {
                case CPPNFunction.Linear:
                    // pass through
                    activation = sum;
                    break;
                case CPPNFunction.Sigmoid:
                    activation = 1.0f / (1.0f + math.exp(-1 * sum));
                    break;
                case CPPNFunction.Gaussian:
                    activation = math.exp(-(sum * sum));
                    break;
                case CPPNFunction.Sine:
                    activation = math.sin(sum);
                    break;
                case CPPNFunction.Abs:
                    activation = math.abs(sum);
                    break;
                case CPPNFunction.Step:
                    if(sum > 0)
                    {
                        activation = 1;
                    }else// if(sum < 0)
                    {
                        activation = 0;
                    }
                    break;
                case CPPNFunction.ReLU:
                    activation = sum > 0 ? sum : 0;
                    break;
                case CPPNFunction.Cube:
                    activation = (sum * sum * sum);
                    break;
                case CPPNFunction.Square:
                    activation = (sum * sum);
                    break;
                case CPPNFunction.Cosine:
                    activation = math.cos(sum);
                    break;
                case CPPNFunction.Tangent:
                    activation = math.tan(sum);
                    break;
                case CPPNFunction.HyperSine:
                    activation = math.sinh(sum);
                    break;
                case CPPNFunction.HyperCosine:
                    activation = math.cosh(sum);
                    break;
                case CPPNFunction.HyperTangent:
                    activation = math.tanh(sum);
                    break;
                case CPPNFunction.SquareRoot:
                    activation = math.sqrt(math.abs(sum));
                    break;
                default:
                    Debug.LogError("Not recognized.");
                    activation = -1;
                    break;
            }

     
            if (!float.IsFinite(activation)) return 0.0f;
            else return activation;
        }

    }



    float EvaluateCPPNNode(CPPNnodeParallel node, float[] CPPN_nodes_outputs)
    {
        float sum = 0;
        for (int i = node.input_connection_start_idx; i < node.input_connection_start_idx + node.number_of_input_connections; i++)
        {
            CPPNconnectionParallel connection = CPPN_connections[i];
            float input_value = CPPN_nodes_outputs[connection.from_idx];
            sum += connection.weight * input_value;
        }
        return CPPNnode.EvaluateCPPNFunction((CPPNFunction)node.function, sum);
    }

    public CPPNOutputArray QueryCPPN(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        int NUM_OF_CPPN_NODES = this.CPPN_nodes.Length;
        CPPNOutputArray output_array = CPPNOutputArray.GetNewDefault();
        float[] CPPN_nodes_outputs = new float[NUM_OF_CPPN_NODES];


        int k;
        float result;

        // do inputs
        for (k = 0; k < HyperNEATBrainGenome.sensorimotor_idxs.x; k++)
        {

            if (k == 0)
            {
                result = 1;
            }
            else if (k == 1)
            {
                result = x1;
            }
            else if (k == 2)
            {
                result = y1;
            }
            else if (k == 3)
            {
                result = z1;
            }
            else if (k == 4)
            {
                result = x2;
            }
            else if (k == 5)
            {
                result = y2;
            }
            else// if (k == 6)
            {
                result = z2;
            }

            CPPN_nodes_outputs[k] = result;
        }


        // do hidden nodes
        for (k = HyperNEATBrainGenome.sensorimotor_idxs.y; k < NUM_OF_CPPN_NODES; k++)
        {
            CPPN_nodes_outputs[k] = EvaluateCPPNNode(CPPN_nodes[k], CPPN_nodes_outputs);
        }

        // do outputs
        int m = 0;
        for (k = HyperNEATBrainGenome.sensorimotor_idxs.x; k < HyperNEATBrainGenome.sensorimotor_idxs.y; k++)
        {
            result = EvaluateCPPNNode(CPPN_nodes[k], CPPN_nodes_outputs);

            if (m == 0)
            {
                output_array.initial_weight = result;
            }
            else if (m == 1)
            {
                output_array.learning_rate = result;
            }
            else if (m == 2)
            {
                output_array.hebb_ABCD_coefficients[0] = ABCD_multiplier * result;
            }
            else if (m == 3)
            {
                output_array.hebb_ABCD_coefficients[1] = ABCD_multiplier * result;
            }
            else if (m == 4)
            {
                output_array.hebb_ABCD_coefficients[2] = ABCD_multiplier * result;
            }
            else if (m == 5)
            {
                output_array.hebb_ABCD_coefficients[3] = ABCD_multiplier * result;
            }
            else if (m == 6)
            {
                output_array.bias = result;
            }
            else if (m == 7)
            {
                output_array.sigmoid_alpha = multiplier * math.abs(result);
            }
            else if (m == 8)
            {
                if (result >= -1 && result <= -0.33)
                {
                    output_array.activation_function = Neuron.NeuronActivationFunction.Sigmoid;

                }
                else if (result >= -0.33 && result <= 0.33)
                {
                    output_array.activation_function = Neuron.NeuronActivationFunction.Tanh;
                }
                else //   result > 0.25
                {
                    output_array.activation_function = Neuron.NeuronActivationFunction.LeakyReLU;
                }
            }
            else if (m == 9)
            {
                output_array.sign = result < 0 ? -1 : 1;
            }
            else if (m == 10)
            {
                output_array.enabled = result > 0 ? true : false;
            }
            else
            {
                Debug.LogError("Error");
            }
            m++;
        }

        return output_array;
    }



    public class CPPNconnection
    {
        public int ID;
        public float weight;
        public int from_node_ID;
        public int to_node_ID;
        public bool enabled;

        public CPPNconnection(int ID,
            float weight,
            int from_ID,
            int to_ID,
            bool enabled=true)
        {
            this.ID = ID;
            this.weight = weight;
            this.from_node_ID = from_ID;
            this.to_node_ID = to_ID;
            this.enabled = enabled;
        }
        public CPPNconnection Clone()
        {
            return new(this.ID,
                this.weight,
                this.from_node_ID,
                this.to_node_ID,
                this.enabled);
        }
    }

    static int NEXT_SYNAPSE_ID = BrainGenome.INVALID_NEAT_ID;
    static int NEXT_NEURON_ID = BrainGenome.INVALID_NEAT_ID;
    static int GetNextGlobalCPPNSynapseID()
    {
        int ID = NEXT_SYNAPSE_ID;
        NEXT_SYNAPSE_ID++;
        return ID;
    }
    static int GetNextGlobalCPPNNeuronID()
    {
        int ID = NEXT_NEURON_ID;
        NEXT_NEURON_ID++;
        return ID;
    }

    public override void ScheduleDevelopGPUJob()
    {
        throw new NotImplementedException();
    }
}