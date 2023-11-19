
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TMPro;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using static GlobalConfig;
using Unity.Mathematics;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEditor;

public class AnimatCreator : MonoBehaviour
{
    enum Mode
    {
        Evolution,
        Manual
    }

    public enum ReproductiveMode
    {
        Asexual,
        Sexual,
        HalfAsexual
    }

    Mode mode = Mode.Evolution;

    public (BrainGenome, BodyGenome) high_score_genome;

    public List<Animat> current_generation;
    public List<(Transform, Transform)> training_pods; // (pod, food block)




    BrainViewer brain_viewer;
    BrainCreator brain_creator;

    // UI
    public Text high_score_text;
    public Text current_generation_text;

    public TMP_Text animat_info_index;
    public TMP_Text animat_info_num_neurons;
    public TMP_Text animat_info_num_connections;
    public TMP_Text animat_info_period;

    public int currently_viewed_animat_idx = 0;


    // cameras
    Rect main_rect, rect1, rect2;
    public Camera animat_creator_camera, brain_viewer_camera, brain_creator_camera;

    // dynamic vars
    int current_generation_num;
    float high_score;
    float timer;

    // constants
    public int NUM_IN_GENERATION;

    [SerializeField]
    public float TIME_PER_GENERATION = 2f;

    public ReproductiveMode REPRODUCTIVE_MODE = ReproductiveMode.HalfAsexual;

    // handles
    string data_filename = Path.Join(Application.dataPath, "score_data.txt");
    StreamWriter data_file;
    GameObject training_pod_prefab;

    // Start is called before the first frame update
    void Start()
    {
        JobsUtility.JobWorkerCount = SystemInfo.processorCount - 1; // number of CPU cores minus 1. This prevents Unity from going overthrottling the CPU (https://thegamedev.guru/unity-performance/job-system-excessive-multithreading/)

        this.current_generation = new();

        this.NUM_IN_GENERATION = 10;

        if (File.Exists(data_filename))
        {
            File.Delete(data_filename);
        }
        data_file = File.CreateText(data_filename);
        data_file.WriteLine("Generation" + "," + "Average Score in Generation" + "," + "Max Score in Generation" + "," + "High Score of All Time");
        data_file.Close();

        // get objects
        this.brain_creator = GameObject.FindFirstObjectByType<BrainCreator>();
        this.brain_viewer = GameObject.FindFirstObjectByType<BrainViewer>();
        this.training_pod_prefab = (GameObject)Resources.Load("Prefabs/Creators/Animat/TrainingPod");

        // set camera views
        main_rect = animat_creator_camera.rect;
        rect1 = brain_viewer_camera.rect;
        rect2 = brain_creator_camera.rect;

        // set vars
        this.current_generation_num = 0;
        this.high_score = 0;
        this.timer = this.TIME_PER_GENERATION;
        this.training_pods = new();

        List<(BrainGenome, BodyGenome)> offspring = new();

        //initialize
        BrainGenome brain_genome;
        if (this.mode == Mode.Evolution)
        {
            BodyGenome body = new BodyGenome(Creature.Bug);
            // make initial genomes
            for (int i = 0; i < NUM_IN_GENERATION; i++)
            {
                brain_genome = CreateDefaultBrainGenome();
                brain_genome.Mutate();
                offspring.Add((brain_genome, body));
                
            }
            this.high_score_genome = (offspring[0].Item1.Clone(), body);
        }
        else if (this.mode == Mode.Manual)
        {
            brain_genome = CreateDefaultBrainGenome();
            BodyGenome body = new BodyGenome(Creature.Bug);
            offspring.Add((brain_genome, body));
        }
        else
        {
            Debug.LogError("ERROR: Mode not supported.");
        }

        SpawnGenomes(offspring,initial: true);
        UpdateHighScoreText();
        UpdateCurrentGenerationNumberText();
        this.handles = new NativeList<JobHandle>(Allocator.Persistent);
    }


