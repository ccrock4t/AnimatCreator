using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using static Brain;

public class BrainViewerSynapse : MonoBehaviour
{
    public GameObject gameObjectA;
    public GameObject gameObjectB;
    public Synapse synapse;
    public LineRenderer lr;

    public const float START_WIDTH = 1.0F;
    public const float END_WIDTH = 0.05F;

    // Start is called before the first frame update
    public void Initialize()
    {
        this.lr = this.gameObject.GetComponent<LineRenderer>();
        this.lr.startWidth = START_WIDTH;
        this.lr.endWidth = END_WIDTH;

    }

    void FixedUpdate()
    {
        
    }

    public void UpdateLinePosition()
    {
        this.lr.SetPositions(new Vector3[] { gameObjectA.transform.position, gameObjectB.transform.position });
    }
}
