using UnityEngine;
using UnityEngine.UI;

public class BrainCreatorGUILink : MonoBehaviour
{
    public LineRenderer LR;
    EdgeCollider2D collider;
    ScrollRect SR;
    bool up;

    public RectTransform targetFrom;
    public RectTransform targetTo;

    public RectTransform input_field;

    // Start is called before the first frame update
    void Start()
    {
    }

    public void Initialize(RectTransform targetFrom, RectTransform targetTo)
    {
        this.LR = GetComponent<LineRenderer>();
        this.collider = GetComponent<EdgeCollider2D>();
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
        Vector3 p1 = this.targetFrom.anchoredPosition;
        Vector3 p2 = this.targetTo.anchoredPosition;
        this.LR.SetPositions(new Vector3[] { p1, p2 });
        this.collider.points = new Vector2[] { new Vector2(p1.x, p1.y), new Vector2(p2.x, p2.y) };
        SetLinkTextPosition();

    }

    public void SetLinkTextPosition()
    {
        this.input_field.anchoredPosition = Vector3.Lerp(this.targetFrom.anchoredPosition, this.targetTo.anchoredPosition, 0.25f);
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

        if (Input.GetMouseButtonDown(0))
        {
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
        this.LR.startColor = Color.black;
        this.LR.endColor = Color.black;
    }
}