    float animat_brain_timer;
    JobHandle all_handles;
    NativeList<JobHandle> handles;
    public void FixedUpdate()
    {
        if (!this.handles.IsCreated) return;

        animat_brain_timer -= Time.fixedDeltaTime;
        
        if (animat_brain_timer <= 0)
        {
            if (GlobalConfig.brain_processing_method == ProcessingMethod.CPU)
            {
                all_handles.Complete();
            }
            
            handles.Clear();
            foreach (Animat animat in this.current_generation)
            {
                animat.UpdateSensorimotorNeurons();
                animat.brain.DoWorkingCycle();
                handles.Add(((BrainCPU)animat.brain).update_job_handle);
            }
            all_handles = JobHandle.CombineDependencies(handles.AsArray());
            
            this.animat_brain_timer = GlobalConfig.ANIMAT_BRAIN_UPDATE_PERIOD;
        }

        foreach (Animat animat in this.current_generation)
        {
            animat.MotorEffect();
        }
        

    }




    NEATBrainGenome neat_genome;
    BrainGenome CreateDefaultBrainGenome()
    {
        if (GlobalConfig.brain_genome_method == BrainGenomeMethod.CellularEncoding)
        {
            return CellularEncodingBrainGenome.CreateBrainGenomeWithHexapodConstraints(); // BrainGenome.LoadFromDisk(); 
        }
        else if (GlobalConfig.brain_genome_method == BrainGenomeMethod.AxonalGrowth)
        {
            return AxonalGrowthBrainGenome.CreateTestGenome();
        }
        else if (GlobalConfig.brain_genome_method == BrainGenomeMethod.NEAT)
        {
            if (neat_genome == null)
            {
                neat_genome = NEATBrainGenome.CreateTestGenome();
                return neat_genome;
            }
            else {
                
                return neat_genome.Clone();
            }

        }
        else if (GlobalConfig.brain_genome_method == BrainGenomeMethod.HyperNEAT)
        {
            return HyperNEATBrainGenome.CreateTestGenome();
        }
        else if (GlobalConfig.brain_genome_method == BrainGenomeMethod.ESHyperNEAT)
        {
            GlobalUtils.LogErrorFeatureNotImplemented("ESHyperNEAT");
            return null;
        }
        else
        {
            Debug.LogError("Invalid genome method");
            return null;
        }
    }


    float max_energy = 0;
    float max_score = 0;
    // Update is called once per frame
    void Update()
    {
        

        if(this.mode == Mode.Evolution)
        {
            this.timer -= Time.deltaTime;
            if (this.timer < 0f)
            {
                this.max_energy = 0;
                this.max_score = 0;
                for (int k = 0; k < current_generation.Count; k++)
                {
                    Animat a = current_generation[k];
                    max_energy = Mathf.Max(max_energy, a.energy_remaining);
                    Transform front_segment = a.GetFrontSegment();
                    max_score = Mathf.Max(max_score, GetAnimatScore(a));
                }

                WriteScoreToFile();

                List<(BrainGenome, BodyGenome)> offspring;
                if (REPRODUCTIVE_MODE == ReproductiveMode.Asexual)
                {
                    offspring = NextGeneration_AsexualReproduce();
                }
                else if (REPRODUCTIVE_MODE == ReproductiveMode.Sexual)
                {
                    offspring = NextGeneration_SexualReproduce();
                }
                else if (REPRODUCTIVE_MODE == ReproductiveMode.HalfAsexual)
                {
                    Debug.Log("Reproducing half sexual, half asexual");
                    List<(BrainGenome, BodyGenome)> offspring1 = NextGeneration_AsexualReproduce();
                    List<(BrainGenome, BodyGenome)> offspring2 = NextGeneration_SexualReproduce();
                    offspring = new();
                    int i = 0;
                    int j = 0;
                   // (BrainGenome, BodyGenome) mutated_high_score_genome = (this.high_score_genome.Item1.Clone(), this.high_score_genome.Item2);
                   // mutated_high_score_genome.Item1.Mutate();
                   // offspring.Add((mutated_high_score_genome)); // add the overall highest score, slightly mutated
                    while (offspring.Count < NUM_IN_GENERATION)
                    {
                        offspring.Add(offspring1[i++]);
                        if (offspring.Count == NUM_IN_GENERATION) break;
                        offspring.Add(offspring2[j++]);
                    }
                }
                else{
                    Debug.LogError("Error");
                    return;
                }


                KillCurrentGeneration();
                SpawnGenomes(offspring);

                current_generation_num++;
                UpdateCurrentGenerationNumberText();


                timer = this.TIME_PER_GENERATION;
            }
        }
        else if (this.mode == Mode.Manual)
        {
    
        }


        ManageCameraViews();
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
        this.TIME_PER_GENERATION = result;
    }


