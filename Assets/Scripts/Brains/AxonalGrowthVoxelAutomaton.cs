using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static AxonalGrowthBrainGenome;
using static BrainGenome;
using static BrainGenomeTree;
using NeuralVoxelCellInfo = CellInfo<NeuralVoxelCell?>;

public class NeuralVoxelCell { 


    public int3 coords;
    public TreeDevelopmentNeuron neuron;

    public NeuralVoxelCell(TreeDevelopmentNeuron neuron, int3 coords)
    {
        this.coords = coords;
        this.neuron = neuron;
    }

    public NeuralVoxelCell Clone(int3 new_coords)
    {
        TreeDevelopmentNeuron cloned_neuron = neuron.Clone(false);
        cloned_neuron.inputs.Clear();
        return new NeuralVoxelCell(neuron: cloned_neuron, coords: new_coords);
    }
}


public class AxonalGrowthVoxelAutomaton
{
    // constants
    const int MAX_ATTEMPTS_TO_SYNAPSE = 5;
    public const float hebb_coefficient_increment = 0.05f;
    public const float LR_coefficient_increment = 0.05f;
    public const float sigmoid_coefficient_increment = 0.05f;

    //public NativeArray<AxonalCellInfo> cell_grid; // stores data for each cell. x is current state, y is the modified flag z is the computed next state
    public int3 automaton_dimensions;
    public NeuralVoxelCellInfo[,,] cell_array;
    public AxonalGrowthBrainGenome genome;

    public List<NeuralVoxelCell> developing_brain;
    public List<DevelopmentNeuron> developed_brain;
    public List<NeuralVoxelCell> cells_born_this_step;


    // for visualization only
    public Material finalized_neuron_material;
    public GameObject link_prefab;

    public const float NEURON_CUBE_SPACING = 1.2f; // above 1
    public bool visualize = false;

    // Start is called before the first frame update
    public AxonalGrowthVoxelAutomaton(AxonalGrowthBrainGenome genome)
    {
        this.automaton_dimensions = new int3(50, 50, 50);
        this.cell_array = new NeuralVoxelCellInfo[this.automaton_dimensions.x, this.automaton_dimensions.y, this.automaton_dimensions.z];
        this.developing_brain = new();
        this.developed_brain = new();
        this.cells_born_this_step = new();
        int automaton_size = this.automaton_dimensions.x * this.automaton_dimensions.y * this.automaton_dimensions.z;
        //this.cell_grid = new(length: automaton_size, Allocator.Persistent);

        this.finalized_neuron_material = (Material)Resources.Load("Materials/pink");
        this.link_prefab = (GameObject)Resources.Load("Prefabs/Creators/Brain/Link");



        if (this.genome == null)
        {
            this.genome = AxonalGrowthBrainGenome.CreateTestGenome();
        }
        else
        {
            this.genome = genome;
        }

        // initialize array
        TreeDevelopmentNeuron ancestor_neuron = new(instruction_pointer: genome.forest[0],
            inputs: new List<TreeDevelopmentSynapse>(),
            outputs: null,
            link_register: 0,
            threshold: 1,
            bias: 0,
            sign: true,
            adaptation: 1,
            decay: 0.99f,
            sigmoid_alpha: 1);

        int3 ancestor_coords = new int3(24, 24, 24);

        ancestor_neuron.position = new float3(1.0f*ancestor_coords.x / this.automaton_dimensions.x, 1.0f * ancestor_coords.y / this.automaton_dimensions.y, 1.0f * ancestor_coords.z / this.automaton_dimensions.z);




        NeuralVoxelCell ancestor_cell = new(neuron: ancestor_neuron, coords: new int3(24,24,24));
        NeuralVoxelCellInfo ancestor_cell_info = this.cell_array[ancestor_coords.x, ancestor_coords.y, ancestor_coords.z];
        ancestor_cell_info.current_state = ancestor_cell;
        this.cell_array[ancestor_coords.x, ancestor_coords.y, ancestor_coords.z] = ancestor_cell_info;
        

        this.developing_brain.Add(ancestor_cell);
    }

    public void InsertSensorimotorNeuron(int3 coords, string extradata)
    {
        int x = coords.x;
        int y = coords.y;
        int z = coords.z;

     /*   if (this.cell_array[x, y, z].current_state != null)
        { 
            Debug.LogError("Cannot insert neuron; space is occupied");
            return;
        }
*/

        TreeDevelopmentNeuron neuron = new(instruction_pointer: null,
            inputs: new List<TreeDevelopmentSynapse>(),
            outputs: null,
            link_register: 0,
            threshold: 1,
            bias: 0,
            sign: true,
            adaptation: 1,
            decay: 0.99f,
            sigmoid_alpha: 1);

        neuron.extradata = extradata;
        neuron.position = new(1.0f * coords.x/ this.automaton_dimensions.x, 1.0f * coords.y/ this.automaton_dimensions.y, 1.0f * coords.z/ this.automaton_dimensions.z);
        NeuralVoxelCell neuron_cell = new(neuron: neuron, coords: coords);
        NeuralVoxelCellInfo cell_info = this.cell_array[x, y, z];
        cell_info.current_state = neuron_cell;

        this.cell_array[x, y, z] = cell_info;

        this.developed_brain.Add(neuron);

    }





