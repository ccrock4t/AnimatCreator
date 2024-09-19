using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UIElements;
using static Brain;
using static GlobalConfig;
using static SoftVoxelRobot;
using static UnityEngine.Random;

/// <summary>
/// It's a HyperNEAT genome
/// </summary>
public class CPPNGenome
{

    // start with fully connected CPPN?
    const bool INITIALIZE_FULLY_CONNECT_CPPN = true;
    const bool INITIALIZE_WITH_HIDDEN_LAYERS = false;
    const bool DROPOUT = true;
    const float DROPOUT_RATE = 0.75f; // higher means more dropout


    // allow disabling synapses
    const bool ENABLE_LEO = true;

    // seed CPPN to bias locality
    const bool SEED_TO_BIAS_LOCALITY_IN_BRAIN_CONNECTIVITY = false; // this connects certain inputs to Gaussian nodes, and those Gaussian nodes to the LEO (see LEO paper)
    const bool SEED_TO_BIAS_LOCALITY_IN_BODY = false; // this connects certain inputs to Gaussian nodes, and those Gaussian nodes to the LEO (see LEO paper)

    // start with extra hidden nodes?
    const bool START_WITH_HIDDEN_NODES = false;
    const int INITIAL_HIDDEN_CPPN_NODES = 10;

    // start with extra connections added?
    const bool START_WITH_ADDITIONAL_CONNECTIONS = false;
    const float INITIAL_ADDITIONAL_CONNECTIONS = 1;

    //
    const bool NORMALIZE_CPPN_WEIGHTS = false; // using normalization function
    const bool CONSTRAIN_CPPN_WEIGHTS = false; // to [-1,1]

    // body parameters
    public const bool EVOLVE_EMPTY_VOXELS = false;


    // Mutation parameters
    const bool ALLOW_MULTIPLE_MUTATIONS = false;
    const bool STACK_MUTATIONS = false;
    const bool ALLOW_EVOLVING_RECURRENT_CONNECTIONS_IN_CPPN = false;

    const bool INCLUDE_EXTRA_CPPN_FUNCTIONS = false;

    public const float RATE_MULTIPLIER = 1;
    public const float ADD_CONNECTION_MUTATION_RATE = 0.05f;
    public const float ADD_NODE_MUTATION_RATE = 0.03f;
    public const float DISABLE_CONNECTION_MUTATION_RATE = 0.1f;
    public const float DISABLE_NODE_MUTATION_RATE = 0.05f;
    public const float CHANGE_NODE_MUTATION_RATE = 0.05f;
    public const float CHANCE_TO_MUTATE_EACH_CONNECTION = 0.9f;
    public const float WEIGHT_MUTATE_INCREMENT = 0.1f;

    public const float BRAIN_UPDATE_PERIOD_MUTATION_RATE = 0.00f;
    public const float BRAIN_UPDATE_PERIOD_MUTATION_INCREMENT = 0.01f;

    const int INVALID_CPPN_LAYER_ID = -9999;
    const int OUTPUT_TEMP_LAYER_ID = -1;
    const int SENSORY_LAYER_ID = 0;

    const bool BFS_for_CPPN_layers = false; // true for BFS, false for DFS

    // variables
    public List<CPPNnode> cppn_nodes;
    public List<CPPNconnection> cppn_connections;
    public List<List<CPPNnode>> layers;
    public NativeArray<CPPNnodeParallel> CPPN_nodes;
    public NativeArray<CPPNconnectionParallel> CPPN_connections;
    

    // variables to preserve
    public int generation = 0;
    int bias_node_id;
    public int2 CPPN_IO_IDXS; // < .x are the inputs to the CPPN, >= x and < y are the outputs to the CPPN

    public enum CPPNtype
    {
        BrainAndBody,
        Brain,
        Body
    }

    public struct BrainBodyDualGenome
    {
        public CPPNGenome brain_genome;
        public CPPNGenome body_genome;

        public BrainBodyDualGenome(CPPNGenome brain_genome, CPPNGenome body_genome)
        {
            this.brain_genome = brain_genome;
            this.body_genome = body_genome;
        }
    }

    CPPNtype cppn_type;

    // Create an empty BrainBodyGenome
    public CPPNGenome(CPPNtype type)
    {
        this.cppn_type = type;
        this.cppn_nodes = new();
        this.cppn_connections = new();
        this.layers = new();
    }


