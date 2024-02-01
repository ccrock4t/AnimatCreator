using System.Collections.Generic;
using UnityEngine;
using static Brain;
using static GlobalConfig;

/// <summary>
///     Script attached to neural network agent. Coordinates neural network and the body.
/// </summary>
public class Animat : MonoBehaviour
{
    public Brain brain;
    public ArticulationBody physical_body;
    public GameObject physical_bodyGO;
    public BodyGenome body_genome;
    public BrainGenome brain_genome;
    public AnimatBody body;

    public Transform animat_creator_food_block;

    public ArticulationBody[] body_segments_abs;
    public List<BodySegment> body_segments;
    public bool initialized = false;
    public bool developed = false;

    public float timer;

    public AnimatCreator animat_creator;

    public float energy_remaining = 1000;

    public float brain_update_period;

    public List<ArticulationBody> listab;

    public Dictionary<int, float> motor_idx_to_activation;


    // Start is called before the first frame update
    public void Initialize(BrainGenome brain_genome, BodyGenome body_genome)
    {
        // set vars
        this.brain_update_period = brain_genome.brain_update_period;
        this.motor_idx_to_activation = new();

        // === create body

        if (body_genome == null)
        {
            this.body_genome = new BodyGenome(Creature.Bug);
        }
        else
        {
            this.body_genome = body_genome;
        }
        this.body = this.gameObject.AddComponent<AnimatBody>();
        this.body.CreateGenomeAndInstantiateBody(this.body_genome);
        this.physical_body = this.body.root_gameobject.GetComponent<ArticulationBody>();
        this.physical_bodyGO = this.body.root_gameobject;

        // store body segments

        this.body_segments_abs = this.physical_body.GetComponentsInChildren<ArticulationBody>();
        this.body_segments = new();

        foreach (ArticulationBody segment1 in body_segments_abs)
        {
            foreach (ArticulationBody segment2 in body_segments_abs)
            {
                // make the colliders ignore each other
                Physics.IgnoreCollision(segment1.GetComponent<BoxCollider>(), segment2.GetComponent<BoxCollider>());
            }

            this.body_segments.Add(segment1.gameObject.GetComponent<BodySegment>());
        }


        // === create brain
        if (brain_genome == null)
        {
            this.brain_genome = CreateTestBrainGenome();
        }
        else
        {
            this.brain_genome = brain_genome;
        }

        if (GlobalConfig.brain_processing_method == GlobalConfig.ProcessingMethod.CPU)
        {
            this.brain = new BrainCPU(brain_genome);
        }
        else if (GlobalConfig.brain_processing_method == GlobalConfig.ProcessingMethod.GPU)
        {
            this.brain = new BrainGPU(brain_genome);
        }
        else
        {
            GlobalUtils.LogErrorEnumNotRecognized("NOT SUPPORTED");
        }

        // flag as initialized
        this.initialized = true;


    }

    public static BrainGenome CreateTestBrainGenome()
    {
        if (GlobalConfig.brain_genome_method == BrainGenomeMethod.CellularEncoding)
        {
            return CellularEncodingBrainGenome.CreateBrainGenomeWithHexapodConstraints();
        }
        else if (GlobalConfig.brain_genome_method == BrainGenomeMethod.SGOCE)
        {
            return AxonalGrowthBrainGenome.CreateTestGenome();
        }
        else if (GlobalConfig.brain_genome_method == BrainGenomeMethod.NEAT)
        {
            return NEATBrainGenome.CreateTestGenome();
        }
        else if (GlobalConfig.brain_genome_method == BrainGenomeMethod.HyperNEAT)
        {
            return RegularHyperNEATBrainGenome.CreateTestGenome();
        }
        else if (GlobalConfig.brain_genome_method == BrainGenomeMethod.ESHyperNEAT)
        {
            return ESHyperNEATBrainGenome.CreateTestGenome();
        }
        else
        {
            GlobalUtils.LogErrorEnumNotRecognized("NOT SUPPORTED");
            return null;
        }
    }

    void OnApplicationQuit()
    {
        // manually dispose of all unmanaged memory

        DiposeOfBrainMemory();
    }



    public void DiposeOfBrainMemory()
    {
        if (this.brain != null) this.brain.DisposeOfNativeCollections();
        if (this.brain_genome is HyperNEATBrainGenome && GlobalConfig.brain_genome_development_processing_method == ProcessingMethod.GPU)
        {
            ((RegularHyperNEATBrainGenome)this.brain_genome).neurons_compute_buffer.Release();
            ((RegularHyperNEATBrainGenome)this.brain_genome).synapses_compute_buffer.Release();
            ((RegularHyperNEATBrainGenome)this.brain_genome).CPPN_node_compute_buffer.Release();
            ((RegularHyperNEATBrainGenome)this.brain_genome).CPPN_connection_compute_buffer.Release();
        }
    }

