using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrainViewerNeuron : MonoBehaviour
{
    public Brain.Neuron neuron;
    public SpriteRenderer SR;

    // Start is called before the first frame update
    void Start()
    {
        this.SR = this.GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateColor(bool excitatory, float activation)
    {
        Color color;
        if (excitatory)
        {
            if(neuron.activation >= 0)
            {
                color = new Color(0, neuron.activation, 0, 1);
            }
            else
            {
                color = new Color(-1*neuron.activation, 0, 0, 1);
            }
            
        }
        else
        {
            if (neuron.activation >= 0)
            {
                color = new Color(neuron.activation, 0, 0, 1);
            }
            else
            {
                color = new Color(0, -1 * neuron.activation, 0, 1);
            }
        }

        if(this.SR == null) this.SR = this.GetComponent<SpriteRenderer>();
        this.SR.color = color;
    }
}