    public void SetCPPNnodesForIO()
    {
        bool add_brain_nodes = this.cppn_type == CPPNtype.BrainAndBody || this.cppn_type == CPPNtype.Brain;
        bool add_body_nodes = this.cppn_type == CPPNtype.BrainAndBody || this.cppn_type == CPPNtype.Body;

        int x1_id, y1_id, z1_id, w1_id=-1, v1_id=-1, x2_id = -1, y2_id =-1, z2_id=-1, w2_id=-1, v2_id=-1;

        // ==== INPUTS TO CPPN
        AddDisconnectedNode(); // cppn_bias_input_node
        int bias_node_id = this.cppn_nodes.Count - 1;
        if (bias_node_id != 0) Debug.LogError("ERROR: bias node is coded as 0 in certain places, it should remain zero");
        AddDisconnectedNode(); // x1_input_node
        x1_id = this.cppn_nodes.Count - 1;
        AddDisconnectedNode(); // y1_input_node
        y1_id = this.cppn_nodes.Count - 1;
        AddDisconnectedNode(); // z1_input_node
        z1_id = this.cppn_nodes.Count - 1;

     
        if (add_brain_nodes)
        {
            if (GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL) AddDisconnectedNode(); // w1_input_node
            w1_id = this.cppn_nodes.Count - 1;
            if (GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL) AddDisconnectedNode(); // v1_input_node
            v1_id = this.cppn_nodes.Count - 1;


            AddDisconnectedNode(); // x2_input_node
            x2_id = this.cppn_nodes.Count - 1;
            AddDisconnectedNode(); // y2_input_node
            y2_id = this.cppn_nodes.Count - 1;
            AddDisconnectedNode(); // z2_input_node
            z2_id = this.cppn_nodes.Count - 1;
            if (GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL) AddDisconnectedNode(); // w2_input_node
            w2_id = this.cppn_nodes.Count - 1;
            if (GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL) AddDisconnectedNode(); // v2_input_node
            v2_id = this.cppn_nodes.Count - 1;
        }
   

        // input the distance of the cell 1 from the origin
        AddDisconnectedNode(); // distance_cell1_input_node
        

        CPPN_IO_IDXS.x = this.cppn_nodes.Count;

        // ==== OUTPUTS FROM CPPN
        int BODY_LEO_ID = -1;
        int LEO_ID=-1;
    
        if (add_body_nodes)
        {
            // morphology
            if (EVOLVE_EMPTY_VOXELS) AddDisconnectedNode(); // morphology_voxel_presence
            BODY_LEO_ID = this.cppn_nodes.Count - 1;
        }

        if (add_brain_nodes)
        {
            // neuron bias
            AddDisconnectedNode(CPPNFunction.Linear);

            // synapse initial weight
            AddDisconnectedNode(CPPNFunction.Linear);

            if (ENABLE_LEO)
            {
                AddDisconnectedNode(); // Link Expression Output (LEO)
                LEO_ID = this.cppn_nodes.Count - 1;
            }
        }



        CPPN_IO_IDXS.y = this.cppn_nodes.Count; // output idx end

        // ==== Hidden neurons
     
        if (SEED_TO_BIAS_LOCALITY_IN_BRAIN_CONNECTIVITY && this.cppn_type != CPPNtype.Body)
        {
            AddNewLocalitySeedNode(x1_id, x2_id, LEO_ID);
            AddNewLocalitySeedNode(y1_id, y2_id, LEO_ID);
            AddNewLocalitySeedNode(z1_id, z2_id, LEO_ID);
            int num_of_seed_nodes = 4;
            CPPNconnection seed_bias_connection = new(ID: this.cppn_connections.Count, weight: -num_of_seed_nodes, from_ID: bias_node_id, to_ID: LEO_ID);
            this.cppn_connections.Add(seed_bias_connection);
        }

        if (SEED_TO_BIAS_LOCALITY_IN_BODY && this.cppn_type != CPPNtype.Brain)
        {
            AddNewLocalitySeedNode(x1_id, x2_id, BODY_LEO_ID);
            AddNewLocalitySeedNode(y1_id, y2_id, BODY_LEO_ID);
            AddNewLocalitySeedNode(z1_id, z2_id, BODY_LEO_ID);
            int num_of_seed_nodes = 3;
            CPPNconnection seed_bias_connection = new(ID: this.cppn_connections.Count, weight: -num_of_seed_nodes, from_ID: bias_node_id, to_ID: BODY_LEO_ID);
            this.cppn_connections.Add(seed_bias_connection);
        }
        


        if (INITIALIZE_FULLY_CONNECT_CPPN)
        {
            if (INITIALIZE_WITH_HIDDEN_LAYERS)
            {
                // add hidden layer of nodes

                //int rnd = UnityEngine.Random.Range(0, INITIAL_HIDDEN_CPPN_NODES);

                // add hidden layer 1
                for (int j = 0; j < INITIAL_HIDDEN_CPPN_NODES; j++)
                {
                    CPPNnode new_node = new(ID: this.cppn_nodes.Count, function: GetRandomCPPNfunction());
                    this.cppn_nodes.Add(new_node);
                }
                int hidden_layer_1_end_idx = this.cppn_nodes.Count;

                // add hidden layer 2
                for (int j = 0; j < INITIAL_HIDDEN_CPPN_NODES; j++)
                {
                    CPPNnode new_node = new(ID: this.cppn_nodes.Count, function: GetRandomCPPNfunction());
                    this.cppn_nodes.Add(new_node);
                }


                // connect input to hidden layer 1
                for (int i = 0; i < CPPN_IO_IDXS.x; i++)
                {
                    for (int j = CPPN_IO_IDXS.y; j < hidden_layer_1_end_idx; j++)
                    {
                        int from_ID = this.cppn_nodes[i].ID;
                        int to_ID = this.cppn_nodes[j].ID;
                        if (SEED_TO_BIAS_LOCALITY_IN_BRAIN_CONNECTIVITY && to_ID == LEO_ID) continue;
                        if (SEED_TO_BIAS_LOCALITY_IN_BODY && to_ID == BODY_LEO_ID) continue;
                        CPPNconnection new_connection = new(ID: this.cppn_connections.Count, weight: GetRandomInitialCPPNWeight(), from_ID: from_ID, to_ID: to_ID);
                        this.cppn_connections.Add(new_connection);
                        if (DROPOUT)
                        {
                            if (UnityEngine.Random.Range(0f, 1f) < DROPOUT_RATE)
                            {
                                new_connection.enabled = false;
                            }
                        }
                    }
                }


                // connect  hidden layer 1 to hidden layer 2
                for (int i = CPPN_IO_IDXS.y; i < hidden_layer_1_end_idx; i++)
                {
                    for (int j = hidden_layer_1_end_idx; j < this.cppn_nodes.Count; j++)
                    {
                        int from_ID = this.cppn_nodes[i].ID;
                        int to_ID = this.cppn_nodes[j].ID;
                        if (SEED_TO_BIAS_LOCALITY_IN_BRAIN_CONNECTIVITY && to_ID == LEO_ID) continue;
                        if (SEED_TO_BIAS_LOCALITY_IN_BODY && to_ID == BODY_LEO_ID) continue;
                        CPPNconnection new_connection = new(ID: this.cppn_connections.Count, weight: GetRandomInitialCPPNWeight(), from_ID: from_ID, to_ID: to_ID);
                        this.cppn_connections.Add(new_connection);
                        if (DROPOUT)
                        {
                            if (UnityEngine.Random.Range(0f, 1f) < DROPOUT_RATE)
                            {
                                new_connection.enabled = false;
                            }
                        }
                    }
                }

                // connect hidden layer to output
                for (int i = CPPN_IO_IDXS.y; i < this.cppn_nodes.Count; i++)
                {
                    for (int j = CPPN_IO_IDXS.x; j < CPPN_IO_IDXS.y; j++)
                    {
                        int from_ID = this.cppn_nodes[i].ID;
                        int to_ID = this.cppn_nodes[j].ID;
                        if (SEED_TO_BIAS_LOCALITY_IN_BRAIN_CONNECTIVITY && to_ID == LEO_ID) continue;
                        if (SEED_TO_BIAS_LOCALITY_IN_BODY && to_ID == BODY_LEO_ID) continue;
                        CPPNconnection new_connection = new(ID: this.cppn_connections.Count, weight: GetRandomInitialCPPNWeight(), from_ID: from_ID, to_ID: to_ID);
                        this.cppn_connections.Add(new_connection);
                        if (DROPOUT)
                        {
                            if (UnityEngine.Random.Range(0f, 1f) < DROPOUT_RATE)
                            {
                                new_connection.enabled = false;
                            }
                        }
                    }
                }
            }
            else
            {
                // connect input directly to output
                for (int i = 0; i < CPPN_IO_IDXS.x; i++)
                {
                    for (int j = CPPN_IO_IDXS.x; j < CPPN_IO_IDXS.y; j++)
                    {
                        int from_ID = this.cppn_nodes[i].ID;
                        int to_ID = this.cppn_nodes[j].ID;
                        if (SEED_TO_BIAS_LOCALITY_IN_BRAIN_CONNECTIVITY && to_ID == LEO_ID) continue;
                        if (SEED_TO_BIAS_LOCALITY_IN_BODY && to_ID == BODY_LEO_ID) continue;
                        CPPNconnection new_connection = new(ID: this.cppn_connections.Count, weight: GetRandomInitialCPPNWeight(), from_ID: from_ID, to_ID: to_ID);
                        this.cppn_connections.Add(new_connection);
                        if (DROPOUT)
                        {
                            if (UnityEngine.Random.Range(0f, 1f) < DROPOUT_RATE)
                            {
                                new_connection.enabled = false;
                            }
                        }
                    }
                }
            }

        }

        // all the IDs up to this point are the same among all genomes.
        // the very first genome to reach this point, will now set the Global IDs
        if (NEXT_GLOBAL_NODE_ID == CPPNGenome.INVALID_CPPN_LAYER_ID) NEXT_GLOBAL_NODE_ID = this.cppn_nodes.Count;
        if (NEXT_GLOBAL_CONNECTION_ID == CPPNGenome.INVALID_CPPN_LAYER_ID) NEXT_GLOBAL_CONNECTION_ID = this.cppn_connections.Count;



        if (START_WITH_ADDITIONAL_CONNECTIONS)
        {
            for (int j = 0; j < INITIAL_ADDITIONAL_CONNECTIONS; j++)
            {
                int from_idx = Range(0, CPPN_IO_IDXS.x); 
                int to_idx = Range(CPPN_IO_IDXS.x, CPPN_IO_IDXS.y);
                CPPNnode from_neuron = (CPPNnode)this.cppn_nodes[from_idx];
                CPPNnode to_neuron = (CPPNnode)this.cppn_nodes[to_idx];
                CPPNconnection new_connection = new(from_ID: from_neuron.ID, to_ID: to_neuron.ID, weight: GetRandomInitialCPPNWeight(), ID: GetNextGlobalCPPNSynapseID());
                this.cppn_connections.Add(new_connection);
            }
        }


    }

