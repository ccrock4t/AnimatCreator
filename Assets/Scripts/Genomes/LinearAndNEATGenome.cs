using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static SoftVoxelRobot;

public class LinearAndNEATGenome
{
    public int generation = 0;
    int3 dimensions3D;
    public NEATGenome brain_genome;
    public RobotVoxel[] body_genome;

    public const bool EVOLVE_BODY = false;

    const float CHANCE_TO_MUTATE_BODY_VOXEL = 0.1f;

    public LinearAndNEATGenome()
    {
    }

    public LinearAndNEATGenome(int3 dimensions3D) {
        this.dimensions3D = dimensions3D;
        this.brain_genome = NEATGenome.CreateTestGenome(dimensions3D);
        this.body_genome = new RobotVoxel[this.dimensions3D.x * this.dimensions3D.y * this.dimensions3D.z];

        // set body
        for (int x = 0; x < this.dimensions3D.x; x++)
        {
            for (int y = 0; y < this.dimensions3D.y; y++)
            {
                for (int z = 0; z < this.dimensions3D.z; z++)
                {
                    int i = GlobalUtils.Index_FlatFromint3(x, y, z, dimensions3D);
                    if (EVOLVE_BODY)
                    {
                        System.Random sysrnd = new();
                        RobotVoxel rnd_voxel = (RobotVoxel)sysrnd.Next(0, Enum.GetNames(typeof(RobotVoxel)).Length);
                        body_genome[i] = rnd_voxel;
                    }
                    else
                    {
                        body_genome[i] = RobotVoxel.Touch_Sensor;
                        if (y == 0)
                        {
                            body_genome[i] = RobotVoxel.Touch_Sensor;
                        }
                        else
                        {
                            if (x == 0)
                            {
                                if (y > 0 && y < this.dimensions3D.y - 1)
                                {
                                    if (z > 0 && z < this.dimensions3D.z - 1)
                                    {
                                        body_genome[i] = RobotVoxel.Raycast_Sensor;
                                    }
                                }
                            }
                        }

                        if (x == 0 && y == 0 && z == 0) body_genome[i] = RobotVoxel.SineWave_Generator;
                    }

                }
            }
        }
    }

    public static LinearAndNEATGenome CreateTestGenome()
    {
        int3 dimensions3D = new(GlobalConfig.ANIMAT_SUBSTRATE_DIMENSIONS.x,
            GlobalConfig.ANIMAT_SUBSTRATE_DIMENSIONS.y,
            GlobalConfig.ANIMAT_SUBSTRATE_DIMENSIONS.z);
        return new LinearAndNEATGenome(dimensions3D);
    }

    public (LinearAndNEATGenome offspring1, LinearAndNEATGenome offspring2) Reproduce(LinearAndNEATGenome parent2)
    {
        (NEATGenome offspring1_brain, NEATGenome offspring2_brain) = this.brain_genome.Reproduce(parent2.brain_genome);
        RobotVoxel[] offspring1_body, offspring2_body;
        if (EVOLVE_BODY)
        {
            (offspring1_body, offspring2_body) = this.ReproduceBodyGenomes(this.body_genome, parent2.body_genome);
        }
        else
        {
            (offspring1_body, offspring2_body) = ((RobotVoxel[])this.body_genome.Clone(), (RobotVoxel[])parent2.body_genome.Clone());
        }
        

        LinearAndNEATGenome offspring1 = new();
        offspring1.brain_genome = offspring1_brain;
        offspring1.body_genome = offspring1_body;

        LinearAndNEATGenome offspring2 = new();
        offspring2.brain_genome = offspring2_brain;
        offspring2.body_genome = offspring2_body;

        return (offspring1, offspring2);
    }

    public (RobotVoxel[], RobotVoxel[]) ReproduceBodyGenomes(RobotVoxel[] parent1, RobotVoxel[] parent2)
    {
        RobotVoxel[] offspring1 = new RobotVoxel[parent1.Length];
        RobotVoxel[] offspring2 = new RobotVoxel[parent2.Length];

        // two point crossover
        int point1 = UnityEngine.Random.Range(0, offspring1.Length);
        int point2 = UnityEngine.Random.Range(0, offspring1.Length);

        if(point1 > point2)
        {
            int swap = point1;
            point1 = point2;
            point2 = swap;
        }

        for(int i=0; i< offspring1.Length; i++)
        {
            if(i > point1 && i <= point2)
            {
                offspring1[i] = parent1[i];
                offspring2[i] = parent2[i];
            }
            else
            {
                offspring1[i] = parent2[i];
                offspring2[i] = parent1[i];
            }
        }

        return (offspring1, offspring2);
    }

