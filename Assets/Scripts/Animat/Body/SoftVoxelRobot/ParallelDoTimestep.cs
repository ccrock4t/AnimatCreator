using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

using CVoxelyze = System.IntPtr;
using CVX_Material = System.IntPtr;
using CVX_MeshRender = System.IntPtr;
using CVX_Voxel = System.IntPtr;
using CPP_OBJECT = System.IntPtr;
using System;
using Unity.Collections;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

[BurstCompile]
// for each quad
public struct ParallelDoTimestep : IJobParallelFor
{
    [NativeDisableUnsafePtrRestriction]
    public CVoxelyze cpp_voxel_object;

    public float recommended_timestep;

    public int num_threads;
    public int linksListSize;
    public int voxelsListSize;
    public int collisionsListSize;

    [NativeDisableParallelForRestriction]
    public NativeArray<int> current_stage; // only thread 0 may write to this
    [NativeDisableParallelForRestriction]
    public NativeArray<int> diverged;
    [NativeDisableParallelForRestriction]
    public NativeArray<int> counter;

    public int test;

    public void Execute(int i)
    {
       
        while (current_stage[0] <= 6) // stage 6 means we are done
        {
            int stage = current_stage[0];
            if(stage == 7)
            {
                while (true)
                {
                    int x = 1;
                }
            }
            int success = 0;
            if (stage == 1 || stage == 3 || stage == 6)
            {
                if(i == 0)
                {
                    // run with 1 thread, so just call the function with thread 0
                    success = VoxelyzeEngine.DoTimeStepInUnityJob(cpp_voxel_object, recommended_timestep, stage, 0);
                }
            }
            else if(stage == 0 || stage == 2 || stage == 4 || stage == 5)
            {
                bool thread_does_work = (stage == 0 && i < linksListSize)
                    || (stage == 2 && i < voxelsListSize)
                    || (stage == 4 && i < collisionsListSize)
                    || (stage == 5 && i < voxelsListSize);

                if(thread_does_work) success = VoxelyzeEngine.DoTimeStepInUnityJob(cpp_voxel_object, recommended_timestep, stage, i);

    
            }


            if (success != 0)
            {
                this.diverged[0] = success; // any thread can set it to true
                break;
            }
            // post-processing
            if (i == 0)
            {
                while (this.counter[0] < (this.num_threads-1))
                {
                }
                this.counter[0] = 0; // all threads are finished, so reset the counter
                current_stage[0]++; // signal all threads to go to next stage
            }
            else
            {
                this.counter[0]++; // count this thread as finished
                while (stage == current_stage[0])
                {
                    // otherwise, we are done, so wait for thread 0 to change the current stage
                    if (this.diverged[0] != 0) break; // break out if the simulation broke
                }
            }
            if (this.diverged[0] != 0) break; // break out if the simulation broke
        }
    }
    
}
