using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Brain;

public class AxonalGrowthBrainGenome : BrainGenomeTree
{

    const int AXONAL_GROWTH_LOCAL_RANGE = 5;


    public AxonalGrowthBrainGenome() : base() {
        ProgramSymbolTree root = new(AxonalGrowthCellularInstruction.END);
        this.forest.Add(root);
        this.size = root.size;
    }

    public AxonalGrowthBrainGenome(ProgramSymbolTree root) : base(root) { }

    public AxonalGrowthBrainGenome(List<ProgramSymbolTree> trees) : base(trees) { }

    public static AxonalGrowthBrainGenome CreateTestGenome()
    {
        List<ProgramSymbolTree> forest = new();


        ProgramSymbolTree A, B, C, D, A0, B0, C0, D0, root;

        // Tree 0
        int how_many_trees = 7;
        forest.Add(GetDIVPST());
        forest.Add(GetDIVPST());
        forest.Add(GetDIVPST());
        forest.Add(GetDIVPST());
        forest.Add(GetDIVPST());
        forest.Add(GetDIVPST());
        forest.Add(GetDIVPST());

        return new AxonalGrowthBrainGenome(forest);
    }

    public static ProgramSymbolTree GetDIVPST()
    {
        ProgramSymbolTree A, B, C, D, A0, B0, C0, D0, root;
        int[] rand_args;

        

        A0 = GetJUMPPST();
        B0 = GetENDPST();
       

        C0 = GetENDPST();
        D0 = GetENDPST();

        rand_args = new int[] {
            UnityEngine.Random.Range(-AXONAL_GROWTH_LOCAL_RANGE, AXONAL_GROWTH_LOCAL_RANGE),
            UnityEngine.Random.Range(-AXONAL_GROWTH_LOCAL_RANGE, AXONAL_GROWTH_LOCAL_RANGE),
            UnityEngine.Random.Range(-AXONAL_GROWTH_LOCAL_RANGE, AXONAL_GROWTH_LOCAL_RANGE)
        };
        B = new(AxonalGrowthCellularInstruction.DIVISION, children: new ProgramSymbolTree[] { A0, B0 }, arguments: rand_args);

        rand_args = new int[] {
            UnityEngine.Random.Range(-AXONAL_GROWTH_LOCAL_RANGE, AXONAL_GROWTH_LOCAL_RANGE),
            UnityEngine.Random.Range(-AXONAL_GROWTH_LOCAL_RANGE, AXONAL_GROWTH_LOCAL_RANGE),
            UnityEngine.Random.Range(-AXONAL_GROWTH_LOCAL_RANGE, AXONAL_GROWTH_LOCAL_RANGE)
        };
        C = new(AxonalGrowthCellularInstruction.DIVISION, children: new ProgramSymbolTree[] { C0, D0 }, arguments: rand_args);

        rand_args = new int[] {
            UnityEngine.Random.Range(-AXONAL_GROWTH_LOCAL_RANGE, AXONAL_GROWTH_LOCAL_RANGE),
            UnityEngine.Random.Range(-AXONAL_GROWTH_LOCAL_RANGE, AXONAL_GROWTH_LOCAL_RANGE),
            UnityEngine.Random.Range(-AXONAL_GROWTH_LOCAL_RANGE, AXONAL_GROWTH_LOCAL_RANGE),
        };
        root = new(AxonalGrowthCellularInstruction.DIVISION, children: new ProgramSymbolTree[] { B, C }, arguments: rand_args);

        return root;
    }