    public void CalculateAutomatonToEnd()
    {
        while(this.developing_brain.Count > 0)
        {
            CalculateNextGridState();
        }
    }

    public bool IsOutOfBounds(int3 coords)
    {
        return (coords.x < 0 || coords.x >= this.automaton_dimensions.x
            || coords.y < 0 || coords.y >= this.automaton_dimensions.y
            || coords.z < 0 || coords.z >= this.automaton_dimensions.z) ;
    }

    public void CalculateNextGridState()
    {

        int targetX, targetY, targetZ;
        NeuralVoxelCell target_voxel_cell;
        NeuralVoxelCellInfo target_voxel_cell_info;


       
        for(int i=0; i < this.developing_brain.Count; i++)
        {
            NeuralVoxelCell neural_voxel_cell = this.developing_brain[i];
            TreeDevelopmentNeuron cell = neural_voxel_cell.neuron;
            ProgramSymbolTree pointer = cell.instruction_pointer;
            AxonalGrowthCellularInstruction instruction = (AxonalGrowthCellularInstruction)pointer.instruction;
            TreeDevelopmentSynapse connection;

            if (cell.life <= 0) instruction = AxonalGrowthCellularInstruction.END;

            switch (instruction)
            {
                case AxonalGrowthCellularInstruction.DIVISION:
                case AxonalGrowthCellularInstruction.CLONE:
                    /*
                     *  Create a new neuron
                     */

                    targetX = pointer.arguments[0] + neural_voxel_cell.coords.x;
                    targetY = pointer.arguments[1] + neural_voxel_cell.coords.y;
                    targetZ = pointer.arguments[2] + neural_voxel_cell.coords.z;

                    if(!IsOutOfBounds(new int3(targetX, targetY, targetZ))){
                        target_voxel_cell_info = this.cell_array[targetX, targetY, targetZ];

                        // try to ensure a valid cell
                        int attempts = 0;
                        while (target_voxel_cell_info.current_state != null && attempts < 1)
                        {
                            targetX += UnityEngine.Random.Range(-1, 2);
                            targetY += UnityEngine.Random.Range(-1, 2);
                            targetZ += UnityEngine.Random.Range(-1, 2);
                            if (!IsOutOfBounds(new int3(targetX, targetY, targetZ))) target_voxel_cell_info = this.cell_array[targetX, targetY, targetZ];
                            attempts++;
                        }

                        if (target_voxel_cell_info.current_state == null)
                        {
                            // cell is empty, so we can divide into it
                            target_voxel_cell = neural_voxel_cell.Clone(new_coords: new int3(targetX, targetY, targetZ));

                            target_voxel_cell.neuron.position = new(1.0f* targetX / this.automaton_dimensions.x,
                                                                   1.0f * targetY / this.automaton_dimensions.y,
                                                                    1.0f * targetZ / this.automaton_dimensions.z);
                            if (instruction == AxonalGrowthCellularInstruction.CLONE)
                            {
                                target_voxel_cell.neuron.MoveToChildA();
                            }
                            else // AxonalGrowthCellularInstruction.DIVISION
                            {
                                target_voxel_cell.neuron.MoveToChildB();
                                // target_voxel_cell.neuron.MoveToChildB();
                       /*         int tree_index2 = cell.instruction_pointer.tree_index;
                                int offset2 = cell.instruction_pointer.arguments[3];
                                int new_tree_idx2 = MathHelper.mod(tree_index2 + offset2, genome.forest.Count);
                                target_voxel_cell.neuron.instruction_pointer = genome.forest[new_tree_idx2];
                                target_voxel_cell.neuron.life--;*/
                            }

                            cells_born_this_step.Add(target_voxel_cell);
                            target_voxel_cell_info.current_state = target_voxel_cell;
                            this.cell_array[targetX, targetY, targetZ] = target_voxel_cell_info;
                        }
                    }


                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.GROW:
                case AxonalGrowthCellularInstruction.DRAW:
                    /*
                     *  Create a new synapse
                     */
                    targetX = pointer.arguments[0] + neural_voxel_cell.coords.x;
                    targetY = pointer.arguments[1] + neural_voxel_cell.coords.y;
                    targetZ = pointer.arguments[2] + neural_voxel_cell.coords.z;


                    if (!IsOutOfBounds(new int3(targetX, targetY, targetZ)))
                    {
                        target_voxel_cell_info = this.cell_array[targetX, targetY, targetZ];

                        // try to ensure a valid cell
                        int attempts = 0;
                        while(target_voxel_cell_info.current_state == null && attempts < MAX_ATTEMPTS_TO_SYNAPSE)
                        {
                            targetX += UnityEngine.Random.Range(-1, 2);
                            targetY += UnityEngine.Random.Range(-1, 2);
                            targetZ += UnityEngine.Random.Range(-1, 2);
                            if(!IsOutOfBounds(new int3(targetX, targetY, targetZ))) target_voxel_cell_info = this.cell_array[targetX, targetY, targetZ];
                            attempts++;
                        }

                        if (target_voxel_cell_info.current_state != null)
                        {
                            // cell contains a neuron, so we can connect
                            target_voxel_cell = target_voxel_cell_info.current_state;
                            NeuralVoxelCell input_neuron, output_neuron;
                            if (instruction == AxonalGrowthCellularInstruction.GROW)
                            {
                                // grow from this cell to the target cell
                                input_neuron = neural_voxel_cell;
                                output_neuron = target_voxel_cell;
                            }
                            else // AxonalGrowthCellularInstruction.DRAW
                            {
                                // draw from the target cell to this cell
                                input_neuron = target_voxel_cell;
                                output_neuron = neural_voxel_cell;

                            }
                            TreeDevelopmentSynapse new_synapse = new(pointer: input_neuron.neuron);

                            output_neuron.neuron.inputs.Add(new_synapse);
                        }
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.INCREMENT_LINK_REGISTER:
                    cell.link_register++;
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.DECREMENT_LINK_REGISTER:
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
                case AxonalGrowthCellularInstruction.INCREMENT_BIAS:
                    cell.bias += hebb_coefficient_increment;
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.DECREMENT_BIAS:
                    cell.bias -= hebb_coefficient_increment;
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.SIGN_LR:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.learning_rate *= -1;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.SIGN_A:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.coefficients[0] *= -1;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.SIGN_B:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.coefficients[1] *= -1;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.SIGN_C:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.coefficients[2] *= -1;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.SIGN_D:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.coefficients[3] *= -1;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.INCREMENT_A:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.coefficients[0] += hebb_coefficient_increment;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.DECREMENT_A:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.coefficients[0] -= hebb_coefficient_increment;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.INCREMENT_B:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.coefficients[1] += hebb_coefficient_increment;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.DECREMENT_B:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.coefficients[1] -= hebb_coefficient_increment;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.INCREMENT_C:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.coefficients[2] += hebb_coefficient_increment;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.DECREMENT_C:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.coefficients[2] -= hebb_coefficient_increment;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.INCREMENT_D:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.coefficients[3] += hebb_coefficient_increment;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.DECREMENT_D:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.coefficients[3] -= hebb_coefficient_increment;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.INCREMENT_LEARNING_RATE:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.learning_rate += LR_coefficient_increment;
                    }
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.DECREMENT_LEARNING_RATE:
                    if (cell.inputs.Count > 0)
                    {
                        connection = (TreeDevelopmentSynapse)cell.inputs[cell.get_link_register()];
                        connection.learning_rate -= LR_coefficient_increment;
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
                case AxonalGrowthCellularInstruction.INCREMENT_SIGMOID_ALPHA:
                    cell.sigmoid_alpha += sigmoid_coefficient_increment;
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.DECREMENT_SIGMOID_ALPHA:
                    cell.sigmoid_alpha -= sigmoid_coefficient_increment;
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.SIGN:
                    cell.sign = !cell.sign;
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.JUMP:
                    //if (cell.call_stack.Count < cell.recursive_limit)
                    //{
                        // call the function if the recursive limit allows it
                    int tree_index = cell.instruction_pointer.tree_index;
                    int offset = cell.instruction_pointer.arguments[0];
                    int new_tree_idx = MathHelper.mod(tree_index + offset, genome.forest.Count);
                    //cell.call_stack.Push(cell.instruction_pointer);
                    cell.instruction_pointer = genome.forest[new_tree_idx];
                    cell.life--;
       /*             }
                    else
                    {
                        // skip the instruction if recursive limit is reached
                        cell.MoveToChildA();
                    }*/

                    break;
 /*               case AxonalGrowthCellularInstruction.SWAP:
                    // simply move to next instruction
                    Debug.LogError("TODO");
                    cell.MoveToChildA();
                    break;*/
                case AxonalGrowthCellularInstruction.WAIT:
                    // simply move to next instruction
                    cell.MoveToChildA();
                    break;
                case AxonalGrowthCellularInstruction.END:
/*                    if(cell.call_stack.Count > 0)
                    {
                        // function is finished, go back up the call stack.
                        ProgramSymbolTree call_point = cell.call_stack.Pop();
                        cell.instruction_pointer = call_point;
                        cell.MoveToChildA();
                    }
                    else
                    {*/
                    // program is complete, finalize the neuron

                    this.developed_brain.Add(cell);
                    this.developing_brain.RemoveAt(i);
                    i--;
                  //  }
                    
                    break;
                default:
                    Debug.LogError("INSTRUCTION NOT RECOGNIZED");
                    break;
            }
        }


        this.developing_brain.AddRange(cells_born_this_step);

        cells_born_this_step.Clear();
    }




}
