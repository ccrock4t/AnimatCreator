using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TouchSensor : MonoBehaviour
{
    public Material green, red;
    public bool touching_terrain = false;

    Renderer mr;

    private void Awake()
    {
        //mr = this.transform.GetComponent<Renderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.name == "Ground")
        {
            touching_terrain = true;
           // mr.material = green;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.name == "Ground")
        {
            touching_terrain = false;
            //mr.material = red;
        }
    }
}