    //todo use this
    /*       Genome genome1 = new Genome(Genome.Creature.Human);
             Genome genome2 = new Genome(Genome.Creature.Spider);
             (this.genome, _) = Genome.MateGenomes(genome1, genome2, Genome.CrossoverType.OnePoint);
             MutateGenome(this.genome);
             MutateGenome(this.genome);
             MutateGenome(this.genome);
     */



    // Update is called once per frame
    public void Update()
    {
        if (!initialized)
        {
            Debug.Log("agent is not initialized");
            return;
        }
    }

    public Transform GetFrontSegment()
    {
        return transform.GetChild(0).GetChild(1).GetChild(1).GetChild(0).Find("Cube");
    }



    public static string GetSensorimotorJointKey(int i)
    {
        if (GlobalConfig.creature_to_use == Creature.Hexapod)
        {
            switch (i)
            {
                case 0:
                    return "TOPBODYSEG";
                    break;
                case 1:
                    return "MIDBODYSEG";
                    break;
                case 2:
                    return "BOTBODYSEG";
                    break;
                case 3:
                    return "LEFTBODYHALF_BOTLEG_TOPLEGSEG";
                    break;
                case 4:
                    return "LEFTBODYHALF_BOTLEG_BOTLEGSEG";
                    break;
                case 5:
                    return "LEFTBODYHALF_BOTLEG_FOOTSEG";
                    break;
                case 6:
                    return "RIGHTBODYHALF_BOTLEG_TOPLEGSEG";
                    break;
                case 7:
                    return "RIGHTBODYHALF_BOTLEG_BOTLEGSEG";
                    break;
                case 8:
                    return "RIGHTBODYHALF_BOTLEG_FOOTSEG";
                    break;
                case 9:
                    return "LEFTBODYHALF_MIDLEG_TOPLEGSEG";
                    break;
                case 10:
                    return "LEFTBODYHALF_MIDLEG_BOTLEGSEG";
                    break;
                case 11:
                    return "LEFTBODYHALF_MIDLEG_FOOTSEG";
                    break;
                case 12:
                    return "RIGHTBODYHALF_MIDLEG_TOPLEGSEG";
                    break;
                case 13:
                    return "RIGHTBODYHALF_MIDLEG_BOTLEGSEG";
                    break;
                case 14:
                    return "RIGHTBODYHALF_MIDLEG_FOOTSEG";
                    break;
                case 15:
                    return "LEFTBODYHALF_TOPLEG_TOPLEGSEG";
                    break;
                case 16:
                    return "LEFTBODYHALF_TOPLEG_BOTLEGSEG";
                    break;
                case 17:
                    return "LEFTBODYHALF_TOPLEG_FOOTSEG";
                    break;
                case 18:
                    return "RIGHTBODYHALF_TOPLEG_TOPLEGSEG";
                    break;
                case 19:
                    return "RIGHTBODYHALF_TOPLEG_BOTLEGSEG";
                    break;
                case 20:
                    return "RIGHTBODYHALF_TOPLEG_FOOTSEG";
                    break;
                default:
                    Debug.LogError("ERROR for " + i);
                    return "";
            }
        }
        else if (GlobalConfig.creature_to_use == Creature.Quadruped)
        {
            switch (i)
            {
                case 0:
                    return "TOPBODYSEG";
                    break;
                case 1:
                    return "LEFTBODYHALF_TOPLEG_TOPLEGSEG";
                    break;
                case 2:
                    return "LEFTBODYHALF_TOPLEG_BOTLEGSEG";
                    break;
                case 3:
                    return "LEFTBODYHALF_TOPLEG_FOOTSEG";
                    break;
                case 4:
                    return "RIGHTBODYHALF_TOPLEG_TOPLEGSEG";
                    break;
                case 5:
                    return "RIGHTBODYHALF_TOPLEG_BOTLEGSEG";
                    break;
                case 6:
                    return "RIGHTBODYHALF_TOPLEG_FOOTSEG";
                    break;
                case 7:
                    return "MIDBODYSEG";
                    break;
                case 8:
                    return "LEFTBODYHALF_MIDLEG_TOPLEGSEG";
                    break;
                case 9:
                    return "LEFTBODYHALF_MIDLEG_BOTLEGSEG";
                    break;
                case 10:
                    return "LEFTBODYHALF_MIDLEG_FOOTSEG";
                    break;
                case 11:
                    return "RIGHTBODYHALF_MIDLEG_TOPLEGSEG";
                    break;
                case 12:
                    return "RIGHTBODYHALF_MIDLEG_BOTLEGSEG";
                    break;
                case 13:
                    return "RIGHTBODYHALF_MIDLEG_FOOTSEG";
                    break;
                default:
                    Debug.LogError("ERROR for " + i);
                    return "";
            }
        }
        else
        {
            Debug.LogError("ERROR");
            return "";
        }

    }


