using System.Collections.Generic;
using UnityEngine;
using static Brain;


public class BrainViewer : MonoBehaviour
{
    public Animat animat;
    Brain brain;

    // prefabs
    GameObject neuron_prefab;
    GameObject sensor_neuron_prefab;
    GameObject motor_neuron_prefab;
    GameObject synapse_prefab;

    public Dictionary<int, BrainViewerNeuron> neurons_GOs;
    public Dictionary<int, BrainViewerSynapse> synapse_GOs;

    private Vector3 spacing;

    bool is_alive = false;


    public bool SHOW_SYNAPSES = false;
    public bool did_develop = false;
    public bool did_show_synapses = false;

    float timer = 0f;

  
    public bool initialize_on_start = true;

    // Start is called before the first frame update
    void Awake()
    {
        // limit framerate to prevent GPU from going overdrive for no reason
        QualitySettings.vSyncCount = 0;  // VSync must be disabled
        Application.targetFrameRate = 45;

        // load prefabs
        neuron_prefab = (GameObject)Resources.Load("Prefabs/Tools/Neuron");
        sensor_neuron_prefab = (GameObject)Resources.Load("Prefabs/Tools/SensoryNeuron");
        motor_neuron_prefab = (GameObject)Resources.Load("Prefabs/Tools/MotorNeuron");
        synapse_prefab = (GameObject)Resources.Load("Prefabs/Tools/Synapse");

        this.spacing = Vector3.one * 200;
    }

    public void ClearScene()
    {
        if (this.neurons_GOs == null) return;
        foreach (KeyValuePair<int, BrainViewerNeuron> neuron in this.neurons_GOs)
        {
            Destroy(neuron.Value.gameObject);
        }
        foreach (KeyValuePair<int, BrainViewerSynapse> synapse in this.synapse_GOs)
        {
             Destroy(synapse.Value.gameObject);     
        }

        this.neurons_GOs.Clear();
        this.synapse_GOs.Clear();
        this.did_develop = false;
        this.did_show_synapses = false;
    }

    public void Initialize()
    {
        Debug.Log("Brain Viewer: Initialization Started.");
        ClearScene();
        this.neurons_GOs = new();
        this.synapse_GOs = new();
        this.animat = StaticSceneManager.animat;
        Debug.Log("Brain Viewer: Initialization Completed.");
    }


    public void SpawnNeurons()
    {
        Vector3 position;
        Vector3 default_location = Vector3.zero; //x=sensor, y=hidden, z=motor
        for (int i=0; i< this.brain.GetNumberOfNeurons();i++)
        {
            Neuron neuron = this.GetCurrentNeuron(i);
            if (!neuron.enabled) continue; // this neuron will never have an activation, it effectively doesnt exist

            if (neuron.position_idxs.x == -1 && neuron.position_idxs.y == -1 && neuron.position_idxs.z == -1)
            {
                // default placements
                if (neuron.neuron_class == Neuron.NeuronClass.Motor)
                {
                    position = new Vector3(default_location.z, 0, 2);
                    default_location.z++;
                }
                else if (neuron.neuron_class == Neuron.NeuronClass.Sensor)
                {
                    position = new Vector3(default_location.x, 0, 0);
                    default_location.x++;
                }
                else//if (neuron.sensory == Neuron.NeuronClass.Motor)
                {
                    position = new Vector3(default_location.y, 0, 1);
                    default_location.y++;
                }

            }
            else
            {
                position = new Vector3(neuron.position_normalized.x, neuron.position_normalized.y, neuron.position_normalized.z);  
            }
            GameObject neuronGO;
            if (neuron.neuron_class == Neuron.NeuronClass.Motor)
            {
                neuronGO = Instantiate(motor_neuron_prefab, this.transform);
                neuronGO.transform.localScale *= 3;
            }
            else if (neuron.neuron_class == Neuron.NeuronClass.Sensor)
            {
                neuronGO = Instantiate(sensor_neuron_prefab, this.transform);
                neuronGO.transform.localScale *= 3;
            }
            else//if (neuron.sensory == Neuron.NeuronClass.Motor)
            {
                neuronGO = Instantiate(neuron_prefab, this.transform);
                neuronGO.transform.localScale *= 1.5f;
            }

          
            // offset the extra hidden neurons by 0.5f
            position.x += (neuron.position_normalized.w * 0.5f/(GlobalConfig.ANIMAT_SUBSTRATE_DIMENSIONS.w));
            position.y += (neuron.position_normalized.v * 0.5f / (GlobalConfig.ANIMAT_SUBSTRATE_DIMENSIONS.v));
            //position.z += (neuron.position.w - 1) * 0.5f;



            neuronGO.transform.localPosition = Vector3.Scale(position, spacing);
            this.neurons_GOs.Add(i, neuronGO.GetComponent<BrainViewerNeuron>());
        }

    }

