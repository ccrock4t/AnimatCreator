using Unity.Mathematics;
using UnityEngine;

public class GlobalConfig : MonoBehaviour
{
    public enum ProcessingMethod
    {
        CPU,
        GPU
    }

    public enum SmoothingMethod
    {
        None,
        MarchingCubes
    }

    public enum GenomeMethod
    {
        CPPN,
        LinearGenomeandNEAT
    }


    // === User Preferences ===
    public const int TARGET_FRAMERATE = 30;

    // ============

    // === GPU ===
    // To maximize usage of the GPU, request the max number of threads per thread group (brand-dependent), and request the min number of thread groups per dispatch
    public const int MAX_NUM_OF_THREAD_GROUPS = 65535;  // can only have 65535 thread groups per Dispatch
    public const int NUM_OF_GPU_THREADS = 64; // AMD uses 64 threads per GPU thread group. NVIDIA uses 32.

    // ============

    // === Genome ===
    public const GenomeMethod GENOME_METHOD = GenomeMethod.LinearGenomeandNEAT;

    // === Generator Network ===
    public const int generator_network_hidden_layer_width = 10;
    public const int generator_network_hidden_layer_quantity = 10;

    // === Animat ===
    public const int NUM_OF_SENSOR_NEURONS = 2; // touch, ray1, or  driving function (sine) 
    public const int NUM_OF_HIDDEN_NEURONS_PER_LAYER = 4;
    public const int NUM_OF_HIDDEN_LAYERS = 3;
    // deleted 
    public const int NUM_OF_MOTOR_NEURONS = 3; // motor x, motor y

    public static readonly int MAX_LAYER_SIZE = math.max(NUM_OF_MOTOR_NEURONS, math.max(NUM_OF_SENSOR_NEURONS, NUM_OF_HIDDEN_NEURONS_PER_LAYER));

    //x,y,z,layernum,neuron num in layer

    public const bool USE_MULTILAYER_PERCEPTRONS_IN_CELL = true;
    public static readonly int5 ANIMAT_SUBSTRATE_DIMENSIONS = USE_MULTILAYER_PERCEPTRONS_IN_CELL ? new (5,3,3, NUM_OF_HIDDEN_LAYERS + 2, MAX_LAYER_SIZE) : new(6, 6, 6, 1, 1); 


    // === Animat brain ===
    public const ProcessingMethod brain_processing_method = ProcessingMethod.CPU;
    public const ProcessingMethod brain_genome_development_processing_method = ProcessingMethod.CPU;

    public const float ANIMAT_BRAIN_UPDATE_PERIOD = 0.04f; // must be a multiple to Time.fixedDeltaTime (default=0.02), since it is used in FixedUpdate()
    public const float BRAIN_VIEWER_UPDATE_PERIOD = 0.08f;

    // ============

    // === Animat body ===
    public const int MAX_VOXELYZE_ITERATIONS = 200000;

    // ============

    // === Saving and loading ===
    public const string save_file_path = "SaveFiles/";
    public const string save_file_base_name = "myfile";
    public const string open_string = "[";
    public const string close_string = "]";
    // ============
}