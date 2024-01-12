using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Brain;
using static Brain.Neuron;

public class ESHyperNEATBrainGenome : HyperNEATBrainGenome
{
    bool USE_SCALAR_VARIANCE = true;

    // quadtree resolution
    int min_depth = 1;
    int max_depth = 2;

    float DIVISION_THRESHOLD = 2.5f;
    float VARIANCE_THRESHOLD = 2.2f;
    float BAND_THRESHOLD = 0.05f;

    // depth of hidden nodes
    int ITERATION_LEVEL = 2;

    float3 cppn_special_point;

    List<(DevelopmentNeuron, float3)> input_positions;
    List<(DevelopmentNeuron, float3)> output_positions;
    public Dictionary<string, ESNeuron> hidden_positions;

    public ESHyperNEATBrainGenome() : base()
    {
        this.cppn_special_point = new float3(0, 0, 0);
        this.substrate_dimensions = new int3(this.num_of_joints, this.num_of_joints, this.num_of_joints);

        // evolvable substrate is stored implicitly
        // sensory-motor neuron positions are stored explicitly to identify them
        this.input_positions = new();
        this.output_positions = new();
        this.hidden_positions = new();

        this.InsertHexapodSensorimotorNeurons();
    }

    public static ESHyperNEATBrainGenome CreateTestGenome()
    {
        ESHyperNEATBrainGenome genome = new();
        genome.SetCPPNnodesForIO();
        genome.FinalizeCPPN();
        return genome;
    }

    public override JobHandle ScheduleDevelopCPUJob()
    {
        return new JobHandle();
    }


    public ESNeuron GetESNeuronFromCoords(float3 coords)
    {
        string key = GetNeuronKeyFromCoords(coords);

        if (!this.hidden_positions.ContainsKey(key))
        {
            this.hidden_positions[key] = new(coords);
        }
        return this.hidden_positions[key];
    }

    public string GetNeuronKeyFromCoords(float3 coords)
    {
        string x = coords.x.ToString("0.000000000");
        string y = coords.y.ToString("0.000000000");
        string z = coords.z.ToString("0.000000000");
        return x + ";" + y + ";" + z;
    }

