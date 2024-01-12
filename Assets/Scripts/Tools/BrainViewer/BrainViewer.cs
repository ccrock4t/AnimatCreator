using System.Collections.Generic;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;
using static Brain;


public class BrainViewer : MonoBehaviour
{
    BrainGenome genome;
    Brain brain;

    public GameObject neuron_prefab;
    public GameObject synapse_prefab;

    public List<BrainViewerNeuron> neurons;
    public List<BrainViewerSynapse> synapses;

    private Vector3 spacing;

    bool is_alive = false;


    public bool SHOW_SYNAPSES = false;

    float timer = 0f;


    public bool initialize_on_start = true;

    // Start is called before the first frame update
    void Start()
    {



    }

    public void ClearScene()
    {
        if (this.neurons == null) return;
        foreach (BrainViewerNeuron neuron in this.neurons)
        {
            Destroy(neuron.gameObject);
        }
        foreach (BrainViewerSynapse synapse in this.synapses)
        {
            Destroy(synapse.gameObject);
        }

        this.neurons.Clear();
        this.synapses.Clear();
    }

    public void Initialize()
    {
        Debug.Log("Brain Viewer: Initialization Started.");
        ClearScene();
        this.neurons = new();
        this.synapses = new();


        if (StaticSceneManager.brain == null)
        {
            if (StaticSceneManager.brain_genome != null)
            {
                this.genome = StaticSceneManager.brain_genome;
                StaticSceneManager.brain_genome = null;
            }
            else
            {
                Debug.LogWarning("No brain genome passed to BrainViewer. Using empty genome.");
                if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.CellularEncoding)
                {
                    this.genome = CellularEncodingBrainGenome.CreateEmptyGenome();
                }
                else
                {
                    GlobalUtils.LogErrorFeatureNotImplemented("ERROR: not yet implemented");
                }

            }

            if (GlobalConfig.brain_processing_method == GlobalConfig.ProcessingMethod.CPU)
            {
                this.brain = new BrainCPU(this.genome);
            }
            else if (GlobalConfig.brain_processing_method == GlobalConfig.ProcessingMethod.GPU)
            {
                this.brain = new BrainGPU(this.genome);
            }
            else
            {
                GlobalUtils.LogErrorEnumNotRecognized("Not recognized.");
            }

            this.is_alive = false;
        }
        else
        {
            this.brain = StaticSceneManager.brain;
            this.genome = StaticSceneManager.brain_genome;
            this.is_alive = true;
        }

        if (this.brain is BrainGPU)
        {
            ((BrainGPU)this.brain).TransferFromGPUToCPU();
        }

        if (this.genome is HyperNEATBrainGenome || this.genome is NEATBrainGenome || this.genome is CellularEncodingBrainGenome || this.genome is ESHyperNEATBrainGenome)
        {
            this.spacing = new Vector3(10f, 10f, 25f);
        }
        else if (this.genome is AxonalGrowthBrainGenome)
        {
            this.spacing = new Vector3(10f, 10f, 25f) * 10f;
        }

        this.SpawnNeurons();
        if (SHOW_SYNAPSES) this.SpawnSynapses();
        if (!is_alive)
        {
            Debug.Log("Viewing genome topology only, so disposing of memory allocated for parallel access of regular neurons.");
            this.brain.DisposeOfNativeCollections();
        }