    public void UpdateSensorimotorNeurons()
    {

        Dictionary<string, int> sensory_indices = this.brain.neuron_indices[Brain.SENSORY_NEURON_KEY];
        Dictionary<string, int> motor_indices = this.brain.neuron_indices[Brain.MOTOR_NEURON_KEY];

        // detect environment with sensory neurons
        for (int j = 0; j < this.body_segments_abs.Length; j++)
        {
            ArticulationBody body_segment_ab = this.body_segments_abs[j];
            BodySegment body_segment = this.body_segments[j];

            string key = "SENSORLAYER_" + GetSensorimotorJointKey(j) + "_";

            for (int i = 0; i < 10; i++)
            {
                int brain_idx = sensory_indices[key + i];

                Neuron sensory_neuron;
                if (this.brain is BrainCPU)
                {
                    sensory_neuron = ((BrainCPU)this.brain).current_state_neurons[brain_idx];
                }
                else// if(this.brain is BrainGPU)
                {
                    sensory_neuron = ((BrainGPU)this.brain).GetCurrentNeuron(brain_idx);
                }


                //quaternion
                if (i <= 5)
                {
                    sensory_neuron.activation = body_segment.touch_sensors[i].touching_terrain ? 1.0f : 0f;
                }
                else if (i == 6)
                {
                    sensory_neuron.activation = body_segment_ab.transform.rotation.x;
                }
                else if (i == 7)
                {
                    sensory_neuron.activation = body_segment_ab.transform.rotation.y;
                }
                else if (i == 8)
                {
                    sensory_neuron.activation = body_segment_ab.transform.rotation.z;
                }
                else if (i == 9)
                {
                    sensory_neuron.activation = body_segment_ab.transform.rotation.w;

                }

                // insert into brain
                if (this.brain is BrainCPU)
                {
                    ((BrainCPU)this.brain).current_state_neurons[brain_idx] = sensory_neuron;
                }
                else// if(this.brain is BrainGPU)
                {
                    ((BrainGPU)this.brain).SetCurrentNeuron(brain_idx, sensory_neuron);
                }


            }
        }




        // effect environment with motor neurons

        for (int j = 0; j < this.body_segments_abs.Length; j++)
        {
            ArticulationBody segment = this.body_segments_abs[j];

            string key = "MOTORLAYER_" + GetSensorimotorJointKey(j) + "_";
            /*            if (key.Contains("BODYSEG"))
                        {
                            continue; // todo remove this; it blocks body from moving
                        }*/
            Vector3 torque = new Vector3(0, 0, 0);
            for (int i = 0; i < 3; i++)
            {
                int brain_idx = motor_indices[key + i];

                Neuron motor_neuron;
                if (this.brain is BrainCPU)
                {
                    motor_neuron = ((BrainCPU)this.brain).current_state_neurons[brain_idx];
                }
                else// if(this.brain is BrainGPU)
                {
                    //todo
                    motor_neuron = ((BrainGPU)this.brain).GetCurrentNeuron(brain_idx);
                }

                float activation = motor_neuron.activation;

                if (!float.IsFinite(activation))
                {
                    Debug.LogWarning("Activation " + activation + " is not finite. Setting to zero. ");
                    activation = 0;
                }

                if (motor_neuron.activation_function == Neuron.NeuronActivationFunction.Sigmoid)
                {
                    activation = (motor_neuron.activation - 0.5f) * 2; // scale between -1 and 1
                }
                /*                else if (motor_neuron.activation_function == Neuron.NeuronActivationFunction.Tanh)
                                {
                                    activation = motor_neuron.activation; // already between -1 and 1
                                }
                                else if (motor_neuron.activation_function == Neuron.NeuronActivationFunction.LeakyReLU)
                                {
                                    activation = motor_neuron.activation; // already between -1 and 1
                                }
                                else if (motor_neuron.activation_function == Neuron.NeuronActivationFunction.LeakyReLU)
                                {
                                    activation = motor_neuron.activation; // already between -1 and 1
                                }
                                else
                                {
                                    GlobalUtils.LogErrorEnumNotRecognized("error");
                                    return;
                                }*/

                activation = motor_neuron.activation;

                if (activation > 1) activation = 1;
                if (activation < -1) activation = -1;
                if (energy_remaining <= 0) activation = 0;

                motor_idx_to_activation[brain_idx] = activation;
            }
        }
    }


