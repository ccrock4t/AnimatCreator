
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Unity.Mathematics;
using Unity.Jobs.LowLevel.Unsafe;
using System.Linq;
using Vector3 = UnityEngine.Vector3;
using Vector2 = UnityEngine.Vector2;
using Quaternion = UnityEngine.Quaternion;
using static Animat;
using static CPPNGenome;
using static GlobalConfig;
using Unity.VisualScripting;

public class AnimatArena : MonoBehaviour
{

    public CPPNGenome high_score_genome;

    public List<Animat> current_generation;

    public List<(float, object)> reproductive_pool_reproduction_score;  // score, genome
    public SortedList<float, (float, int, int, int, object)> reproductive_pool_datas;


    public float reproductive_pool_total_score;
    public List<GameObject> food_blocks;

    public const bool USE_OBSTACLES = true;
    public List<GameObject> obstacle_blocks;

    BrainViewer brain_viewer;
    GenomeCreator brain_creator;

    // UI
    public Text high_score_text;

    public TMP_Text animat_info_index;
    public TMP_Text animat_info_generation;
    public TMP_Text animat_info_num_neurons;
    public TMP_Text animat_info_num_connections;
    public TMP_Text animat_info_period;
    public TMP_Text animat_info_energy;
    public TMP_Text animat_info_life;

    public RawImage animat_vision_view;

    public int currently_viewed_animat_idx = -1;
    Animat currently_viewed_animat;

    // prefabs
    [SerializeField]
    GameObject food_prefab;

    [SerializeField]
    GameObject obstacle_prefab;

    // cameras
    public Camera animat_creator_camera, brain_viewer_camera, brain_creator_camera;

    // dynamic vars
    float high_score;

    // data
    const float WRITE_DATA_TO_FILE_TIMER = 50; // in seconds
    const float CHECK_FOOD_COLLISIONS_TIMER = 1f;
    float write_data_timer;
    float check_animat_food_collisions_timer;

    const int MAX_PLACEMENT_ATTEMPTS = 10000;

    // constants
    int MAX_NUMBER_OF_ANIMATS_IN_REPRODUCTIVE_POOL = 50; // for reproduction

    [SerializeField]
    public int2 ARENA_SIZE;

    [SerializeField]
    public int MINIMUM_POPULATION_QUANTITY;

    const int FOOD_QUANTITY = 150;
    const int OBSTACLE_QUANTITY = 100;
    public const int FOOD_GAMEOBJECT_LAYER = 10; // for raycast detections
    public const int OBSTACLE_GAMEOBJECT_LAYER = 11; // for raycast detections
    public const int ANIMAT_GAMEOBJECT_LAYER = 12; // for raycast detections
    bool LOAD_MODE = false; // Load mode means to load the brain from disk, for testing purposes, rather than to evolve 

    // handles

    string data_filename;

    StreamWriter data_file;
    Comparer<float> ascending_score_comparer;
    Comparer<float> descending_score_comparer;

    static AnimatArena _instance;

