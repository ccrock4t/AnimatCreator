using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Brain;
using static CPPNGenome;
using static GlobalConfig;
using static GlobalUtils;
using static TMPro.TMP_Dropdown;

public class GenomeCreator : MonoBehaviour
{
    object current_genome;

    // prefabs
    GameObject CPPN_node_prefab;
    GameObject Generator_node_prefab;
    GameObject link_prefab;
    GameObject link_text_prefab;
    GameObject link_arrow_prefab;

    public bool initialize_on_start = true;

    [SerializeField]
    GameObject page_button_panel;
    List<Button> page_buttons;
    string current_page = "CPPN";

    [SerializeField]
    GameObject content;

    List<GameObject> pages;
    GameObject CPPN_page;
    GameObject brain_genome_page;
    GameObject body_genome_page;



    // Start is called before the first frame update
    private void Start()
    {

        this.page_buttons = new();
        this.pages = new();

        if (initialize_on_start)
        {
            Initialize();
        }
    }

    public void EnableAllPageButtons()
    {
        foreach(Button page_button in this.page_buttons){
            page_button.interactable = true;
        }
    }


    public void Initialize(object genome = null)
    {
        Debug.Log("Brain Creator: Initialization Started.");

        // get genome
        if (genome == null)
        {
            if (StaticSceneManager.genome != null)
            {
                genome = StaticSceneManager.genome;
            }
            else
            {
                Debug.LogError("No brain genome passed to BrainCreator.");
                return;
            }
        }
        this.current_genome = genome;

        List<string> list = new();

        CPPNFunction[] array_of_instruction_names = (CPPNFunction[])Enum.GetValues(typeof(CPPNFunction));
        foreach (CPPNFunction instruction in array_of_instruction_names)
        {
            DescriptionAttribute[] da = (DescriptionAttribute[])(instruction.GetType().GetField(instruction.ToString())).GetCustomAttributes(typeof(DescriptionAttribute), false);
            list.Add(da[0].Description);
        }

        if (this.CPPN_node_prefab == null) this.CPPN_node_prefab = (GameObject)Resources.Load("Prefabs/Creators/Genome/CPPNNode");
        if (this.Generator_node_prefab == null) this.Generator_node_prefab = (GameObject)Resources.Load("Prefabs/Creators/Genome/GeneratorNode");


        if (this.link_prefab == null) this.link_prefab = (GameObject)Resources.Load("Prefabs/Creators/Genome/GUILink");
        if (this.link_text_prefab == null) this.link_text_prefab = (GameObject)Resources.Load("Prefabs/Creators/Genome/GUILinkText");
        if (this.link_arrow_prefab == null) this.link_arrow_prefab = (GameObject)Resources.Load("Prefabs/Creators/Genome/GUILinkArrow");

        while(this.pages.Count > 0)
        {
            GameObject page = this.pages[0];
            this.pages.RemoveAt(0);
            Destroy(page);
        }

        this.CPPN_page = new GameObject("CPPN_page");
        this.brain_genome_page = new GameObject("Brain_generator_page");
        this.body_genome_page = new GameObject("Body_generator_page");

        this.pages.Add(this.CPPN_page);
        this.pages.Add(this.brain_genome_page);
        this.pages.Add(this.body_genome_page);

        foreach (GameObject page in this.pages)
        {
            page.transform.parent = this.content.transform;
            page.transform.localPosition = Vector3.zero;
        }

        // display genome
        if (GlobalConfig.GENOME_METHOD == GlobalConfig.GenomeMethod.CPPN)
        {
            CreateGUINodesFromCPPN(this.CPPN_page.transform, ((CPPNGenome)this.current_genome));
        }
        else if (GlobalConfig.GENOME_METHOD == GenomeMethod.LinearGenomeandNEAT)
        {
            // seen in brain viewer
        }
        else
        {
            Debug.LogError("error");
        }


        ShowPage(current_page);

        Debug.Log("Brain Creator: Initialization Completed.");
    }

    public void ShowPage(string name) {
        foreach (GameObject page in this.pages)
        {
            page.SetActive(false);
        }
        if(name == "CPPN")
        {
            this.CPPN_page.SetActive(true);
        }
        else if(name == "Brain")
        {
            this.brain_genome_page.SetActive(true);
        }
        else if (name == "Body")
        {
            this.body_genome_page.SetActive(true);
        }
        else
        {
            this.CPPN_page.SetActive(true);
        }
        current_page = name;
    }


 

 

