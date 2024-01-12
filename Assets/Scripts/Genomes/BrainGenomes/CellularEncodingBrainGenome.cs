using System;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Brain;

public class CellularEncodingBrainGenome : BrainGenomeTree
{


    // constants
    public const float hebb_coefficient_increment = 0.05f;
    public const float LR_coefficient_increment = 0.05f;
    public const float sigmoid_coefficient_increment = 0.05f;


    // constructors
    public CellularEncodingBrainGenome() : base(){
        ProgramSymbolTree root = new(CECellularInstruction.END);
        this.forest.Add(root);
        this.size = root.size;
    }

    public CellularEncodingBrainGenome(ProgramSymbolTree root) : base(root) {}

    public CellularEncodingBrainGenome(List<ProgramSymbolTree> trees) : base(trees) { }


    public override ProgramSymbolTree GenerateRandomMutation()
    {
        int tree_length = UnityEngine.Random.Range(1, MUTATION_TREE_DEPTH+1);

        ProgramSymbolTree root = default;
        ProgramSymbolTree previous = default;
        for (int i = 0; i < tree_length; i++)
        {
            CECellularInstruction instruction;
            int counter = 0;
            do
            {
                instruction = CellularEncodingBrainGenome.GetRandomInstruction();
                counter++;
                if (counter > 10000)
                {
                    Debug.LogError("Mutation is taking an extremely long time to generate");
                }
            } while (IsForbiddenInstructionToAdd(instruction));


            ProgramSymbolTree node = new ProgramSymbolTree(instruction: instruction,
                children: new ProgramSymbolTree[0]);

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






    /// <summary>
    /// Given the trees of 2 parents, recombinate them and return the 2 offspring genomes.
    /// </summary>
    /// <param name="genome_parent1"></param>
    /// <param name="genome_parent2"></param>
    /// <returns></returns>
    public static (ProgramSymbolTree, ProgramSymbolTree) ReproduceTree(ProgramSymbolTree genome_parent1, ProgramSymbolTree genome_parent2)
    {
        ProgramSymbolTree offspring1 = genome_parent1.Clone();
        ProgramSymbolTree offspring2 = genome_parent2.Clone();


        // one-point crossover
        // select one point in each parent
        (ProgramSymbolTree random_supernode1, ProgramSymbolTree random_node1) = offspring1.SelectRandomNode();
        (ProgramSymbolTree random_supernode2, ProgramSymbolTree random_node2) = offspring2.SelectRandomNode();

        if (random_supernode1 == null)
        {
            if (random_node1.children.Length == 0)
            {
                random_node1.children = new ProgramSymbolTree[1];
            }
            random_node1.children[0] = random_node2;
        }
        else
        {
            if (random_node1 == random_supernode1.children[0])
            {
                random_supernode1.children[0] = random_node2;
            }
            else //if(random_node1 == random_supernode1.children[1])
            {
                random_supernode1.children[1] = random_node2;
            }
        }

        if (random_supernode2 == null)
        {
            if (random_node2.children.Length == 0)
            {
                random_node2.children = new ProgramSymbolTree[1];
            }
            random_node2.children[0] = random_node1;
        }
        else
        {
            if (random_node2 == random_supernode2.children[0])
            {
                random_supernode2.children[0] = random_node1;
            }
            else //if(random_node2 == random_supernode2.children[1])
            {
                random_supernode2.children[1] = random_node1;
            }
        }



        return (offspring1, offspring2);
    }

 

    /// <summary>
    /// Are we currently preventing this instruction from entering/leaving the gene pool
    /// </summary>
    public static bool IsForbiddenInstructionToAdd(CECellularInstruction instruction)
    {
        return instruction == CECellularInstruction.JUMP
            || instruction == CECellularInstruction.END
            || instruction == CECellularInstruction.SIGN
            // instruction == CECellularInstruction.CLIP
            || instruction == CECellularInstruction.SEQUENTIAL_DIVISION
            || instruction == CECellularInstruction.PARALLEL_DIVISION
            || instruction == CECellularInstruction.SEQUENTIAL_CLONE
            || instruction == CECellularInstruction.PARALLEL_CLONE;
    }

    public static bool IsForbiddenInstructionToAdd(int instruction)
    {
        return IsForbiddenInstructionToAdd((CECellularInstruction)instruction);
    }

    /// <summary>
    /// Are we currently preventing this instruction from entering/leaving the gene pool
    /// </summary>
    public static bool IsForbiddenInstructionToChangeOrDelete(CECellularInstruction instruction)
    {
        return instruction == CECellularInstruction.JUMP
            // || instruction == CECellularInstruction.CLIP
            || instruction == CECellularInstruction.SEQUENTIAL_DIVISION
            || instruction == CECellularInstruction.PARALLEL_DIVISION
            || instruction == CECellularInstruction.SEQUENTIAL_CLONE
            || instruction == CECellularInstruction.PARALLEL_CLONE;
    }

    public static bool IsForbiddenInstructionToChangeOrDelete(int instruction)
    {
        return IsForbiddenInstructionToChangeOrDelete((CECellularInstruction)instruction);
    }

    public static int HowManyChildren(object instruction)
    {
        switch ((CECellularInstruction)instruction)
        {
            case CECellularInstruction.END:
            case CECellularInstruction.JUMP:
                return 0;
            case CECellularInstruction.SEQUENTIAL_DIVISION:
            case CECellularInstruction.PARALLEL_DIVISION:
                return 2;
            default:
                return 1;
        }
    }



    public static string ToDescription(CECellularInstruction value)
    {
        DescriptionAttribute[] da = (DescriptionAttribute[])(value.GetType().GetField(value.ToString())).GetCustomAttributes(typeof(DescriptionAttribute), false);
        return da.Length > 0 ? da[0].Description : value.ToString();
    }

    public static CECellularInstruction GetRandomInstruction()
    {
        System.Random sysrnd = new();
        return (CECellularInstruction)sysrnd.Next(0, Enum.GetNames(typeof(CECellularInstruction)).Length);
    }

    public enum CECellularInstruction
    {
        // Division symbols
        [Description("SEQ")] SEQUENTIAL_DIVISION, // Cell A gets the input, cell B gets the output, pointers move to 2 subtrees
        [Description("PAR")] PARALLEL_DIVISION, // Cell A and B get inputs and outputs, pointers move to 2 subtrees
        [Description("SEQCL")] SEQUENTIAL_CLONE, // Sequential division, except instruction pointers move to the same instruction
        [Description("PARCL")] PARALLEL_CLONE, // Parallel division, except instruction pointers move to the same instruction*/

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
        [Description("DBLA")] DOUBLE_A,
        [Description("HALFA")] HALF_A,
        [Description("INCB")] INCREMENT_B,
        [Description("DECB")] DECREMENT_B,
        [Description("DBLB")] DOUBLE_B,
        [Description("HALFB")] HALF_B,
        [Description("INCC")] INCREMENT_C,
        [Description("DECC")] DECREMENT_C,
        [Description("DBLC")] DOUBLE_C,
        [Description("HALFC")] HALF_C,
        [Description("INCD")] INCREMENT_D,
        [Description("DECD")] DECREMENT_D,
        [Description("DBLD")] DOUBLE_D,
        [Description("HALFD")] HALF_D,
        [Description("INCLN")] INCREMENT_LEARNING_RATE,
        [Description("DECLN")] DECREMENT_LEARNING_RATE,
        [Description("DBLLN")] DOUBLE_LEARNING_RATE,
        [Description("HALFLN")] HALF_LEARNING_RATE,
        /*        INCREMENT_VOLTAGE_DECAY,
                DECREMENT_VOLTAGE_DECAY,*/
        [Description("INCSG")] INCREMENT_SIGMOID_ALPHA,
        [Description("DECSG")] DECREMENT_SIGMOID_ALPHA,
        [Description("DBLSG")] DOUBLE_SIGMOID_ALPHA,
        [Description("HALFSG")] HALF_SIGMOID_ALPHA,

        // Other
        [Description("CLIP")] CLIP, // cut the connection at the Link Register index
        [Description("SIGN")] SIGN, // change sign of output
        //MERGE, // takes argument 'i', where neighbor 'c' is connected by link 'i'./
        [Description("JMP")] JUMP, // Jump the instrution pointer to the root of another tree
        [Description("WAIT")] WAIT, // Does nothing for the step
        [Description("END")] END
    }

    //
    // manually designed GENOMES
    //


    public static CellularEncodingBrainGenome CreateEmptyGenome()
    {
        return new CellularEncodingBrainGenome(new ProgramSymbolTree(CECellularInstruction.END));
    }

    public static CellularEncodingBrainGenome CreateEmptyGenome3Page()
    {
        List<ProgramSymbolTree> list = new();
        list.Add(new ProgramSymbolTree(CECellularInstruction.END));
        list.Add(new ProgramSymbolTree(CECellularInstruction.END));
        list.Add(new ProgramSymbolTree(CECellularInstruction.END));
        return new CellularEncodingBrainGenome(list);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="depth">How many PARs deep? The tree will be depth+1 layers (+1 is for the END commands) </param>
    /// <returns></returns>
    public static ProgramSymbolTree CreateRecursivePAR(int depth)
    {
        ProgramSymbolTree child1, child2;

        if (depth == 1)
        {
            child1 = new(CECellularInstruction.END);
            child2 = new(CECellularInstruction.END);
        }
        else
        {
            child1 = CreateRecursivePAR(depth - 1);
            child2 = CreateRecursivePAR(depth - 1);
        }
        return new ProgramSymbolTree(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { child1, child2 });
    }



    public static CellularEncodingBrainGenome CreateBrainGenomeWithHexapodConstraints()
    {
        ProgramSymbolTree A, B, C, D;
        ProgramSymbolTree A0, B0, C0, D0;


        int index = 14;

        //****
        // MOTOR LAYER
        //****
        //

        // Create Rotate Sensor (RS).
        // 4 neurons in the segment
        A = new(CECellularInstruction.END); // W
        B = new(CECellularInstruction.END); // X
        C = new(CECellularInstruction.END); // Y
        D = new(CECellularInstruction.END); // Z

        A0 = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A, B });
        B0 = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { C, D });

        ProgramSymbolTree RS = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A0, B0 });
        RS.SetTreeIndex(index--);

        // Create Touch Sensor (TS).
        A0 = new(CECellularInstruction.END); // top
        B0 = new(CECellularInstruction.END); // bot
        C0 = new(CECellularInstruction.END); // front
        D0 = new(CECellularInstruction.END); // back

        A = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A0, B0 });
        B = new(CECellularInstruction.END); // left
        C = new(CECellularInstruction.END); // right
        D = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { C0, D0 });

        A0 = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A, B });
        B0 = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { C, D });

        ProgramSymbolTree TS = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A0, B0 });
        TS.SetTreeIndex(index--);

        //****
        // HIDDEN LAYER parallel 
        //****
        ProgramSymbolTree HLP = CreateRecursivePAR(6);
        HLP.SetTreeIndex(index--);


        // Create Body Joint Motor (BJM)
        A = new(CECellularInstruction.END);
        B = new(CECellularInstruction.END);

        A0 = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A, B });
        B0 = new(CECellularInstruction.END);

        ProgramSymbolTree BJM = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A0, B0 });
        BJM.SetTreeIndex(index--);

        // Next, create Leg Joint Motor (LJM).
        // 3 neurons in the joint
        A = new(CECellularInstruction.END);
        B = new(CECellularInstruction.END);

        A0 = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A, B });
        B0 = new(CECellularInstruction.END);

        ProgramSymbolTree LJM = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A0, B0 });
        LJM.SetTreeIndex(index--);

        // Next, create Leg Motor (LM).
        // 2 LJMs
        A = new(CECellularInstruction.JUMP, arguments: new int[] { LJM.tree_index - index }); // LJM
        A.extrainfo = "TOPLEGSEG";
        B = new(CECellularInstruction.JUMP, arguments: new int[] { LJM.tree_index - index }); // LJM
        B.extrainfo = "BOTLEGSEG";

        A0 = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { A, B });
        B0 = new(CECellularInstruction.JUMP, arguments: new int[] { LJM.tree_index - index }); // LJM
        B0.extrainfo = "FOOTSEG";

        ProgramSymbolTree LM = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A0, B0 });
        LM.SetTreeIndex(index--);

        // Next, create Half Body Motor (HBM).
        // 3 LMs
        A = new(CECellularInstruction.JUMP, arguments: new int[] { LM.tree_index - index }); // LM
        A.extrainfo = "TOPLEG";
        B = new(CECellularInstruction.JUMP, arguments: new int[] { LM.tree_index - index }); // LM
        B.extrainfo = "MIDLEG";

        A0 = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { A, B });
        B0 = new(CECellularInstruction.JUMP, arguments: new int[] { LM.tree_index - index }); // LM
        B0.extrainfo = "BOTLEG";

        ProgramSymbolTree HBM = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { A0, B0 });
        HBM.SetTreeIndex(index--);

        // Finally, create the Full Body Motor (FBM).
        // 2 HBMs
        A = new(CECellularInstruction.JUMP, arguments: new int[] { HBM.tree_index - index }); // HBM
        A.extrainfo = "LEFTBODYHALF";
        B = new(CECellularInstruction.JUMP, arguments: new int[] { BJM.tree_index - index }); // BJM
        B.extrainfo = "TOPBODYSEG";
        C = new(CECellularInstruction.JUMP, arguments: new int[] { BJM.tree_index - index }); // BJM
        C.extrainfo = "MIDBODYSEG";
        D = new(CECellularInstruction.JUMP, arguments: new int[] { HBM.tree_index - index }); // HBM
        D.extrainfo = "RIGHTBODYHALF";

        A0 = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { A, B });
        B0 = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { C, D });

        ProgramSymbolTree FBM = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { A0, B0 });
        FBM.SetTreeIndex(index--);

        //****
        // HIDDEN LAYER sequential
        //****
        A0 = new(CECellularInstruction.JUMP, arguments: new int[] { HLP.tree_index - index });
        B0 = new(CECellularInstruction.JUMP, arguments: new int[] { HLP.tree_index - index });
        //C0 = new(CellularInstruction.JUMP, argument: HLP.tree_index - index, children: new Tree[] { });
        // D0 = new(CellularInstruction.JUMP, argument: HLP.tree_index - index, children: new Tree[] { });

        //A = new(CellularInstruction.SEQUENTIAL_DIVISION, children: new Tree[] { A0, B0 });
        // B = new(CellularInstruction.SEQUENTIAL_DIVISION, children: new Tree[] { C0, D0 });

        ProgramSymbolTree HLS = new(CECellularInstruction.SEQUENTIAL_DIVISION, children: new ProgramSymbolTree[] { A0, B0 });
        HLS.SetTreeIndex(index--);

        //****
        // SENSOR LAYER
        //****
        // create Leg Segment Sensor (LSS).
        A = new(CECellularInstruction.JUMP, arguments: new int[] { TS.tree_index - index }); // TS
        A.extrainfo = "TOUCHSENSE";
        B = new(CECellularInstruction.JUMP, arguments: new int[] { RS.tree_index - index }); // RS
        B.extrainfo = "ROTATESENSE";

        ProgramSymbolTree LSS = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A, B });
        LSS.SetTreeIndex(index--);

        // Next, create Leg Sensor (LS).
        // 2 LSSs
        A = new(CECellularInstruction.JUMP, arguments: new int[] { LSS.tree_index - index }); // LSS
        A.extrainfo = "BOTLEGSEG";
        B = new(CECellularInstruction.JUMP, arguments: new int[] { LSS.tree_index - index }); // LSS
        B.extrainfo = "TOPLEGSEG";

        A0 = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { A, B });
        B0 = new(CECellularInstruction.JUMP, arguments: new int[] { LSS.tree_index - index}); // LSS
        B0.extrainfo = "FOOTSEG";

        ProgramSymbolTree LS = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A0, B0 });
        LS.SetTreeIndex(index--);

        // Next, create Half Body Sensor (HBS).
        // 3 LSs
        A = new(CECellularInstruction.JUMP, arguments: new int[] { LS.tree_index - index }); // LS
        A.extrainfo = "TOPLEG";
        B = new(CECellularInstruction.JUMP, arguments: new int[] { LS.tree_index - index }); // LS
        B.extrainfo = "MIDLEG";

        A0 = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { A, B });
        B0 = new(CECellularInstruction.JUMP, arguments: new int[] { LS.tree_index - index }); // LS
        B0.extrainfo = "BOTLEG";

        ProgramSymbolTree HBS = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { A0, B0 });
        HBS.SetTreeIndex(index--);

        // Next, create Body Segment Sensor (BSS)
        A = new(CECellularInstruction.JUMP, arguments: new int[] { TS.tree_index - index }); // TS
        A.extrainfo = "TOUCHSENSE";
        B = new(CECellularInstruction.JUMP, arguments: new int[] { RS.tree_index - index }); // RS
        B.extrainfo = "ROTATESENSE";

        ProgramSymbolTree BSS = new(CECellularInstruction.PARALLEL_DIVISION, new ProgramSymbolTree[] { A, B });
        BSS.SetTreeIndex(index--);

        // Finally, create the Full Body Sensor (FBS).
        // 2 HBSs + 3 BSSs
        A = new(CECellularInstruction.JUMP, arguments: new int[] { BSS.tree_index - index }); //BSS
        A.extrainfo = "TOPBODYSEG";
        B = new(CECellularInstruction.JUMP, arguments: new int[] { BSS.tree_index - index }); //BSS
        B.extrainfo = "MIDBODYSEG";

        A0 = new(CECellularInstruction.JUMP, arguments: new int[] { HBS.tree_index - index }); // HBS
        A0.extrainfo = "LEFTBODYHALF";
        B0 = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { A, B });
        C0 = new(CECellularInstruction.JUMP, arguments: new int[] { BSS.tree_index - index }); // BSS
        C0.extrainfo = "BOTBODYSEG";
        D0 = new(CECellularInstruction.JUMP, arguments: new int[] { HBS.tree_index - index }); // HBS
        D0.extrainfo = "RIGHTBODYHALF";

        A = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { A0, B0 });
        B = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { C0, D0 });

        ProgramSymbolTree FBS = new(CECellularInstruction.PARALLEL_DIVISION, children: new ProgramSymbolTree[] { A, B });
        FBS.SetTreeIndex(index--);

        //****
        // FULL NETWORK (ROOT)
        //****
        A = new(CECellularInstruction.JUMP, arguments: new int[] { FBS.tree_index - index }); // FBS
        A.extrainfo = "SENSORLAYER";
        B = new(CECellularInstruction.JUMP, arguments: new int[] { HLS.tree_index - index });
        B.extrainfo = "HIDDENLAYER";

        A0 = new(CECellularInstruction.SEQUENTIAL_DIVISION, children: new ProgramSymbolTree[] { A, B });
        B0 = new(CECellularInstruction.JUMP, arguments: new int[] { FBM.tree_index - index }); // FBM
        B0.extrainfo = "MOTORLAYER";

        ProgramSymbolTree ROOT = new(CECellularInstruction.SEQUENTIAL_DIVISION, children: new ProgramSymbolTree[] { A0, B0 });
        ROOT.SetTreeIndex(index--);


        List<ProgramSymbolTree> list = new();
        list.Add(ROOT);
        list.Add(FBS);
        list.Add(BSS);
        list.Add(HBS);
        list.Add(LS);
        list.Add(LSS);
        list.Add(HLS);
        list.Add(FBM);
        list.Add(HBM);
        list.Add(LM);
        list.Add(LJM);
        list.Add(BJM);
        list.Add(HLP);
        list.Add(TS);
        list.Add(RS);

        return new CellularEncodingBrainGenome(list);
    }


    public static CellularEncodingBrainGenome CreateHexapodNoREC()
    {
        ProgramSymbolTree A, B, C;
        ProgramSymbolTree A0, B0, C0;

        A0 = CreateRecursivePAR(6);
        B0 = CreateRecursivePAR(6);
        C0 = CreateRecursivePAR(6);

        //Layer 
        A = new(CECellularInstruction.WAIT, new[] { A0 });
        B = new(CECellularInstruction.SEQUENTIAL_DIVISION, new[] { B0, C0 });

        //Layer 0
        ProgramSymbolTree ROOT = new(CECellularInstruction.SEQUENTIAL_DIVISION, new[] { A, B });

        return new CellularEncodingBrainGenome(ROOT);
    }


    public static BrainGenomeTree Create3LayerMinimum()
    {
        ProgramSymbolTree A, B, C;
        ProgramSymbolTree A0, B0, C0;

        A0 = new(CECellularInstruction.END);
        B0 = new(CECellularInstruction.END);
        C0 = new(CECellularInstruction.END);

        //Layer 
        A = new(CECellularInstruction.WAIT, new[] { A0 });
        B = new(CECellularInstruction.SEQUENTIAL_DIVISION, new[] { B0, C0 });

        //Layer 0
        ProgramSymbolTree ROOT = new(CECellularInstruction.SEQUENTIAL_DIVISION, new[] { A, B });

        return new CellularEncodingBrainGenome(ROOT);
    }

  

    /// <summary>
    /// Develop brain from the genomee
    /// </summary>
    public override (NativeArray<Neuron>, NativeArray<Synapse>) DevelopCPU(Dictionary<string, Dictionary<string, int>> neuron_indices)
    {
        CellularEncodingBrainGenome genome = this;

        GraphVisualization3D gui_graph;
        List<DevelopmentNeuron> developed_brain = new();
        List<TreeDevelopmentNeuron> developing_brain = new();
        List<Neuron> final_brain_neurons = new();
        List<Synapse> final_brain_synapses = new();
        IOBlock inputIOBlock = new();
        IOBlock outputIOBlock = new();

        //first, create ACYC, the initial network graph without a recurrent link
        List<TreeDevelopmentSynapse> inputs = new();
        List<TreeDevelopmentNode> outputs = new();
        TreeDevelopmentSynapse input_connection = new(inputIOBlock); // ignore this synapse

        inputs.Add(input_connection);
        outputs.Add(outputIOBlock);

        TreeDevelopmentNeuron ancestor = new(instruction_pointer: genome.forest[0],
            inputs: inputs,
            outputs: outputs,
            link_register: 0,
            threshold: 1,
            bias: 0,
            sign: true,
            adaptation: 1,
            decay: 0.99f,
            sigmoid_alpha: 1);


        // GUI
        gui_graph = new(ancestor);


        // turn ACYC into CYC
        TreeDevelopmentSynapse recurrent_connection = new(ancestor,
            learning_rate: 1.0f);
        ancestor.inputs.Add(recurrent_connection);
        ancestor.outputs.Add(ancestor);

        developing_brain.Add(ancestor);

        // while more developing cells with instructions left to process, execute them
        while (developing_brain.Count > 0)
        {

            int count = developing_brain.Count; // count at beginning of time step, since others may be added during development

            for (int i = 0; i < count; i++)
            {
                TreeDevelopmentNeuron cell = developing_brain[i];
                TreeDevelopmentNeuron clone;
                TreeDevelopmentSynapse connection;
                CECellularInstruction instruction = (CECellularInstruction)cell.instruction_pointer.instruction;
                switch (instruction)
                {
                    case CECellularInstruction.SEQUENTIAL_DIVISION:
                    case CECellularInstruction.SEQUENTIAL_CLONE:
                        // clone the cell
                        clone = cell.Clone();

                        //GUI - sequential is in z direction
                        gui_graph.InsertForward(cell, clone);

                        // the original cell loses its outputs, then develop new output to clone
                        foreach (TreeDevelopmentNode node in cell.outputs)
                        {
                            // for each cell in the ouput
                            if (node is TreeDevelopmentNeuron)
                            {
                                for (int j = 0; j < ((TreeDevelopmentNeuron)node).inputs.Count; j++)
                                {
                                    // sever the synapse connecting the original cell to its output
                                    TreeDevelopmentSynapse c = ((TreeDevelopmentNeuron)node).inputs[j];
                                    if (c.from_pointer == cell)
                                    {
                                        ((TreeDevelopmentNeuron)node).inputs.RemoveAt(j);

                                        break;
                                    }
                                }
                            }
                        }
                        cell.outputs.Clear();
                        cell.outputs.Add(clone);

                        // the clone loses its inputs, but gets input from original cell

                        bool is_recurrent = false;
                        List<TreeDevelopmentSynapse> to_remove = new();
                        foreach (TreeDevelopmentSynapse synapse1 in clone.inputs)
                        {
                            if (synapse1.from_pointer == cell)
                            {
                                // clone has original cell as input already
                                is_recurrent = true;

                            }
                            foreach (TreeDevelopmentSynapse synapse2 in cell.inputs)
                            {
                                if (synapse1.from_pointer == synapse2.from_pointer
                                    && synapse1.from_pointer != cell)
                                {
                                    to_remove.Add(synapse1);
                                }
                            }
                        }

                        if (!is_recurrent)
                        {
                            TreeDevelopmentSynapse synapse_clone = new TreeDevelopmentSynapse(pointer: cell);
                            clone.inputs.Add(synapse_clone);
                        }
                        else
                        {
                            // otherwise, it already has the original cell as input, and also it needs its own recurrent connection.
                            TreeDevelopmentSynapse synapse_clone = new TreeDevelopmentSynapse(pointer: clone);
                            clone.inputs.Add(synapse_clone);
                        }

                        foreach (TreeDevelopmentSynapse synapse in to_remove)
                        {
                            clone.inputs.Remove(synapse);
                        }


                        // now add the clone to the developing brain
                        developing_brain.Add(clone);

                        // and update both pointers
                        cell.MoveToChildA();
                        if (instruction == CECellularInstruction.SEQUENTIAL_DIVISION)
                        {
                            clone.MoveToChildB();
                        }
                        else if (instruction == CECellularInstruction.SEQUENTIAL_CLONE)
                        {
                            clone.MoveToChildA();
                        }

                        break;
                    case CECellularInstruction.PARALLEL_DIVISION:
                    case CECellularInstruction.PARALLEL_CLONE:
                        // clone the cell and its connections
                        clone = cell.Clone();


                        //GUI - parallel is in yz direction
                        gui_graph.InsertRightUpAlternating(cell, clone);

                        // now add the clone to the developing brain
                        developing_brain.Add(clone);

                        bool trackLR = false;
                        if (genome.forest.Count == 15)
                        {
                            if (cell.instruction_pointer.tree_index == 13
                                || cell.instruction_pointer.tree_index == 14
                                || cell.instruction_pointer.tree_index == 10
                                || cell.instruction_pointer.tree_index == 11) trackLR = true;
                        }
                        else if (genome.forest.Count == 14)
                        {
                            if (cell.instruction_pointer.tree_index == 12
                                || cell.instruction_pointer.tree_index == 13
                                || cell.instruction_pointer.tree_index == 10
                                || cell.instruction_pointer.tree_index == 11) trackLR = true;
                        }

                        if (trackLR)
                        {
                            cell.extradata += "L";
                            clone.extradata += "R";
                        }

                        // and update both pointers
                        cell.MoveToChildA();
                        if (instruction == CECellularInstruction.PARALLEL_DIVISION)
                        {

                            if (clone.outputs.Contains(clone))
                            {
                                foreach (TreeDevelopmentSynapse s in clone.inputs)
                                {
                                    if (s.from_pointer == clone)
                                    {
                                        // recurrent synapse
                                        TreeDevelopmentSynapse cloned_recurrent_synapse1 = s.Clone();
                                        TreeDevelopmentSynapse cloned_recurrent_synapse2 = s.Clone();

                                        cloned_recurrent_synapse1.from_pointer = cell;
                                        clone.inputs.Add(cloned_recurrent_synapse1);
                                        cell.outputs.Add(clone);

                                        cloned_recurrent_synapse2.from_pointer = clone;
                                        cell.inputs.Add(cloned_recurrent_synapse1);
                                        clone.outputs.Add(cell);
                                        break;
                                    }

                                }

                            }

                            clone.MoveToChildB();

                        }
                        else if (instruction == CECellularInstruction.PARALLEL_CLONE)
                        {
                            clone.MoveToChildA();
                        }

                        break;
                    case CECellularInstruction.INCREMENT_LINK_REGISTER:
                        cell.link_register++;
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.DECREMENT_LINK_REGISTER:
                        cell.link_register--;
                        cell.MoveToChildA();
                        break;
                    /*                    case CellularInstruction.INCREMENT_THRESHOLD:
                                            cell.threshold++;
                                            cell.MoveToChildA();
                                            break;
                                        case CellularInstruction.DECREMENT_THRESHOLD:
                                            cell.threshold--;
                                            cell.MoveToChildA();
                                            break;*/
                    case CECellularInstruction.INCREMENT_BIAS:
                        cell.bias += hebb_coefficient_increment;
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.DECREMENT_BIAS:
                        cell.bias -= hebb_coefficient_increment;
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.SIGN_LR:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            connection.learning_rate *= -1;
                        }
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.SIGN_A:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            connection.coefficients[0] *= -1;
                        }
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.SIGN_B:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            connection.coefficients[1] *= -1;
                        }
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.SIGN_C:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            connection.coefficients[2] *= -1;
                        }
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.SIGN_D:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            connection.coefficients[3] *= -1;
                        }
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.INCREMENT_A:
                    case CECellularInstruction.INCREMENT_B:
                    case CECellularInstruction.INCREMENT_C:
                    case CECellularInstruction.INCREMENT_D:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            int idx = -1;
                            if (instruction == CECellularInstruction.INCREMENT_A) idx = 0;
                            if (instruction == CECellularInstruction.INCREMENT_B) idx = 1;
                            if (instruction == CECellularInstruction.INCREMENT_C) idx = 2;
                            if (instruction == CECellularInstruction.INCREMENT_D) idx = 3;
                            connection.coefficients[idx] = connection.coefficients[idx] + hebb_coefficient_increment;
                        }
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.DECREMENT_A:
                    case CECellularInstruction.DECREMENT_B:
                    case CECellularInstruction.DECREMENT_C:
                    case CECellularInstruction.DECREMENT_D:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            int idx = -1;
                            if (instruction == CECellularInstruction.DECREMENT_A) idx = 0;
                            if (instruction == CECellularInstruction.DECREMENT_B) idx = 1;
                            if (instruction == CECellularInstruction.DECREMENT_C) idx = 2;
                            if (instruction == CECellularInstruction.DECREMENT_D) idx = 3;
                            connection.coefficients[idx] = connection.coefficients[idx] - hebb_coefficient_increment;
                        }
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.DOUBLE_A:
                    case CECellularInstruction.DOUBLE_B:
                    case CECellularInstruction.DOUBLE_C:
                    case CECellularInstruction.DOUBLE_D:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            int idx = -1;
                            if (instruction == CECellularInstruction.DOUBLE_A) idx = 0;
                            if (instruction == CECellularInstruction.DOUBLE_B) idx = 1;
                            if (instruction == CECellularInstruction.DOUBLE_C) idx = 2;
                            if (instruction == CECellularInstruction.DOUBLE_D) idx = 3;
                            connection.coefficients[idx] = connection.coefficients[idx] * 2;
                        }
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.HALF_A:
                    case CECellularInstruction.HALF_B:
                    case CECellularInstruction.HALF_C:
                    case CECellularInstruction.HALF_D:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            int idx = -1;
                            if (instruction == CECellularInstruction.HALF_A) idx = 0;
                            if (instruction == CECellularInstruction.HALF_B) idx = 1;
                            if (instruction == CECellularInstruction.HALF_C) idx = 2;
                            if (instruction == CECellularInstruction.HALF_D) idx = 3;
                            connection.coefficients[idx] = connection.coefficients[idx] / 2;
                        }
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.INCREMENT_LEARNING_RATE:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            connection.learning_rate += LR_coefficient_increment;
                        }
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.DECREMENT_LEARNING_RATE:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            connection.learning_rate -= LR_coefficient_increment;
                        }
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.DOUBLE_LEARNING_RATE:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            connection.learning_rate *= 2;
                        }
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.HALF_LEARNING_RATE:
                        if (cell.inputs.Count > 0)
                        {
                            connection = cell.inputs[cell.get_link_register()];
                            connection.learning_rate /= 2;
                        }
                        cell.MoveToChildA();
                        break;
                    /*                   case CellularInstruction.INCREMENT_VOLTAGE_DECAY:
                                           cell.decay = Mathf.Min(cell.decay + 0.01f, 0.99f);
                                           cell.MoveToChildA();
                                           break;
                                       case CellularInstruction.DECREMENT_VOLTAGE_DECAY:
                                           cell.decay = Mathf.Max(cell.decay - 0.01f, 0);
                                           cell.MoveToChildA();
                                           break;*/
                    case CECellularInstruction.INCREMENT_SIGMOID_ALPHA:
                        cell.sigmoid_alpha += sigmoid_coefficient_increment;
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.DECREMENT_SIGMOID_ALPHA:
                        cell.sigmoid_alpha -= sigmoid_coefficient_increment;
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.DOUBLE_SIGMOID_ALPHA:
                        cell.sigmoid_alpha *= 2;
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.HALF_SIGMOID_ALPHA:
                        cell.sigmoid_alpha /= 2;
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.SIGN:
                        cell.sign = !cell.sign;
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.CLIP:
                        if (cell.inputs.Count > 0)
                        {
                            int idx = cell.get_link_register();
                            if (!(cell.inputs[idx].from_pointer is IOBlock))
                            {
                                cell.inputs.RemoveAt(idx);
                            }
                        }
                        cell.MoveToChildA();
                        break;
                                    /*    case CellularInstruction.MERGE:
                                            break;*/

                    case CECellularInstruction.JUMP:
                        int tree_index = cell.instruction_pointer.tree_index;
                        int offset = cell.instruction_pointer.arguments[0];
                        int new_tree_idx = tree_index + offset;
                        if (new_tree_idx > genome.forest.Count || new_tree_idx < 0)
                        {
                            Debug.LogError("Tried jumping to out-of-bounds tree.");
                            developed_brain.Add(cell);
                            developing_brain.RemoveAt(i);
                            break;
                        }

                        cell.extradata += (cell.instruction_pointer.extrainfo + "_");
                        cell.instruction_pointer = genome.forest[new_tree_idx];
                        break;
                    case CECellularInstruction.WAIT:
                        // simply move to next instruction
                        cell.MoveToChildA();
                        break;
                    case CECellularInstruction.END:
                        developed_brain.Add(cell);
                        developing_brain.RemoveAt(i);
                        i--;
                        count--;
                        break;
                    default:
                        Debug.LogError("Could not execute instruction! " + instruction);
                        cell.MoveToChildA();
                        break;
                }
            }
        }

        // at this point, network is fully developed
        // so convert the network into structs for parallel processing
        // allocate temporary memory to develop the brain
  

        int num_of_connections = 0;

        // first turn all the cells into neurons
        for (int i = 0; i < developed_brain.Count; i++)
        {
            TreeDevelopmentNeuron cell = (TreeDevelopmentNeuron)developed_brain[i];

            Neuron neuron = new(threshold: cell.threshold,
                bias: cell.bias,
                adaptation_delta: cell.adaptation_delta,
                decay_rate_tau: cell.decay,
                sign: cell.sign,
                sigmoid_alpha: cell.sigmoid_alpha,
                activation_function: Neuron.NeuronActivationFunction.Tanh);

            Vector3Int position = gui_graph.data_to_position[cell];
            neuron.position = new int3(position.x, position.y, position.z);

            foreach (TreeDevelopmentNode n in cell.outputs)
            {
                if (n is IOBlock)
                {
                    // this is a motor (output) neuron, so turn it into a perceptron
                    neuron.type = Neuron.NeuronType.Perceptron;
                    // connect to motor interface
                    string[] strings = cell.extradata.Split("_");
                    string neuron_type = strings[strings.Length - 1];

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
                    else if (neuron_type == "")
                    {
                        neuron_idx = 0;
                    }
                    else
                    {
                        Debug.LogError("ERROR " + neuron_type);
                    }


                    neuron_indices[Brain.MOTOR_NEURON_KEY][cell.extradata[0..^neuron_type.Length] + neuron_idx] = i;
                    neuron.neuron_class = Neuron.NeuronClass.Motor;
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
                if (c.from_pointer is TreeDevelopmentNeuron)
                {
                    Synapse connection = new Synapse(learning_rate: c.learning_rate,
                        from_neuron_idx: developed_brain.IndexOf((TreeDevelopmentNeuron)c.from_pointer),
                        to_neuron_idx: -1,
                        coefficients: c.coefficients);

                    final_brain_synapses.Add(connection);

                    synapse_idx++;
                    neuron.synapse_count++;
                }
                else if (c.from_pointer == inputIOBlock)
                {

                    // this is a sensory (input) neuron, so turn it into a perceptron

                    neuron.type = Neuron.NeuronType.Perceptron;

                    // connect to sensory interface
                    string[] strings = cell.extradata.Split("_");
                    string neuron_type = strings[strings.Length - 1];
                    string sensor_type = strings[strings.Length - 2];


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


            final_brain_neurons[i] = neuron;
     

        }


        return (final_brain_neurons.ToNativeArray<Neuron>(Allocator.Persistent), final_brain_synapses.ToNativeArray<Synapse>(Allocator.Persistent));
    }


    public override (ComputeBuffer, ComputeBuffer) DevelopGPU(Dictionary<string, Dictionary<string, int>> neuron_indices)
    {
        throw new NotImplementedException();
    }

    public override JobHandle ScheduleDevelopCPUJob()
    {
        return new JobHandle();
    }

    public override void ScheduleDevelopGPUJob()
    {
        throw new NotImplementedException();
    }

}