    // Query CPPN given coordinates in range [-1,1]

    public static CPPNOutput QueryCPPN(CPPNtype type, int2 CPPN_IO_IDXS,
        NativeArray<CPPNnodeParallel> CPPN_nodes, NativeArray<CPPNconnectionParallel> CPPN_connections,
        float x1, float y1, float z1, float w1, float v1,
        float x2, float y2, float z2, float w2, float v2)
    {

        int NUM_OF_CPPN_NODES = CPPN_nodes.Length;
        CPPNOutput output_array = CPPNOutput.GetDefault();

        output_array.robot_voxel_material = RobotVoxel.Touch_Sensor;

        // WARNING --- DO NOT USE NATIVEARRAY HERE, EVEN WITH .TEMP, IT CAUSES MASSIVE MEMORY OVERFLOW WITH NO ERROR MESSAGES, apparently because the job runs for multiple frames
        float[] CPPN_nodes_outputs = new float[NUM_OF_CPPN_NODES];

        int k;
        float result;

        // do inputs
        for (k = 0; k < CPPN_IO_IDXS.x; k++)
        {
            int n = 0;
            if (k == n++)
            {
                result = 1; // bias node
            }
            else if (k == n++)
            {
                result = x1;
            }
            else if (k == n++)
            {
                result = y1;
            }
            else if (k == n++)
            {
                result = z1;
            }
            else if (GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL && type != CPPNtype.Body && k == n++)
            {
                result = w1;
            }
            else if (GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL && type != CPPNtype.Body && k == n++)
            {
                result = v1;
            }
            else if (type != CPPNtype.Body && k == n++)
            {
                result = x2;
            }
            else if (type != CPPNtype.Body && k == n++)
            {
                result = y2;
            }
            else if (type != CPPNtype.Body && k == n++)
            {
                result = z2;
            }
            else if (GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL && type != CPPNtype.Body && k == n++)
            {
                result = w2;
            }
            else if (GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL && type != CPPNtype.Body && k == n++)
            {
                result = v2;
            }
            else if (k == n++)
            {
                result = Vector3.Distance(float3.zero, new float3(x1, y1, z1));
            }
            else
            {
                Debug.LogError("error");
                continue;
            }

            CPPN_nodes_outputs[k] = result;
        }


        // do hidden nodes
        for (k = CPPN_IO_IDXS.y; k < NUM_OF_CPPN_NODES; k++)
        {
            CPPN_nodes_outputs[k] = EvaluateCPPNNode(CPPN_nodes[k], CPPN_nodes_outputs, CPPN_connections);
        }

        // do outputs

        bool is_material_present = true;
        int m = 0;

        int max_n = 0;
        for (k = CPPN_IO_IDXS.x; k < CPPN_IO_IDXS.y; k++)
        {
            result = EvaluateCPPNNode(CPPN_nodes[k], CPPN_nodes_outputs, CPPN_connections);

            int n = 0;

            if (EVOLVE_EMPTY_VOXELS && type != CPPNtype.Brain && m == n++)
            {
                if (result >= 0)
                {
                    is_material_present = true;
                }
                else
                {
                    is_material_present = false;
                }
            }
            else if (type != CPPNtype.Body && m == n++)
            {
                // neuron bias
                output_array.neuron_bias = result;
            }
            else if (type != CPPNtype.Body && m == n++)
            {
                // initial weight
                output_array.initial_weight = result;

            }
            else if (ENABLE_LEO && type != CPPNtype.Body && m == n++)
            {
                // LEO
                output_array.link_expression_output = result > 0;
            }
            else
            {
                Debug.LogError("QueryCPPN error");
                continue;
            }
            m++;
            max_n = n;
        }

        if (max_n != (CPPN_IO_IDXS.y - CPPN_IO_IDXS.x))
        {
            Debug.LogError("ERROR: n reached " + max_n + " instead of " + (CPPN_IO_IDXS.y - CPPN_IO_IDXS.x));
        }

        if (!is_material_present)
        {
            // actually, the material is disabled here
            output_array.robot_voxel_material = RobotVoxel.Empty;
        }


        return output_array;
    }