    public void UIChangeReproductiveMode(string value)
    {
        if (value == "Asexual")
        {
            this.REPRODUCTIVE_MODE = ReproductiveMode.Asexual;
        }
        else if (value == "Sexual")
        {
            this.REPRODUCTIVE_MODE = ReproductiveMode.Sexual;
        }
        else if (value == "HalfAsexual")
        {
            this.REPRODUCTIVE_MODE = ReproductiveMode.HalfAsexual;
        }
        else
        {
            Debug.LogError("Invalid reproductive mode."); 
        }
        
    }

    /// <summary>
    /// Swap camera views/control when clicked
    /// </summary>
    void ManageCameraViews()
    {
        // TODO this
        bool contains = rect1.Contains(Input.mousePosition);
        bool click = Input.GetMouseButtonDown(0);
       // Debug.Log("contains " + contains + " and click "+ click);
        if (contains && click)
        {
            brain_viewer_camera.rect = main_rect;
            animat_creator_camera.rect = rect1; 
        }
    }

    void SetAnimatInfoGUI(Brain brain)
    {
        animat_info_index.text = this.currently_viewed_animat_idx + "";
        animat_info_num_neurons.text = "Neurons: " + brain.GetNumberOfNeurons();
        animat_info_num_connections.text = "Connections: " + brain.GetNumberOfSynapses();
        animat_info_period.text = "Update Period: " + brain.genome.brain_update_period;
    }

    void SetNewBrainForBrainViewer(Brain brain)
    {
        Debug.Log("Animat Creator: Setting new brain for visualization. Has "
            + brain.GetNumberOfNeurons() + " neurons "
            + "SENSORY: " + brain.neuron_indices[Brain.SENSORY_NEURON_KEY].Count
            + "MOTOR: " + brain.neuron_indices[Brain.MOTOR_NEURON_KEY].Count
            + " and Has " + brain.GetNumberOfSynapses() + " synapses "
            );

       
        StaticSceneManager.brain = brain;
        StaticSceneManager.brain_genome = brain.genome;
        if(this.brain_viewer != null) this.brain_viewer.Initialize();
        if (this.brain_creator != null) this.brain_creator.Initialize();
    }


    void SpawnGenomes(List<(BrainGenome, BodyGenome)> current_generation_genomes, bool initial=false)
    {
        Debug.Log("Animat Creator: Spawning New Generation.");

        float x_pos = 0f;
        float y_pos = 0f;
        float z_pos = 0f;
        int index = 0;
        foreach((BrainGenome brain_genome, BodyGenome body_genome) in current_generation_genomes)
        {

            Vector3 position = new Vector3(x_pos, y_pos, z_pos);

            if (initial)
            {
                //first time spawning, so spawn the training pod
                GameObject training_pod = Instantiate(this.training_pod_prefab);
                training_pod.transform.position = position - new Vector3(0,0,2);
                //training_pod.transform.rotation = Quaternion.Euler(-10,0,0); // tilt the pod
                training_pods.Add((training_pod.transform, training_pod.transform.Find("Food")));
            }

            GameObject new_agent_GO = new GameObject("agent");
            new_agent_GO.transform.position = position;

            Animat animat = new_agent_GO.AddComponent<Animat>();
            animat.Initialize(brain_genome, body_genome);
            animat.animat_creator = this;

            animat.animat_creator_food_block = training_pods[index].Item2;

            if (index % 20 == 0)
            {
                x_pos = 0;
                z_pos += 75F;
            }
            else
            {
                x_pos += 10f;
            }
            this.current_generation.Add(animat);
            index++;
        }

        if(GlobalConfig.brain_genome_development_processing_method == ProcessingMethod.CPU)
        {
            Stopwatch watch = Stopwatch.StartNew();
            NativeList <JobHandle> handles = new NativeList<JobHandle>(Allocator.TempJob);
            foreach (Animat animat in this.current_generation)
            {
                JobHandle job_handle = animat.brain.genome.ScheduleDevelopCPUJob();
                handles.Add(job_handle);
            }
            JobHandle all_handles = JobHandle.CombineDependencies(handles.AsArray());
            all_handles.Complete();
            watch.Stop();
            
            Debug.Log("develop parallel took " + watch.ElapsedMilliseconds/1000f +" seconds");
            handles.Dispose();

            foreach (Animat animat in this.current_generation)
            {
                animat.brain.DevelopFromGenome();
            }
        }
        else
        {
            foreach (Animat animat in this.current_generation)
            {
                animat.brain.genome.ScheduleDevelopGPUJob();
            }
        }



        // view the brain
        SwitchViewToAnimat(currently_viewed_animat_idx);
        

        Debug.Log("Animat Creator: Completed Spawning New Generation.");
    }