    // Start is called before the first frame update
    void Start()
    {
        _instance = this;

        string data_directory = Path.Join(Path.Join(Application.dataPath, "ExperimentDataFiles"), "AnimatArena");
        if (!Directory.Exists(data_directory)) Directory.CreateDirectory(data_directory);
        this.data_filename = Path.Join(data_directory, "arena_score_data.txt");

        Time.captureDeltaTime = 1f / GlobalConfig.TARGET_FRAMERATE; // set a target framerate
        JobsUtility.JobWorkerCount = SystemInfo.processorCount - 1; // number of CPU cores minus 1. This prevents Unity from going overthrottling the CPU (https://thegamedev.guru/unity-performance/job-system-excessive-multithreading/)
        Debug.Log("Running with " + JobsUtility.JobWorkerCount + " threads");
        this.current_generation = new();
        this.ascending_score_comparer = Comparer<float>.Create((x, y) =>
        {
            int result = x.CompareTo(y);
            if (result == 0) return 1;
            else return result;
        }); // best animats, sorted in ascending order by score (lowest score first)
        this.descending_score_comparer = Comparer<float>.Create((x, y) =>
        {
            int result = y.CompareTo(x);
            if (result == 0) return 1;
            else return result;
        }); // best animats, sorted in descending order by score (highest score first)
        this.reproductive_pool_reproduction_score = new(); 
        this.reproductive_pool_datas = new(ascending_score_comparer); 
        this.food_blocks = new();
        if(USE_OBSTACLES) this.obstacle_blocks = new();

        if (File.Exists(data_filename))
        {
            File.Delete(data_filename);
        }
        data_file = File.CreateText(data_filename);
        string title = "";
        title += "Num of Elite Animats";
        title += ",";
        title += "Average Distance Score";
        title += ",";
        title += "Max Distance Score";
        title += ",";
        title += "Average Food Eaten";
        title += ",";
        title += "Max Food Eaten";
        title += ",";
        title += "Average Reproductive Score";
        title += ",";
        title += "Max Reproductive Score";
        title += ",";
        title += "Average Times Self-Reproduced";
        title += ",";
        title += "Max Times Self-Reproduced";
        title += ",";
        title += "Generation";
        data_file.WriteLine(title);
        data_file.Close();

        // get objects
        this.brain_creator = GameObject.FindFirstObjectByType<GenomeCreator>();
        this.brain_viewer = GameObject.FindFirstObjectByType<BrainViewer>();

        // set vars
        this.high_score = 0;
        this.reproductive_pool_total_score = 0;

        // spawn food
        for (int i = 0; i < FOOD_QUANTITY; i++)
        {
            SpawnFoodBlock();
        }

        if (USE_OBSTACLES)
        {
            // spawn obstacles
            for (int i = 0; i < OBSTACLE_QUANTITY; i++)
            {
                SpawnObstacleBlock();
            }
        }

        // spawn anim
        // at population
        if (LOAD_MODE)
        { 
            // load the brain from disk and place it in animats
            // evolve animats
            for (int i = 0; i < MINIMUM_POPULATION_QUANTITY; i++)
            {
                LoadGenomeAndSpawn();
            }
        }
        
        SpawnTestGenomeAnimat();
        SwitchViewToAnimat(0);

        //initialize
        UpdateHighScoreText();
    }



    public void SpawnFoodBlock()
    {
        Vector3 position = GetRandomPositionForBlock();
        GameObject new_food_block = Instantiate(food_prefab, position, Quaternion.identity);
        new_food_block.layer = FOOD_GAMEOBJECT_LAYER;
        food_blocks.Add(new_food_block);
        new_food_block.GetComponent<AnimatArenaFood>().arena = this;

    }

    public void SpawnObstacleBlock()
    {
        Vector3 position = GetRandomPositionForBlock();
        GameObject new_obstacle_block = Instantiate(obstacle_prefab, position, Quaternion.identity);
        new_obstacle_block.layer = OBSTACLE_GAMEOBJECT_LAYER;
        obstacle_blocks.Add(new_obstacle_block);
        new_obstacle_block.GetComponent<AnimatArenaFood>().arena = this;

    }

    public void ChangeBlockPosition(GameObject block)
    {
        Vector3 position = GetRandomPositionForBlock();
        block.transform.position = position;
    }

    public Vector3 GetRandomPositionForBlock()
    {
        Vector3 position;
        bool position_found = false;
        int attempts = 0;
        do
        {
            position = new Vector3(UnityEngine.Random.Range(-1f, 1f) * ARENA_SIZE.x, 0f, UnityEngine.Random.Range(-1f, 1f) * ARENA_SIZE.y);
            bool good_position = true;
            foreach (Animat animat in this.current_generation)
            {
                float distance = Vector3.Distance(position, animat.transform.position);
                if (distance < 1.5f)
                {
                    // bad position, try again
                    good_position = false;
                    break;
                }
            }
            foreach (GameObject foodblock in this.food_blocks)
            {
                float distance = Vector3.Distance(position, foodblock.transform.position);
                if (distance < 1.5f)
                {
                    // bad position, try again
                    good_position = false;
                    break;
                }
            }
            if (USE_OBSTACLES)
            {
                foreach (GameObject obstacleblock in this.obstacle_blocks)
                {
                    float distance = Vector3.Distance(position, obstacleblock.transform.position);
                    if (distance < 1.5f)
                    {
                        // bad position, try again
                        good_position = false;
                        break;
                    }
                }
            }

            if (good_position) position_found = true;
            attempts++;
        } while (!position_found && attempts < MAX_PLACEMENT_ATTEMPTS);

        return position;
    }

