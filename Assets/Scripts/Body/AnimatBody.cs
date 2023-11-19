using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BodyGenome;

public class AnimatBody : MonoBehaviour
{
    public enum BodySegmentType
    {
        Cube=0,
        Sphere=1
    }

    // 3d objects
    public GameObject root_gameobject;
    static GameObject[] body_segment_prefabs;

    // materials
    public static Material vertex_color;
    public static Material red;
    public static Material green;

    public const float FORCE_MODE_TORQUE_SCALE = 1f;
    public const float TARGET_MODE_MAX_DEGREES_MOVED_PER_SECOND = 8;

    public const float DRIVE_LIMITS = 45; // angle
    public const float STIFFNESS = 500f;
    public const float MASS = 1.25f;
    public const float DAMPING = 25.75f;
   

    public WaitForSeconds wfs;

    public List<ArticulationBody> segments;

    public void Awake()
    {
        if(AnimatBody.body_segment_prefabs == null)
        {
            AnimatBody.body_segment_prefabs = new GameObject[2];
            AnimatBody.body_segment_prefabs[(int)BodySegmentType.Cube] = (GameObject)Resources.Load("Prefabs/Body/BodySegmentType0");
            AnimatBody.body_segment_prefabs[(int)BodySegmentType.Sphere] = (GameObject)Resources.Load("Prefabs/Body/BodySegmentType1");
            AnimatBody.vertex_color = (Material)Resources.Load("Materials/VertexColor");
            AnimatBody.red = (Material)Resources.Load("Materials/red");
            AnimatBody.green = (Material)Resources.Load("Materials/green");
        }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="joint"></param>
    /// <param name="activation"></param>
    public void SetXDrive(ArticulationBody joint, float activation)
    {
       joint.xDrive = SetDrive(joint.xDrive, activation);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="joint"></param>
    /// <param name="activation"></param>
    public void SetYDrive(ArticulationBody joint, float activation)
    {
        joint.yDrive = SetDrive(joint.yDrive, activation);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="joint"></param>
    /// <param name="activation"></param>
    public void SetZDrive(ArticulationBody joint, float activation)
    {
        joint.zDrive = SetDrive(joint.zDrive, activation);
    }



   
    /// <summary>
    /// 
    /// </summary>
    /// <param name="joint"></param>
    /// <param name="activation"></param>
    public ArticulationDrive SetDrive(ArticulationDrive drive, float activation)
    {
  /*      // period
        float seconds_per_degree = 0.1f;

        float speed = Time.fixedDeltaTime/seconds_per_degree;
        float rotationChange = activation * speed;

        drive.target += rotationChange;

        drive.target = Mathf.Min(drive.target, DRIVE_LIMITS);
        drive.target = Mathf.Max(drive.target, -DRIVE_LIMITS);*/

        drive.target = activation * DRIVE_LIMITS;
        if(drive.target > DRIVE_LIMITS) drive.target = DRIVE_LIMITS;
        if(drive.target < -DRIVE_LIMITS) drive.target = -DRIVE_LIMITS;
        return drive;
    }

    /// <summary>
    /// The genome is instantiated once the script is created
    /// </summary>
    public void CreateGenomeAndInstantiateBody(BodyGenome genome)
    {
        segments = new();
        InstantiateNode(genome.node_array, genome.node_array[0], null);
        foreach(ArticulationBody ab in this.segments)
        {
            ab.enabled = true;
        }
        vertex_color.color = Color.red;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="n"></param>
    /// <param name="parentInfo"></param>
    /// <param name="initial_recursions_remaining">null if not recurring</param>
    /// <returns></returns>
    int number = 0;
    public void InstantiateNode(MorphologyNode[] g, MorphologyNode n, (Transform, MorphologyConnection, float)? parentInfo)
    {
        if (n.recursive_limit <= 0) return;

        

        GameObject nodeGO;
        float scaleFactor;
        nodeGO = Instantiate(AnimatBody.body_segment_prefabs[(int)n.body_type]);
        Transform segment = nodeGO.transform.Find("Segment");
        ArticulationBody ab;
        if (parentInfo == null)
        {
            segment.localScale = n.dimensions * GlobalConfig.ANIMAT_BODY_SCALE;
            scaleFactor = 1;
            nodeGO.transform.parent = this.transform;
            nodeGO.transform.localPosition = Vector3.zero;
            ab = nodeGO.AddComponent<ArticulationBody>();
        }
        else
        {
            (Transform parent_transform, MorphologyConnection parent_connection, float parentScaleFactor) = ((Transform, MorphologyConnection, float))parentInfo;

            scaleFactor = parentScaleFactor * parent_connection.scale;

            nodeGO.transform.parent = parent_transform;

            //translate and rotate transform
            nodeGO.transform.localPosition = parent_connection.position_offset * parentScaleFactor * GlobalConfig.ANIMAT_BODY_SCALE;

            nodeGO.transform.localRotation = Quaternion.identity;



            segment.localScale = n.dimensions * scaleFactor * GlobalConfig.ANIMAT_BODY_SCALE;


            ab = nodeGO.AddComponent<ArticulationBody>();
            ab.enabled = false;
            //nodeGO.SetActive(false);
            ab.jointType = parent_connection.joint_type;
            ab.jointPosition = new ArticulationReducedSpace(nodeGO.transform.position.x, nodeGO.transform.position.y, nodeGO.transform.position.z);
            Vector3 new_rotation = parent_transform.transform.rotation * parent_connection.rotation;
            ab.anchorRotation = Quaternion.identity;// Quaternion.Euler(new_rotation);


            nodeGO.transform.Rotate(parent_connection.rotation, Space.Self);

            ArticulationDrive drive;

            ArticulationDrive SetDriveProperties(ArticulationDrive local_drive)
            {
                local_drive.stiffness = STIFFNESS;
                local_drive.damping = DAMPING;
                local_drive.upperLimit = DRIVE_LIMITS;
                local_drive.lowerLimit = -DRIVE_LIMITS;
                local_drive.driveType = GlobalConfig.USE_FORCE_MODE ? ArticulationDriveType.Force : ArticulationDriveType.Force;
                return local_drive;
            }
        
            if (ab.jointType == ArticulationJointType.RevoluteJoint)
            {
                drive = ab.xDrive;
                drive = SetDriveProperties(drive);
                ab.xDrive = drive;
            }
            else if(ab.jointType == ArticulationJointType.SphericalJoint)
            {
                drive = ab.xDrive;
                drive = SetDriveProperties(drive);
                ab.xDrive = drive;

                drive = ab.yDrive;
                drive = SetDriveProperties(drive);
                ab.yDrive = drive;

                drive = ab.zDrive;
                drive = SetDriveProperties(drive);
                ab.zDrive = drive;
            }

            segments.Add(ab);

        }

        // enforce drive limits
        ab.linearLockX = GlobalConfig.USE_FORCE_MODE ? ArticulationDofLock.LimitedMotion : ArticulationDofLock.FreeMotion;
        ab.linearLockY = GlobalConfig.USE_FORCE_MODE ? ArticulationDofLock.LimitedMotion : ArticulationDofLock.FreeMotion;
        ab.linearLockZ = GlobalConfig.USE_FORCE_MODE ? ArticulationDofLock.LimitedMotion : ArticulationDofLock.FreeMotion;
        ab.swingYLock = GlobalConfig.USE_FORCE_MODE ? ArticulationDofLock.LimitedMotion : ArticulationDofLock.FreeMotion;
        ab.swingZLock = GlobalConfig.USE_FORCE_MODE ? ArticulationDofLock.LimitedMotion : ArticulationDofLock.FreeMotion;
        ab.twistLock = GlobalConfig.USE_FORCE_MODE ? ArticulationDofLock.LimitedMotion : ArticulationDofLock.FreeMotion;

        //ab.angularDamping = 0.5f*STIFFNESS;


        if (n == g[0] && root_gameobject == null)
        {
            this.root_gameobject = nodeGO;
            this.root_gameobject.transform.parent = this.transform;
            this.root_gameobject.transform.localRotation = Quaternion.identity;
        }
        nodeGO.name = n.name + " " + number;
        number++;

        ab.mass = MASS * n.dimensions.x * n.dimensions.y * n.dimensions.z;


        if (n.name == "leg node")
        {
            segment.Find("Cube").GetComponent<Renderer>().material = green;
            

        }
        else if(n.name == "body node")
        {
            segment.Find("Cube").GetComponent<Renderer>().material = red;
    
        }
        else if (n.name == "foot node")
        {
           
        }
        

        nodeGO.GetComponent<BoxCollider>().center = Vector3.Scale(nodeGO.GetComponent<BoxCollider>().center, segment.transform.localScale);
        nodeGO.GetComponent<BoxCollider>().size = Vector3.Scale(nodeGO.GetComponent<BoxCollider>().size, segment.transform.localScale);
        
       

        n.recursive_limit--; // lower the recursive limit in case this node occurs in a cycle
        foreach (MorphologyConnection connection in n.connections)
        {
            MorphologyNode to_node = connection.to_node;
            if (!connection.terminal_only || (connection.terminal_only && n.recursive_limit == 0))
            {
                InstantiateNode(g, to_node, (nodeGO.transform, connection, scaleFactor));
            }
        }
        n.recursive_limit++;

        //yield break;
    }
}
