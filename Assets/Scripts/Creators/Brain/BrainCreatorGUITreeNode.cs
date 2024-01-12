using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static AxonalGrowthBrainGenome;
using static BrainGenomeTree;
using static CellularEncodingBrainGenome;
using static GraphVisualization2D;

public class BrainCreatorGUITreeNode : MonoBehaviour
{
    public TMP_Dropdown dropdown;
    BrainCreator creator;
    public CellularEncodingBrainGenome.ProgramSymbolTree tree;
    public BrainCreatorGUITreeLink link_to_parent;
    public BrainCreatorGUITreeNode parent;

    // for GUI only
    public PositionData2D position_data_2D;

    // Start is called before the first frame update
    void Start()
    {

    }

    public void Initialize(CellularEncodingBrainGenome.ProgramSymbolTree tree, PositionData2D parent=null)
    {
        this.dropdown = this.transform.GetComponent<TMP_Dropdown>();
        this.tree = tree;
        this.position_data_2D = new(parent: parent);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// Insert a WAIT instruction between this node and parent
    /// </summary>
    public void InsertNewInstructionAbove()
    {

        ProgramSymbolTree empty_instruction = new CellularEncodingBrainGenome.ProgramSymbolTree(CECellularInstruction.WAIT );
        for (int i=0; i< this.parent.tree.children.Length; i++)
        {
            CellularEncodingBrainGenome.ProgramSymbolTree child = this.parent.tree.children[i];
            if(child == this.tree)
            {
                empty_instruction.SetTreeIndex(child.tree_index);
                this.parent.tree.InsertNode(i, empty_instruction);
                break;
            }
        }

        // tell manager so it can spawn child nodes
        this.creator.Initialize();
    }

    /// <summary>
    /// Provide this node's updates to "creator"
    /// </summary>
    /// <param name="creator"></param>
    /// <param name="tree"></param>
    public void Subscribe(BrainCreator creator)
    {
        this.creator = creator;
    }

    public void InstructionChanged()
    {
        // overwrite instruction
        string instruction_short = this.dropdown.options[this.dropdown.value].text;

        object to_instruction, from_instruction;

        if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.CellularEncoding)
        {
            to_instruction = (CECellularInstruction)GlobalUtils.enumValueOf(instruction_short, typeof(CECellularInstruction));
            from_instruction = (CECellularInstruction)this.tree.instruction;
        }
        else if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.SGOCE)
        {
            to_instruction = (AxonalGrowthCellularInstruction)GlobalUtils.enumValueOf(instruction_short, typeof(AxonalGrowthCellularInstruction));
            from_instruction = (AxonalGrowthCellularInstruction)this.tree.instruction;
        }
        else
        {
            Debug.LogError("Not supported.");
            return;
        }

        // tell manager so it can spawn child nodes
        this.creator.NodeChanged(this, from_instruction, to_instruction);

        UpdateColor();
    }

    public void UpdateColor()
    {
        // colors
        ColorBlock block = this.dropdown.colors;

        if ((int)this.tree.instruction == (int)BrainGenomeTree.GetENDInstruction())
        {
            block.normalColor = Color.red;
        }
        else
        {
            block.normalColor = Color.white;
        }
        this.dropdown.colors = block;
    }

    public void ResetPositionData()
    {
        this.position_data_2D.x = 0;
        this.position_data_2D.y = 0;
        this.position_data_2D.mod = 0;
        this.position_data_2D.thread = null;
        this.position_data_2D.ancestor = this.position_data_2D;
        this.position_data_2D.change = 0;
        this.position_data_2D.shift = 0;
    }

    public void AddChild(BrainCreatorGUITreeNode child)
    {
        this.position_data_2D.children.Add(child.position_data_2D);
    }

    public void SetPosition(float root_x)
    {
        // calculate node position, with some weird tweaking to make it work correctly
        float newX = (this.position_data_2D.x - root_x) * 80;
        if (float.IsNaN(newX))
        {
            newX = Random.Range(-500, 500);
            Debug.Log("X coordinate of button was NaN. Moving button to this.x = " + newX + " and rootX = " + root_x);
            //newX = Random.Range(-500, 500);
        }
        float newY = -this.position_data_2D.y * 100;
        if (float.IsNaN(newY))
        {
            newY = Random.Range(-500, 500);
            Debug.Log("Y coordinate of button was NaN. Moving button to this.y = " + newY);
            //newX = Random.Range(-500, 500);
        }
       
        
        this.gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector3(
            newX,
            newY);

        // position links
        if (this.link_to_parent != null)
        {
            this.link_to_parent.Initialize(this, this.parent.gameObject.GetComponent<RectTransform>(), this.gameObject.GetComponent<RectTransform>());
            this.link_to_parent.SetPositions();
        }



    }
}