    public Vector3 GetRandomPositionForAnimat()
    {
        Vector3 position;
        bool position_found = false;
        int attempts = 0;
        do
        {
            position = new Vector3(UnityEngine.Random.Range(-1f, 1f) * ARENA_SIZE.x, 0f, UnityEngine.Random.Range(-1f, 1f) * ARENA_SIZE.y);
            bool good_position = true;
            foreach (GameObject foodblock in this.food_blocks)
            {
                float distance = Vector3.Distance(position, foodblock.transform.position);
                if (distance < 1.5f)
                {
                    // bad position, try again
                    good_position = false;
                    break;
                }
            }
            if (USE_OBSTACLES)
            {
                foreach (GameObject obstacleblock in this.obstacle_blocks)
                {
                    float distance = Vector3.Distance(position, obstacleblock.transform.position);
                    if (distance < 1.5f)
                    {
                        // bad position, try again
                        good_position = false;
                        break;
                    }
                }
            }

            if (good_position) position_found = true;
            attempts++;
        } while (!position_found && attempts < MAX_PLACEMENT_ATTEMPTS);

        return position;
    }

    public static AnimatArena GetInstance()
    {
        return _instance;
    }

    public void FixedUpdate()
    {
        if (this.current_generation.Count < MINIMUM_POPULATION_QUANTITY)
        {
            if (this.current_generation.Count == 1)
            {
                // only animat
                SwitchViewToAnimat(0);
            }

            GenerateNewAnimat();
        }
        
        for (int animat_idx=0; animat_idx < this.current_generation.Count; animat_idx++)
        {
            Animat animat = this.current_generation[animat_idx];
            if (!animat.initialized) continue;
            if (animat.lifespan <= 0 || animat.energy_remaining <= 0 || animat.body.soft_voxel_object.crashed || !animat.body.soft_voxel_object.contains_solid_voxels)
            {
                if (animat.body.soft_voxel_object.crashed) Debug.LogWarning("Voxelyze crashed, killing Animat");
                if (!animat.body.soft_voxel_object.contains_solid_voxels) Debug.LogWarning("Killing animat, it contained no voxels.");
                KillAnimat(animat_idx, !animat.body.soft_voxel_object.contains_solid_voxels);

            }
            else
            {
                animat.DoFixedUpdate();

                float3 pos = animat.GetCenterOfMass();
                int neurons_per_cell = GlobalConfig.USE_MULTILAYER_PERCEPTRONS_IN_CELL ? animat.network_info.GetNumOfNeurons() : 1;



                if (USE_OBSTACLES)
                {
                    // check for obstacle collision
                    foreach (GameObject obstacle_block in this.obstacle_blocks)
                    {
                        float distance = Vector3.Distance(pos, obstacle_block.transform.position);
                        if (distance < obstacle_block.transform.localScale.x + 0.1f)
                        {
                            //animat.number_of_food_eaten = 0;
                            animat.number_of_food_eaten = 0;
                            animat.energy_remaining -= Animat.ENERGY_IN_A_FOOD/4;
                            KillAnimat(animat_idx, false);
                            this.ChangeBlockPosition(obstacle_block);
                            break;
                        }
                    }
                }

            }

            if (this.current_generation.Count < MINIMUM_POPULATION_QUANTITY) GenerateNewAnimat();
        }



        SetAnimatInfoGUI(this.currently_viewed_animat);
    }

    float GetDistanceFromFoodScore(Animat animat)
    {
        return math.max(0, (animat.original_distance_from_food - GetDistanceFromFood(animat)));
    }

    float GetDistanceFromFood(Animat animat)
    {
        return Vector3.Distance(this.food_blocks[0].transform.position, animat.GetCenterOfMass()); 
    }

   
    public float GetAnimatReproductionScore(Animat animat, float distance, int food_eaten)
    {
        if (food_eaten == 0 && animat.times_reproduced == 0)
        {
            return math.min(1,distance / MAX_VISION_DISTANCE);
        }
        else if (food_eaten > 0 && animat.times_reproduced == 0)
        {
            return 1 + food_eaten;
        }
        else if (animat.times_reproduced > 0)
        {
            return food_eaten + math.pow(1 + animat.times_reproduced, 2);
        }
        else // if (food_eaten < 0)
        {
            return 0;
        }

    }