    public void MotorEffect()
    {
        Dictionary<string, int> motor_indices = this.brain.neuron_indices[Brain.MOTOR_NEURON_KEY];
        for (int j = 0; j < this.body_segments_abs.Length; j++)
        {
            ArticulationBody segment = this.body_segments_abs[j];

            string key = "MOTORLAYER_" + GetSensorimotorJointKey(j) + "_";
            /*            if (key.Contains("BODYSEG"))
                        {
                            continue; // todo remove this; it blocks body from moving
                        }*/
            Vector3 torque = new Vector3(0, 0, 0);
            for (int i = 0; i < 3; i++)
            {
                if (!motor_indices.ContainsKey(key + i))
                {
                    Debug.LogWarning("NO KEY");
                    continue;
                }
                int brain_idx = motor_indices[key + i];

                if (!motor_idx_to_activation.ContainsKey(brain_idx)) continue;

                float activation = motor_idx_to_activation[brain_idx];

                if (GlobalConfig.USE_FORCE_MODE)
                {
                    if (i == 0)
                    {
                        torque.x = activation;
                    }
                    else if (i == 1)
                    {
                        torque.y = activation;
                    }
                    else if (i == 2)
                    {
                        torque.z = activation;
                    }
                    continue;
                }

                float energy_used = 0;
                float difference = 0;
                float new_activation = 0;

                float GetLerpedActivation(float activation, ArticulationDrive drive)
                {
                    float difference = activation * AnimatBody.DRIVE_LIMITS - drive.target;
                    if (difference > AnimatBody.TARGET_MODE_MAX_DEGREES_MOVED_PER_SECOND) difference = AnimatBody.TARGET_MODE_MAX_DEGREES_MOVED_PER_SECOND;
                    if (difference < -AnimatBody.TARGET_MODE_MAX_DEGREES_MOVED_PER_SECOND) difference = -AnimatBody.TARGET_MODE_MAX_DEGREES_MOVED_PER_SECOND;
                    float new_activation = (drive.target + difference) / AnimatBody.DRIVE_LIMITS;
                    if (new_activation > 1) new_activation = 1;
                    if (new_activation < -1) new_activation = -1;
                    return new_activation;
                }


                if (i == 0)
                {
                    new_activation = GetLerpedActivation(activation, segment.xDrive);
                    energy_used = Mathf.Abs(new_activation - (segment.xDrive.target / AnimatBody.DRIVE_LIMITS));
                    if (GlobalConfig.USE_FORCE_MODE) new_activation = activation;
                    //if (!GlobalConfig.USE_FORCE_MODE) new_activation = activation;
                    body.SetXDrive(segment, new_activation);
                }
                else if (i == 1)
                {
                    new_activation = GetLerpedActivation(activation, segment.yDrive);
                    energy_used = Mathf.Abs(new_activation - (segment.yDrive.target / AnimatBody.DRIVE_LIMITS));
                    if (GlobalConfig.USE_FORCE_MODE) new_activation = activation;
                    // if (!GlobalConfig.USE_FORCE_MODE) new_activation = activation;
                    body.SetYDrive(segment, new_activation);
                }
                else if (i == 2)
                {
                    new_activation = GetLerpedActivation(activation, segment.zDrive);
                    energy_used = Mathf.Abs(new_activation - (segment.zDrive.target / AnimatBody.DRIVE_LIMITS));
                    if (GlobalConfig.USE_FORCE_MODE) new_activation = activation;
                    //if (!GlobalConfig.USE_FORCE_MODE) new_activation = activation;
                    body.SetZDrive(segment, new_activation);
                }


                //energy_remaining -= Mathf.Abs(energy_used);

            }
            if (GlobalConfig.USE_FORCE_MODE)
            {
                segment.AddTorque(torque * AnimatBody.FORCE_MODE_TORQUE_SCALE, ForceMode.Force);
            }
        }
    }



    /// <summary>
    /// motor op
    /// </summary>
    public void Reproduce()
    {
        GameObject offspring = new GameObject("AgentClone");
        offspring.AddComponent<Animat>();
        offspring.transform.position = this.transform.position + new Vector3(UnityEngine.Random.Range(1, 10), 0, UnityEngine.Random.Range(1, 10));
    }

}