    // Create a bare minimum genome, which can be a starting point for evolution
    public static CPPNGenome CreateUnifiedTestGenome()
    {
        CPPNGenome genome = new(CPPNtype.BrainAndBody);
        genome.SetCPPNnodesForIO();
        genome.FinalizeGenome();

        return genome;
    }

    // Create a bare minimum genome, which can be a starting point for evolution
    public static BrainBodyDualGenome CreateDualTestGenome()
    {

        CPPNGenome brain_genome = new(CPPNtype.Brain);
        brain_genome.SetCPPNnodesForIO();
        brain_genome.FinalizeGenome();

        CPPNGenome body_genome = new(CPPNtype.Body);
        body_genome.SetCPPNnodesForIO();
        body_genome.FinalizeGenome();

        BrainBodyDualGenome genome = new(brain_genome, body_genome);

        return genome;
    }

    public CPPNnode AddDisconnectedNode(CPPNFunction function = CPPNFunction.HyperTangent)
    {
        CPPNnode new_node = new(ID: this.cppn_nodes.Count, function);
        this.cppn_nodes.Add(new_node);
        return new_node;
    }


    public CPPNGenome Clone()
    {
        CPPNGenome genome = new(this.cppn_type);
        genome.CPPN_IO_IDXS = this.CPPN_IO_IDXS;

        foreach (CPPNnode n in this.cppn_nodes)
        {
            genome.cppn_nodes.Add(n.Clone());
        }
        foreach (CPPNconnection c in this.cppn_connections)
        {
            genome.cppn_connections.Add(c.Clone());
        }
        genome.FinalizeGenome();

        genome.generation = this.generation;

        return genome;


    }



    public static CPPNFunction GetRandomCPPNfunction()
    {
        System.Random sysrnd = new();
        if (INCLUDE_EXTRA_CPPN_FUNCTIONS) return (CPPNFunction)sysrnd.Next(0, Enum.GetNames(typeof(CPPNFunction)).Length);
        else return (CPPNFunction)sysrnd.Next(0, (int)CPPNFunction.Cube);
    }

    public void ChangeRandomCPPNNodeFunction()
    {
        if (CPPN_IO_IDXS.y == this.cppn_nodes.Count) return;
        CPPNnode node = this.cppn_nodes[UnityEngine.Random.Range(CPPN_IO_IDXS.y, this.cppn_nodes.Count)];
        node.function = GetRandomCPPNfunction();
    }

    public void DisableRandomCPPNConnection()
    {
        if (this.cppn_connections.Count == 0) return;
        CPPNconnection connection = this.cppn_connections[UnityEngine.Random.Range(0, this.cppn_connections.Count)];
        connection.enabled = false;
    }