    public void InsertHexapodSensorimotorNeurons(AxonalGrowthVoxelAutomaton axonalGrowthVoxelAutomaton)
    {
        // insert sensory neurons
        int3 sensor_coords = new int3(10, 5, 20);
        int3 motor_coords = new int3(20, 5, axonalGrowthVoxelAutomaton.automaton_dimensions.z - 20);
        for (int i = 0; i <= 20; i++) // 20 joints in hexapod
        {
            string joint_key = Animat.GetSensorimotorJointKey(i);
            // 3 for the motor
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: motor_coords, extradata: "MOTORLAYER_" + joint_key + "_LL");
            motor_coords += new int3(5, 0, 0);
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: motor_coords, extradata: "MOTORLAYER_" + joint_key + "_LR");
            motor_coords += new int3(5, 0, 0);
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: motor_coords, extradata: "MOTORLAYER_" + joint_key + "_R");
            motor_coords += new int3(-10, 2, 0);

            // 10 for the sensor
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: sensor_coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_LLL");
            sensor_coords += new int3(3, 0, 0);
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: sensor_coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_LLR");
            sensor_coords += new int3(3, 0, 0);
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: sensor_coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_LR");
            sensor_coords += new int3(3, 0, 0);
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: sensor_coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_RL");
            sensor_coords += new int3(3, 0, 0);
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: sensor_coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_RRL");
            sensor_coords += new int3(3, 0, 0);
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: sensor_coords, extradata: "SENSORLAYER_" + joint_key + "_TOUCHSENSE" + "_RRR");
            sensor_coords += new int3(3, 0, 0);
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: sensor_coords, extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_LL");
            sensor_coords += new int3(3, 0, 0);
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: sensor_coords, extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_LR");
            sensor_coords += new int3(3, 0, 0);
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: sensor_coords, extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_RL");
            sensor_coords += new int3(3, 0, 0);
            axonalGrowthVoxelAutomaton.InsertSensorimotorNeuron(coords: sensor_coords, extradata: "SENSORLAYER_" + joint_key + "_ROTATESENSE" + "_RR");
            sensor_coords += new int3(3, 0, 0);
            sensor_coords += new int3(-30, 2, 0);

        }
    }

    public override (NativeArray<Brain.Neuron>, NativeArray<Brain.Synapse>) DevelopCPU(Dictionary<string, Dictionary<string, int>> neuron_indices)
    {

        AxonalGrowthVoxelAutomaton axonalGrowthVoxelAutomaton = new AxonalGrowthVoxelAutomaton(this);
        InsertHexapodSensorimotorNeurons(axonalGrowthVoxelAutomaton);

        // compute automaton
        axonalGrowthVoxelAutomaton.CalculateAutomatonToEnd();

        List<DevelopmentNeuron> developed_brain = axonalGrowthVoxelAutomaton.developed_brain;


        List<Neuron> final_brain_neurons = new();
        List<Synapse> final_brain_synapses = new();

        int num_of_connections = 0;

        string[] strings;
        string neuron_type;
        string sensor_type;

        // first turn all the cells into neurons
        for (int i = 0; i < developed_brain.Count; i++)
        {
            TreeDevelopmentNeuron cell = (TreeDevelopmentNeuron)developed_brain[i];

            Neuron neuron = new Neuron(threshold: cell.threshold,
                bias: cell.bias,
                adaptation_delta: cell.adaptation_delta,
                decay_rate_tau: cell.decay,
                sign: cell.sign,
                sigmoid_alpha: cell.sigmoid_alpha);

     

            if (cell.extradata != "")
            {
                strings = cell.extradata.Split("_");
                neuron_type = strings[strings.Length - 1];
                sensor_type = strings[strings.Length - 2];

                if (sensor_type != "TOUCHSENSE" && sensor_type != "ROTATESENSE")
                {
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
                else {
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


            final_brain_neurons.Add(neuron);

            foreach (TreeDevelopmentSynapse c in cell.inputs)
            {
                if (c.from_pointer is TreeDevelopmentNeuron)
                {
                    num_of_connections++;
                }
            }
        }


        // and all connections
        int synapse_idx = 0;
        for (int i = 0; i < developed_brain.Count; i++)
        {
            TreeDevelopmentNeuron cell = (TreeDevelopmentNeuron)developed_brain[i];
            Neuron neuron = final_brain_neurons[i];

            neuron.synapse_start_idx = synapse_idx;

            foreach (TreeDevelopmentSynapse c in cell.inputs)
            {
                Synapse connection = new Synapse(learning_rate: c.learning_rate,
                    from_neuron_idx: developed_brain.IndexOf((TreeDevelopmentNeuron)c.from_pointer),
                    to_neuron_idx: -1,
                    coefficients: c.coefficients);

                final_brain_synapses.Add(connection);

                synapse_idx++;
                neuron.synapse_count++;

            }
            final_brain_neurons[i] = neuron;


        }

        return (final_brain_neurons.ToNativeArray<Neuron>(Allocator.Persistent), final_brain_synapses.ToNativeArray<Synapse>(Allocator.Persistent));

    }

    public override ProgramSymbolTree GenerateRandomMutation()
    {
        int tree_length = UnityEngine.Random.Range(1, MUTATION_TREE_DEPTH+1);

        ProgramSymbolTree root = default;
        ProgramSymbolTree previous = default;
        for (int i = 0; i < tree_length; i++)
        {
            AxonalGrowthCellularInstruction instruction = AxonalGrowthBrainGenome.GetRandomInstruction();

            int[] arguments = GenerateRandomArguments(instruction);

            ProgramSymbolTree node = new ProgramSymbolTree(instruction: instruction,
                children: new ProgramSymbolTree[0],
                arguments: arguments);

            if (i == 0)
            {
                root = node;
            }
            else
            {
                previous.children = new ProgramSymbolTree[] { node };
            }
            previous = node;
        }

        return root;
    }



    public static AxonalGrowthCellularInstruction GetRandomInstruction()
    {
        System.Random sysrnd = new();
        return (AxonalGrowthCellularInstruction)sysrnd.Next(0, Enum.GetNames(typeof(AxonalGrowthCellularInstruction)).Length);
    }

    public enum AxonalGrowthCellularInstruction
    {
        // Division symbols
        [Description("DIVI")] DIVISION, // divide into a seperate cell
        [Description("CLONE")] CLONE, // divide into a seperate cell, but a clone
        [Description("GROW")] GROW, // produce output to a neuron at a certain position
        [Description("DRAW")] DRAW, // bring input from a neuron at a certain position
                                    //[Description("SWAP")] SWAP, // swap an input link to come from a neuron at a new position

        // Register symbols
        [Description("INCLR")] INCREMENT_LINK_REGISTER,
        [Description("DECLR")] DECREMENT_LINK_REGISTER,
        /*        INCREMENT_THRESHOLD,
                DECREMENT_THRESHOLD,*/
        [Description("INCBS")] INCREMENT_BIAS,
        [Description("DECBS")] DECREMENT_BIAS,

        // Sign of Hebb coefficients
        [Description("SIGNLR")] SIGN_LR,
        [Description("SIGNA")] SIGN_A,
        [Description("SIGNB")] SIGN_B,
        [Description("SIGNC")] SIGN_C,
        [Description("SIGND")] SIGN_D,

        // Increment values on Link, pointed to by link register
        [Description("INCA")] INCREMENT_A,
        [Description("DECA")] DECREMENT_A,
        [Description("INCB")] INCREMENT_B,
        [Description("DECB")] DECREMENT_B,
        [Description("INCC")] INCREMENT_C,
        [Description("DECC")] DECREMENT_C,
        [Description("INCD")] INCREMENT_D,
        [Description("DECD")] DECREMENT_D,
        [Description("INCLN")] INCREMENT_LEARNING_RATE,
        [Description("DECLN")] DECREMENT_LEARNING_RATE,
        /*        INCREMENT_VOLTAGE_DECAY,
                DECREMENT_VOLTAGE_DECAY,*/
        [Description("INCSG")] INCREMENT_SIGMOID_ALPHA,
        [Description("DECSG")] DECREMENT_SIGMOID_ALPHA,

        // Other
        //[Description("CLIP")] CLIP, // cut the connection at the Link Register index
        [Description("SIGN")] SIGN, // change sign of output

        [Description("JUMP")] JUMP, // pointer jumps to a new tree, like a function call.
        [Description("WAIT")] WAIT, // pointer jumps to a new tree, like a function call.

        [Description("END")] END // cell becomes a final neuron
    }

    public static int HowManyArguments(object instruction)
    {
        switch ((AxonalGrowthCellularInstruction)instruction)
        {
            case AxonalGrowthCellularInstruction.DIVISION:
                return 3;
            case AxonalGrowthCellularInstruction.CLONE:
            case AxonalGrowthCellularInstruction.GROW:
            case AxonalGrowthCellularInstruction.DRAW:
                return 3;
            case AxonalGrowthCellularInstruction.JUMP:
                return 1;
            default:
                return 0;
        }
    }

    public static int HowManyChildren(object instruction)
    {
        switch ((AxonalGrowthCellularInstruction)instruction)
        {
            case AxonalGrowthCellularInstruction.END:
                return 0;
            case AxonalGrowthCellularInstruction.JUMP:
                return 1;
            case AxonalGrowthCellularInstruction.DIVISION:
                return 2;
            default:
                return 1;
        }
    }

    public override int[] ArgumentRange(object instruction)
    {
        switch ((AxonalGrowthCellularInstruction)instruction)
        {
            case AxonalGrowthCellularInstruction.DIVISION:
            case AxonalGrowthCellularInstruction.CLONE:
            case AxonalGrowthCellularInstruction.GROW:
            case AxonalGrowthCellularInstruction.DRAW:
                return new int[] { -AXONAL_GROWTH_LOCAL_RANGE, AXONAL_GROWTH_LOCAL_RANGE };
            case AxonalGrowthCellularInstruction.JUMP:
                return new int[] { -this.forest.Count, this.forest.Count };
            default:
                return new int[0];
        }
    }

    public override (ComputeBuffer, ComputeBuffer) DevelopGPU(Dictionary<string, Dictionary<string, int>> neuron_indices)
    {
        throw new NotImplementedException();
    }

    public override JobHandle ScheduleDevelopCPUJob()
    {
        throw new NotImplementedException();
    }

    public override void ScheduleDevelopGPUJob()
    {
        throw new NotImplementedException();
    }
}