    public LinearAndNEATGenome Clone()
    {
        LinearAndNEATGenome cloned_genome = new();
        cloned_genome.brain_genome = this.brain_genome.Clone();
        cloned_genome.body_genome = (RobotVoxel[])this.body_genome.Clone();
        cloned_genome.generation = this.generation;
        return cloned_genome;


        
    }


    public void Mutate()
    {
        this.brain_genome.Mutate();

        if (EVOLVE_BODY)
        {
            System.Random sysrnd = new();
            for (int i = 0; i < body_genome.Length; i++)
            {
                float rnd = UnityEngine.Random.Range(0f, 1f);
                if (rnd < CHANCE_TO_MUTATE_BODY_VOXEL)
                {

                    RobotVoxel rnd_voxel = (RobotVoxel)sysrnd.Next(0, Enum.GetNames(typeof(RobotVoxel)).Length);
                    body_genome[i] = rnd_voxel;
                }

            }
        }
    }

    public class NEATGenome
    {
        public const int NUM_OF_SENSOR_NEURONS = 2;
        public const int NUM_OF_MOTOR_NEURONS = 3;

        const bool ADD_RANDOM_STARTING_CONNECTIONS = false;
        const bool ADD_RANDOM_STARTING_NODES = false;
        const int NUM_OF_RANDOM_STARTING_CONNECTIONS = 100;
        const bool DROPOUT = true;
        const float DROPOUT_RATE = 0.5f;
        const int NUM_OF_RANDOM_STARTING_NODES = 30;
        const float WEIGHT_MUTATE_INCREMENT = 0.1f;
        const float CHANCE_TO_MUTATE_EACH_CONNECTION = 0.8f;
        const float ADD_CONNECTION_MUTATION_RATE = 0.05f;
        const float RATE_MULTIPLIER = 3f;
        const float ADD_NODE_MUTATION_RATE = 0.03f;


        public Dictionary<int, int> nodeID_to_idx;
        public List<NEATNode> nodes;
        public List<NEATConnection> connections;
        public List<NEATConnection> enabled_connections;

        public static int NEXT_GLOBAL_NODE_ID = -1;
        public static int NEXT_GLOBAL_CONNECTION_ID = -1;

        public static int2 IO_idxs;

        public NEATGenome()
        {
            this.nodes = new();
            this.connections = new();
            this.nodeID_to_idx = new();
            this.enabled_connections = new();
        }