        Debug.Log("Brain Viewer: Initialization Completed.");
    }

    private void Awake()
    {
        // limit framerate to prevent GPU from going overdrive for no reason
        QualitySettings.vSyncCount = 0;  // VSync must be disabled
        Application.targetFrameRate = 45;
    }

    public void SpawnNeurons()
    {
        Vector3 position;
        Vector3 default_location = Vector3.zero; //x=sensor, y=hidden, z=motor
        for (int i = 0; i < this.brain.GetNumberOfNeurons(); i++)
        {
            GameObject neuronGO = Instantiate(neuron_prefab, this.transform);
            neuronGO.transform.localScale *= 3;
            Neuron neuron = this.GetCurrentNeuron(i);
            if (neuron.position.x == -1 && neuron.position.y == -1 && neuron.position.z == -1)
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
                position = new Vector3(neuron.position.x, neuron.position.y, neuron.position.z);
            }
            neuronGO.transform.localPosition = Vector3.Scale(position, spacing);
            this.neurons.Add(neuronGO.GetComponent<BrainViewerNeuron>());
        }

    }

    public void SpawnSynapses()
    {


        for (int i = 0; i < this.brain.GetNumberOfNeurons(); i++)
        {
            GameObject to_neuron_GO = this.neurons[i].gameObject;
            Neuron neuron = this.GetCurrentNeuron(i);
            for (int j = neuron.synapse_start_idx; j < neuron.synapse_start_idx + neuron.synapse_count; j++)
            {
                Synapse synapse = this.GetCurrentSynapse(j);
                if (!synapse.enabled) continue;
                GameObject from_neuron_GO = this.neurons[synapse.from_neuron_idx].gameObject;

                GameObject synapseGO = Instantiate(synapse_prefab, this.transform);
                BrainViewerSynapse brain_viewer_synapse = synapseGO.GetComponent<BrainViewerSynapse>();
                brain_viewer_synapse.synapse = synapse;
                brain_viewer_synapse.gameObjectA = to_neuron_GO;
                brain_viewer_synapse.gameObjectB = from_neuron_GO;
                this.synapses.Add(brain_viewer_synapse);
                synapseGO.GetComponent<LineRenderer>().SetPositions(new Vector3[] { to_neuron_GO.transform.position, from_neuron_GO.transform.position });

            }
        }

    }



    void Update()
    {

        timer -= Time.deltaTime;
        if (timer > 0) return;

        if (this.brain is BrainCPU)
        {
            if (!((BrainCPU)this.brain).current_state_neurons.IsCreated) return;
        }
        else //if (this.brain is BrainGPU)
        {
            //todo
            if (((BrainGPU)this.brain).current_state_neurons == null) return;
        }

        // update visual of neurons
        for (int i = 0; i < this.brain.GetNumberOfNeurons(); i++)
        {
            Neuron neuron = GetCurrentNeuron(i);
            GameObject neuronGO = this.neurons[i].gameObject;


            this.neurons[i].UpdateColor(neuron.sign == 1, neuron.activation);
            neuronGO.GetComponent<BrainViewerNeuron>().neuron = neuron;
        }

        // update visual of synapses
        /*      for (int i = 0; i < this.brain.GetNumberOfSynapses(); i++)
              {
                  Synapse synapse = GetCurrentSynapse(i);
                  GameObject synapseGO = this.synapses[i].gameObject;


                  this.neurons[i].UpdateColor(neuron.sign == 1, neuron.activation);
                  neuronGO.GetComponent<BrainViewerNeuron>().neuron = neuron;
              }*/

        timer = GlobalConfig.BRAIN_VIEWER_UPDATE_PERIOD;
    }

    public Neuron GetCurrentNeuron(int i)
    {
        Neuron neuron;
        if (this.brain is BrainCPU)
        {
            ((BrainCPU)this.brain).update_job_handle.Complete();
            //((BrainCPU)this.brain).update_job2_handle.Complete();
            neuron = ((BrainCPU)this.brain).current_state_neurons[i];
        }
        else //if (this.brain is BrainGPU)
        {

            neuron = ((BrainGPU)this.brain).GetCurrentNeuron(i);
        }
        return neuron;
    }

    public Synapse GetCurrentSynapse(int i)
    {
        Synapse synapse;
        if (this.brain is BrainCPU)
        {
            ((BrainCPU)this.brain).update_job_handle.Complete();
            //if (((BrainCPU)this.brain).current_state_synapses.Length > 0 && ((BrainCPU)this.brain).current_state_neurons.Length > 0) ((BrainCPU)this.brain).update_job2_handle.Complete();
            synapse = ((BrainCPU)this.brain).current_state_synapses[i];
        }
        else //if (this.brain is BrainGPU)
        {
            synapse = ((BrainGPU)this.brain).GetCurrentSynapse(i);
        }
        return synapse;
    }
}