    void CreateGUINodesFromCPPN(Transform page_transform, CPPNGenome genome)
    {
        if (genome.layers == null) return;

        Dictionary<int, RectTransform> cppnNodeIdx_to_rect = new();
        Dictionary<int, int> num_of_nodes_in_layers = new();

        Vector2 node_spacing = new(300, 100);

        for(int i=0; i < genome.CPPN_nodes.Length; i++)
        {
            CPPNnodeParallel cppn_node = genome.CPPN_nodes[i];
            int layer_num = cppn_node.layer;
            float offset = layer_num % 2 == 0 ? 0 : node_spacing.y / 2;
        
            GameObject GUI_node_GO = Instantiate(this.CPPN_node_prefab, page_transform);

            int num_of_nodes_in_layer;
            if (!num_of_nodes_in_layers.ContainsKey(layer_num)) num_of_nodes_in_layer = 0;
            else num_of_nodes_in_layer = num_of_nodes_in_layers[layer_num];
            Vector3 position = new Vector3(layer_num * node_spacing.x, num_of_nodes_in_layer * node_spacing.y + offset);
            RectTransform rect = GUI_node_GO.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            cppnNodeIdx_to_rect[i] = rect; 
            BrainCreatorGUICPPNNode GUI_node = GUI_node_GO.AddComponent<BrainCreatorGUICPPNNode>();
            GUI_node.Initialize();
            for (int k = 0; k < GUI_node.dropdown.options.Count; k++)
            {
                OptionData x = GUI_node.dropdown.options[k];
                try
                {
                    int instruction = (int)SyntaxUtils.enumValueOf(x.text, typeof(CPPNFunction));
          

                    if (instruction == (int)cppn_node.function)
                    {
                        GUI_node.dropdown.value = k;
                        break;
                    }
                }
                catch
                {
                    Debug.LogError("No enum for " + x.text);
                }
            }
            if (!num_of_nodes_in_layers.ContainsKey(layer_num)) num_of_nodes_in_layers[layer_num] = 1;
            else num_of_nodes_in_layers[layer_num]++;
        }

        for (int to_idx = 0; to_idx < genome.CPPN_nodes.Length; to_idx++)
        {
            CPPNnodeParallel cppn_node = genome.CPPN_nodes[to_idx];
            int start_idx = cppn_node.input_connection_start_idx;
            int end_idx = cppn_node.input_connection_start_idx + cppn_node.number_of_input_connections;
            for (int j= start_idx; j < end_idx; j++)
            {
                CPPNconnectionParallel connection = genome.CPPN_connections[j];
                int from_idx = connection.from_idx;
   
                RectTransform pos1 = cppnNodeIdx_to_rect[from_idx];
                RectTransform pos2 = cppnNodeIdx_to_rect[to_idx];

                BrainCreatorGUILink link = Instantiate(link_prefab, page_transform).AddComponent<BrainCreatorGUILink>();
                link.transform.SetSiblingIndex(0);

                // draw directional arrow
                link.directional_arrow = Instantiate(link_arrow_prefab, link.transform).GetComponent<RectTransform>();

                //draw weight
                link.input_field = Instantiate(link_text_prefab, link.transform).GetComponent<RectTransform>();
                link.input_field.GetComponent<TMP_InputField>().text = connection.weight + "";

                //setup link
                link.Initialize(pos1, pos2);
                
            }
        }

    }


    // FILE helpers

    public void SaveCurrentGenomeToDisk()
    {
        if (GlobalConfig.GENOME_METHOD == GenomeMethod.CPPN)
        {
            ((CPPNGenome)this.current_genome).SaveToDisk();
        }
        else
        {
            Debug.LogError("error");
        }
        
    }


    public void LoadGenomeFromDisk() {

        Debug.LogError("TODO");
    }



    // END file helpers


    // Update is called once per frame
    void Update()
    {
        HandleUserZoom();
    }


    const float min_scale = 0.01f;
    const float max_scale = 5f;
    void HandleUserZoom()
    {
        //
        float scale_change = Input.mouseScrollDelta.y * Time.deltaTime * 10;

        float new_local_scale = Mathf.Max(min_scale, this.CPPN_page.transform.parent.localScale.x + scale_change);
        new_local_scale = Mathf.Min(max_scale, new_local_scale);
        this.CPPN_page.transform.parent.localScale = Vector3.one * new_local_scale;
    }

    /// <summary>
    /// Scene loading
    /// ================
    /// </summary>
    /// 

    public void ReloadScene()
    {
        SceneManager.LoadScene("BrainCreator");
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void VisualizeCurrentGenome()
    {
        StaticSceneManager.genome = this.current_genome;
        SceneManager.LoadScene("BrainViewer");
    }

   
}
