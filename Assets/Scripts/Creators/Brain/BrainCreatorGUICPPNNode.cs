using TMPro;
using UnityEngine;

public class BrainCreatorGUICPPNNode : MonoBehaviour
{
    public TMP_Dropdown dropdown;
    GenomeCreator creator;

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
    public void Subscribe(GenomeCreator creator)
    {
        this.creator = creator;
    }

  

    
}
