using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static AxonalGrowthBrainGenome;
using static BrainGenomeTree;
using static CellularEncodingBrainGenome;
using static GraphVisualization2D;

public class BrainCreatorGUINode : MonoBehaviour
{
    public TMP_Dropdown dropdown;
    BrainCreator creator;

    // Start is called before the first frame update
    void Start()
    {

    }

    public void Initialize()
    {
        this.dropdown = this.transform.GetComponent<TMP_Dropdown>();
    }

    // Update is called once per frame
    void Update()
    {
        
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

  

    
}
