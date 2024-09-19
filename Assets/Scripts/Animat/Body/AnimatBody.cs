using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public abstract class AnimatBody : MonoBehaviour
{
    public abstract void Teleport(Vector3 position, Quaternion rotation);
    public abstract void SetColorToColorful();
    public abstract void SetColorToStone();
    public abstract float3 GetCenterOfMass();
    public abstract void ResetPositionsAndVelocities();
}