    public override (NativeArray<Neuron>, NativeArray<Synapse>) DevelopCPU(Dictionary<string, Dictionary<string, int>> neuron_indices)
    {
        Dictionary<string, int> coordinates_to_native_neuron_idx = new();
        List<Neuron> native_neurons = new();
        List<Synapse> native_synapses = new();

        void CreateHiddenNeuron(float3 coords)
        {
            string key = GetNeuronKeyFromCoords(coords);
            if (coordinates_to_native_neuron_idx.ContainsKey(key)) return;
            coordinates_to_native_neuron_idx.Add(key, native_neurons.Count);
            CPPNOutputArray value = QueryCPPN(coords.x, coords.y, coords.z,
                                                                cppn_special_point.x, cppn_special_point.y, cppn_special_point.z);
            Neuron neuron = new(threshold: 0,
                bias: value.bias,
                adaptation_delta: 1.0f,
                decay_rate_tau: 1.0f,
                sigmoid_alpha: value.sigmoid_alpha,
                sign: value.sign == 1,
                type: NeuronType.Perceptron,
                activation_function: value.activation_function);
            neuron.position = coords * 10f;
            native_neurons.Add(neuron);
        }

        // add sensory neurons
        foreach ((DevelopmentNeuron development_neuron, float3 coordinates) in this.input_positions)
        {
            int native_neuron_idx = native_neurons.Count;

            coordinates_to_native_neuron_idx.Add(GetNeuronKeyFromCoords(coordinates), native_neuron_idx);
            Neuron sensory_neuron = new(threshold: 0,
                                        bias: 0,
                                        adaptation_delta: 1.0f,
                                        decay_rate_tau: 1.0f,
                                        sigmoid_alpha: 1.0f,
                                        sign: true,
                                        type: NeuronType.Perceptron,
                                        activation_function: NeuronActivationFunction.Tanh);
            sensory_neuron.neuron_class = NeuronClass.Sensor;
            sensory_neuron.synapse_start_idx = 0;
            sensory_neuron.synapse_count = 0;
            sensory_neuron.position = coordinates * 10f;
            native_neurons.Add(sensory_neuron);

            LabelSensoryMotorNeuron(development_neuron, native_neuron_idx, neuron_indices);
        }

        // create default motor neurons and label them
        foreach ((DevelopmentNeuron development_neuron, float3 coordinates) in this.output_positions)
        {
            int native_neuron_idx = native_neurons.Count;

            coordinates_to_native_neuron_idx.Add(GetNeuronKeyFromCoords(coordinates), native_neuron_idx);
            CPPNOutputArray value = QueryCPPN(coordinates.x, coordinates.y, coordinates.z,
                                                                cppn_special_point.x, cppn_special_point.y, cppn_special_point.z);
            Neuron motor_neuron = new(threshold: 0,
                bias: value.bias,
                adaptation_delta: 1.0f,
                decay_rate_tau: 1.0f,
                sigmoid_alpha: value.sigmoid_alpha,
                sign: value.sign == 1,
                type: NeuronType.Perceptron,
                activation_function: NeuronActivationFunction.Tanh);
            motor_neuron.neuron_class = NeuronClass.Motor;
            motor_neuron.position = coordinates * 10f;
            native_neurons.Add(motor_neuron);

            LabelSensoryMotorNeuron(development_neuron, native_neuron_idx, neuron_indices);
        }

        (Dictionary<ESNeuron, List<ESSynapse>> input_to_hidden,
            Dictionary<ESNeuron, List<ESSynapse>> hidden_to_hidden,
            Dictionary<ESNeuron, List<ESSynapse>> hidden_to_output) = ESHyperNEATAlgorithm();


        // add hidden neurons
        var neuron_octpoints = new[] { input_to_hidden, hidden_to_hidden, hidden_to_output };
        foreach (Dictionary<ESNeuron, List<ESSynapse>> neuron_octpoint_dict in neuron_octpoints)
        {
            foreach (KeyValuePair<ESNeuron, List<ESSynapse>> hidden_neuron_octpoint in neuron_octpoint_dict)
            {

                List<ESSynapse> connections = hidden_neuron_octpoint.Value; // get the connections going into the OctPoint
                foreach (ESSynapse es_synapse in connections)
                {
                    CreateHiddenNeuron(es_synapse.input_coords);
                    CreateHiddenNeuron(es_synapse.output_coords);
                }
            }
        }

        // add connections
        Dictionary<int, List<Synapse>> neuron_synapses = new();
        foreach (Dictionary<ESNeuron, List<ESSynapse>> neuron_octpoint_dict in neuron_octpoints)
        {
            foreach (KeyValuePair<ESNeuron, List<ESSynapse>> neuron_octpoint in neuron_octpoint_dict)
            {
                List<ESSynapse> connections = neuron_octpoint.Value; // get the connections going into the OctPoint
                foreach (ESSynapse es_synapse in connections)
                {
                    int native_input_neuron_index = coordinates_to_native_neuron_idx[GetNeuronKeyFromCoords(es_synapse.input_coords)];
                    int native_output_neuron_index = coordinates_to_native_neuron_idx[GetNeuronKeyFromCoords(es_synapse.output_coords)];

                    CPPNOutputArray value = es_synapse.CPPNoutputs;

                    Synapse synapse = CPPNOutputArrayToNativeSynapse(value);
                    synapse.from_neuron_idx = native_input_neuron_index;
                    synapse.to_neuron_idx = native_output_neuron_index;
                    if (!neuron_synapses.ContainsKey(synapse.to_neuron_idx)) neuron_synapses[synapse.to_neuron_idx] = new();
                    neuron_synapses[synapse.to_neuron_idx].Add(synapse);

                }

            }
        }

        // associate neurons and connections
        for (int i = 0; i < native_neurons.Count; i++)
        {
            if (!neuron_synapses.ContainsKey(i)) continue;
            Neuron neuron = native_neurons[i];
            List<Synapse> synapses = neuron_synapses[i];
            neuron.synapse_start_idx = native_synapses.Count;
            neuron.synapse_count = synapses.Count;
            foreach (Synapse synapse in synapses)
            {
                native_synapses.Add(synapse);
            }
            native_neurons[i] = neuron;
        }

        //TODO
        return (native_neurons.ToNativeArray<Neuron>(Allocator.Persistent), native_synapses.ToNativeArray<Synapse>(Allocator.Persistent));
    }