        public static NEATGenome CreateTestGenome(int3 dimensions3D)
        {
            NEATGenome genome = new();

            int num_of_voxels = dimensions3D.x * dimensions3D.y * dimensions3D.z;

            // add sensor neurons
            for (int i=0; i< num_of_voxels; i++)
            {
                int3 coords = GlobalUtils.Index_int3FromFlat(i, dimensions3D);
                for (int k = 0; k < NUM_OF_SENSOR_NEURONS; k++)
                { 
                    NEATNode sensor_node = new(
                        new int3(coords.x, coords.y, coords.z),
                        Brain.Neuron.NeuronClass.Sensor,
                        ID: genome.nodes.Count);
                    genome.nodeID_to_idx[sensor_node.ID] = genome.nodes.Count;
                    genome.nodes.Add(sensor_node);
                }
            }
 
                
           

            IO_idxs.x = genome.nodes.Count;

            // add motor neurons
            for (int i = 0; i < num_of_voxels; i++)
            {
                int3 coords = GlobalUtils.Index_int3FromFlat(i, dimensions3D);
                for (int k = 0; k < NUM_OF_MOTOR_NEURONS; k++)
                {
                    NEATNode motor_node = new(
                         new int3(coords.x, coords.y, coords.z),
                         Brain.Neuron.NeuronClass.Motor,
                         ID: genome.nodes.Count);
                    genome.nodeID_to_idx[motor_node.ID] = genome.nodes.Count;
                    genome.nodes.Add(motor_node);
                    for (int j = 0; j < NUM_OF_SENSOR_NEURONS; j++)
                    {
                        // add a connection from sensor to motor in the voxel as well
                        NEATConnection sr_connection = new(weight: GetRandomConnectionWeight(),
                            fromID: genome.nodes[NUM_OF_SENSOR_NEURONS*i + j].ID,
                            toID: motor_node.ID,
                            ID: genome.connections.Count);
                            genome.connections.Add(sr_connection);
                        genome.enabled_connections.Add(sr_connection);
                    }
                }
            }
            IO_idxs.y = genome.nodes.Count;


        

            if (ADD_RANDOM_STARTING_CONNECTIONS)
            {
                for (int j = 0; j < NUM_OF_RANDOM_STARTING_CONNECTIONS; j++)
                {

                    NEATConnection connection = genome.AddNewRandomConnection(genome.connections.Count);
                    if (DROPOUT && UnityEngine.Random.Range(0, 1f) < DROPOUT_RATE)
                    {
                        connection.enabled = false;
                    }
                    else
                    {
                        genome.enabled_connections.Add(connection);
                    }
                }
            }



            if (ADD_RANDOM_STARTING_NODES)
            {
                for (int j = 0; j < NUM_OF_RANDOM_STARTING_NODES; j++)
                {
                    NEATNode node = genome.AddNewNode(genome.nodes.Count);
                }
            }

            
            if (ADD_RANDOM_STARTING_CONNECTIONS)
            {
                for (int j = 0; j < NUM_OF_RANDOM_STARTING_CONNECTIONS; j++)
                {
                    NEATConnection connection = genome.AddNewRandomConnection(genome.connections.Count);
                    if (DROPOUT && UnityEngine.Random.Range(0, 1f) < DROPOUT_RATE)
                    {
                        connection.enabled = false;
                    }
                    else
                    {
                        genome.enabled_connections.Add(connection);
                    }
                }
            }
                      

            if (NEXT_GLOBAL_NODE_ID == -1) NEXT_GLOBAL_NODE_ID = genome.nodes.Count;
            if (NEXT_GLOBAL_CONNECTION_ID == -1) NEXT_GLOBAL_CONNECTION_ID = genome.connections.Count;
            return genome;
        }

        public static float GetRandomConnectionWeight()
        {
            return UnityEngine.Random.Range(-1f, 1f);
        }


        public void Mutate()
        {
            bool should_mutate;
            float rnd;
            // first, mutate synapse parameters
            foreach (NEATConnection connection in this.connections)
            {
                should_mutate = UnityEngine.Random.Range(0f, 1f) < CHANCE_TO_MUTATE_EACH_CONNECTION;
                if (!should_mutate) continue;
                rnd = UnityEngine.Random.Range(0f, 1f);

                if (rnd < 0.9)
                {
                    connection.weight += UnityEngine.Random.Range(-WEIGHT_MUTATE_INCREMENT, WEIGHT_MUTATE_INCREMENT);
                }
                else
                {
                    connection.weight = UnityEngine.Random.Range(-3, 3);
                }

            }

            foreach (NEATNode node in this.nodes)
            {
                should_mutate = UnityEngine.Random.Range(0f, 1f) < CHANCE_TO_MUTATE_EACH_CONNECTION;
                if (!should_mutate) continue;
                rnd = UnityEngine.Random.Range(0f, 1f);

                if (rnd < 0.9f)
                {
                    node.bias += UnityEngine.Random.Range(-WEIGHT_MUTATE_INCREMENT, WEIGHT_MUTATE_INCREMENT);
                }
/*                else if (rnd < 0.99)
                {
                    node.enabled = false;
                }*/
                else
                {
                    node.bias = UnityEngine.Random.Range(-3, 3);
                }

            }

            rnd = UnityEngine.Random.Range(0f, 1f);


            // add connection?
            if (rnd < ADD_CONNECTION_MUTATION_RATE * RATE_MULTIPLIER)
            {
                AddNewRandomConnection();
            }


            // add node?
            if (rnd < ADD_NODE_MUTATION_RATE * RATE_MULTIPLIER)
            {
                AddNewNode();
            }

        }

        private NEATConnection AddNewRandomConnection(int ID=-1)
        {
            int from_idx = UnityEngine.Random.Range(0, this.nodes.Count);
            int to_idx = UnityEngine.Random.Range(IO_idxs.x, this.nodes.Count);
            NEATNode from_neuron = this.nodes[from_idx];
            NEATNode to_neuron = this.nodes[to_idx];

            float rnd_weight = GetRandomConnectionWeight();
            NEATConnection new_connection = new(rnd_weight, from_neuron.ID, to_neuron.ID, ID);
            this.connections.Add(new_connection);
            return new_connection;
        }