    public void Mutate()
    {
        bool should_mutate;
        float rnd;
        // first, mutate synapse parameters
        foreach (CPPNconnection connection in this.cppn_connections)
        {
            should_mutate = Range(0f, 1f) < CHANCE_TO_MUTATE_EACH_CONNECTION;
            if (!should_mutate) continue;
            rnd = Range(0f, 1f);

            if (rnd < 0.9)
            {
                connection.weight += UnityEngine.Random.Range(-WEIGHT_MUTATE_INCREMENT, WEIGHT_MUTATE_INCREMENT);
            }
            else
            {
                connection.weight = GetRandomInitialCPPNWeight();
            }

            // constrain weights
            if (CONSTRAIN_CPPN_WEIGHTS)
            {
                connection.weight = math.max(connection.weight, -1);
                connection.weight = math.min(connection.weight, 1);
            }


        }


        rnd = UnityEngine.Random.Range(0f,1f);

        // add connection?
        if (rnd < ADD_CONNECTION_MUTATION_RATE * RATE_MULTIPLIER)
        {
            AddNewRandomConnection();
        }

        rnd = UnityEngine.Random.Range(0f, 1f);
        // add node?
        if (rnd < ADD_NODE_MUTATION_RATE * RATE_MULTIPLIER)
        {
            AddNewRandomNode();
        }



        this.FinalizeGenome();
    }


    public void AddNewLocalitySeedNode(int from_ID1, int fromID2, int LEO_node_ID)
    {
        CPPNnode new_node = new(ID: this.cppn_nodes.Count, function: CPPNFunction.Gaussian);
        this.cppn_nodes.Add(new_node);
        CPPNconnection new_connectionA = new(ID: this.cppn_connections.Count, weight: 1, from_ID: from_ID1, to_ID: new_node.ID);
        this.cppn_connections.Add(new_connectionA);
        CPPNconnection new_connectionB = new(ID: this.cppn_connections.Count, weight: -1, from_ID: fromID2, to_ID: new_node.ID);
        this.cppn_connections.Add(new_connectionB);
        CPPNconnection new_connectionC = new(ID: this.cppn_connections.Count, weight: 1, from_ID: new_node.ID, to_ID: LEO_node_ID);
        this.cppn_connections.Add(new_connectionC);
    }

    public void AddNewRandomNode()
    {
        if (this.cppn_connections.Count == 0) return;
        int attempts = 0;
        CPPNconnection random_connection = this.cppn_connections[Range(0, this.cppn_connections.Count)];
        while (!random_connection.enabled && attempts < 1000)
        {
            random_connection = this.cppn_connections[Range(0, this.cppn_connections.Count)];
            attempts++;
            if (attempts >= 1000)
            {
                Debug.LogWarning("Could not add random node.");
                return;
            }
        }

        random_connection.enabled = false;
        CPPNnode new_node = new(ID: GetNextGlobalCPPNNeuronID(), function: GetRandomCPPNfunction());
        CPPNconnection new_connectionA = new(ID: GetNextGlobalCPPNSynapseID(), weight: 1, from_ID: random_connection.from_node_ID, to_ID: new_node.ID);
        CPPNconnection new_connectionB = new(ID: GetNextGlobalCPPNSynapseID(), weight: random_connection.weight, from_ID: new_node.ID, to_ID: random_connection.to_node_ID);
        this.cppn_connections.Add(new_connectionA);
        this.cppn_connections.Add(new_connectionB);

        this.cppn_nodes.Add(new_node);
    }

    public void AddNewRandomConnection()
    {
        int num_of_outputs = (CPPN_IO_IDXS.y - CPPN_IO_IDXS.x);
        int from_idx = Range(0, this.cppn_nodes.Count - num_of_outputs);
        if (from_idx >= CPPN_IO_IDXS.x)
        {
            // its an output, cant connect from an output
            from_idx += num_of_outputs;
        }
        int to_idx = Range(CPPN_IO_IDXS.x, this.cppn_nodes.Count); // can go to outputs or hidden nodes, but not inputs
        CPPNnode from_neuron = (CPPNnode)this.cppn_nodes[from_idx];
        CPPNnode to_neuron = (CPPNnode)this.cppn_nodes[to_idx];

        if (!ALLOW_EVOLVING_RECURRENT_CONNECTIONS_IN_CPPN)
        {
            int attempts = 0;
            // try to find another connection if the randomly generated one is recurrent
            while (to_neuron.layer_idx <= from_neuron.layer_idx && attempts < 100)
            {
                from_idx = Range(0, this.cppn_nodes.Count);
                to_idx = Range(0, this.cppn_nodes.Count);
                from_neuron = (CPPNnode)this.cppn_nodes[from_idx];
                to_neuron = (CPPNnode)this.cppn_nodes[to_idx];
                attempts++;
            }

            if (to_neuron.layer_idx <= from_neuron.layer_idx) return;
        }


        CPPNconnection new_connection = new(from_ID: from_neuron.ID, to_ID: to_neuron.ID, weight: GetRandomInitialCPPNWeight(), ID: GetNextGlobalCPPNSynapseID());
        this.cppn_connections.Add(new_connection);
    }

    public float GetRandomInitialCPPNWeight()
    {
        if (CONSTRAIN_CPPN_WEIGHTS) return Range(-1f, 1f);
        else return Range(-3f, 3f);
    }

