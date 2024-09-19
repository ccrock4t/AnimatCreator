using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MathHelper
{

    /// <summary>
    ///  x MOD m
    /// </summary>
    /// <param name="x"></param>
    /// <param name="m"></param>
    /// <returns></returns>
    public static int mod(int x, int m)
    {
        int r = x % m;
        return r < 0 ? r + m : r;
    }

}
