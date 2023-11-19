using Unity.Mathematics;
using UnityEngine;

public class GlobalConfig : MonoBehaviour
{
    public enum ProcessingMethod
    {
        CPU,
        GPU
    }


    public enum BrainGenomeMethod
    {
        CellularEncoding,
        AxonalGrowth,
        NEAT,
        HyperNEAT,
        ESHyperNEAT
    }




    // types of creatures
    public enum Creature
    {
        Asparagus,
        Tree,
        Bug,
        Human,
        Spider,
        Hexapod,
        Quadruped,
        Biped
    };

 


    public static Creature creature_to_use = Creature.Quadruped;

    // === GPU ===
    // To maximize usage of the GPU, request the max number of threads per thread group (brand-dependent), and request the min number of thread groups per dispatch
    public const int MAX_NUM_OF_THREAD_GROUPS = 65535;  // can only have 65535 thread groups per Dispatch
    public const int NUM_OF_GPU_THREADS = 64; // AMD uses 64 threads per GPU thread group. NVIDIA uses 32.

    // ============


    // === Animat brain ===
    public static ProcessingMethod brain_processing_method = ProcessingMethod.CPU;
    public static ProcessingMethod brain_genome_development_processing_method = ProcessingMethod.CPU;
    public static BrainGenomeMethod brain_genome_method = BrainGenomeMethod.HyperNEAT;

    public static float ANIMAT_BRAIN_UPDATE_PERIOD = 0.2f; // must be a multiple to Time.fixedDeltaTime (default=0.02)
    public static float BRAIN_VIEWER_UPDATE_PERIOD = 0.2f;

    // ============

    // === Animat body ===
    public const float ANIMAT_BODY_SCALE = 0.2f;

    public const bool USE_FORCE_MODE = false;
    // ============


    private void Awake()
    {

    }
}