    public void LabelSensoryMotorNeuron(DevelopmentNeuron cell, int cell_idx_flat, Dictionary<string, Dictionary<string, int>> neuron_indices)
    {
        string[] strings = cell.extradata.Split("_");
        string neuron_type = strings[strings.Length - 1];
        string sensor_type = strings[strings.Length - 2];

        if (sensor_type != "TOUCHSENSE" && sensor_type != "ROTATESENSE")
        {
            // connect to motor interface

            int tree_idx = -1;
            if (neuron_type == "LL")
            {
                tree_idx = 0;
            }
            else if (neuron_type == "LR")
            {
                tree_idx = 1;
            }
            else if (neuron_type == "R")
            {
                tree_idx = 2;
            }
            else
            {
                Debug.LogError("ERROR " + neuron_type);
            }
            neuron_indices[Brain.MOTOR_NEURON_KEY][cell.extradata[0..^neuron_type.Length] + tree_idx] = cell_idx_flat;
        }
        else
        {


            // connect to sensory interface
            int tree_idx = -1;

            if (sensor_type == "TOUCHSENSE")
            {
                if (neuron_type == "LLL")
                {
                    tree_idx = 0; // TOP
                }
                else if (neuron_type == "LLR")
                {
                    tree_idx = 1; // BOT
                }
                else if (neuron_type == "LR")
                {
                    tree_idx = 2; // LEFT
                }
                else if (neuron_type == "RL")
                {
                    tree_idx = 3; // RIGHT
                }
                else if (neuron_type == "RRL")
                {
                    tree_idx = 4; // FRONT
                }
                else if (neuron_type == "RRR")
                {
                    tree_idx = 5; // BACK
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
                    tree_idx = 6; // W
                }
                else if (neuron_type == "LR")
                {
                    tree_idx = 7; // X
                }
                else if (neuron_type == "RL")
                {
                    tree_idx = 8; // Y
                }
                else if (neuron_type == "RR")
                {
                    tree_idx = 9; // Z
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
            for (int m = 0; m < strings.Length - 2; m++)
            {
                key += strings[m] + "_";
            }
            key += tree_idx;

            neuron_indices[Brain.SENSORY_NEURON_KEY][key] = cell_idx_flat;

        }
    }

    public override void InsertHexapodSensorimotorNeurons()
    {

        // insert sensory neurons
        for (int x = 0; x < this.num_of_joints; x++) // joints in hexapod
        {
            string joint_key = Animat.GetSensorimotorJointKey(x);

            int3 coords = new int3(-this.num_of_joints + 2 * x, -this.num_of_joints, -this.num_of_joints);
            // 10 for the sensor
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_LLL");
            coords.y += 2;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_LLR");
            coords.y += 2;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_LR");
            coords.y += 2;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_RL");
            coords.y += 2;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_RRL");
            coords.y += 2;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_RRR");
            coords.y += 2;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_LL");
            coords.y += 2;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_LR");
            coords.y += 2;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_RL");
            coords.y += 2;
            this.InsertNeuron(coords, extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_RR");
        }


        // insert motor neurons
        for (int x = 0; x < this.num_of_joints; x++)
        {
            string joint_key = Animat.GetSensorimotorJointKey(x);

            // 3 for the motor
            int3 coords = new int3(-this.num_of_joints + 2 * x, -this.num_of_joints, this.substrate_dimensions.z - 1);
            this.InsertNeuron(coords, extradata: "MOTORLAYER_" + joint_key + "_LL");
            coords.y += 8;
            this.InsertNeuron(coords, extradata: "MOTORLAYER_" + joint_key + "_LR");
            coords.y += 8;
            this.InsertNeuron(coords, extradata: "MOTORLAYER_" + joint_key + "_R");

        }

    }

    public override void WriteToSubstrate(int x, int y, int z, DevelopmentNeuron neuron)
    {
        string neuron_sensorymotor_type = neuron.extradata.Split("_")[0];
        float3 neuron_position = new float3(
            (float)x / this.substrate_dimensions.x,
            (float)y / this.substrate_dimensions.y,
            (float)z / this.substrate_dimensions.z);
        if (neuron_sensorymotor_type == "SENSORLAYER")
        {
            this.input_positions.Add((neuron, neuron_position));
        }
        else if (neuron_sensorymotor_type == "MOTORLAYER")
        {
            this.output_positions.Add((neuron, neuron_position));
        }
        else
        {
            Debug.LogError("error");
        }
    }



    /// <summary>
    /// 
    /// </summary>
    /// <param name=""></param>
    /// <param name=""></param>
    /// <param name=""></param>
    public (Dictionary<ESNeuron, List<ESSynapse>>, Dictionary<ESNeuron, List<ESSynapse>>, Dictionary<ESNeuron, List<ESSynapse>>) ESHyperNEATAlgorithm()
    {
        Debug.Log("start ESHyperNEAT");

        /*
         *  Form input to hidden connections
         */
        Dictionary<ESNeuron, List<ESSynapse>> input_to_hidden_connections = new();
        foreach ((_, float3 input_position) in this.input_positions)
        {
            OctPoint root = DivisionAndInitialization(input_position.x, input_position.y, input_position.z, true);
            PruningAndExtraction(input_position.x, input_position.y, input_position.z, input_to_hidden_connections, root, true);
        }


        /*
         *  Form hidden to hidden connections
         */
        Dictionary<ESNeuron, List<ESSynapse>> hidden_to_hidden_connections = new();
        Stack<ESNeuron> nodes_to_explore = new(input_to_hidden_connections.Keys);
        HashSet<ESNeuron> explored_nodes = new();
        for (int i = 0; i < ITERATION_LEVEL; i++)
        {
            Stack<ESNeuron> new_nodes_to_explore = new();
            while (nodes_to_explore.Count > 0)
            {
                ESNeuron hidden_node = nodes_to_explore.Pop();
                float3 hidden_node_position = hidden_node.coordinates;

                OctPoint root = DivisionAndInitialization(hidden_node_position.x, hidden_node_position.y, hidden_node_position.z, true);
                PruningAndExtraction(hidden_node_position.x, hidden_node_position.y, hidden_node_position.z, hidden_to_hidden_connections, root, true);

                foreach (ESNeuron es_Neuron in hidden_to_hidden_connections.Keys)
                {
                    if (!explored_nodes.Contains(es_Neuron))
                    {
                        explored_nodes.Add(es_Neuron);
                        new_nodes_to_explore.Push(es_Neuron);
                    }
                }
            }

            nodes_to_explore = new_nodes_to_explore;
        }

        /*
         *  Form hidden to output connections
         */
        Dictionary<ESNeuron, List<ESSynapse>> hidden_to_output_connections = new();
        foreach ((_, float3 output_position) in this.output_positions)
        {
            OctPoint root = DivisionAndInitialization(output_position.x, output_position.y, output_position.z, false);
            PruningAndExtraction(output_position.x, output_position.y, output_position.z, hidden_to_output_connections, root, false);

        }
        //TODO: REMOVE ALL NEURONS AND CONNECTIONS THAT DONT HAVE A PATH TO AN INPUT AND OUTPUT NEURON
        Debug.Log("complete ESHyperNEAT");

        return (input_to_hidden_connections, hidden_to_hidden_connections, hidden_to_output_connections);
    }


    /// <summary>
    ///     The initial cube (from [-1,-1,-1] to [1,1,1]) is recursively split using Octrees. For each Octree node at point (x,y,z), 
    ///     the CPPN is queried from/to given point (a,b,c) to/from Octree point (x,y,z).
    /// 
    ///     Input: coordinates of source (outgoing=true) or target node (outgoing = false) at (a,b,c)
    ///     Output: Octree, in which each octnode at (x,y,z) stores CPPN outputs for its position.
    ///         The initialized octree is used in the PruningAndExtraction phase to generate the actual ANN connections.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="outgoing"></param>
    static int[] axis_multipliers = new int[] { -1, 1 };
    public OctPoint DivisionAndInitialization(float a, float b, float c, bool outgoing)
    {
        OctPoint root = new(x: 0, y: 0, z: 0, width: 1, level: 1);
        Queue<OctPoint> queue = new();
        queue.Enqueue(root);

        while (queue.Count != 0)
        {
            // get the midpoint p
            OctPoint p = queue.Dequeue();

            // divide 3D space into subregions and assign children to parent
            int child_idx = 0;
            foreach (int i in axis_multipliers)
            {
                foreach (int j in axis_multipliers)
                {
                    foreach (int k in axis_multipliers)
                    {
                        OctPoint child = new(
                            x: p.x + i * p.width / 2,
                            y: p.y + j * p.width / 2,
                            z: p.z + k * p.width / 2,
                            width: p.width / 2,
                            level: p.level + 1);

                        // now query CPPN value for input/output neuron (a,b,c) connection with hidden neuron 'p', located at (x,y,z)
                        if (outgoing)
                        {
                            child.value = QueryCPPN(a, b, c, child.x, child.y, child.z);
                        }
                        else
                        {
                            child.value = QueryCPPN(child.x, child.y, child.z, a, b, c);
                        }

                        p.children[child_idx] = child;
                        child_idx++;
                    }
                }
            }

            // divide until minimum resolution or if variance still too high
            float variance = calculate_variance(p);
            if (p.level < min_depth || (p.level < max_depth && variance > DIVISION_THRESHOLD))
            {
                foreach (OctPoint child in p.children)
                {
                    queue.Enqueue(child);
                }
            }


        }

        return root;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="connections"></param>
    /// <param name="p"></param>
    /// <param name="outgoing"></param>
    public void PruningAndExtraction(float a, float b, float c, Dictionary<ESNeuron, List<ESSynapse>> connections, OctPoint p, bool outgoing)
    {

        foreach (OctPoint child in p.children)
        {
            if (calculate_variance(child) >= VARIANCE_THRESHOLD)
            {
                PruningAndExtraction(a, b, c, connections, child, outgoing);
            }
            else
            {
                // determine if point is in a band by checking neighbor CPPN values
                float[] d = new float[6];
                if (outgoing)
                {
                    d[0] = CPPNOutputArray.Distance(child.value, QueryCPPN(a, b, c, child.x - p.width, child.y, child.z)); // left
                    d[1] = CPPNOutputArray.Distance(child.value, QueryCPPN(a, b, c, child.x + p.width, child.y, child.z)); // right
                    d[2] = CPPNOutputArray.Distance(child.value, QueryCPPN(a, b, c, child.x, child.y - p.width, child.z)); // bottom
                    d[3] = CPPNOutputArray.Distance(child.value, QueryCPPN(a, b, c, child.x, child.y + p.width, child.z)); // top
                    d[4] = CPPNOutputArray.Distance(child.value, QueryCPPN(a, b, c, child.x, child.y, child.z - p.width)); // back
                    d[5] = CPPNOutputArray.Distance(child.value, QueryCPPN(a, b, c, child.x, child.y, child.z + p.width)); // forward
                }
                else
                {
                    d[0] = CPPNOutputArray.Distance(child.value, QueryCPPN(child.x - p.width, child.y, child.z, a, b, c)); // left
                    d[1] = CPPNOutputArray.Distance(child.value, QueryCPPN(child.x + p.width, child.y, child.z, a, b, c)); // right
                    d[2] = CPPNOutputArray.Distance(child.value, QueryCPPN(child.x, child.y - p.width, child.z, a, b, c)); // bottom
                    d[3] = CPPNOutputArray.Distance(child.value, QueryCPPN(child.x, child.y + p.width, child.z, a, b, c)); // top
                    d[4] = CPPNOutputArray.Distance(child.value, QueryCPPN(child.x, child.y, child.z - p.width, a, b, c)); // back
                    d[5] = CPPNOutputArray.Distance(child.value, QueryCPPN(child.x, child.y, child.z + p.width, a, b, c)); // forward
                }
                float min_left_right = math.min(d[0], d[1]);
                float min_bottom_top = math.min(d[2], d[3]);
                float min_back_front = math.min(d[4], d[5]);
                float max_value = math.max(math.max(min_left_right, min_bottom_top), min_back_front);
                if (max_value > BAND_THRESHOLD)
                {
                    // Create new connection
                    ESSynapse new_connection;

                    if (outgoing)
                    {
                        // connection goes from input neuron to hidden, or from hidden to hidden
                        new_connection = new(input_coords: new float3(a, b, c),
                                     output_coords: new float3(child.x, child.y, child.z),
                                     CPPNoutputs: child.value);
                    }
                    else
                    {
                        // connection goes from hidden to output neuron
                        new_connection = new(input_coords: new float3(child.x, child.y, child.z),
                                    output_coords: new float3(a, b, c),
                                    CPPNoutputs: child.value);
                    }
                    ESNeuron to_es_neuron = GetESNeuronFromCoords(new_connection.output_coords);
                    if (!connections.ContainsKey(to_es_neuron)) connections[to_es_neuron] = new();
                    connections[to_es_neuron].Add(new_connection);
                }
            }
        }
    }

    public Synapse CPPNOutputArrayToNativeSynapse(CPPNOutputArray array)
    {
        Synapse synapse = new();
        synapse.learning_rate_r = array.learning_rate;
        synapse.coefficient_A = array.hebb_ABCD_coefficients[0];
        synapse.coefficient_B = array.hebb_ABCD_coefficients[1];
        synapse.coefficient_C = array.hebb_ABCD_coefficients[2];
        synapse.coefficient_D = array.hebb_ABCD_coefficients[3];
        synapse.weight = array.initial_weight;
        synapse.enabled = true;
        return synapse;
    }


    float calculate_variance(OctPoint p)
    {


        if (p.children[0] == null)
        {
            p.variance = 0; // leaf node
            return 0;
        }
        else
        {

            // calculate the mean of each variable
            CPPNOutputArray mean = CPPNOutputArray.GetNewDefault();

            float scalar_mean = 0;
            for (int i = 0; i < p.children.Length; i++)
            {
                OctPoint child = p.children[i];
                CPPNOutputArray value = child.value;
                mean.initial_weight += value.initial_weight;
                scalar_mean += value.initial_weight;
                mean.learning_rate += value.learning_rate;
                scalar_mean += value.learning_rate;
                for (int j = 0; j < 4; j++)
                {
                    mean.hebb_ABCD_coefficients[j] += value.hebb_ABCD_coefficients[j];
                    scalar_mean += value.hebb_ABCD_coefficients[j];
                }
                mean.bias += value.bias;
                scalar_mean += value.bias;
                mean.sigmoid_alpha += value.sigmoid_alpha;
                scalar_mean += value.sigmoid_alpha;
            }

            int num_children = p.children.Length;
            mean.initial_weight /= num_children;
            mean.learning_rate /= num_children;
            for (int j = 0; j < 4; j++)
            {
                mean.hebb_ABCD_coefficients[j] /= num_children;
            }
            mean.bias /= num_children;
            mean.sigmoid_alpha /= num_children;
            scalar_mean /= num_children;

            // use the mean to calculate the variance
            // TODO: is this the correct way?
            float total_variance = 0;
            float total_scalar_variance = 0;
            for (int i = 0; i < p.children.Length; i++)
            {
                float scalar_sum = 0;
                OctPoint child = p.children[i];
                CPPNOutputArray value = child.value;
                total_variance += math.pow(mean.initial_weight - value.initial_weight, 2) / num_children;
                scalar_sum += math.pow(mean.initial_weight - value.initial_weight, 2) / num_children;
                total_variance += math.pow(mean.learning_rate - value.learning_rate, 2) / num_children;
                scalar_sum += math.pow(mean.learning_rate - value.learning_rate, 2) / num_children;
                for (int j = 0; j < 4; j++)
                {
                    total_variance += 0;// math.pow(mean.hebb_ABCD_coefficients[j] - child.hebb_ABCD_coefficients[j], 2) / 4;
                }
                total_variance += math.pow(mean.bias - value.bias, 2) / num_children;
                scalar_sum += math.pow(mean.bias - value.bias, 2) / num_children;
                total_variance += math.pow(mean.sigmoid_alpha - value.sigmoid_alpha, 2) / num_children;
                scalar_sum += math.pow(mean.sigmoid_alpha - value.sigmoid_alpha, 2) / num_children;

                total_scalar_variance += math.pow(scalar_mean - scalar_sum, 2);
            }

            return USE_SCALAR_VARIANCE ? total_scalar_variance : total_variance;
        }


    }

    public override (ComputeBuffer, ComputeBuffer) DevelopGPU(Dictionary<string, Dictionary<string, int>> neuron_indices)
    {
        throw new System.NotImplementedException();
    }

    public override DevelopmentNeuron ReadFromSubstrate(int x, int y, int z)
    {
        throw new System.NotImplementedException();
    }

    public class ESSynapse
    {
        public float3 input_coords;
        public float3 output_coords;
        public CPPNOutputArray CPPNoutputs;

        public ESSynapse(float3 input_coords, float3 output_coords, CPPNOutputArray CPPNoutputs)
        {
            this.input_coords = input_coords;
            this.output_coords = output_coords;
            this.CPPNoutputs = CPPNoutputs;
        }
    }

    public class ESNeuron
    {
        public float3 coordinates;
        public ESNeuron(float3 coord)
        {
            this.coordinates = coord;
        }
    }



    public class OctPoint
    {
        public CPPNOutputArray value;
        public float x;
        public float y;
        public float z;
        public float width;
        public int level;
        public OctPoint[] children;

        public float variance;

        public OctPoint(float x, float y, float z, float width, int level)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.width = width;
            this.level = level;
            this.variance = 0;
            this.children = new OctPoint[8];
        }
    }


}
