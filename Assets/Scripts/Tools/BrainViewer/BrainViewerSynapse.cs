using UnityEngine;
using static Brain;

public class BrainViewerSynapse : MonoBehaviour
{
    public GameObject gameObjectA;
    public GameObject gameObjectB;
    public Synapse synapse;
    public LineRenderer lr;

    // Start is called before the first frame update
    void Start()
    {

    }

    void FixedUpdate()
    {

    }

    public void UpdateLinePosition()
    {
        if (this.lr == null) this.lr = this.gameObject.GetComponent<LineRenderer>();
        this.lr.SetPositions(new Vector3[] { gameObjectA.transform.position, gameObjectB.transform.position });
    }
}
