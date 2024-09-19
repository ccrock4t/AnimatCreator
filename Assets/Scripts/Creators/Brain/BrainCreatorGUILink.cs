using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

public class BrainCreatorGUILink : MonoBehaviour
{
    public UILineRenderer LR;
    EdgeCollider2D edge_collider;
    ScrollRect SR;
    bool up;

    public RectTransform targetFrom;
    public RectTransform targetTo;

    public RectTransform input_field;
    public RectTransform directional_arrow;
    public Brain.Synapse synapse;

    // Start is called before the first frame update
    void Start()
    {
    }

    public void Initialize(RectTransform targetFrom, RectTransform targetTo)
    { 
        this.LR = GetComponent<UILineRenderer>();
        this.edge_collider = GetComponent<EdgeCollider2D>();
        this.up = false;

        this.SR = this.transform.GetComponentInParent<ScrollRect>();

        this.targetFrom = targetFrom;
        this.targetTo = targetTo;
        this.SetLinePositions();

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetLinePositions()
    {
        Vector2 p1 = this.targetFrom.anchoredPosition;
        Vector2 p2 = this.targetTo.anchoredPosition;
        this.LR.Points = new Vector2[] { p1, p2 };
        this.edge_collider.points = new Vector2[] { new Vector2(p1.x, p1.y), new Vector2(p2.x, p2.y) };
        SetLinkDecoratorPositions();

    }

    public void SetLinkDecoratorPositions()
    {
        if (this.input_field != null) this.input_field.anchoredPosition = Vector3.Lerp(this.targetFrom.anchoredPosition, this.targetTo.anchoredPosition,0.25f);
        if (this.directional_arrow != null)
        {
            this.directional_arrow.anchoredPosition = Vector3.Lerp(this.targetFrom.anchoredPosition, this.targetTo.anchoredPosition, 0.75f);
            float x = this.targetTo.position.x - this.targetFrom.position.x;
            float h = Vector2.Distance(this.targetTo.position, this.targetFrom.position);
            float angle_to_rotate = Mathf.Rad2Deg * -Mathf.Asin(x / h);
            if (this.targetTo.position.y - this.targetFrom.position.y < 0) angle_to_rotate -= 90f;
            this.directional_arrow.rotation = Quaternion.Euler(new Vector3(this.directional_arrow.eulerAngles.x, this.directional_arrow.eulerAngles.y,angle_to_rotate )); 
        }

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
            this.LR.color = colorA;
            /*this.LR.startColor = colorA;
            this.LR.endColor = colorA;*/
        }
        else
        {
            this.LR.color = colorA;
            /*     this.LR.startColor = colorA;
                 this.LR.endColor = colorA;*/
        }

        if(Input.GetMouseButtonDown(0)){
            // left click
            Canvas.ForceUpdateCanvases();

            if (this.up)
            {
                SnapTo(this.targetFrom);
            }
            else
            {
                SnapTo(this.targetTo);
            }
            this.up = !this.up;
        }

    }

    void OnMouseExit()
    {
        this.LR.color = Color.black;
    }
}
