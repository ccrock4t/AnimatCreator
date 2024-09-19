using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimatArenaFood : MonoBehaviour
{
    public AnimatArena arena;

    // Start is called before the first frame update
    void Start()
    {
        this.transform.gameObject.tag = "Food";
    }

    // Update is called once per frame
    void Update()
    {
    }

}