        private NEATNode AddNewNode(int ID=-1)
        {
            // insert the node at a random connection
            NEATConnection random_connection = this.enabled_connections[UnityEngine.Random.Range(0, this.enabled_connections.Count)];
            random_connection.enabled = false;
            this.enabled_connections.Remove(random_connection);
            NEATNode new_node = new(float3.zero, Brain.Neuron.NeuronClass.Hidden, ID: ID) ;
            new_node.bias = GetRandomConnectionWeight();
            NEATConnection new_connectionA = new(weight: 1, fromID: random_connection.fromID, toID: new_node.ID);
            NEATConnection new_connectionB = new(weight: random_connection.weight, fromID: new_node.ID, toID: random_connection.toID);
            this.connections.Add(new_connectionA);
            this.connections.Add(new_connectionB);
            this.enabled_connections.Add(new_connectionA);
            this.enabled_connections.Add(new_connectionB);
            this.nodes.Add(new_node);

            // set node position for BrainViewer
            NEATNode to_node = this.nodes[this.nodeID_to_idx[random_connection.toID]];
            NEATNode from_node = this.nodes[this.nodeID_to_idx[random_connection.fromID]];
            float3 coords = (to_node.coords + from_node.coords)/ 2;
            new_node.coords = coords;
            this.nodeID_to_idx[new_node.ID] = this.nodes.Count-1;
            return new_node;
        }

        static int GetNextGlobalNodeID()
        {
            int ID = NEXT_GLOBAL_NODE_ID;
            NEXT_GLOBAL_NODE_ID++;
            return ID;
        }

        static int GetNextGlobalConnectionID()
        {
            int ID = NEXT_GLOBAL_CONNECTION_ID;
            NEXT_GLOBAL_CONNECTION_ID++;
            return ID;
        }

        public (NEATGenome, NEATGenome) Reproduce(NEATGenome parent2)
        {

            Dictionary<int, int> validate_node_to_connections_offspring1 = new();
            Dictionary<int, int> validate_node_to_connections_offspring2 = new();

            NEATGenome offspring1 = new();
            NEATGenome offspring2 = new();
            NEATGenome parent1 = this;
            int rnd;

            int i = 0, j = 0;
            while (i < parent1.nodes.Count || j < parent2.nodes.Count)
            {
                NEATNode neuron1 = null;
                if (i < parent1.nodes.Count)
                {
                    neuron1 = (NEATNode)parent1.nodes[i];
                }

                NEATNode neuron2 = null;
                if (j < parent2.nodes.Count)
                {
                    neuron2 = (NEATNode)parent2.nodes[j];
                }

                if (neuron1 != null && neuron2 != null)
                {
                    if (neuron1.ID < neuron2.ID)
                    {
                        neuron2 = null;
                    }
                    else if (neuron1.ID > neuron2.ID)
                    {
                        neuron1 = null;
                    }
                }

                

                if (neuron1 != null && neuron2 != null)
                {
                    rnd = UnityEngine.Random.Range(0, 2);
                    NEATNode[] nodes = { neuron1, neuron2 };
                    offspring1.nodes.Add(nodes[rnd].Clone());
                    offspring2.nodes.Add(nodes[1 - rnd].Clone());
                    i++;
                    j++;
                }
                else if (neuron1 != null && neuron2 == null)
                {
                    offspring1.nodes.Add(neuron1.Clone());
                    offspring2.nodes.Add(neuron1.Clone());
                    i++;
                }
                else if (neuron1 == null && neuron2 != null)
                {
                    offspring1.nodes.Add(neuron2.Clone());
                    offspring2.nodes.Add(neuron2.Clone());
                    j++;
                }
                offspring1.nodeID_to_idx[offspring1.nodes[^1].ID] = offspring1.nodes.Count-1;
                offspring2.nodeID_to_idx[offspring2.nodes[^1].ID] = offspring2.nodes.Count-1;

                if(offspring1.nodes[^1].type == Brain.Neuron.NeuronClass.Hidden) validate_node_to_connections_offspring1[offspring1.nodes[^1].ID] = 0;
                if (offspring2.nodes[^1].type == Brain.Neuron.NeuronClass.Hidden) validate_node_to_connections_offspring2[offspring2.nodes[^1].ID] = 0;

            }

            i = 0;
            j = 0;
            while (i < parent1.connections.Count || j < parent2.connections.Count)
            {
                NEATConnection connection1 = null;
                if (i < parent1.connections.Count)
                {
                    connection1 = (NEATConnection)parent1.connections[i];
                }

                NEATConnection connection2 = null;
                if (j < parent2.connections.Count)
                {
                    connection2 = (NEATConnection)parent2.connections[j];
                }

                if (connection1 != null && connection2 != null)
                {
                    if (connection1.ID < connection2.ID)
                    {
                        connection2 = null;
                    }
                    else if (connection1.ID > connection2.ID)
                    {
                        connection1 = null;
                    }
                }



                if (connection1 != null && connection2 != null)
                {
                    rnd = UnityEngine.Random.Range(0, 2);
                    NEATConnection[] connections = new NEATConnection[] { connection1, connection2 };
                    offspring1.connections.Add(connections[rnd].Clone());
                    offspring2.connections.Add(connections[1 - rnd].Clone());
                    i++;
                    j++;
                }
                else if (connection1 != null && connection2 == null)
                {
                    offspring1.connections.Add(connection1.Clone());
                    offspring2.connections.Add(connection1.Clone());
                    i++;
                }
                else if (connection1 == null && connection2 != null)
                {
                    offspring1.connections.Add(connection2.Clone());
                    offspring2.connections.Add(connection2.Clone());
                    j++;
                }

                //re-enable connections
                if ((connection1 != null && !connection1.enabled) || (connection2 != null && !connection2.enabled))
                {
                    offspring1.connections[^1].enabled = UnityEngine.Random.Range(0f, 1.0f) < 0.75f ? false : true;
                    offspring2.connections[^1].enabled = UnityEngine.Random.Range(0f, 1.0f) < 0.75f ? false : true;
                }
               
                if (validate_node_to_connections_offspring1.ContainsKey(offspring1.connections[^1].toID)) validate_node_to_connections_offspring1[offspring1.connections[^1].toID]++;
                if (validate_node_to_connections_offspring2.ContainsKey(offspring2.connections[^1].toID)) validate_node_to_connections_offspring2[offspring2.connections[^1].toID]++;

                if (offspring1.connections[^1].enabled) offspring1.enabled_connections.Add(offspring1.connections[^1]);
                if (offspring2.connections[^1].enabled) offspring2.enabled_connections.Add(offspring2.connections[^1]);
            }

            foreach(KeyValuePair<int,int> pair in validate_node_to_connections_offspring1)
            {
                if(pair.Value == 0)
                {
                    Debug.LogError("ERROR no incoming connections to node ID: " + pair.Key);
                }
            }

            foreach (KeyValuePair<int, int> pair in validate_node_to_connections_offspring2)
            {
                if (pair.Value == 0)
                {
                    Debug.LogError("ERROR no incoming connections to node ID: " + pair.Key);
                }
            }

            return (offspring1, offspring2);
        }

