using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using static AnimatBody;
using static GlobalConfig;

/*
 *  Genome based on Karl Sims' work
 */
public class BodyGenome
{
    // CONFIG
    public const float NODE_MUTATION_RATE = 0.4f;
    public const float CONNECTION_MUTATION_RATE = 0.2f;
    public const float MUTATION_DELTA_DIMENSION = 0.2f;
    public const float MUTATION_DELTA_ROTATION = 10.0f;
    public const float MUTATION_DELTA_OFFSET = 0.2f;
    public const float MUTATION_DELTA_SCALE = 1.0f;

    // object members
    public MorphologyNode[] node_array;

    public enum CellularInstruction
    {
        // Division symbols
        [Description("DIV")] DIVIDE, // The cell divides, placing a new cell in the given direction. The cells differentiate, and will traverse different subtrees
        [Description("CLONE")] CLONE, // The cell divides, placing a new cell in the given direction. The cells will traverse the same subtree

        [Description("WAIT")] WAIT, // Does nothing for the step
        [Description("END")] END
    }


    // types of crossover 
    public enum CrossoverType
    {
        OnePoint,
        TwoPoint,
        Uniform
    };



    static Dictionary<string, (Vector3, Vector3)> segment_face_positions_and_rotations = new Dictionary<string, (Vector3, Vector3)>
    {
        { "+x" , (new Vector3(0.5f, 0.5f, 0), new Vector3(0, 0, -90)) } ,
        {"-x" , (new Vector3(-0.5f, 0.5f, 0), new Vector3(180, 0, 90))} ,
        {"+y" , (new Vector3(0, 1.0f, 0), new Vector3(0, 0, 0))} ,
        {"-y" , (new Vector3(0, 0, 0), new Vector3(0, 0, 180))} ,
        {"+z" , (new Vector3(0, 0.5f, 0.5f), new Vector3(90, 0, 0))} ,
        {"-z" , (new Vector3(0, 0.5f, -0.5f), new Vector3(-90, 0, 0)) }
    };




    /// <summary>
    /// Constructor
    /// </summary>
    public BodyGenome(Creature? creature)
    {
        if (creature != null)
        {

            MorphologyNode[] nodes;
            if (creature == Creature.Tree)
            {
                nodes = CreateTreeGenome();

            }
            else if (creature == Creature.Bug)
            {
                nodes = CreateBugGenome();

            }
            else if (creature == Creature.Human)
            {
                nodes = CreateHumanGenome();
            }
            else if (creature == Creature.Asparagus)
            {
                nodes = CreateAsparagusGenome();
            }
            else if (creature == Creature.Spider)
            {
                nodes = CreateSpiderGenome();
            }
            else if (creature == Creature.Hexapod)
            {
                nodes = null; //CreateHexapodGenome();
            }
            else
            {
                return;
            }

            this.node_array = nodes;
        }

    }