    public void KillAnimat(int i, bool ignore_for_reproduction)
    {
        Animat animat = this.current_generation[i];
       
        // animat must die
        bool view_new_animat = false;
        if (i == currently_viewed_animat_idx)
        {
            view_new_animat = true;
        }
        else if (i < currently_viewed_animat_idx)
        {
            currently_viewed_animat_idx--;
        }
        this.current_generation.RemoveAt(i);

        if (!ignore_for_reproduction)
        {


            float distance_travelled = animat.GetDisplacementFromBirthplace();
            if (distance_travelled < 0) distance_travelled = 0;
            int food_eaten = animat.number_of_food_eaten;

            Debug.Log("Killing animat: Distance travelled: " + distance_travelled);

            float distance_from_food_score = GetDistanceFromFoodScore(animat);
            float animat_reproduction_score = GetAnimatReproductionScore(animat, distance_travelled, food_eaten);

            float worst_score_in_reproductive_pool = 0;
            if(this.reproductive_pool_reproduction_score.Count > 0) worst_score_in_reproductive_pool = this.reproductive_pool_reproduction_score.ElementAt(0).Item1;

            bool should_add_animat_to_pool = true;
            if (this.reproductive_pool_reproduction_score.Count >= MAX_NUMBER_OF_ANIMATS_IN_REPRODUCTIVE_POOL && should_add_animat_to_pool)
            {
                // take out worst animat
                int remove_idx = 0;
                float remove_score = this.reproductive_pool_reproduction_score.ElementAt(remove_idx).Item1;
                this.reproductive_pool_total_score -= remove_score;
                this.reproductive_pool_reproduction_score.RemoveAt(remove_idx);
                this.reproductive_pool_datas.RemoveAt(remove_idx);
            }

            if (this.reproductive_pool_reproduction_score.Count < MAX_NUMBER_OF_ANIMATS_IN_REPRODUCTIVE_POOL)
            {
                // add the new animat  

                this.reproductive_pool_reproduction_score.Add((animat_reproduction_score, animat.genome));
                int generation = -1;
                if (animat.genome is CPPNGenome) {
                    generation = ((CPPNGenome)animat.genome).generation;
                }
                else if (animat.genome is LinearAndNEATGenome)
                {
                    generation = ((LinearAndNEATGenome)animat.genome).generation;
                }
                this.reproductive_pool_datas.Add(animat_reproduction_score, (distance_from_food_score, food_eaten, animat.times_reproduced, generation, animat.genome));
                this.reproductive_pool_total_score += animat_reproduction_score;
                if (float.IsInfinity(this.reproductive_pool_total_score))
                {
                    Debug.LogError("error");
                }


                float high_score_contender = animat_reproduction_score;

                if (high_score_contender > this.high_score && !LOAD_MODE)
                {
                    Debug.Log("NEW HIGH SCORE: " + high_score_contender + "! Saving its genome to disk.");
                    this.high_score = high_score_contender;
                    if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN)
                    {
                        animat.unified_CPPN_genome.SaveToDisk();
                    }
                    else if (GlobalConfig.GENOME_METHOD == GenomeMethod.LinearGenomeandNEAT)
                    {
                        Debug.LogWarning("todo save linear genome to disk");
                    }
                    else
                    {
                        Debug.LogError("error");
                    }
                    UpdateHighScoreText();
                }
            }
            

        }

        animat.Kill();