        internal NEATGenome Clone()
        {
            NEATGenome cloned_genome = new();
            int i = 0;
            foreach (NEATNode n in this.nodes)
            {
                cloned_genome.nodes.Add(n.Clone());
                cloned_genome.nodeID_to_idx[n.ID] = i;
                i++;
            }
            foreach (NEATConnection c in this.connections)
            {
                cloned_genome.connections.Add(c.Clone());
            }
            return cloned_genome;
        }

        public class NEATNode
        {
            public int ID;
            public float bias;
            public float3 coords;
            public Brain.Neuron.NeuronClass type;

            public NEATNode(float3 coords, Brain.Neuron.NeuronClass type, int ID = -1)
            {
                if (ID == -1) ID = NEXT_GLOBAL_NODE_ID++;
                this.ID = ID;
                this.coords = coords;
                this.type = type;
                this.bias = 0;
            }

            public NEATNode Clone()
            {
                NEATNode cloned_node = new(this.coords, this.type, this.ID);
                cloned_node.bias = this.bias;
                return cloned_node;
            }
        }

        public class NEATConnection
        {
            public int ID;
            public int fromID;
            public int toID;
            public float weight;
            public bool enabled;

            public NEATConnection(float weight,int fromID, int toID, int ID= -1)
            {
                if (ID == -1) ID = NEXT_GLOBAL_CONNECTION_ID++;
                this.ID = ID;
                this.fromID = fromID;
                this.toID = toID;
                this.weight = weight;
                this.enabled = true;
            }

            public NEATConnection Clone()
            {
                NEATConnection cloned_connection = new(this.weight, this.fromID, this.toID, this.ID);
                cloned_connection.enabled = this.enabled;
                return cloned_connection;
            }
        }

    }
}
