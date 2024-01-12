using System;
using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static AxonalGrowthBrainGenome;
using static BrainGenomeTree;
using static CellularEncodingBrainGenome;
using static HyperNEATBrainGenome;
using static TMPro.TMP_Dropdown;

public class BrainCreator : MonoBehaviour
{

    BrainGenome current_genome;

    List<List<BrainCreatorGUITreeNode>> GUI_tree_nodes;
    public Dictionary<ProgramSymbolTree, BrainCreatorGUITreeNode> GUI_nodes_dictionary;

    //pages
    public GameObject pages_panel;
    public GameObject pages_button_prefab;
    List<GameObject> pages;
    int active_page_num = 0;

    // prefabs
    GameObject node_prefab;
    GameObject link_prefab;
    GameObject link_text_prefab;

    public bool initialize_on_start = true;

    // Start is called before the first frame update
    private void Start()
    {
        if (initialize_on_start)
        {

            if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.CellularEncoding)
            {
                Initialize(CellularEncodingBrainGenome.CreateBrainGenomeWithHexapodConstraints());

            }
            else if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.SGOCE)
            {
                Initialize(AxonalGrowthBrainGenome.CreateTestGenome());
            }
            else if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.HyperNEAT)
            {
                Initialize(RegularHyperNEATBrainGenome.CreateTestGenome());
            }
            else
            {
                Debug.LogError("ERROR: not yet implemented");
                return;
            }
        }
    }


    public void Initialize(BrainGenome genome = null)
    {
        Debug.Log("Brain Creator: Initialization Started.");

        // get genome
        if (genome == null)
        {
            if (StaticSceneManager.brain_genome != null)
            {
                genome = StaticSceneManager.brain_genome;
            }
            else
            {
                Debug.Log("No brain genome passed to BrainCreator. Using empty genome.");
                if (GlobalConfig.brain_genome_method != GlobalConfig.BrainGenomeMethod.CellularEncoding)
                {
                    Debug.LogError("error");
                }
                genome = CellularEncodingBrainGenome.CreateEmptyGenome3Page();
            }
        }
        this.current_genome = genome;

        List<string> list = new();

        if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.CellularEncoding)
        {
            CECellularInstruction[] array_of_instruction_names = (CECellularInstruction[])Enum.GetValues(typeof(CECellularInstruction));
            foreach (CECellularInstruction instruction in array_of_instruction_names)
            {
                DescriptionAttribute[] da = (DescriptionAttribute[])(instruction.GetType().GetField(instruction.ToString())).GetCustomAttributes(typeof(DescriptionAttribute), false);
                list.Add(da[0].Description);
            }

        }
        else if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.SGOCE)
        {
            AxonalGrowthCellularInstruction[] array_of_instruction_names = (AxonalGrowthCellularInstruction[])Enum.GetValues(typeof(AxonalGrowthCellularInstruction));
            foreach (AxonalGrowthCellularInstruction instruction in array_of_instruction_names)
            {
                DescriptionAttribute[] da = (DescriptionAttribute[])(instruction.GetType().GetField(instruction.ToString())).GetCustomAttributes(typeof(DescriptionAttribute), false);
                list.Add(da[0].Description);
            }
        }
        else if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.HyperNEAT || GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.ESHyperNEAT)
        {
            CPPNFunction[] array_of_instruction_names = (CPPNFunction[])Enum.GetValues(typeof(CPPNFunction));
            foreach (CPPNFunction instruction in array_of_instruction_names)
            {
                DescriptionAttribute[] da = (DescriptionAttribute[])(instruction.GetType().GetField(instruction.ToString())).GetCustomAttributes(typeof(DescriptionAttribute), false);
                list.Add(da[0].Description);
            }
        }
        else if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.NEAT)
        {
            return;
        }
        else
        {
            Debug.LogError("ERROR: not yet implemented");
            return;
        }




        if (this.node_prefab == null) this.node_prefab = (GameObject)Resources.Load("Prefabs/Creators/Brain/Node");

        this.node_prefab.GetComponent<TMP_Dropdown>().AddOptions(list);

        if (this.link_prefab == null) this.link_prefab = (GameObject)Resources.Load("Prefabs/Creators/Brain/Link");
        if (this.link_text_prefab == null) this.link_text_prefab = (GameObject)Resources.Load("Prefabs/Creators/Brain/LinkText");
        if (this.pages_button_prefab == null) this.pages_button_prefab = (GameObject)Resources.Load("Prefabs/Creators/Brain/PageButton");

        if (this.GUI_tree_nodes == null) this.GUI_tree_nodes = new();
        if (this.GUI_nodes_dictionary == null) this.GUI_nodes_dictionary = new();
        if (this.pages == null) this.pages = new();


        // destroy gameobjects
        while (this.GUI_tree_nodes.Count > 0)
        {
            DestroyGUITree(0);
        }

        while (this.pages.Count > 0)
        {
            GameObject obj = this.pages[0];
            this.pages.RemoveAt(0);
            Destroy(obj);
        }

        foreach (Transform page_button_GO in pages_panel.transform)
        {
            GameObject.Destroy(page_button_GO.gameObject);
        }





        this.GUI_tree_nodes.Clear();
        this.GUI_nodes_dictionary.Clear();

        if (genome is BrainGenomeTree)
        {
            BrainGenomeTree genome_tree = (BrainGenomeTree)genome;
            for (int i = 0; i < genome_tree.forest.Count; i++)
            {
                ProgramSymbolTree root = genome_tree.forest[i];

                // make page to display tree
                GameObject page = new GameObject("Page" + i);
                List<BrainCreatorGUITreeNode> node_list = new();
                RectTransform RT = page.AddComponent<RectTransform>();

                page.transform.SetParent(this.transform, worldPositionStays: false);
                RT.anchoredPosition3D = Vector3.zero;

                // display tree
                CreateGUINodesFromTree(node_list, RT, root);

                this.GUI_tree_nodes.Add(node_list);
                if (i != this.active_page_num)
                {
                    page.SetActive(false); // disable all but the first page
                }

                // make button to access pages
                GameObject page_button_GO = Instantiate(pages_button_prefab, pages_panel.transform);
                Button page_button = page_button_GO.GetComponentInChildren<Button>();
                TMP_Text page_button_text = page_button_GO.GetComponentInChildren<TMP_Text>();
                page_button_text.text = (i).ToString();
                int j = i; // copy i or else the delegate wont work properly
                page_button.onClick.AddListener(delegate { OpenPage(j); });
                this.pages.Add(page);
            }

            LayoutGUITrees();

        }
        else
        {
            // make page to display genome
            GameObject page = new GameObject("Page0");
            this.pages.Add(page);

            RectTransform RT = page.AddComponent<RectTransform>();

            page.transform.SetParent(this.transform, worldPositionStays: false);
            RT.anchoredPosition3D = Vector3.zero;

            // display genome
            CreateGUINodesFromCPPN((HyperNEATBrainGenome)this.current_genome);


            this.pages.Add(page);
        }





        Debug.Log("Brain Creator: Initialization Completed.");
    }

    public void DestroyGUITree(int i, bool remove_tree_from_forest = true)
    {
        List<BrainCreatorGUITreeNode> gui_tree = this.GUI_tree_nodes[i];
        while (gui_tree.Count > 0)
        {
            BrainCreatorGUITreeNode GUI_node = gui_tree[0];
            DestroyGUINode(GUI_node);
            this.GUI_nodes_dictionary.Remove(GUI_node.tree);
            gui_tree.RemoveAt(0);
        }
        if (remove_tree_from_forest) this.GUI_tree_nodes.RemoveAt(i);
    }

    public void OpenPage(int page_num)
    {
        Debug.Log("open " + page_num);
        for (int i = 0; i < this.pages.Count; i++)
        {
            GameObject page_GO = this.pages[i];
            page_GO.SetActive(i == page_num);
            if (i == page_num) this.active_page_num = i;
        }
    }

    void CreateGUINodesFromCPPN(HyperNEATBrainGenome genome)
    {
        if (genome.layers == null) return;

        Dictionary<CPPNnode, RectTransform> cppnNode_to_rect = new();

        Transform page_transform = this.pages[0].transform;


        Vector2 node_spacing = new(300, 100);

        for (int i = 0; i < genome.layers.Count; i++)
        {
            List<CPPNnode> layer = genome.layers[i];
            for (int j = 0; j < layer.Count; j++)
            {
                CPPNnode cppn_node = layer[j];
                GameObject GUI_node_GO = Instantiate(this.node_prefab, page_transform);
                Vector3 position = new Vector3(i * node_spacing.x, j * node_spacing.y);
                RectTransform rect = GUI_node_GO.GetComponent<RectTransform>();
                rect.anchoredPosition = position;
                cppnNode_to_rect[cppn_node] = rect;
                BrainCreatorGUINode GUI_node = GUI_node_GO.AddComponent<BrainCreatorGUINode>();
                GUI_node.Initialize();
                for (int k = 0; k < GUI_node.dropdown.options.Count; k++)
                {
                    OptionData x = GUI_node.dropdown.options[k];
                    try
                    {
                        int instruction = (int)GlobalUtils.enumValueOf(x.text, typeof(CPPNFunction));


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

            }
        }



        for (int i = 0; i < genome.layers.Count; i++)
        {
            List<CPPNnode> layer = genome.layers[i];
            for (int j = 0; j < layer.Count; j++)
            {
                CPPNnode cppn_node = layer[j];
                foreach ((CPPNnode, CPPNconnection) output in cppn_node.outputs)
                {
                    CPPNconnection connection = output.Item2;
                    if (!connection.enabled) continue;
                    CPPNnode output_node = output.Item1;
                    RectTransform pos1 = cppnNode_to_rect[cppn_node];
                    RectTransform pos2 = cppnNode_to_rect[output_node];

                    BrainCreatorGUILink link = Instantiate(link_prefab, page_transform).AddComponent<BrainCreatorGUILink>();
                    link.transform.parent = this.pages[0].transform;
                    link.input_field = Instantiate(link_text_prefab, link.transform).GetComponent<RectTransform>();
                    link.input_field.GetComponent<TMP_InputField>().text = connection.weight + "";
                    link.Initialize(pos1, pos2);
                }


            }
        }



    }

    BrainCreatorGUITreeNode CreateGUINodesFromTree(List<BrainCreatorGUITreeNode> list, RectTransform GUIpage, ProgramSymbolTree tree)
    {

        BrainCreatorGUITreeNode GUI_node;


        if (!this.GUI_nodes_dictionary.ContainsKey(tree))
        {
            GameObject GUI_node_GO = Instantiate(this.node_prefab, GUIpage);
            GUI_node = GUI_node_GO.AddComponent<BrainCreatorGUITreeNode>();
            GUI_node_GO.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate
            {
                GUI_node.InstructionChanged();
            });
            GUI_node.Initialize(tree);
            GUI_node.Subscribe(this);
            list.Add(GUI_node);
            this.GUI_nodes_dictionary[GUI_node.tree] = GUI_node;

            for (int i = 0; i < GUI_node.dropdown.options.Count; i++)
            {
                OptionData x = GUI_node.dropdown.options[i];
                try
                {
                    int instruction;
                    if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.CellularEncoding)
                    {
                        // continue
                        instruction = (int)GlobalUtils.enumValueOf(x.text, typeof(CECellularInstruction));
                    }
                    else if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.SGOCE)
                    {
                        instruction = (int)GlobalUtils.enumValueOf(x.text, typeof(AxonalGrowthCellularInstruction));
                    }
                    else
                    {
                        Debug.LogError("Not supported.");
                        return null;
                    }

                    if (instruction == (int)tree.instruction)
                    {
                        GUI_node.dropdown.value = i;
                        GUI_node.UpdateColor();
                        break;
                    }
                }
                catch
                {
                    Debug.LogError("No enum for " + x.text);
                }


            }
        }
        else
        {
            GUI_node = this.GUI_nodes_dictionary[tree];
        }

        int j = 0;
        foreach (ProgramSymbolTree subtree in tree.children)
        {
            BrainCreatorGUITreeNode child_GUI_node = CreateGUINodesFromTree(list, GUIpage, subtree);
            child_GUI_node.link_to_parent = Instantiate(link_prefab, GUIpage).AddComponent<BrainCreatorGUITreeLink>();
            child_GUI_node.parent = GUI_node;
            child_GUI_node.position_data_2D.index = j;
            child_GUI_node.position_data_2D.parent = GUI_node.position_data_2D;
            j++;
            GUI_node.AddChild(child_GUI_node);
        }


        return GUI_node;
    }

    // FILE helpers

    public void SaveCurrentGenomeToDisk()
    {
        this.current_genome.SaveToDisk();
    }


    public void LoadGenomeFromDisk()
    {

        Debug.LogError("TODO");
        //BrainGenomeTree genome = BrainGenomeTree.LoadFromDisk();
        //Initialize(genome);
    }



    // END file helpers


    /// <summary>
    /// returns a list of all GUI nodes associated with the given root and its children
    /// </summary>
    /// <param name="root">TreeNode root, whose GUI node will be included in the list</param>
    /// <returns></returns>
    public List<BrainCreatorGUITreeNode> GetListOfGUINodes(ProgramSymbolTree root)
    {
        List<BrainCreatorGUITreeNode> list = new();

        BrainCreatorGUITreeNode root_node = this.GUI_nodes_dictionary[root];
        list.Add(root_node);

        foreach (ProgramSymbolTree child in root.children)
        {
            List<BrainCreatorGUITreeNode> subtree = GetListOfGUINodes(child);
            foreach (BrainCreatorGUITreeNode GUI_node in subtree)
            {
                list.Add(GUI_node);
            }
        }

        return list;
    }

    public void DestroyGUINode(BrainCreatorGUITreeNode node)
    {
        if (node.link_to_parent != null)
        {
            Destroy(node.link_to_parent.gameObject);
        }

        Destroy(node.gameObject);
    }

    /// <summary>
    /// A GUI Node has changed
    /// </summary>
    /// <param name="changed_GUI_node"></param>
    /// <param name="from"></param>
    /// <param name="to"></param>
    public void NodeChanged(BrainCreatorGUITreeNode changed_GUI_node, object from, object to)
    {
        if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.CellularEncoding)
        {
            if ((CECellularInstruction)from == (CECellularInstruction)to) return;
        }
        else if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.SGOCE)
        {
            if ((AxonalGrowthCellularInstruction)from == (AxonalGrowthCellularInstruction)to) return;
        }
        else
        {
            Debug.LogError("");
        }


        int from_children, to_children;

        if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.CellularEncoding)
        {
            from_children = CellularEncodingBrainGenome.HowManyChildren(from);
            to_children = CellularEncodingBrainGenome.HowManyChildren(to);
        }
        else if (GlobalConfig.brain_genome_method == GlobalConfig.BrainGenomeMethod.SGOCE)
        {
            from_children = AxonalGrowthBrainGenome.HowManyChildren(from);
            to_children = AxonalGrowthBrainGenome.HowManyChildren(to);
        }
        else
        {
            Debug.LogError("Not supported.");
            return;
        }


        if (this.current_genome is BrainGenomeTree)
        {
            changed_GUI_node.tree.ChangeInstruction(to, new int[0]);
            if (from_children == to_children) return; // done if the amount of children is the same, since children will not change


            DestroyGUITree(active_page_num, remove_tree_from_forest: false);

            //otherwise, the child trees were deleted, and we must create new nodes for the new END children and re-layout the tree
            CreateGUINodesFromTree(this.GUI_tree_nodes[active_page_num], this.pages[active_page_num].GetComponent<RectTransform>(), ((BrainGenomeTree)this.current_genome).forest[active_page_num]);
            LayoutGUITrees();
        }

    }


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
        if (this.pages == null) return;
        float scale_change = Input.mouseScrollDelta.y * Time.deltaTime * 10;

        if (active_page_num != this.pages.Count) active_page_num = 0;
        float new_local_scale = Mathf.Max(min_scale, this.pages[active_page_num].transform.parent.localScale.x + scale_change);
        new_local_scale = Mathf.Min(max_scale, new_local_scale);
        this.pages[active_page_num].transform.parent.localScale = Vector3.one * new_local_scale;
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
        StaticSceneManager.brain_genome = this.current_genome;
        SceneManager.LoadScene("BrainViewer");
    }

    ///
    /// ================
    ///


    /// <summary>
    /// Layout the entire tree
    /// </summary>
    /// <param name="tree"></param>
    public void LayoutGUITrees()
    {

        if (this.current_genome == null)
        {
            Debug.LogWarning("Attempted to layout null genome.");
            return;
        }

        if (!(this.current_genome is BrainGenomeTree)) return;

        BrainGenomeTree genome = (BrainGenomeTree)this.current_genome;

        for (int i = 0; i < genome.forest.Count; i++)
        {
            ProgramSymbolTree root = genome.forest[i];

            BrainCreatorGUITreeNode GUI_root = this.GUI_nodes_dictionary[root];
            List<BrainCreatorGUITreeNode> GUI_nodes = this.GUI_tree_nodes[i];
            foreach (BrainCreatorGUITreeNode GUI_node in GUI_nodes)
            {
                if (GUI_node == GUI_root) continue;
                GUI_node.ResetPositionData();
            }

            GraphVisualization2D.setup(GUI_root.position_data_2D);

            foreach (BrainCreatorGUITreeNode GUI_node in GUI_nodes)
            {
                if (GUI_node == GUI_root) continue;
                GUI_node.SetPosition(GUI_root.position_data_2D.x);
            }
        }




    }


}