    public void SpawnSynapses(bool update_only=false)
    {

        did_show_synapses = true;
        
        for (int i = 0; i < this.brain.GetNumberOfNeurons(); i++)
        {
            GameObject to_neuron_GO = this.neurons_GOs[i].gameObject;
            Neuron neuron = this.GetCurrentNeuron(i);
            for (int j = neuron.synapse_start_idx; j < neuron.synapse_start_idx + neuron.synapse_count; j++)
            {
                Synapse synapse = this.GetCurrentSynapse(j);
                BrainViewerSynapse brain_viewer_synapse;
                if (!update_only)
                {
                    if (!synapse.IsEnabled()) continue;
                    GameObject from_neuron_GO = this.neurons_GOs[synapse.from_neuron_idx].gameObject;

                    GameObject synapseGO = Instantiate(synapse_prefab, this.transform);
                    brain_viewer_synapse = synapseGO.GetComponent<BrainViewerSynapse>();
                    brain_viewer_synapse.Initialize();

                    brain_viewer_synapse.gameObjectA = from_neuron_GO;
                    brain_viewer_synapse.gameObjectB = to_neuron_GO;
                    this.synapse_GOs[j] = brain_viewer_synapse;
                    synapseGO.GetComponent<LineRenderer>().SetPositions(new Vector3[] { brain_viewer_synapse.gameObjectA.transform.position, brain_viewer_synapse.gameObjectB.transform.position });
                }
                else
                {
                    brain_viewer_synapse = this.synapse_GOs[j];
                }

                brain_viewer_synapse.synapse = synapse;
             /*   brain_viewer_synapse.lr.startWidth = BrainViewerSynapse.START_WIDTH * synapse.weight;
                brain_viewer_synapse.lr.endWidth = BrainViewerSynapse.END_WIDTH * synapse.weight;*/

            }
        }
  
    }


    public bool UPDATE_SYNAPSES = false;
    void FixedUpdate()
    {
        timer -= Time.fixedDeltaTime;
        if (timer > 0 || this.animat == null || !this.animat.initialized) return;
       
        timer = GlobalConfig.BRAIN_VIEWER_UPDATE_PERIOD;
        if (!this.did_develop)
        {
            this.brain = animat.brain;
            this.SpawnNeurons();
            this.did_develop = true;
        }

        if (SHOW_SYNAPSES && !did_show_synapses) this.SpawnSynapses(false);

        // update visual of neurons
        for (int i = 0; i < this.brain.GetNumberOfNeurons(); i++)
        {
            Neuron neuron = GetCurrentNeuron(i);
            if (!neuron.enabled) continue;
            GameObject neuronGO = this.neurons_GOs[i].gameObject;
            this.neurons_GOs[i].UpdateColor();
            neuronGO.GetComponent<BrainViewerNeuron>().neuron = neuron; // update current values
        }

        // update visual of synapses
        if (UPDATE_SYNAPSES) SpawnSynapses(true);

        
    }

    public Neuron GetCurrentNeuron(int i)
    {
        Neuron neuron = this.brain.GetNeuronCurrentState(i);
        return neuron;
    }

    public Synapse GetCurrentSynapse(int i)
    {
        Synapse synapse;
        if (this.brain is BrainCPU)
        {
            synapse = ((BrainCPU)this.brain).current_state_synapses[i];
        }
        else //if (this.brain is BrainGPU)
        {
            synapse = ((BrainGPU)this.brain).GetCurrentSynapse(i);
        }
        return synapse;
    }
}
