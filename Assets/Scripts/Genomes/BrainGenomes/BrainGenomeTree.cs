using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using static AxonalGrowthBrainGenome;
using static CellularEncodingBrainGenome;

/// <summary>
///     The grammar tree encoding the brain development
/// </summary>
public abstract class BrainGenomeTree : BrainGenome
{
    public float AVG_NODE_MUTATIONS_PER_MUTATE = 5; // on average, how many nodes to mutate in the genome, when the overall genome is mutated
    public float NODE_MUTATION_RATE_MUTATION_RATE = 0.2f; // on average, how many nodes to mutate in the genome, when the overall genome is mutated
    public float FOREST_MUTATION_RATE = 0.05f; //
    public float TREE_DEPTH_MUTATION_RATE = 0.2f;

    public int MUTATION_TREE_DEPTH = 1;

    public const string save_file_extension = ".TreeBrainGenome";

    // variables 
    public int size = 0;
    public List<ProgramSymbolTree> forest;



    /// <summary>
    /// Constructors
    /// </summary>
    public BrainGenomeTree()
    {
        this.forest = new();
    }

    public BrainGenomeTree(ProgramSymbolTree root)
    {
        this.forest = new();
        this.forest.Add(root);
        this.size = root.size;
    }

    public BrainGenomeTree(List<ProgramSymbolTree> trees)
    {
        this.forest = trees;
        this.recalculate_total_size();
    }


    /// <summary>
    /// Total genome size, including all trees
    /// </summary>
    /// <returns></returns>
    public void recalculate_total_size()
    {
        this.size = 0;
        foreach (ProgramSymbolTree tree_root in this.forest)
        {
            this.size += tree_root.size;
        }
    }

    public override BrainGenome Clone()
    {
        List<ProgramSymbolTree> cloned_trees = new();
        foreach (ProgramSymbolTree tree in this.forest)
        {
            cloned_trees.Add(tree.Clone());
        }


        BrainGenomeTree cloned_genome;
        if (this is CellularEncodingBrainGenome)
        {
            cloned_genome = new CellularEncodingBrainGenome(cloned_trees);
        }
        else if (this is AxonalGrowthBrainGenome)
        {
            cloned_genome = new AxonalGrowthBrainGenome(cloned_trees);
        }
        else
        {
            GlobalUtils.LogErrorEnumNotRecognized("error invalid genome type");
            return null;
        }

        return cloned_genome;
    }


    /// <summary>
    ///     Mutate the genome
    /// </summary>
    public override void Mutate()
    {
        for (int i = 0; i < this.forest.Count; i++)
        {
            ProgramSymbolTree tree_root = this.forest[i];
            Mutate(tree_root);
        }

        bool should_mutate;

        // mutate the Mutation Rate itself
        should_mutate = UnityEngine.Random.Range(0f, 1f) < NODE_MUTATION_RATE_MUTATION_RATE;
        if (should_mutate)
        {
            AVG_NODE_MUTATIONS_PER_MUTATE += UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1;
            AVG_NODE_MUTATIONS_PER_MUTATE = Mathf.Max(AVG_NODE_MUTATIONS_PER_MUTATE, 1); // minimum of 1 mutation average
        }


        // mutate the tree depth
        /*        should_mutate = UnityEngine.Random.Range(0f, 1f) < TREE_DEPTH_MUTATION_RATE;
                if (should_mutate)
                {
                    MUTATION_TREE_DEPTH += UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1;
                    MUTATION_TREE_DEPTH = Mathf.Max(MUTATION_TREE_DEPTH, 1); // minimum of 1 mutation average
                }*/


        // mutate the number of trees
        /*        should_mutate = UnityEngine.Random.Range(0f, 1f) < FOREST_MUTATION_RATE;
                if (should_mutate)
                {
                    int mutation_type = UnityEngine.Random.Range(0, 2);
                    int rnd_idx = UnityEngine.Random.Range(1, this.forest.Count);
                    if (mutation_type == 0 || this.forest.Count == 1)
                    {
                        // add a tree
                        this.forest.Insert(rnd_idx, GetENDPST());
                    }else if(mutation_type == 1)
                    {
                        // remove a tree
                        this.forest.RemoveAt(rnd_idx);   
                    }
                }*/
        this.recalculate_total_size();
    }