    public (CPPNGenome, CPPNGenome) Reproduce(CPPNGenome genome_parent2)
    {
        CPPNGenome parent1;
        CPPNGenome parent2;
        CPPNGenome offspring1;
        CPPNGenome offspring2;


        parent1 = this;
        parent2 = genome_parent2;
        offspring1 = new(this.cppn_type);
        offspring2 = new(this.cppn_type);

        int rnd;

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

           //re-enable connections
            if ((connection1 != null && !connection1.enabled) || (connection2 != null && !connection2.enabled))
            {
                offspring1.cppn_connections[^1].enabled = Range(0f, 1.0f) < 0.75f ? false : true;
                offspring2.cppn_connections[^1].enabled = Range(0f, 1.0f) < 0.75f ? false : true;
            }


        }

        offspring1.CPPN_IO_IDXS = parent1.CPPN_IO_IDXS;
        offspring2.CPPN_IO_IDXS = parent1.CPPN_IO_IDXS;

        offspring1.FinalizeGenome();
        offspring2.FinalizeGenome();



        return (offspring1, offspring2);
    }



    public const string save_file_extension = ".HyperNEATBrainGenome";
    public void SaveToDisk(string filename="")
    {
        string[] existing_saves = Directory.GetFiles(path: GlobalConfig.save_file_path, searchPattern: GlobalConfig.save_file_base_name + "*" + save_file_extension);
        int num_files = existing_saves.Length;
        if(filename.Length == 0) filename = GlobalConfig.save_file_path + GlobalConfig.save_file_base_name + num_files.ToString();
        string full_path = filename + save_file_extension;
        Debug.Log("Saving brain genome to disk: " + full_path);
        StreamWriter data_file;
        data_file = new(path: full_path, append: false);

        BinaryFormatter formatter = new BinaryFormatter();
        //object[] objects_to_save = new object[] { CPPN_nodes, CPPN_connections };
        object[] objects_to_save = new object[] { cppn_nodes, cppn_connections, this.CPPN_IO_IDXS };
        formatter.Serialize(data_file.BaseStream, objects_to_save);
        data_file.Close();
        Debug.Log("Saved brain genome successfully!");
    }

    public static CPPNGenome LoadFromDisk(string filename = "")
    {

        if (filename == "")
        {
            string[] existing_saves = Directory.GetFiles(path: GlobalConfig.save_file_path, searchPattern: GlobalConfig.save_file_base_name + "*" + save_file_extension);
            filename = existing_saves[^1];
        }

        Debug.Log("Loading file from disk: " + filename);
        //CPPNnodeParallel[] CPPN_nodes = null;
        //CPPNconnectionParallel[] CPPN_connections = null;
        List<CPPNnode> CPPN_nodes = null;
        List<CPPNconnection> CPPN_connections = null;
        int2 CPPN_IO_IDXS = int2.zero;

        BinaryFormatter formatter = new BinaryFormatter();
        string full_path = filename;
        // loading
        using (FileStream fs = File.Open(full_path, FileMode.Open))
        {
            object obj = formatter.Deserialize(fs);
            // = new object[] { this.current_state_neurons.ToArray(), this.current_state_synapses.ToArray() };
            var newlist = (object[])obj;
            int i = 0;
            for (i = 0; i < newlist.Length; i++)
            {
                if (i == 0)
                {
                    CPPN_nodes = (List<CPPNnode>)newlist[i];//(CPPNnodeParallel[])newlist[i];
                }
                else if (i == 1)
                {
                    CPPN_connections = (List<CPPNconnection>)newlist[i];//(CPPNconnectionParallel[])newlist[i];
                }
                else if (i == 2)
                {
                    CPPN_IO_IDXS = (int2)newlist[i];//(CPPNconnectionParallel[])newlist[i];
                }
                else
                {
                    Debug.LogWarning("ERROR LOADING BRAIN");
                }

            }

            if (i != 3) Debug.LogError("ERROR LOADING BRAIN");
        }

        if (GlobalConfig.GENOME_METHOD != GenomeMethod.CPPN) Debug.LogError("Fix for dual CPPNs");
        CPPNGenome genome = new(CPPNtype.BrainAndBody);
        genome.cppn_nodes = CPPN_nodes;
        genome.cppn_connections = CPPN_connections;
        genome.CPPN_IO_IDXS = CPPN_IO_IDXS;
        genome.FinalizeGenome();

        return genome;
    }

    // must call this function when all CPPN nodes and connections are set, and whenever they have been modified
    // it identifies the layers of each node, using either depth- or breadth- first search.
    // Then, it converts them to a format where they can be operated by parallel processing

    public void FinalizeGenome()
    {
        // reset nodes
        foreach (List<CPPNnode> layer in this.layers)
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
                Debug.LogError("Duplicate nodes " + n.ID);
            }
            ID_to_idx[n.ID] = j;
            n.inputs.Clear();
            n.outputs.Clear();
            n.layer_idx = INVALID_CPPN_LAYER_ID;
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
        for (int i = 0; i < CPPN_IO_IDXS.x; i++)
        {
            this.cppn_nodes[i].layer_idx = SENSORY_LAYER_ID; // sensor layer
            nodes_to_explore.Push(this.cppn_nodes[i]);
        }

        for (int i = CPPN_IO_IDXS.x; i < CPPN_IO_IDXS.y; i++)
        {
            this.cppn_nodes[i].layer_idx = OUTPUT_TEMP_LAYER_ID; // motor layer
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
                    if (output_node.layer_idx == OUTPUT_TEMP_LAYER_ID || output_node.layer_idx == SENSORY_LAYER_ID || output_node == node) continue;
                    if (output_node.layer_idx == INVALID_CPPN_LAYER_ID || (output_node.layer_idx > node.layer_idx + 1))
                    {
                        output_node.layer_idx = node.layer_idx + 1;
                        max_hidden_layer = Math.Max(max_hidden_layer, output_node.layer_idx);
                        if (!nodes_to_explore.Contains(output_node)) nodes_to_explore.Push(output_node);
                    }

                }
            }
        }
        else
        {
            // depth first search, produces the longest path to each node
            Dictionary<CPPNnode, bool> in_cycle = new(); // to prevent infinite loops
            void Explore(CPPNnode node)
            {
                in_cycle[node] = true;
                foreach ((CPPNnode, CPPNconnection) c in node.outputs)
                {
                    CPPNnode output_node = c.Item1;
                    if (output_node.layer_idx == OUTPUT_TEMP_LAYER_ID || output_node.layer_idx == SENSORY_LAYER_ID || output_node == node) continue;
                    bool output_node_not_contained_in_cycle = !in_cycle.ContainsKey(output_node) || !in_cycle[output_node];
                    if (output_node.layer_idx == INVALID_CPPN_LAYER_ID || (output_node.layer_idx < node.layer_idx + 1 && output_node_not_contained_in_cycle))
                    {
                        output_node.layer_idx = node.layer_idx + 1;
                        max_hidden_layer = Math.Max(max_hidden_layer, output_node.layer_idx);

                        Explore(output_node);

                        //if (!nodes_to_explore.Contains(output_node) && output_node != node) nodes_to_explore.Push(output_node);
                    }

                }
                in_cycle[node] = false;
            }

            while (nodes_to_explore.Count != 0)
            {
                CPPNnode node = nodes_to_explore.Pop();
                Explore(node);
            }
        }


        int output_layer = max_hidden_layer + 1;
        for (int i = CPPN_IO_IDXS.x; i < CPPN_IO_IDXS.y; i++)
        {
            this.cppn_nodes[i].layer_idx = output_layer; // motor layer
        }


        //

        for (int i = 0; i <= output_layer; i++)
        {
            this.layers.Add(new List<CPPNnode>());
        }



        foreach (CPPNnode n in this.cppn_nodes)
        {
            if (n.layer_idx == INVALID_CPPN_LAYER_ID) continue;
            this.layers[n.layer_idx].Add(n);
        }


        // convert to nativearrays
        this.CleanCPPNAndConvertToParallel();

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
        public int layer;
    }


    [Serializable]
    public class CPPNnode
    {
        public List<(CPPNnode, CPPNconnection)> inputs;
        public List<(CPPNnode, CPPNconnection)> outputs;

        public int ID;
        public CPPNFunction function;
        public int layer_idx;

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
                    activation = 1.0f / (1.0f + math.exp(-sum));
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
                case CPPNFunction.Triangle:
                    activation = math.abs((sum % 4) - 2) - 1;
                    break;
                case CPPNFunction.Sawtooth:
                    activation = 2 * ((sum/2) - math.floor(0.5f + (sum/2)));
                    break;
                case CPPNFunction.Sign:
                    if (sum >= 0)
                    {
                        activation = 1;
                    }
                    else
                    {
                        activation = -1;
                    }
                    break;
                case CPPNFunction.Step:
                    if (sum > 0)
                    {
                        activation = 1;
                    }
                    else
                    {
                        activation = 0;
                    }
                    break;
                case CPPNFunction.ReLU:
                    activation = sum > 0 ? sum : 0;
                    break;
                case CPPNFunction.LeakyReLU:
                    activation = sum > 0 ? sum : sum * 0.1f;
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
                    if (sum < 0) activation *= -1;
                    break;
                case CPPNFunction.Exponential:
                    activation = math.exp(sum);
                    break;
                case CPPNFunction.Log:
                    activation = math.log(sum);
                    break;
                case CPPNFunction.Modulus1:
                    activation = math.fmod(sum, 1);
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



    static float EvaluateCPPNNode(CPPNnodeParallel node, float[] CPPN_nodes_outputs, NativeArray<CPPNconnectionParallel> CPPN_connections)
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

    public struct CPPNOutput
    {
        // morphology
        public RobotVoxel robot_voxel_material;

        // neuron
        public float neuron_bias;
        public float sigmoid_alpha;
        public Brain.Neuron.ActivationFunction neuron_activation_function;

        // synapse
        public float initial_weight;
        public bool excitatory;
        public bool link_expression_output;

        public float synapse_decay_rate;

        public CPPNOutput(bool dummy_var=true)
        {
            robot_voxel_material = RobotVoxel.Empty;
            neuron_bias = 1;
   
            sigmoid_alpha = 1;
            initial_weight = 1;
            this.link_expression_output = true;
            this.excitatory = true;
            this.synapse_decay_rate = 1;
            neuron_activation_function = Brain.Neuron.ActivationFunction.Tanh;
        }

        public static CPPNOutput GetDefault()
        {
            return new CPPNOutput(true);
        }
    }



    // Converts the CPPN a format where it can be operated by parallel processing
    public void CleanCPPNAndConvertToParallel()
    {
        // clear old data if it exists
        this.CPPN_nodes.Dispose();
        this.CPPN_connections.Dispose();


        bool ShouldRemoveConnection(int layer_idx, CPPNnode incoming_node, CPPNconnection incoming_connection)
        {
            if (!incoming_connection.enabled) return true;
            if (incoming_node.layer_idx >= layer_idx) return true; // can't accept input from a later layer, since it hasn't been computed yet
            if (incoming_node.layer_idx == INVALID_CPPN_LAYER_ID) return true; // the input node has no inputs for itself, so its activation is zero and can be ignored
            return false;
        }

        // create parallelizable nodes
        this.CPPN_nodes = new(this.cppn_nodes.Count, Allocator.Persistent);
        Dictionary<CPPNnode, int> node_to_array_idx = new();
        int i = 0;
        int total_num_of_synapses_created = 0;

        // add sensor layer, output layer, then all the hidden layers
        List<int> layer_idxs = new() { 0, this.layers.Count - 1 };
        for (int layer_idx = 1; layer_idx < this.layers.Count-1; layer_idx++)
        {
            layer_idxs.Add(layer_idx);
        }
        foreach (int layer_idx in layer_idxs)
        {
            List<CPPNnode> layer = this.layers[layer_idx];
            foreach (CPPNnode node in layer)
            {
                CPPNnodeParallel parallel_node = new();
                parallel_node.input_connection_start_idx = total_num_of_synapses_created;
                parallel_node.function = (int)node.function;
                parallel_node.layer = layer_idx;
                int number_of_inputs = 0;
                foreach ((CPPNnode incoming_node, CPPNconnection incoming_connection) in node.inputs)
                {
                    if (ShouldRemoveConnection(layer_idx, incoming_node, incoming_connection)) continue;
                    number_of_inputs++;
                }

                parallel_node.number_of_input_connections = number_of_inputs;

                this.CPPN_nodes[i] = parallel_node;
                node_to_array_idx[node] = i;

                total_num_of_synapses_created += number_of_inputs;
                i++;
            }

        }




        // normalize
        Dictionary<CPPNnode, float> euclidean_norms = new();
        if (NORMALIZE_CPPN_WEIGHTS)
        {
            foreach (int layer_idx in layer_idxs)
            {
                List<CPPNnode> layer = this.layers[layer_idx];
                foreach (CPPNnode node in layer)
                {
                    euclidean_norms[node] = 0;
                    foreach ((CPPNnode incoming_node, CPPNconnection incoming_connection) in node.inputs)
                    {
                        if (ShouldRemoveConnection(layer_idx, incoming_node, incoming_connection)) continue;
                        euclidean_norms[node] += math.pow(incoming_connection.weight,2);
                    }
                    euclidean_norms[node] = math.sqrt(euclidean_norms[node]);
                }
            }
        }
 



        // finalize synapses
        this.CPPN_connections = new(total_num_of_synapses_created, Allocator.Persistent);
        i = 0;
        foreach (int layer_idx in layer_idxs)
        {
            List<CPPNnode> layer = this.layers[layer_idx];
            foreach (CPPNnode node in layer)
            {
                foreach ((CPPNnode incoming_node, CPPNconnection incoming_connection) in node.inputs)
                {
                    if (ShouldRemoveConnection(layer_idx, incoming_node, incoming_connection)) continue;
                    CPPNconnectionParallel parallel_connection = new();
                    parallel_connection.from_idx = node_to_array_idx[incoming_node];
                    parallel_connection.weight = incoming_connection.weight;
                    if (NORMALIZE_CPPN_WEIGHTS) parallel_connection.weight /= euclidean_norms[node];
                    this.CPPN_connections[i] = parallel_connection;
                    i++;
                    total_num_of_synapses_created--;
                }
            }
        }

        if (total_num_of_synapses_created != 0) Debug.LogError("Did not create all synapses.");
    }

    public static void NormalizeSynapseWeights(NativeArray<Synapse> next_state_synapses, int start_idx, int end_idx, float euclidean_norm)
    {
        for (int j = start_idx; j < end_idx; j++)
        {
            Synapse connection = next_state_synapses[j];
            if (!connection.IsEnabled()) continue;
            connection.weight /= euclidean_norm;
            next_state_synapses[j] = connection;
        }
    }



    public void GPU_Setup()
    {
        throw new System.NotImplementedException();

    }

    public (ComputeBuffer, ComputeBuffer) DevelopGPUBrain()
    {
        throw new System.NotImplementedException();
    }


    [Serializable]
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
            bool enabled = true)
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

    static int NEXT_GLOBAL_CONNECTION_ID = INVALID_CPPN_LAYER_ID;
    static int NEXT_GLOBAL_NODE_ID = INVALID_CPPN_LAYER_ID;


    static int GetNextGlobalCPPNSynapseID()
    {
        int ID = NEXT_GLOBAL_CONNECTION_ID;
        NEXT_GLOBAL_CONNECTION_ID++;
        return ID;
    }
    static int GetNextGlobalCPPNNeuronID()
    {
        int ID = NEXT_GLOBAL_NODE_ID;
        NEXT_GLOBAL_NODE_ID++;
        return ID;
    }

    public void DisposeOfNativeCollections()
    {
        this.CPPN_nodes.Dispose();
        this.CPPN_connections.Dispose();
    }

    public enum CPPNFunction
    {
        [Description("LIN")] Linear,
        [Description("SIG")] Sigmoid,
        [Description("GAU")] Gaussian,
        [Description("SIN")] Sine,

        [Description("SQ")] Square,
        [Description("ABS")] Abs,
        [Description("STP")] Step,
        [Description("TANH")] HyperTangent,
        [Description("EXP")] Exponential,
        [Description("COS")] Cosine,
        [Description("SQR")] SquareRoot,
        [Description("SAW")] Sawtooth,
        [Description("TRI")] Triangle,
        [Description("RLU")] ReLU,
        [Description("LN")] Log,

        // truncate here if extra cppn functions are turned off
        [Description("CUB")] Cube,

        [Description("MOD")] Modulus1,

        [Description("SGN")] Sign,



        [Description("LKYRLU")] LeakyReLU,




        [Description("TAN")] Tangent,

        [Description("COSH")] HyperCosine,
        [Description("SINH")] HyperSine,


    }
}