    /// <summary>
    /// Constructor
    /// </summary>
    public BodyGenome(MorphologyNode[] nodes)
    {
        this.node_array = nodes;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="genome1"></param>
    /// <param name="genome2"></param>
    /// <param name="crossover"></param>
    /// <returns>2 offspring genomes produced by crossover</returns>
    public static (BodyGenome, BodyGenome) MateGenomes(BodyGenome genome1, BodyGenome genome2, CrossoverType crossover = CrossoverType.OnePoint)
    {
        if (crossover == CrossoverType.OnePoint)
        {
            int new_genome_length = (genome1.node_array.Length < genome2.node_array.Length) ?
             genome1.node_array.Length : genome2.node_array.Length; // use shorter genome


            MorphologyNode[] offspring1 = new MorphologyNode[new_genome_length];
            MorphologyNode[] offspring2 = new MorphologyNode[new_genome_length];
            int crossover_idx = UnityEngine.Random.Range(0, new_genome_length);

            for (int i = 0; i < new_genome_length; i++)
            {
                if (i < crossover_idx || i > crossover_idx)
                {
                    offspring1[i] = new MorphologyNode(genome1.node_array[i]);
                    offspring2[i] = new MorphologyNode(genome2.node_array[i]);
                }
                else
                {
                    offspring1[i] = new MorphologyNode(genome2.node_array[i]);
                    offspring2[i] = new MorphologyNode(genome1.node_array[i]);
                }

                MorphologyConnection connection;
                for (int j = 0; j < offspring1[i].connections.Count; j++)
                {
                    connection = offspring1[i].connections[j];
                    /*                   
                     *                   TODO FIX THIS
                     *                   
                     *                   if(connection.to_node >= new_genome_length)
                                        {
                                            // node points out of bounds, randomly re-assign
                                            int rnd_idx = UnityEngine.Random.Range(0, new_genome_length);
                                            connection.to_node = rnd_idx;
                                        }*/
                    offspring1[i].connections[j] = connection;
                }

            }

            return (new BodyGenome(offspring1), new BodyGenome(offspring2));
        }
        else if (crossover == CrossoverType.TwoPoint)
        {
            Debug.LogError("Two-point crossover is not yet supported.");
            return (null, null);
        }
        else if (crossover == CrossoverType.Uniform)
        {
            Debug.LogError("Uniform crossover is not yet supported.");
            return (null, null);
        }
        else
        {
            Debug.LogError("Invalid crossover type.");
            return (null, null);
        }
    }

    /// <summary>
    ///     Mutate the provided genome.
    /// </summary>
    /// <param name="genome"></param>
    public static void MutateGenome(BodyGenome genome)
    {
        float rand, rand2;
        int randint, randint2;
        for (int i = 0; i < genome.node_array.Length; i++)
        {
            MorphologyNode node = genome.node_array[i];
            rand = UnityEngine.Random.Range(0.0f, 1.0f);

            if (rand < NODE_MUTATION_RATE)
            {
                randint = UnityEngine.Random.Range(0, 3); // 3 parameter types
                switch (randint)
                {
                    case 0: // recursive limit
                        node.recursive_limit += (UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1);
                        if (node.recursive_limit < 1)
                        {
                            node.recursive_limit = 1;
                        }
                        break;
                    case 1: // dimensions
                        randint2 = UnityEngine.Random.Range(0, 3); // 3 dimensions
                        switch (randint2)
                        {
                            case 0: // x
                                node.dimensions.x += UnityEngine.Random.Range(-1.0f, 1.0f) * MUTATION_DELTA_DIMENSION;
                                break;
                            case 1: // y
                                node.dimensions.y += UnityEngine.Random.Range(-1.0f, 1.0f) * MUTATION_DELTA_DIMENSION;
                                break;
                            case 2: // z
                                node.dimensions.z += UnityEngine.Random.Range(-1.0f, 1.0f) * MUTATION_DELTA_DIMENSION;
                                break;
                        }
                        break;
                    case 2: // body segment shape
                        randint2 = UnityEngine.Random.Range(0, Enum.GetNames(typeof(BodySegmentType)).Length);
                        node.body_type = (BodySegmentType)randint2;
                        break;
                }
            }

            for (int j = 0; j < node.connections.Count; j++)
            {

                rand = UnityEngine.Random.Range(0.0f, 1.0f);

                if (rand < CONNECTION_MUTATION_RATE)
                {
                    MorphologyConnection connection = node.connections[j];

                    randint = UnityEngine.Random.Range(0, 4); // parameter type
                    if (randint == 0 || randint == 1 || randint == 2)
                    {
                        //vector3 type
                        randint2 = UnityEngine.Random.Range(0, 3); // x, y, or z
                        switch (randint2)
                        {
                            case 0: // reflection (vector3)
                                connection.reflection[randint2] *= -1;
                                break;
                            case 1: // rotation (vector3)
                                connection.rotation[randint2] += UnityEngine.Random.Range(-1.0f, 1.0f) * MUTATION_DELTA_ROTATION;
                                connection.rotation[randint2] = (connection.rotation[randint2] < -180) ? -180 : connection.rotation[randint2];
                                connection.rotation[randint2] = (connection.rotation[randint2] > 180) ? 180 : connection.rotation[randint2];
                                break;
                            case 2: // offset (vector3)
                                connection.position_offset[randint2] += UnityEngine.Random.Range(-1.0f, 1.0f) * MUTATION_DELTA_OFFSET;
                                if (randint2 == 1)
                                {
                                    // y value clamp
                                    connection.position_offset[randint2] = (connection.position_offset[randint2] < 0f) ? 0f : connection.position_offset[randint2];
                                    connection.position_offset[randint2] = (connection.position_offset[randint2] > 1.0f) ? 1.0f : connection.position_offset[randint2];
                                }
                                else
                                {
                                    //x,z value clamp
                                    connection.position_offset[randint2] = (connection.position_offset[randint2] < -0.5f) ? -0.5f : connection.position_offset[randint2];
                                    connection.position_offset[randint2] = (connection.position_offset[randint2] > 0.5f) ? 0.5f : connection.position_offset[randint2];
                                }
                                break;
                        }

                    }
                    else
                    {
                        switch (randint)
                        {
                            case 3: // scale
                                connection.scale += UnityEngine.Random.Range(-1.0f, 1.0f) * MUTATION_DELTA_SCALE;
                                break;
                        }
                    }

                    node.connections[j] = connection;
                }


            }
        }
    }


    /// <summary>
    /// Genome that looks like a branching tree
    /// </summary>
    /// <returns></returns>
    public static MorphologyNode[] CreateAsparagusGenome()
    {
        List<MorphologyNode> nodes = new();


        (Vector3 topFacePosition, Vector3 topFaceRotation) = segment_face_positions_and_rotations["+y"];

        Vector3 dimensions = new Vector3(0.5f, 2, 0.5f);


        // make tree trunk

        MorphologyNode node = new(dimensions: dimensions,
             recursive_limit: 11);

        topFacePosition.x *= dimensions.x;
        topFacePosition.y *= dimensions.y;
        topFacePosition.z *= dimensions.z;

        nodes.Add(node);


        // tilt top right branch
        topFaceRotation.z = -20;
        topFacePosition.x = 0.5f * dimensions.x;

        MorphologyConnection connection =
            new(to_node: node,
            terminal_only: false,
            position_offset: topFacePosition,
            rotation: topFaceRotation,
            scale: 0.6f);

        node.connections.Add(connection);

        // tilt top left branch
        topFacePosition.x *= -1;
        connection =
            new(to_node: node,
            terminal_only: false,
            position_offset: topFacePosition,
            rotation: topFaceRotation,
            scale: 0.6f);
        node.connections.Add(connection);

        return nodes.ToArray();
    }

    /// <summary>
    /// Genome that looks like a branching tree
    /// </summary>
    /// <returns></returns>
    public static MorphologyNode[] CreateTreeGenome()
    {
        List<MorphologyNode> nodes = new();

        (Vector3 topFacePosition, Vector3 topFaceRotation) = segment_face_positions_and_rotations["+y"];

        Vector3 dimensions = new Vector3(0.5f, 2, 0.5f);


        // make tree trunk

        MorphologyNode root_node = new MorphologyNode(dimensions: dimensions,
             recursive_limit: 11);

        topFacePosition.x *= dimensions.x;
        topFacePosition.y *= dimensions.y;
        topFacePosition.z *= dimensions.z;

        nodes.Add(root_node);

        // tilt top right branch
        topFaceRotation.z = -20;
        topFacePosition.x = 0.5f * dimensions.x;

        MorphologyConnection connection =
            new(to_node: root_node,
            terminal_only: false,
            position_offset: topFacePosition,
            rotation: topFaceRotation,
            scale: 0.6f);

        root_node.connections.Add(connection);

        // tilt top left branch
        connection =
            new(to_node: root_node,
            terminal_only: false,
            position_offset: topFacePosition,
            rotation: topFaceRotation,
            scale: 0.6f,
            reflection: new Vector3Int(1, -1, 1));
        root_node.connections.Add(connection);

        return nodes.ToArray();
    }

    /// <summary>
    /// Genome that looks like a bug
    /// </summary>
    /// <returns></returns>
    public static MorphologyNode[] CreateBugGenome()
    {
        List<MorphologyNode> nodes = new();

        // make body node
        int rec_limit = GlobalConfig.creature_to_use == Creature.Hexapod ? 3 : 2; // hexapod or quadruped


        (Vector3 top_face_position, Vector3 top_face_rotation) = segment_face_positions_and_rotations["+y"];
        Vector3 body_dimensions = new Vector3(1.0f, 2.0f, 0.75f);
        MorphologyNode body_node = new MorphologyNode(dimensions: body_dimensions,
             recursive_limit: rec_limit);

        Vector3 body_recursive_face_position = Vector3.Scale(top_face_position, body_dimensions);
        Vector3 body_rotation = top_face_rotation;
        body_rotation.x += 10f;

        MorphologyConnection body_recursive_connection =
            new(to_node: body_node,
            terminal_only: false,
            position_offset: body_recursive_face_position,
            rotation: body_rotation,
            joint_type: ArticulationJointType.SphericalJoint,
            scale: 1);

        body_node.name = "body node";
        nodes.Add(body_node);

        // make limb node
        Vector3 leg_dimensions = new Vector3(0.5f, 0.75f, 0.5f);

        MorphologyNode leg_node = new MorphologyNode(dimensions: leg_dimensions,
            recursive_limit: 2);

        leg_node.name = "leg node";
        nodes.Add(leg_node);

        // make foot node
        Vector3 foot_dimensions = new Vector3(0.75f, 0.3f, 0.5f);

        MorphologyNode foot_node = new MorphologyNode(dimensions: foot_dimensions);

        foot_node.name = "foot node";
        nodes.Add(foot_node);

        //make recursive leg connections
        Vector3 leg_recursive_face_position = Vector3.Scale(top_face_position, leg_dimensions);
        Vector3 leg_recursive_rotation = top_face_rotation;
        leg_recursive_rotation.x += 35f;

        MorphologyConnection leg_recursive_connection =
            new(to_node: leg_node,
            terminal_only: false,
            position_offset: leg_recursive_face_position,
            rotation: leg_recursive_rotation,
            scale: 1,
            joint_type: ArticulationJointType.SphericalJoint);

        leg_node.connections.Add(leg_recursive_connection);

        //make foot connection
        Vector3 foot_face_position = Vector3.Scale(top_face_position, leg_dimensions);
        // move foot forward foot_face_position.x = -0.4f;
        Vector3 foot_rotation = top_face_rotation;
        //foot_rotation.x += 20f;

        MorphologyConnection foot_connection =
            new(to_node: foot_node,
            terminal_only: true,
            position_offset: foot_face_position,
            rotation: foot_rotation,
            scale: 1,
            joint_type: ArticulationJointType.SphericalJoint);

        leg_node.connections.Add(foot_connection);

        // make left leg connection
        (Vector3 left_face_position, Vector3 left_face_rotation) = segment_face_positions_and_rotations["-x"];
        Vector3 left_leg_rotation = left_face_rotation;
        left_leg_rotation.z -= 35f;
        left_leg_rotation.x += 180;

        Vector3 left_leg_body_face_position = Vector3.Scale(left_face_position, body_dimensions);

        MorphologyConnection left_leg_connection =
            new(to_node: leg_node,
            terminal_only: false,
            position_offset: left_leg_body_face_position,
            rotation: left_leg_rotation,
            scale: 1,
            joint_type: ArticulationJointType.SphericalJoint);

        body_node.connections.Add(left_leg_connection);

        // make right leg connection
        (Vector3 right_face_position, Vector3 right_face_rotation) = segment_face_positions_and_rotations["+x"];
        Vector3 right_leg_body_face_position = Vector3.Scale(right_face_position, body_dimensions);
        Vector3 right_leg_rotation = right_face_rotation;
        right_leg_rotation.z += 35f;

        MorphologyConnection right_leg_connection =
            new(to_node: leg_node,
            terminal_only: false,
            position_offset: right_leg_body_face_position,
            rotation: right_leg_rotation,
            scale: 1,
            joint_type: ArticulationJointType.SphericalJoint);

        body_node.connections.Add(right_leg_connection);

        body_node.connections.Add(body_recursive_connection);

        return nodes.ToArray();
    }

    /// <summary>
    /// Genome that looks like a humanoid
    /// </summary>
    /// <returns></returns>
    public static MorphologyNode[] CreateHumanGenome()
    {
        List<MorphologyNode> nodes = new();

        // make body node
        (Vector3 top_face_position, Vector3 top_face_rotation) = segment_face_positions_and_rotations["+y"];
        Vector3 body_dimensions = new Vector3(1, 2, 1);
        MorphologyNode body_node = new(dimensions: body_dimensions);

        body_node.name = "body node";
        nodes.Add(body_node);

        // make limb node
        Vector3 limb_dimensions = new Vector3(0.5f, 1, 0.5f);

        MorphologyNode limb_node = new MorphologyNode(dimensions: limb_dimensions,
            recursive_limit: 2);

        limb_node.name = "limb node";
        nodes.Add(limb_node);

        // make head node
        Vector3 head_dimensions = new Vector3(0.75f, 0.8f, 0.75f);

        MorphologyNode head_node = new MorphologyNode(dimensions: head_dimensions, recursive_limit: 1);

        head_node.name = "head node";
        nodes.Add(head_node);

        //make recursive limb connections
        Vector3 limb_recursive_face_position = Vector3.Scale(top_face_position, limb_dimensions);
        Vector3 limb_recursive_rotation = top_face_rotation;
        limb_recursive_rotation.z += 20f;

        MorphologyConnection limb_recursive_connection =
            new(to_node: limb_node,
            terminal_only: false,
            position_offset: limb_recursive_face_position,
            rotation: limb_recursive_rotation,
            scale: 1);

        limb_node.connections.Add(limb_recursive_connection);

        // make left leg connection
        (Vector3 bottom_face_position, Vector3 bottom_face_rotation) = segment_face_positions_and_rotations["-y"];

        Vector3 unscaled_left_leg_body_face_position = bottom_face_position;
        unscaled_left_leg_body_face_position.x = -0.5f;
        Vector3 left_leg_body_face_position = Vector3.Scale(unscaled_left_leg_body_face_position, body_dimensions);
        Vector3 left_leg_rotation = bottom_face_rotation;
        left_leg_rotation.z -= 20;

        MorphologyConnection left_leg_connection =
            new(to_node: limb_node,
            terminal_only: false,
            position_offset: left_leg_body_face_position,
            rotation: left_leg_rotation,
            scale: 1);

        body_node.connections.Add(left_leg_connection);

        // make right leg connection
        MorphologyConnection right_leg_connection =
            new(to_node: limb_node,
            terminal_only: false,
            position_offset: left_leg_body_face_position,
            rotation: left_leg_rotation,
            scale: 1,
            reflection: new Vector3Int(1, -1, 1));

        body_node.connections.Add(right_leg_connection);

        // make left arm connection
        (Vector3 left_face_position, Vector3 left_face_rotation) = segment_face_positions_and_rotations["-x"];

        Vector3 left_arm_body_face_position = left_face_position;
        left_arm_body_face_position.y = 1.0f;
        left_arm_body_face_position = Vector3.Scale(left_arm_body_face_position, body_dimensions);

        Vector3 left_arm_rotation = left_face_rotation;
        left_arm_rotation.z += 40f;

        MorphologyConnection left_arm_connection =
            new(to_node: limb_node,
            terminal_only: false,
            position_offset: left_arm_body_face_position,
            rotation: left_arm_rotation,
            scale: 1);

        body_node.connections.Add(left_arm_connection);

        // make right arm connection
        (Vector3 right_face_position, Vector3 right_face_rotation) = segment_face_positions_and_rotations["+x"];

        Vector3 right_arm_body_face_position = right_face_position;
        right_arm_body_face_position.y = 1.0f;
        right_arm_body_face_position = Vector3.Scale(right_arm_body_face_position, body_dimensions);

        Vector3 right_arm_rotation = right_face_rotation;
        right_arm_rotation.z += 40f;

        right_arm_rotation.x += 180;

        MorphologyConnection right_arm_connection =
            new(to_node: limb_node,
            terminal_only: false,
            position_offset: right_arm_body_face_position,
            rotation: right_arm_rotation,
            scale: 1);


        body_node.connections.Add(right_arm_connection);

        // make head connection
        Vector3 head_face_position = Vector3.Scale(top_face_position, body_dimensions);
        Vector3 head_rotation = top_face_rotation;

        MorphologyConnection head_connection =
            new(to_node: head_node,
            terminal_only: false,
            position_offset: head_face_position,
            rotation: head_rotation,
            scale: 1);

        body_node.connections.Add(head_connection);


        return nodes.ToArray();
    }

    public static MorphologyNode[] CreateSpiderGenome()
    {
        List<MorphologyNode> nodes = new();

        (Vector3 top_face_position, Vector3 top_face_rotation) = segment_face_positions_and_rotations["+y"];

        // make head node
        (Vector3 front_face_position, Vector3 front_face_rotation) = segment_face_positions_and_rotations["+z"];
        Vector3 body_dimensions = new Vector3(2f, 1, 3);
        MorphologyNode body_node0 = new(dimensions: body_dimensions);

        body_node0.name = "body node";
        nodes.Add(body_node0);

        // make abdomen node
        (Vector3 back_face_position, Vector3 back_face_rotation) = segment_face_positions_and_rotations["-z"];
        Vector3 abdomen_dimensions = new Vector3(3, 3, 1);
        MorphologyNode abdomen_node = new(dimensions: abdomen_dimensions);
        abdomen_node.name = "abdomen node";
        nodes.Add(abdomen_node);

        // make fang node
        /*    Vector3 fang_dimensions = new Vector3(0.2f, 0.75f, 0.2f) * scale;
            MorphologyNode fang_node2 = new(dimensions: fang_dimensions);
            fang_node2.name = "fang node";
            nodes.Add(fang_node2);*/

        // make limb node
        Vector3 limb_dimensions = new Vector3(0.5f, 3, 0.5f);

        MorphologyNode leg_node = new(dimensions: limb_dimensions,
            recursive_limit: 2);

        leg_node.name = "leg node";
        nodes.Add(leg_node);

        //make head-ab connection

        Vector3 ab_face_position = Vector3.Scale(back_face_position, body_dimensions);
        Vector3 ab_rotation = back_face_rotation;
        MorphologyConnection body_head_connection =
            new(to_node: abdomen_node,
            terminal_only: false,
            position_offset: ab_face_position,
            rotation: ab_rotation,
            scale: 1);



        body_node0.connections.Add(body_head_connection);

        //make head-fang connections (left and right fang)

        /*        Vector3 fang_face_position = front_face_position;
                fang_face_position.x -= 0.33f;

                fang_face_position = Vector3.Scale(fang_face_position, body_dimensions);
                Vector3 fang_rotation = front_face_rotation;
                MorphologyConnection fang_head_connection =
                    new (to_node: 2,
                    terminal_only: false,
                    position: fang_face_position,
                    rotation: fang_rotation,
                    scale: 1);

                body_node0.connections.Add(fang_head_connection);

                fang_face_position = front_face_position;
                fang_face_position.x += 0.33f;

                fang_face_position = Vector3.Scale(fang_face_position, body_dimensions);
                fang_rotation = front_face_rotation;
                fang_head_connection =
                    new (to_node: 2,
                    terminal_only: false,
                    position: fang_face_position,
                    rotation: fang_rotation,
                    scale: 1);

                body_node0.connections.Add(fang_head_connection);
        */



        //make recursive leg connections

        Vector3 leg_recursive_face_position = Vector3.Scale(top_face_position, limb_dimensions);
        Vector3 leg_recursive_rotation = top_face_rotation;
        leg_recursive_rotation.x += 35f;

        MorphologyConnection leg_recursive_connection =
            new(to_node: leg_node,
            terminal_only: false,
            position_offset: leg_recursive_face_position,
            rotation: leg_recursive_rotation,
            scale: 1);

        leg_node.connections.Add(leg_recursive_connection);

        // make left leg connection
        (Vector3 left_face_position, Vector3 left_face_rotation) = segment_face_positions_and_rotations["-x"];

        Vector3 left_leg_body_face_position = Vector3.Scale(left_face_position, body_dimensions);

        for (float i = 0.5f; i >= -0.5f; i -= 0.33f)
        {
            Vector3 position = left_leg_body_face_position;
            position.z = i * body_dimensions.z;
            Vector3 rotation = left_face_rotation;
            rotation.x += (i) * -20;
            MorphologyConnection left_leg_connection =
                new(to_node: leg_node,
                terminal_only: false,
                position_offset: position,
                rotation: rotation,
                scale: 1);

            body_node0.connections.Add(left_leg_connection);
        }


        // make right leg connection
        (Vector3 right_face_position, Vector3 right_face_rotation) = segment_face_positions_and_rotations["+x"];
        Vector3 right_leg_body_face_position = Vector3.Scale(right_face_position, body_dimensions);


        for (float i = 0.5f; i >= -0.5f; i -= 0.33f)
        {

            Vector3 position = right_leg_body_face_position;
            position.z = i * body_dimensions.z;
            Vector3 rotation = right_face_rotation;
            rotation.x += (i) * 20;
            MorphologyConnection right_leg_connection =
                new(to_node: leg_node,
                terminal_only: false,
                position_offset: position,
                rotation: rotation,
                scale: 1);

            body_node0.connections.Add(right_leg_connection);
        }

        return nodes.ToArray();
    }


    public static MorphologyNode[] CreateHexapodGenome()
    {
        List<MorphologyNode> nodes = new();

        // make body node
        (Vector3 top_face_position, Vector3 top_face_rotation) = segment_face_positions_and_rotations["+y"];
        Vector3 body_dimensions = new Vector3(0.25f, 2, 0.5f);
        MorphologyNode body_node = new MorphologyNode(dimensions: body_dimensions,
             recursive_limit: 3);

        Vector3 body_recursive_face_position = Vector3.Scale(top_face_position, body_dimensions);
        Vector3 body_rotation = top_face_rotation;
        body_rotation.x += 10f;

        MorphologyConnection body_recursive_connection =
            new(to_node: body_node,
            terminal_only: false,
            position_offset: body_recursive_face_position,
            rotation: body_rotation,
            joint_type: ArticulationJointType.RevoluteJoint,
            scale: 1);

        body_node.connections.Add(body_recursive_connection);

        body_node.name = "body node";
        nodes.Add(body_node);

        // make limb node
        Vector3 leg_dimensions = new Vector3(0.5f, 1, 0.5f);

        MorphologyNode leg_node = new MorphologyNode(dimensions: leg_dimensions,
            recursive_limit: 2);

        leg_node.name = "leg node";
        nodes.Add(leg_node);

        // make foot node
        Vector3 foot_dimensions = new Vector3(1.0f, 0.25f, 0.5f);

        MorphologyNode foot_node = new MorphologyNode(dimensions: foot_dimensions);

        foot_node.name = "foot node";
        nodes.Add(foot_node);

        //make recursive leg connections
        Vector3 leg_recursive_face_position = Vector3.Scale(top_face_position, leg_dimensions);
        Vector3 leg_recursive_rotation = top_face_rotation;
        leg_recursive_rotation.x += 35f;

        MorphologyConnection leg_recursive_connection =
            new(to_node: leg_node,
            terminal_only: false,
            position_offset: leg_recursive_face_position,
            rotation: leg_recursive_rotation,
            scale: 1,
            joint_type: ArticulationJointType.SphericalJoint);

        leg_node.connections.Add(leg_recursive_connection);

        //make foot connection
        Vector3 foot_face_position = Vector3.Scale(top_face_position, leg_dimensions);
        Vector3 foot_rotation = top_face_rotation;
        //foot_rotation.x += 20f;

        MorphologyConnection foot_connection =
            new(to_node: foot_node,
            terminal_only: true,
            position_offset: foot_face_position,
            rotation: foot_rotation,
            scale: 1,
            joint_type: ArticulationJointType.SphericalJoint);

        leg_node.connections.Add(foot_connection);

        // make left leg connection
        (Vector3 left_face_position, Vector3 left_face_rotation) = segment_face_positions_and_rotations["-x"];

        Vector3 left_leg_body_face_position = Vector3.Scale(left_face_position, body_dimensions);

        MorphologyConnection left_leg_connection =
            new(to_node: leg_node,
            terminal_only: false,
            position_offset: left_leg_body_face_position,
            rotation: left_face_rotation,
            scale: 1,
            joint_type: ArticulationJointType.SphericalJoint);

        body_node.connections.Add(left_leg_connection);

        // make right leg connection
        (Vector3 right_face_position, Vector3 right_face_rotation) = segment_face_positions_and_rotations["+x"];
        Vector3 right_leg_body_face_position = Vector3.Scale(right_face_position, body_dimensions);

        MorphologyConnection right_leg_connection =
            new(to_node: leg_node,
            terminal_only: false,
            position_offset: right_leg_body_face_position,
            rotation: right_face_rotation,
            scale: 1,
            joint_type: ArticulationJointType.SphericalJoint);

        body_node.connections.Add(right_leg_connection);

        return nodes.ToArray();
    }


    /// <summary>
    ///     Initial organism genome for flora
    /// </summary>
    /// <returns></returns>
    public static MorphologyNode[] CreateMinimumFloraGenome()
    {
        List<MorphologyNode> nodes = new();

        (Vector3 topFacePosition, Vector3 topFaceRotation) = segment_face_positions_and_rotations["+y"];

        Vector3 dimensions = new Vector3(0.5f, 2, 0.5f);


        // make tree trunk

        MorphologyNode root_node = new(dimensions: dimensions,
             recursive_limit: 11);
        nodes.Add(root_node);

        topFacePosition.x *= dimensions.x;
        topFacePosition.y *= dimensions.y;
        topFacePosition.z *= dimensions.z;


        // tilt top right branch
        topFaceRotation.z = -20;
        topFacePosition.x = 0.5f * dimensions.x;

        MorphologyConnection connection =
            new(to_node: root_node,
            terminal_only: false,
            position_offset: topFacePosition,
            rotation: topFaceRotation,
            scale: 0.6f);

        root_node.connections.Add(connection);

        // tilt top left branch
        connection =
            new(to_node: root_node,
            terminal_only: false,
            position_offset: topFacePosition,
            rotation: topFaceRotation,
            scale: 0.6f,
            reflection: new Vector3Int(1, -1, 1));
        root_node.connections.Add(connection);

        return nodes.ToArray();
    }


    /// <summary>
    ///     Return the center position on a random face of a standard Unity cube.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public static (Vector3, Vector3) GetRandomCubeFacePositionAndRotations()
    {
        int face = UnityEngine.Random.Range(0, 3); // x=0,y=1,z=2
        int posneg = UnityEngine.Random.Range(0, 2); // negative=0, positive=1
        posneg = posneg == 0 ? -1 : 1; // negative=-1, positive=1

        Vector3 returnPositionValue;
        Vector3 returnRotationValue;

        //middle of x face is y=0.5,z=0
        //middle of y face is x=0, z=0
        //middle of z face is y=0.5,x=0

        if (face == 0)
        {
            //x face (left or right)
            returnPositionValue = posneg == 1 ? new Vector3(0.5f, 0.5f, 0) : new Vector3(-0.5f, 0.5f, 0);
            returnRotationValue = posneg == 1 ? new Vector3(0, 0, 90) : new Vector3(0, 0, -90);
        }
        else if (face == 1)
        {
            //y face (top or bottom)
            returnPositionValue = posneg == 1 ? new Vector3(0, 1.0f, 0) : Vector3.zero;
            returnRotationValue = posneg == 1 ? new Vector3(0, 0, 0) : new Vector3(0, 0, 180);
        }
        else if (face == 2)
        {
            //z face (front or back)
            returnPositionValue = posneg == 1 ? new Vector3(0, 0.5f, 0.5f) : new Vector3(0, 0.5f, -0.5f);
            returnRotationValue = posneg == 1 ? new Vector3(90, 0, 0) : new Vector3(-90, 0, 0);
        }
        else
        {
            Debug.Log("Face Position error.");
            return (Vector3.one, Vector3.one);
        }

        return (returnPositionValue, returnRotationValue);
    }



    /// <summary>
    ///     CLASSES, STRUCTS, ENUMS, CONSTANTS
    /// </summary>

    public class MorphologyNode
    {
        public string name = "";

        public Vector3 dimensions;
        public BodySegmentType body_type;
        public int recursive_limit;
        public List<MorphologyConnection> connections;

        public MorphologyNode(Vector3 dimensions,
            int recursive_limit = 1)
        {
            this.dimensions = dimensions;
            this.recursive_limit = recursive_limit;
            this.connections = new();
        }

        public MorphologyNode(MorphologyNode node_to_clone)
        {
            this.dimensions = node_to_clone.dimensions;
            this.recursive_limit = node_to_clone.recursive_limit;
            this.connections = new(node_to_clone.connections);
        }

    }

    // struct so it is treated as a value type
    public struct MorphologyConnection
    {
        public MorphologyNode to_node;
        public bool terminal_only; // only follow this connection when recursive_limit hits zero. Allows for tails, hands, etc. to appear at the end of a chain of repeating units
        public Vector3 position_offset;
        public Vector3 rotation; // in degrees
        public float scale;
        public Vector3Int reflection;
        public ArticulationJointType joint_type;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="to_node"></param>
        /// <param name="terminal_only"></param>
        /// <param name="position_offset"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <param name="reflection">Set axes to reflect over as -1, axes to keep as 1</param>
        public MorphologyConnection(MorphologyNode to_node,
            bool terminal_only,
            Vector3 position_offset,
            Vector3 rotation,
            float scale,
            Vector3Int? reflection = null,
            ArticulationJointType joint_type = ArticulationJointType.RevoluteJoint)
        {
            this.to_node = to_node;
            this.terminal_only = terminal_only;
            this.position_offset = position_offset;
            this.joint_type = joint_type;
            if (reflection == null) reflection = new Vector3Int(1, 1, 1);

            this.position_offset = Vector3.Scale(position_offset, (Vector3Int)reflection);
            this.rotation = rotation;

            this.reflection = (Vector3Int)reflection;
            if (this.reflection != null)
            {

                if (this.reflection.x == -1)
                {
                    this.rotation.x += 180;
                }
                if (this.reflection.y == -1)
                {
                    this.rotation.y += 180;
                }
                if (this.reflection.z == -1)
                {
                    this.rotation.z += 180;
                }
            }
            this.scale = scale;

        }

    }

}