    public abstract ProgramSymbolTree GenerateRandomMutation();



    /// <summary>
    ///     Recursively mutate the given tree
    /// </summary>
    /// <param name="tree"></param>
    public void Mutate(ProgramSymbolTree tree)
    {

        // recursive mutate children
        foreach (ProgramSymbolTree child in tree.children)
        {
            Mutate(child);
        }


        bool should_mutate = UnityEngine.Random.Range(0f, 1f) < (AVG_NODE_MUTATIONS_PER_MUTATE / this.size);
        if (!should_mutate) return;
        int mutation_type = UnityEngine.Random.Range(1, 4); // 1 change gene, 2 add gene, 3 delete gene

        if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.CellularEncoding)
        {
            if ((CECellularInstruction)tree.instruction == CECellularInstruction.END && (mutation_type == 2 || mutation_type == 3))
            {
                // can't delete or add to an END instruction
                mutation_type = 1; // change gene
            }

            if (IsForbiddenInstructionToChangeOrDelete((CECellularInstruction)tree.instruction) && (mutation_type == 1 || mutation_type == 3))
            {
                mutation_type = 2; // add a gene
            }
        }
        else if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.SGOCE)
        {
            if ((AxonalGrowthCellularInstruction)tree.instruction == AxonalGrowthCellularInstruction.END && (mutation_type == 2 || mutation_type == 3))
            {
                // can't delete or add to an END instruction
                mutation_type = 1; // change gene
            }

        }


        // now mutate this node if its allowed
        // generate a random mutation
        ProgramSymbolTree mutation = GenerateRandomMutation();

        ProgramSymbolTree swap;
        // do the mutation
        if (mutation_type == 1)
        {
            // change gene
            //Debug.Log("mutating... change gene from " + tree.instruction + " to " + mutation.instruction);
            int change = UnityEngine.Random.Range(0, tree.arguments.Length + 1); // more args => more chance to change 1 arg rather than whole instruction
            if (change == 0 || GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.CellularEncoding)
            {
                // change the whole instruction
                tree.ChangeInstruction(mutation.instruction, GenerateRandomArguments(mutation.instruction));
            }
            else
            {
                // change a random argument to a random value
                int[] rnd_args = GenerateRandomArguments(tree.instruction);
                int rnd_arg_idx = UnityEngine.Random.Range(0, tree.arguments.Length);
                tree.arguments[rnd_arg_idx] = rnd_args[rnd_arg_idx];
            }

        }
        else if (mutation_type == 2)
        {
            // insert new gene below node
            // Debug.Log("mutating... ADD new gene " + mutation.instruction);


            if (tree.children.Length == 2)
            {
                int rnd = UnityEngine.Random.Range(1, 3);
                if (rnd == 1)
                {
                    // insert as left child
                    tree.InsertNode(0, mutation);

                }
                else //if(rnd == 2)
                {
                    // insert as right child
                    tree.InsertNode(1, mutation);
                }
            }
            else if (tree.children.Length == 1)
            {
                // insert as only child
                tree.InsertNode(0, mutation);
            }
            else // if (random_node.children.Length == 0 && random_node.parent == null)
            {
                //super node is null, and it is END instruction, so make the mutation the root node
                swap = tree;
                tree = mutation;
                mutation.children = new ProgramSymbolTree[] { swap };
                swap.parent = mutation;
                mutation.parent = null;
                tree.RecalculateSize();
                mutation.RecalculateSize();
            }
        }
        else if (mutation_type == 3)
        {

            if (tree.parent == null)
            {
                if (tree.children.Length > 0)
                {
                    this.forest[tree.tree_index] = tree.children[UnityEngine.Random.Range(0, tree.children.Length)];
                }
            }
            else
            {
                // // delete this node
                //Debug.Log("mutating... DELETE gene " + mutation.instruction);

                if (tree.parent.children.Length == 1)
                {
                    tree.parent.SetChildren(new[] { tree.children[UnityEngine.Random.Range(0, tree.children.Length)] });
                }
                else if (tree.parent.children.Length == 2)
                {

                    if (tree.parent.children[0] == tree)
                    {
                        // deleted gene is left child, so set parent's new left child as deleted gene's children
                        tree.parent.SetChildren(new ProgramSymbolTree[] { tree.children[UnityEngine.Random.Range(0, tree.children.Length)], tree.parent.children[1] });
                    }
                    else
                    {
                        // deleted gene is right child, so set parent's new right child as deleted gene's children
                        tree.parent.SetChildren(new ProgramSymbolTree[] { tree.parent.children[0], tree.children[UnityEngine.Random.Range(0, tree.children.Length)] });
                    }


                }





            }

        }


    }

    /// <summary>
    ///     Load genome from disk.
    /// </summary>
    /// <returns></returns>
    public static BrainGenome LoadFromDisk()
    {

        Debug.LogError("TODO: SPECIFY WHAT KIND OF BRAIN GENOME IS BEING LOADED SO THE SAVE DATA CAN BE INTERPRETED CORRECTLY");

        string full_path = GlobalConfig.save_file_path + GlobalConfig.save_file_base_name + save_file_extension;
        Debug.Log("Loading brain genome from disk: " + full_path);

        StreamReader data_file;
        data_file = new(path: full_path);
        List<ProgramSymbolTree> trees = new();
        int i = 0;
        while (!data_file.EndOfStream)
        {
            data_file.ReadLine();
            ProgramSymbolTree root = ReadTree(data_file);
            root.SetTreeIndex(i);
            trees.Add(root);
            i++;
        }

        data_file.Close();

        Debug.Log("Done loading brain genome.");

        return new CellularEncodingBrainGenome(trees);
    }

    /// <summary>
    ///     Save genome to disk.
    /// </summary>
    /// <param name="genome"></param>
    public override void SaveToDisk()
    {
        string[] existing_saves = Directory.GetFiles(path: GlobalConfig.save_file_path, searchPattern: GlobalConfig.save_file_base_name + "*" + save_file_extension);

        int num = existing_saves.Length;

        string full_path = GlobalConfig.save_file_path + GlobalConfig.save_file_base_name + num.ToString() + save_file_extension;
        Debug.Log("Saving brain genome to disk: " + full_path);
        StreamWriter data_file;
        data_file = new(path: full_path, append: false);

        foreach (ProgramSymbolTree tree in this.forest)
        {
            WriteTree(data_file, tree);

        }

        data_file.Close();
        Debug.Log("Done saving brain genome.");
    }

    const string ARGS_DELIMITER = "ARGS";

    /// <summary>
    ///     Write a node and its children
    /// </summary>
    /// <param name="data_file"></param>
    /// <param name="tree"></param>
    public static void WriteTree(StreamWriter data_file, ProgramSymbolTree tree)
    {
        data_file.WriteLine(GlobalConfig.open_string);

        int instruction = (int)tree.instruction;
        data_file.WriteLine(instruction);

        data_file.WriteLine(ARGS_DELIMITER);
        foreach (int argument in tree.arguments)
        {
            data_file.WriteLine(argument);
        }
        data_file.WriteLine(ARGS_DELIMITER);
        data_file.WriteLine(tree.extrainfo);

        foreach (ProgramSymbolTree child in tree.children)
        {
            WriteTree(data_file, child);
        }

        data_file.WriteLine(GlobalConfig.close_string);
    }

    /// <summary>
    ///     Read a node and its children
    /// </summary>
    /// <param name="data_file"></param>
    /// <returns></returns>
    public static ProgramSymbolTree ReadTree(StreamReader data_file)
    {

        CECellularInstruction instruction = (CECellularInstruction)int.Parse(data_file.ReadLine());

        data_file.ReadLine(); // ARGS Delimiter
        string line = data_file.ReadLine();
        List<int> args = new();
        while (line != ARGS_DELIMITER)
        {
            args.Add(int.Parse(line));
            line = data_file.ReadLine();
        }
        int[] argument = args.ToArray();
        string extrainfo = data_file.ReadLine();

        List<ProgramSymbolTree> children = new();

        string separator = data_file.ReadLine();
        if (separator == GlobalConfig.close_string)
        {
            // no children
        }
        else if (separator == GlobalConfig.open_string)
        {
            while (separator == GlobalConfig.open_string)
            {
                // child
                ProgramSymbolTree child = ReadTree(data_file);
                children.Add(child);
                separator = data_file.ReadLine();
            }
        }
        else
        {
            Debug.LogError("ERROR in loading GenomeBrain!");
        }


        ProgramSymbolTree tree = new(instruction: instruction,
            children: children.ToArray(),
            arguments: argument);

        tree.extrainfo = extrainfo;

        return tree;
    }



    /// <summary>
    /// Given the trees of 2 parents, recombinate them and return the 2 offspring genomes.
    /// </summary>
    /// <param name="genome_parent1"></param>
    /// <param name="genome_parent2"></param>
    /// <returns></returns>
    public override (BrainGenome, BrainGenome) Reproduce(BrainGenome parent2)
    {
        BrainGenomeTree genome_parent1 = this;
        BrainGenomeTree genome_parent2 = (BrainGenomeTree)parent2;

        // two-point crossover
        // select one point in each parent
        int min_forest_length = Mathf.Min(genome_parent1.forest.Count, genome_parent2.forest.Count);
        int max_forest_length = Mathf.Max(genome_parent1.forest.Count, genome_parent2.forest.Count);

        int x1 = UnityEngine.Random.Range(1, min_forest_length);
        int x2 = UnityEngine.Random.Range(1, min_forest_length);

        if (x1 > x2)
        {
            int tmp = x1;
            x2 = x1;
            x1 = tmp;
        }

        List<ProgramSymbolTree> offspring1_trees = new();
        List<ProgramSymbolTree> offspring2_trees = new();



        for (int i = 0; i < max_forest_length; i++)
        {
            if (i < x1 || i >= x2)
            {
                if (i < genome_parent1.forest.Count) offspring1_trees.Add(genome_parent1.forest[i].Clone());
                if (i < genome_parent2.forest.Count) offspring2_trees.Add(genome_parent2.forest[i].Clone());
            }
            else
            {
                offspring1_trees.Add(genome_parent2.forest[i].Clone());
                offspring2_trees.Add(genome_parent1.forest[i].Clone());
            }
        }

        if (this is CellularEncodingBrainGenome)
        {
            return (new CellularEncodingBrainGenome(offspring1_trees), new CellularEncodingBrainGenome(offspring2_trees));
        }
        else if (this is AxonalGrowthBrainGenome)
        {
            return (new AxonalGrowthBrainGenome(offspring1_trees), new AxonalGrowthBrainGenome(offspring2_trees));
        }
        else
        {
            GlobalUtils.LogErrorEnumNotRecognized("Invalid genome type");
            return (null, null);
        }


    }

    public class ProgramSymbolTree
    {
        //node info
        public object instruction;
        public int[] arguments; // an integer argument used for some instructions
        public string extrainfo; // delete after prelim 2

        //linkage
        public ProgramSymbolTree[] children;
        public ProgramSymbolTree parent;

        // metadata
        public int size;
        public int tree_index;


        public ProgramSymbolTree(object instruction,
            ProgramSymbolTree[] children = null,
            int[] arguments = null,
            int tree_index = 0)
        {
            this.instruction = instruction;

            if (children == null) children = new ProgramSymbolTree[0];
            this.SetChildren(children);

            if (arguments == null) arguments = new int[0];
            this.arguments = arguments;
            this.SetTreeIndex(tree_index);

        }


        public ProgramSymbolTree Clone(ProgramSymbolTree parent = null)
        {

            ProgramSymbolTree clone = new(this.instruction,
                children: new ProgramSymbolTree[0],
                tree_index: this.tree_index);


            clone.extrainfo = this.extrainfo;

            int[] cloned_args = new int[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                cloned_args[i] = arguments[i];
            }
            clone.arguments = cloned_args;

            ProgramSymbolTree[] cloned_children = new ProgramSymbolTree[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                ProgramSymbolTree child = children[i];
                ProgramSymbolTree cloned_child = child.Clone(clone);
                cloned_children[i] = cloned_child;
            }

            clone.SetChildren(cloned_children);

            return clone;
        }


        /// <summary>
        ///     Select a random node from this tree
        /// </summary>
        /// <returns>(parent,random_node)</returns>
        public (ProgramSymbolTree, ProgramSymbolTree) SelectRandomNode(int rnd = 0, ProgramSymbolTree? parent = null)
        {
            if (rnd == 0)
            {
                //choose a random node
                rnd = UnityEngine.Random.Range(1, this.size + 1);
            }

            if (rnd == 1)
            {
                return (parent, this);
            }
            else
            {
                if (children.Length == 1)
                {
                    return children[0].SelectRandomNode(rnd - 1, this);
                }
                else if (children.Length == 2)
                {
                    if (children[0].size >= (rnd - 1))
                    {
                        return this.children[0].SelectRandomNode(rnd - 1, this);
                    }
                    else
                    {
                        return this.children[1].SelectRandomNode(rnd - 1 - this.children[0].size, this);
                    }

                }
                else// if(children.Length == 0)
                {
                    return (parent, this);
                }
            }
        }


        // functions
        public void ChangeInstruction(object instruction, int[] new_args)
        {
            this.arguments = new_args;

            object to_instruction = instruction;
            object from_instruction = this.instruction;

            if (HowManyChildren(to_instruction) != HowManyChildren(from_instruction))
            {
                // new instruction, change amount of children if necessary
                Debug.Log("Changing instruction " + from_instruction + " to " + to_instruction + " with different amount of children; old children will be deleted.");

                ProgramSymbolTree[] new_children = this.children;

                if (HowManyChildren(to_instruction) == 0)
                {
                    new_children = new ProgramSymbolTree[0];
                }
                else if (HowManyChildren(to_instruction) == 1)
                {
                    if (HowManyChildren(from_instruction) == 0)
                    {
                        // from 0 children to 1 --- add an extra blank branch
                        ProgramSymbolTree child = GetENDPST();
                        new_children = new ProgramSymbolTree[] { child };
                    }
                    else if (HowManyChildren(from_instruction) == 1)
                    {
                        // from 1 children to 1 --- children will not change, so nothing to do
                    }
                    else if (HowManyChildren(from_instruction) == 2)
                    {
                        // from 2 children to 1 --- remove a branch
                        ProgramSymbolTree random_child = this.children[UnityEngine.Random.Range(0, 2)];
                        new_children = new ProgramSymbolTree[] { random_child };
                    }

                }
                else if (HowManyChildren(to_instruction) == 2)
                {
                    if (HowManyChildren(from_instruction) == 0)
                    {
                        // from 0 children to 2 --- add 2 blank branches
                        ProgramSymbolTree child1 = GetENDPST();
                        ProgramSymbolTree child2 = GetENDPST();
                        new_children = new ProgramSymbolTree[] { child1, child2 };
                    }
                    else if (HowManyChildren(from_instruction) == 1)
                    {
                        // from 1 children to 2 --- children will not change, so nothing to do
                        ProgramSymbolTree child1 = this.children[0];
                        ProgramSymbolTree child2 = GetENDPST();
                        new_children = new ProgramSymbolTree[] { child1, child2 };
                    }
                    else if (HowManyChildren(from_instruction) == 2)
                    {
                        // from 2 children to 2 --- children will not change, so nothing to do
                    }

                }

                this.SetChildren(new_children);

                //mark as parent of new nodes
                foreach (ProgramSymbolTree child in children)
                {
                    child.parent = this;
                }
            }
            else
            {
                // new instruction, same amount of children, so nothing to do
            }

            this.instruction = to_instruction;


        }

        public void SetChildren(ProgramSymbolTree[] children)
        {
            this.children = children;
            this.RecalculateSize();
            foreach (ProgramSymbolTree child in children)
            {
                child.parent = this;
                child.SetTreeIndex(this.tree_index);
            }
        }

        public void SetTreeIndex(int num)
        {
            this.tree_index = num;
            foreach (ProgramSymbolTree child in children)
            {
                child.SetTreeIndex(num);
            }
        }

        public void RecalculateSize()
        {
            this.size = 1;
            //update size and mark as parent of new nodes
            foreach (ProgramSymbolTree child in children)
            {
                this.size += child.size;
            }
        }

        /// <summary>
        /// Insert a node at the child index. The child there will become a grandchild.
        /// </summary>
        /// <param name="index"></param>
        public void InsertNode(int index, ProgramSymbolTree new_child)
        {

            ProgramSymbolTree old_child = this.children[index];
            if (HowManyChildren(new_child.instruction) == 1)
            {
                new_child.SetChildren(new ProgramSymbolTree[] { old_child });
            }
            else if (HowManyChildren(new_child.instruction) == 2)
            {
                int rnd = UnityEngine.Random.Range(0, 2); // RANDOM
                if (rnd == 0) new_child.SetChildren(new ProgramSymbolTree[] { old_child, GetENDPST() });
                else new_child.SetChildren(new ProgramSymbolTree[] { GetENDPST(), old_child });

            }

            if (HowManyChildren(this.instruction) == 1)
            {
                this.SetChildren(new ProgramSymbolTree[] { new_child });
            }
            else if (HowManyChildren(this.instruction) == 2)
            {
                if (index == 0)
                {
                    this.SetChildren(new ProgramSymbolTree[] { new_child, this.children[1] });
                }
                else if (index == 1)
                {
                    this.SetChildren(new ProgramSymbolTree[] { this.children[0], new_child });
                }

            }

        }

        public int HowManyChildren(object instruction)
        {
            if (instruction is CECellularInstruction)
            {
                return CellularEncodingBrainGenome.HowManyChildren(instruction);
            }
            else if (instruction is AxonalGrowthCellularInstruction)
            {
                return AxonalGrowthBrainGenome.HowManyChildren(instruction);
            }
            else
            {
                GlobalUtils.LogErrorEnumNotRecognized("Instruction not valid type");
                return -1;
            }
        }

        public int HowManyArguments(object instruction)
        {
            if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.CellularEncoding)
            {
                return 0;
            }
            else if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.SGOCE)
            {
                return AxonalGrowthBrainGenome.HowManyArguments(instruction);
            }
            else
            {
                GlobalUtils.LogErrorEnumNotRecognized("Instruction not valid type");
                return -1;
            }
        }

    }


    public static ProgramSymbolTree GetENDPST()
    {
        return new ProgramSymbolTree(instruction: GetENDInstruction(), children: new ProgramSymbolTree[0]);
    }

    public static object GetENDInstruction()
    {
        if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.CellularEncoding)
        {
            return CECellularInstruction.END;
        }
        else if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.SGOCE)
        {
            return AxonalGrowthCellularInstruction.END;
        }
        else
        {
            GlobalUtils.LogErrorEnumNotRecognized("Instruction not valid type");
            return null;
        }
    }

    public class TreeDevelopmentNode : DevelopmentNeuron
    {
        public List<TreeDevelopmentSynapse> inputs;
        public List<TreeDevelopmentNode> outputs; // for information purposes only

        public TreeDevelopmentNode(List<TreeDevelopmentSynapse> inputs,
            List<TreeDevelopmentNode> outputs,
            int threshold = 1,
            float bias = 1,
            bool sign = true,
            float adaptation = 1,
            float decay = 1,
            float sigmoid_alpha = 1) : base(threshold, bias, sign, adaptation, decay, sigmoid_alpha)
        {
            this.inputs = inputs;
            this.outputs = outputs;
        }
    }

    public class IOBlock : TreeDevelopmentNode
    {
        public IOBlock() : base(null, null)
        {
            this.inputs = new();
            this.outputs = new();
        }
    }

    public class TreeDevelopmentSynapse : DevelopmentSynapse
    {
        public TreeDevelopmentNode from_pointer;
        public TreeDevelopmentSynapse(TreeDevelopmentNode pointer, float[] coefficients = null, float learning_rate = 1) : base(coefficients, learning_rate)
        {
            this.from_pointer = pointer;
        }

        public TreeDevelopmentSynapse Clone()
        {
            return new TreeDevelopmentSynapse(this.from_pointer,
                (float[])this.coefficients.Clone(),
                this.learning_rate);
        }
    }



    public class TreeDevelopmentNeuron : TreeDevelopmentNode
    {
        public ProgramSymbolTree instruction_pointer;

        public int life = 5;

        public int recursive_limit; // how many function calls deep can this cell go
        public int link_register;
        public float3 position;

        public TreeDevelopmentNeuron(ProgramSymbolTree instruction_pointer,
            List<TreeDevelopmentSynapse> inputs,
            List<TreeDevelopmentNode> outputs,
            int link_register,
            int threshold,
            float bias,
            bool sign,
            float adaptation,
            float decay,
            float sigmoid_alpha) : base(inputs, outputs, threshold, bias, sign, adaptation, decay, sigmoid_alpha)
        {

            this.instruction_pointer = instruction_pointer;
            this.link_register = link_register;
            this.recursive_limit = 5;
        }



        public void MoveToChildA()
        {
            this.instruction_pointer = this.instruction_pointer.children[0];
        }

        public void MoveToChildB()
        {
            this.instruction_pointer = this.instruction_pointer.children[1];
        }

        public int get_link_register()
        {
            if (this.inputs.Count == 0) return 0;
            return MathHelper.mod(this.link_register, this.inputs.Count);
        }


        public TreeDevelopmentNeuron Clone(bool clone_synapses = true)
        {

            List<TreeDevelopmentSynapse> cloned_inputs = new();
            List<TreeDevelopmentNode> cloned_outputs = new();


            TreeDevelopmentNeuron clone = new TreeDevelopmentNeuron(this.instruction_pointer,
                 cloned_inputs,
                 cloned_outputs,
                 link_register: this.link_register,
                 threshold: this.threshold,
                 bias: this.bias,
                 sign: this.sign,
                 adaptation: this.adaptation_delta,
                 decay: this.decay,
                 sigmoid_alpha: this.sigmoid_alpha);

            clone.extradata = this.extradata;


            if (!clone_synapses) return clone;

            // for each input
            foreach (TreeDevelopmentSynapse c in this.inputs)
            {
                TreeDevelopmentSynapse cloned_synapse = c.Clone(); //clone it

                if (c.from_pointer == this) // if this is a recursive synapse
                {
                    cloned_synapse.from_pointer = clone; // make it recursive on the clone instead
                    cloned_outputs.Add(clone);
                }
                else
                {
                    c.from_pointer.outputs.Add(clone); // add
                }
                cloned_inputs.Add(cloned_synapse);
            }



            // clone the outputs
            if (this.outputs != null)
            {
                foreach (TreeDevelopmentNode o in this.outputs)
                {
                    // add to outputs for info purposes
                    cloned_outputs.Add(o);

                    // clone output connection
                    foreach (TreeDevelopmentSynapse c in o.inputs)
                    {
                        if (c.from_pointer == this)
                        {
                            TreeDevelopmentSynapse c_clone = c.Clone();
                            o.inputs.Add(c_clone); // clone the output connection
                            c_clone.from_pointer = clone;
                            break;
                        }
                    }
                }
            }



            return clone;

        }

    }



}