    List<(BrainGenome, BodyGenome)> NextGeneration_SexualReproduce()
    {
        List<(BrainGenome, BodyGenome)> offspring = new();
        current_generation.Sort(SortAgentByScore);

        Animat best_animat = current_generation[0];
        float best_score = GetAnimatScore(best_animat);

        Debug.Log("best score that round (" + current_generation_num + "): " + best_score);

        if (best_score > this.high_score)
        {
            this.high_score = best_score;
            UpdateHighScoreText();
            this.high_score_genome = (best_animat.brain_genome.Clone(), best_animat.body_genome);
            //this.best_genome.SaveToDisk();
        }

        BrainGenome best_animat_brain_genome = best_animat.brain_genome.Clone();
        best_animat_brain_genome.Mutate();
        offspring.Add((best_animat_brain_genome, best_animat.body_genome)); // add the best animat of the generation, slightly mutated

        // reproduce proportionally, by fitness. Start with most fit and work down

        for (int k = 0; k < current_generation.Count; k++)
        {
            if (offspring.Count >= NUM_IN_GENERATION) break;
            int mate2_idx = k + 1;
            Animat parent2 = current_generation[mate2_idx];

            offspring.Add((parent2.brain_genome.Clone(), parent2.body_genome)); // add the subsequent best animat
            for (int mate1_idx = 0; mate1_idx < mate2_idx; mate1_idx++)
            {
                if (offspring.Count >= NUM_IN_GENERATION) break;
                Animat parent1 = current_generation[mate1_idx];
               
                (BrainGenome offspring1_brain_genome, BrainGenome offspring2_brain_genome) = parent1.brain_genome.Reproduce(parent2.brain_genome);
             
                //offspring1_brain_genome.Mutate();
                //offspring2_brain_genome.Mutate();

                offspring.Add((offspring1_brain_genome, parent1.body_genome));
                offspring.Add((offspring2_brain_genome, parent1.body_genome)); 
                
            }
        }


        return offspring;
    }

    List<(BrainGenome, BodyGenome)> NextGeneration_AsexualReproduce()
    {
        List<(BrainGenome, BodyGenome)> offspring = new();
        current_generation.Sort(SortAgentByScore);

        Animat best_animat = current_generation[0];
        Animat worst_animat = current_generation[^1];
        float best_score = GetAnimatScore(best_animat);
        float worst_score = GetAnimatScore(worst_animat);

        Debug.Log("best score that round (" + current_generation_num + "): " + best_score);

        if (best_score > this.high_score)
        {
            this.high_score = best_score;
            UpdateHighScoreText();
            this.high_score_genome = (best_animat.brain_genome.Clone(), best_animat.body_genome);
            //this.best_genome.SaveToDisk();
        }

        BrainGenome best_animat_brain_genome = best_animat.brain_genome.Clone();
        best_animat_brain_genome.Mutate();
        offspring.Add((best_animat_brain_genome, best_animat.body_genome)); // add the best animat of the generation, slightly mutated

        //int num_parents = (int)(.2f * NUM_IN_GENERATION);
        float sum = 0;
        foreach(Animat agent in current_generation)
        {
            float score = GetAnimatScore(agent);
            if (score < 0) score = 0;
            sum += score;
        }

        // reproduce proportionally, by fitness. Start with most fit and work down
        for (int j = 0; j < current_generation.Count; j++)
        {
            Animat agent = current_generation[j];
            float score = GetAnimatScore(agent);

            int num_offspring = Mathf.CeilToInt(score / sum) * NUM_IN_GENERATION;
            for (int i = 0; i < num_offspring; i++)
            {
                if (offspring.Count >= NUM_IN_GENERATION) break;
                BrainGenome brain_genome = agent.brain_genome.Clone();
                // reproduce / mutate

                brain_genome.Mutate();
                offspring.Add((brain_genome, agent.body_genome));
            }
        }
        /*        for (int i = 0; i < NUM_IN_GENERATION; i++) { 
                    Animat agent;
                    int rnd_idx = UnityEngine.Random.Range(1, 6); // from 1 to N, where N determines the range of top performers to pick from

                    agent = current_generation[current_generation.Count - rnd_idx]; // get one of the current best agents

                    BrainGenome brain_genome = agent.brain_genome.Clone();
                    // reproduce / mutate
                    brain_genome.Mutate();
                    offspring.Add((brain_genome, agent.body_genome));

                }*/


        return offspring;

    }


