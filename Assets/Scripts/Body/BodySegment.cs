using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BodySegment : MonoBehaviour
{
    public TouchSensor[] touch_sensors;
    // Start is called before the first frame update
    void Awake()
    {
        touch_sensors = new TouchSensor[6];
        Transform segment = this.transform.Find("Segment");
        touch_sensors[0] = segment.Find("TopTouchSensor").GetComponent<TouchSensor>();
        touch_sensors[1] = segment.Find("BottomTouchSensor").GetComponent<TouchSensor>();
        touch_sensors[2] = segment.Find("LeftTouchSensor").GetComponent<TouchSensor>();
        touch_sensors[3] = segment.Find("RightTouchSensor").GetComponent<TouchSensor>();
        touch_sensors[4] = segment.Find("FrontTouchSensor").GetComponent<TouchSensor>();
        touch_sensors[5] = segment.Find("BackTouchSensor").GetComponent<TouchSensor>();
    }



    // Update is called once per frame
    void Update()
    {
        
    }



}
