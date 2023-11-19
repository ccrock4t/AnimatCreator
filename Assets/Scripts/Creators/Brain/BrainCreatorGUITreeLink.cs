using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static BrainGenomeTree;

public class BrainCreatorGUITreeLink : MonoBehaviour
{
    public LineRenderer LR;
    EdgeCollider2D collider;
    ScrollRect SR;
    bool up;
    bool initialized = false;
    public RectTransform targetUp;
    public RectTransform targetDown;

    BrainCreatorGUITreeNode source_node;


    // Start is called before the first frame update
    void Start()
    {

    }

    public void Initialize(BrainCreatorGUITreeNode source_node, RectTransform targetUp, RectTransform targetDown)
    { 
        this.LR = GetComponent<LineRenderer>();
        this.collider = GetComponent<EdgeCollider2D>();
        this.up = false;
        this.initialized = true;
        this.SR = this.transform.GetComponentInParent<ScrollRect>();

        this.targetUp = targetUp;
        this.targetDown = targetDown;

        this.source_node = source_node;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetPositions()
    {
        Vector3 p1 = this.targetUp.anchoredPosition;
        Vector3 p2 = this.targetDown.anchoredPosition;
        this.LR.SetPositions(new Vector3[] { p1, p2 });
        this.collider.points = new Vector2[] { new Vector2(p1.x, p1.y), new Vector2(p2.x, p2.y) };
    }
    

    public void SnapTo(RectTransform target)
    {
        Canvas.ForceUpdateCanvases();

        this.SR.content.anchoredPosition = (Vector2)this.SR.transform.InverseTransformPoint(this.SR.content.position) - (Vector2)this.SR.transform.InverseTransformPoint(target.position);
    }

    Color colorA = Color.yellow;
    Color colorB = Color.black;
    void OnMouseOver()
    {
        if (up)
        {
            this.LR.startColor = colorA;
            this.LR.endColor = colorA;
        }
        else
        {
            this.LR.startColor = colorA;
            this.LR.endColor = colorA;
        }

        if(Input.GetMouseButtonDown(0)){
            // left click
            Canvas.ForceUpdateCanvases();

            if (this.up)
            {
                SnapTo(this.targetUp);
            }
            else
            {
                SnapTo(this.targetDown);
            }
            this.up = !this.up;
        }else if(Input.GetMouseButtonDown(1)){
            // right click
            this.source_node.InsertNewInstructionAbove();
        }

    }

    void OnMouseExit()
    {
        this.LR.startColor = Color.black;
        this.LR.endColor = Color.black;
    }
}