    void SwitchViewToAnimat(int i)
    {
        try
        {
            animat_creator_camera.transform.position = this.training_pods[i].Item1.transform.position + new Vector3(0, 7, 3);
            SetAnimatInfoGUI(this.current_generation[i].brain);
            SetNewBrainForBrainViewer(this.current_generation[i].brain);
        }
        catch
        {
            Debug.LogWarning("Could not switch view to Animat " + i);
        }

    }
  
    public void ViewNextAnimat()
    {
        currently_viewed_animat_idx = MathHelper.mod(currently_viewed_animat_idx + 1,  this.NUM_IN_GENERATION);
        SwitchViewToAnimat(currently_viewed_animat_idx);
    }

    public void ViewPreviousAnimat()
    {
        currently_viewed_animat_idx = MathHelper.mod(currently_viewed_animat_idx - 1, this.NUM_IN_GENERATION);
        SwitchViewToAnimat(currently_viewed_animat_idx);
    }


    int SortAgentByScore(Animat p1, Animat p2)
    {
        float p1_score = GetAnimatScore(p1);
        float p2_score = GetAnimatScore(p2);

        // p2_score.CompareTo(p1_score); for the largest number to come first
        // p1_score.CompareTo(p2_score); for the smallest number to come first
        return p2_score.CompareTo(p1_score);
    }

    float GetAnimatScore(Animat a)
    {
        Transform front_segment = a.GetFrontSegment();
        /*        float energy_ratio = a.energy_used / max_energy; // in [0,1] -- higher energy ratio is worse
                float energy_score = (1 - energy_ratio); // in [0,1] -- higher is better
                float distance_score =  / max_distance;  // in [0,1] -- higher is better*/
        return (50 - Vector3.Distance(front_segment.position, a.animat_creator_food_block.transform.position)); // a.start_pos.z);// * (1 + front_segment.position.y); // (1 + a.energy_used);
    }

    void KillCurrentGeneration()
    {
        foreach(Animat agent in this.current_generation)
        {
            agent.DiposeOfBrainMemory();
            Destroy(agent.gameObject);
        }

        this.current_generation.Clear();
    }

    void UpdateHighScoreText()
    {
        Debug.Log("updating high score text with " + this.high_score);
        this.high_score_text.text = "HIGH SCORE: " + this.high_score;

    }

    void WriteScoreToFile()
    {
        if (data_file == null) {
            Debug.LogError("No data file write stream.");
        }

        // write to file
        float max = 0;
        float avg = 0;
        foreach (Animat agent in current_generation)
        { 
            float score = GetAnimatScore(agent);
            avg += score;
            max = Mathf.Max(max, score);
        }
        avg /= current_generation.Count;
        data_file = new StreamWriter(data_filename, true);
        data_file.WriteLine(current_generation_num + "," + avg  + "," + max + "," + this.high_score);
        data_file.Close();
    }

    void UpdateCurrentGenerationNumberText()
    {
        this.current_generation_text.text = "Current Generation: # " + current_generation_num;
    }

    private void OnApplicationQuit()
    {
        this.handles.Dispose();
        data_file.Close();
    }





}