        if (view_new_animat)
        {
            ViewRandomAnimat();
        }

       
    }


    // Update is called once per frame
    void Update()
    {
        CameraTrackCurrentlySelectedAnimat();

        write_data_timer -= Time.deltaTime;
        if (write_data_timer < 0)
        {
            write_data_timer = WRITE_DATA_TO_FILE_TIMER;
            WriteScoreToFile();
        }
    }
   

    (object, int) PeekProbabilisticFromReproductivePool(int ignore_idx=-1)
    {
        float probability;

        int randomly_selected_idx;

        float total_score = this.reproductive_pool_total_score;
        if (ignore_idx != -1) total_score -= this.reproductive_pool_reproduction_score.ElementAt(ignore_idx).Item1; // this will be treated as having 0 probability

        if(total_score == 0)
        {
            randomly_selected_idx = UnityEngine.Random.Range(0, this.reproductive_pool_reproduction_score.Count);
            if ((ignore_idx != -1 && randomly_selected_idx == ignore_idx))
            {
                randomly_selected_idx = (randomly_selected_idx + 1) % this.reproductive_pool_reproduction_score.Count;
            }
        }
        else
        {
            // generate probability distribution
            float[] probabilities = new float[this.reproductive_pool_reproduction_score.Count];
            int idx = 0;
            foreach ((float score, _) in this.reproductive_pool_reproduction_score)
            {
                if(idx != ignore_idx)
                {
                    probability = score / total_score;
                }
                else
                {
                    probability = 0;
                }

                probabilities[idx] = probability;

                idx++;
            }

            // generate Cumulative Distribution Function (CDF)
            List<float> CDF = new();
            CDF.Add(probabilities[0]);
            for (int i=1; i < probabilities.Length; i++)
            {
                CDF.Add(CDF[^1] + probabilities[i]);
            }

            // use the CDF to pick a random animat
            float rnd = UnityEngine.Random.Range(0, 1f);
            randomly_selected_idx = CDF.BinarySearch(rnd);
            if (randomly_selected_idx < 0) randomly_selected_idx = ~randomly_selected_idx; // have to take bitwise complement for some reason, according to C# docs
        }

        if (randomly_selected_idx < 0 || randomly_selected_idx >= this.reproductive_pool_reproduction_score.Count) randomly_selected_idx = 0;

        return (this.reproductive_pool_reproduction_score.ElementAt(randomly_selected_idx).Item2, randomly_selected_idx);
    }

    void LoadGenomeAndSpawn()
    {
        if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN)
        {
            CPPNGenome genome = CPPNGenome.LoadFromDisk();
            Animat animat = SpawnGenomeInRandomSpot(genome, 0);
        }
        else
        {
            Debug.LogError("Error");
        }

    }

    void GenerateNewAnimat()
    {
        if (LOAD_MODE)
        {
            LoadGenomeAndSpawn();
            return;
        }

        int rnd = UnityEngine.Random.Range(0, 10);
    
        if (this.reproductive_pool_reproduction_score.Count < MAX_NUMBER_OF_ANIMATS_IN_REPRODUCTIVE_POOL)
        {
            SpawnTestGenomeAnimat(); // generate brand new animat
        }
        else if(rnd >= 0 && rnd < 5) // 0-4
        {
            SpawnExplicitFitnessAnimat(false); // asexual 
        }
        else if (rnd >= 5) // 5-9
        {
            SpawnExplicitFitnessAnimat(true); // sexual
        }
    }

    void SpawnExplicitFitnessAnimat(bool sexual)
    {
        (object parent1, int parent1_idx) = PeekProbabilisticFromReproductivePool();
        int generation;

        if (sexual)
        {
            // sexual
            (object parent2, int parent2_idx) = PeekProbabilisticFromReproductivePool(ignore_idx: parent1_idx);

      
            object offspring1;
            object offspring2;
            if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN)
            {
                generation = math.max(((CPPNGenome)parent1).generation, ((CPPNGenome)parent2).generation);
                (offspring1, offspring2) = ((CPPNGenome)parent1).Reproduce(((CPPNGenome)parent2));
            }
            else if (GlobalConfig.GENOME_METHOD == GenomeMethod.LinearGenomeandNEAT) { 
                generation = math.max(((LinearAndNEATGenome)parent1).generation, ((LinearAndNEATGenome)parent2).generation);
                (offspring1, offspring2) = ((LinearAndNEATGenome)parent1).Reproduce(((LinearAndNEATGenome)parent2));
            }
            else
            {
                Debug.LogError("error not implemented");
                return;
            }
            

            Debug.Log("Sexually Reproducing Animat #" + parent1_idx + " with Animat #" + parent2_idx);

            

            SpawnGenomeInRandomSpot(offspring1, generation + 1);
            SpawnGenomeInRandomSpot(offspring2, generation + 1);
        }
        else
        {
            // asexual
            Debug.Log("Asexually Reproducing Animat #" + parent1_idx);

            object genome;

            if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN)
            {
                CPPNGenome cloned_genome = ((CPPNGenome)parent1).Clone();
                cloned_genome.Mutate();
                genome = cloned_genome;
                generation = cloned_genome.generation;
            }
            else if (GlobalConfig.GENOME_METHOD == GenomeMethod.LinearGenomeandNEAT)
            {
                LinearAndNEATGenome cloned_genome = ((LinearAndNEATGenome)parent1).Clone();
                cloned_genome.Mutate();
                genome = cloned_genome;
                generation = cloned_genome.generation;
            }
            else
            {
                Debug.LogError("error not implemented");
                return;
            }
            
            SpawnGenomeInRandomSpot(genome, generation + 1);
        }

    }

    void SpawnTestGenomeAnimat()
    {
        if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN)
        {
            SpawnGenomeInRandomSpot(CPPNGenome.CreateUnifiedTestGenome(), 0);
        }
        else if (GlobalConfig.GENOME_METHOD == GenomeMethod.LinearGenomeandNEAT)
        {
            SpawnGenomeInRandomSpot(LinearAndNEATGenome.CreateTestGenome(), 0);

        }
        else
        {
            Debug.LogError("error not implemented");
        }
       
    }

    void CameraTrackCurrentlySelectedAnimat()
    {
        if (this.currently_viewed_animat == null || !this.currently_viewed_animat.initialized) return;
        animat_creator_camera.transform.position = Vector3.Lerp(new float3(0, 7, 0) + this.currently_viewed_animat.GetCenterOfMass(), animat_creator_camera.transform.position, 0.25f);
    }

    public void UIUpdateTimePerGeneration(string value)
    {
        float result;
        bool successful_parse = float.TryParse(value, out result);

        if (!successful_parse)
        {
            Debug.LogWarning("Please enter a valid number for Time Per Generation. Invalid string was " + value);
            return;
        }
    }




    void SetAnimatInfoGUI(Animat animat)
    {
        if (this.currently_viewed_animat == null || !animat.initialized) return;
        Brain brain = animat.brain;
        animat_info_energy.text = "Energy: " + animat.energy_remaining;
        animat_info_life.text = "Life: " + animat.lifespan;
        animat_info_num_neurons.text = "Neurons: " + brain.GetNumberOfNeurons();
        animat_info_num_connections.text = "Connections: " + brain.GetNumberOfSynapses();
        animat_info_period.text = "Update Period: " + animat.brain_update_period;
    }


    void SetNewAnimatForBrainViewer(Animat animat)
    { 
        StaticSceneManager.animat = animat;

        StaticSceneManager.genome = animat.genome;
        
            
        if(this.brain_viewer != null && this.brain_viewer.animat != animat) this.brain_viewer.Initialize();
        if (this.brain_creator != null) this.brain_creator.Initialize();
    }


    public Animat SpawnGenomeInRandomSpot(object genome, int generation)
    {
        Debug.Log("Animat Arena: Spawning New Animat.");

        if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN)
        {
            ((CPPNGenome)genome).generation = generation;
        }
        else if (GlobalConfig.GENOME_METHOD == GenomeMethod.LinearGenomeandNEAT)
        {
            ((LinearAndNEATGenome)genome).generation = generation;
        }
        else
        {
            Debug.LogError("error not implemented");
        }


        Vector3 position = GetRandomPositionForAnimat();
        GameObject new_animat_GO = new GameObject("agent");
        new_animat_GO.transform.position = position;
        Animat animat = new_animat_GO.AddComponent<Animat>();

        animat.food_position = this.food_blocks[0].transform.position;

        animat.Initialize(genome);
        this.current_generation.Add(animat);

        return animat;
    }


    void SwitchViewToAnimat(int i)
    {
        Animat animat = this.current_generation[i];
        this.currently_viewed_animat_idx = i;
        int generation;
        if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN)
        {
            generation = animat.unified_CPPN_genome.generation;
        }
        else if (GlobalConfig.GENOME_METHOD == GenomeMethod.LinearGenomeandNEAT)
        {
            generation = animat.linear_and_neat_genome.generation;
        }
        else
        {
            Debug.LogError("error not implemented");
            return;
        }
        this.animat_info_generation.text = "Generation: " + generation + "";
        this.currently_viewed_animat = animat;
        animat_info_index.text = this.currently_viewed_animat_idx + "";
   /*     if (animat.body.vision_sensor != null)
        {
            animat_vision_view.texture = animat.body.vision_sensor.currentTex2d;
        }*/
        SetNewAnimatForBrainViewer(animat);      
    }

    public void NextGeneration()
    {
        KillAnimat(this.currently_viewed_animat_idx, false);
    }

    public void ViewRandomAnimat()
    {
        if (this.current_generation.Count == 0) return;
        SwitchViewToAnimat(UnityEngine.Random.Range(0, this.current_generation.Count));
    }

    public void ViewNextAnimat()
    {
        Debug.Log("viewing next animat");
        SwitchViewToAnimat(MathHelper.mod(currently_viewed_animat_idx + 1, this.current_generation.Count));
    }

    public void ViewPreviousAnimat()
    {
        Debug.Log("viewing previous animat");
        SwitchViewToAnimat(MathHelper.mod(currently_viewed_animat_idx - 1, this.current_generation.Count));
    }


    void UpdateHighScoreText()
    {
        this.high_score_text.text = "HIGH SCORE: " + this.high_score;
    }

    void WriteScoreToFile()
    {
        if (data_file == null) {
            Debug.LogError("No data file write stream.");
        }

        // write to file
        float max_fitness = 0;
        float total_fitness = 0;
  

        int max_food_eaten = 0;
        float total_food_eaten = 0;


        int max_times_reproduced = 0;
        float total_times_reproduced = 0;

        float max_reproductive_score = 0;
        float total_reproductive_score = 0;


        float avg_generation = 0;

        foreach ((float score, (float,int,int,int,object) data) in this.reproductive_pool_datas)
        {
            float fitness = data.Item1; // reproductive score / fitness
            total_fitness += fitness;
            max_fitness = math.max(max_fitness, fitness);

            int food_eaten = data.Item2; // food eaten
            total_food_eaten += food_eaten;
            max_food_eaten = math.max(max_food_eaten, food_eaten);

            int times_reproduced = data.Item3; // times reproduced
            total_times_reproduced += times_reproduced;
            max_times_reproduced = math.max(max_times_reproduced, times_reproduced);

            avg_generation += data.Item4; // generation

            total_reproductive_score += score;
            max_reproductive_score = math.max(max_reproductive_score, score);
        }

        float avg_fitness = total_fitness / this.reproductive_pool_datas.Count;
        float avg_food_eaten = total_food_eaten / this.reproductive_pool_datas.Count;
        float avg_reproductive_score = total_reproductive_score / this.reproductive_pool_reproduction_score.Count;
        float avg_times_reproduced = total_times_reproduced / this.reproductive_pool_reproduction_score.Count;
        avg_generation /= this.reproductive_pool_reproduction_score.Count;
        data_file = new StreamWriter(data_filename, true);
        data_file.WriteLine(this.reproductive_pool_datas.Count + ","
            + avg_fitness  + "," + max_fitness + "," 
            + avg_food_eaten + "," + max_food_eaten + "," 
            + avg_reproductive_score + "," + max_reproductive_score + ","
            + avg_times_reproduced + "," + max_times_reproduced + ","
            + avg_generation);
        data_file.Close();
    }


    private void OnApplicationQuit()
    {

        data_file.Close();
    }


    public void SaveCurrentAnimatBrain()
    {
        Animat animat = this.current_generation[currently_viewed_animat_idx];
        if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN)
        {
            animat.unified_CPPN_genome.SaveToDisk();
        }
        else
        {
            Debug.LogError("error");
        }
        if (animat.initialized)
        {
            animat.brain.SaveToDisk();
        }
        else
        {
            Debug.LogWarning("WARNING: Could only save brain genome, not brain, since it was not created yet.");
        }
    }

 
}
