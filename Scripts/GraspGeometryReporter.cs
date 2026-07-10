using System.Collections.Generic;
using UnityEngine;

public class GraspGeometryReporter : MonoBehaviour
{
    [Header("Hand References")]
    public Transform handRoot;
    public Transform m9Origin;
    public Transform m10YawJoint;
    public Transform m1RightJoint1;
    public Transform m2RightJoint2;
    public Transform m5LeftJoint1;
    public Transform m6LeftJoint2;
    public Transform leftFingerBody;
    public Transform rightFingerBody;
    public Transform graspCenter;
    public Transform leftFingerTip;
    public Transform rightFingerTip;
    public Transform leftContactPoint;
    public Transform rightContactPoint;

    [Header("Object References")]
    public ObjectSpawner objectSpawner;
    public GameObject[] objectPrefabs;
    public LayerMask graspableLayerMask;

    [Header("Experiment Coordinates")]
    public float unityToExperimentScale = 1000f;

    [Header("Display")]
    public bool drawGizmos = true;
    public float gizmoSphereRadius = 0.01f;
    public bool logOnStart = false;

    void Start()
    {
        if (logOnStart)
        {
            ReportAll();
        }
    }

    [ContextMenu("Report All Grasp Geometry")]
    public void ReportAll()
    {
        Debug.Log("========== Grasp Geometry Report ==========");
        ReportHandGeometry();
        ReportObjectPrefabGeometry();
        ReportSpawnedObjectGeometry();
        Debug.Log("===========================================");
    }

    [ContextMenu("Report Hand Geometry")]
    public void ReportHandGeometry()
    {
        Debug.Log("---- Hand Geometry ----");

        ReportTransform("Hand Root", handRoot, handRoot);
        ReportTransform("M9 Origin", m9Origin, handRoot);
        ReportTransform("M10 Yaw Joint", m10YawJoint, handRoot);
        ReportTransform("M1 Right Joint1", m1RightJoint1, handRoot);
        ReportTransform("M2 Right Joint2", m2RightJoint2, handRoot);
        ReportTransform("M5 Left Joint1", m5LeftJoint1, handRoot);
        ReportTransform("M6 Left Joint2", m6LeftJoint2, handRoot);
        ReportTransform("Left Finger Body", leftFingerBody, handRoot);
        ReportTransform("Right Finger Body", rightFingerBody, handRoot);
        ReportTransform("Grasp Center", graspCenter, handRoot);
        ReportTransform("Left Finger Tip", leftFingerTip, handRoot);
        ReportTransform("Right Finger Tip", rightFingerTip, handRoot);
        ReportTransform("Left Contact Point", leftContactPoint, handRoot);
        ReportTransform("Right Contact Point", rightContactPoint, handRoot);

        ReportExperimentTransform("M10 Yaw Joint", m10YawJoint);
        ReportExperimentTransform("M1 Right Joint1", m1RightJoint1);
        ReportExperimentTransform("M2 Right Joint2", m2RightJoint2);
        ReportExperimentTransform("M5 Left Joint1", m5LeftJoint1);
        ReportExperimentTransform("M6 Left Joint2", m6LeftJoint2);
        ReportExperimentTransform("Left Finger Tip", leftFingerTip);
        ReportExperimentTransform("Right Finger Tip", rightFingerTip);

        ReportDistance("Finger tip distance", leftFingerTip, rightFingerTip);
        ReportDistance("Contact point distance", leftContactPoint, rightContactPoint);
        ReportFingerBodyBounds();

        if (leftContactPoint != null && rightContactPoint != null)
        {
            Vector3 center = (leftContactPoint.position + rightContactPoint.position) * 0.5f;
            Vector3 axis = rightContactPoint.position - leftContactPoint.position;

            Debug.Log(
                $"Contact center: world={FormatVector(center)}, " +
                $"localToHand={FormatVector(ToLocal(handRoot, center))}, " +
                $"axis={FormatVector(axis)}, length={axis.magnitude:F4} m"
            );
        }

        ReportColliders("Hand Root Colliders", handRoot);
    }

    private void ReportFingerBodyBounds()
    {
        Bounds leftBounds;
        Bounds rightBounds;
        bool hasLeftBounds = TryGetColliderBounds(leftFingerBody, out leftBounds);
        bool hasRightBounds = TryGetColliderBounds(rightFingerBody, out rightBounds);

        if (hasLeftBounds)
        {
            Debug.Log(
                "Left Finger Body bounds: " +
                $"centerWorld={FormatVector(leftBounds.center)}, " +
                $"centerExp={FormatVector(ToExperimentFromM9(leftBounds.center))}, " +
                $"size={FormatVector(leftBounds.size)}"
            );
        }

        if (hasRightBounds)
        {
            Debug.Log(
                "Right Finger Body bounds: " +
                $"centerWorld={FormatVector(rightBounds.center)}, " +
                $"centerExp={FormatVector(ToExperimentFromM9(rightBounds.center))}, " +
                $"size={FormatVector(rightBounds.size)}"
            );
        }

        if (hasLeftBounds && hasRightBounds)
        {
            Vector3 center = (leftBounds.center + rightBounds.center) * 0.5f;
            Vector3 axis = rightBounds.center - leftBounds.center;

            Debug.Log(
                "Finger body bounds center: " +
                $"world={FormatVector(center)}, " +
                $"exp={FormatVector(ToExperimentFromM9(center))}, " +
                $"axisWorld={FormatVector(axis)}, distance={axis.magnitude:F4} m"
            );
        }
    }

    [ContextMenu("Report Object Prefab Geometry")]
    public void ReportObjectPrefabGeometry()
    {
        Debug.Log("---- Object Prefab Geometry ----");

        if (objectPrefabs == null || objectPrefabs.Length == 0)
        {
            Debug.Log("Object prefabs are not assigned.");
            return;
        }

        for (int i = 0; i < objectPrefabs.Length; i++)
        {
            GameObject prefab = objectPrefabs[i];

            if (prefab == null)
            {
                Debug.Log($"Prefab {i}: null");
                continue;
            }

            ReportGameObjectGeometry($"Prefab {i}: {prefab.name}", prefab);
        }
    }

    [ContextMenu("Report Spawned Object Geometry")]
    public void ReportSpawnedObjectGeometry()
    {
        Debug.Log("---- Spawned Object Geometry ----");

        if (objectSpawner == null)
        {
            Debug.Log("ObjectSpawner is not assigned.");
            return;
        }

        List<GameObject> spawnedObjects = objectSpawner.GetSpawnedObjects();

        if (spawnedObjects == null || spawnedObjects.Count == 0)
        {
            Debug.Log("No spawned objects found.");
            return;
        }

        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            GameObject obj = spawnedObjects[i];

            if (obj == null)
            {
                Debug.Log($"Spawned {i}: null");
                continue;
            }

            ReportGameObjectGeometry($"Spawned {i}: {obj.name}", obj);
        }
    }

    [ContextMenu("Report Graspable Objects In Scene")]
    public void ReportGraspableObjectsInScene()
    {
        Debug.Log("---- Graspable Objects In Scene ----");

        Collider[] colliders = FindObjectsOfType<Collider>();
        HashSet<GameObject> reportedRoots = new HashSet<GameObject>();

        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled || col.isTrigger)
            {
                continue;
            }

            if ((graspableLayerMask.value & (1 << col.gameObject.layer)) == 0)
            {
                continue;
            }

            GameObject root = GetRootObject(col.gameObject);

            if (root != null && reportedRoots.Add(root))
            {
                ReportGameObjectGeometry(root.name, root);
            }
        }
    }

    private void ReportTransform(string label, Transform target, Transform localRoot)
    {
        if (target == null)
        {
            Debug.Log($"{label}: not assigned");
            return;
        }

        Vector3 localPosition = localRoot != null
            ? localRoot.InverseTransformPoint(target.position)
            : target.localPosition;

        Debug.Log(
            $"{label}: world={FormatVector(target.position)}, " +
            $"local={FormatVector(localPosition)}, " +
            $"rotation={FormatVector(target.rotation.eulerAngles)}"
        );
    }

    private void ReportExperimentTransform(string label, Transform target)
    {
        if (target == null)
        {
            Debug.Log($"{label} experiment coordinates: not assigned");
            return;
        }

        Debug.Log(
            $"{label} experiment coordinates from M9: " +
            $"{FormatVector(ToExperimentFromM9(target.position))}"
        );
    }

    private void ReportDistance(string label, Transform a, Transform b)
    {
        if (a == null || b == null)
        {
            Debug.Log($"{label}: not available");
            return;
        }

        Debug.Log($"{label}: {Vector3.Distance(a.position, b.position):F4} m");
    }

    private void ReportColliders(string label, Transform root)
    {
        if (root == null)
        {
            Debug.Log($"{label}: root is not assigned");
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>();

        if (colliders.Length == 0)
        {
            Debug.Log($"{label}: no colliders found");
            return;
        }

        Debug.Log($"{label}: {colliders.Length} collider(s)");

        foreach (Collider col in colliders)
        {
            ReportCollider(col, root);
        }
    }

    private void ReportGameObjectGeometry(string label, GameObject obj)
    {
        if (obj == null)
        {
            Debug.Log($"{label}: null");
            return;
        }

        Bounds bounds;
        bool hasBounds = TryGetColliderBounds(obj, out bounds);

        Debug.Log(
            $"{label}: position={FormatVector(obj.transform.position)}, " +
            $"scale={FormatVector(obj.transform.lossyScale)}, " +
            (hasBounds
                ? $"boundsCenter={FormatVector(bounds.center)}, boundsSize={FormatVector(bounds.size)}"
                : "bounds=none")
        );

        ReportColliders(label + " Colliders", obj.transform);
    }

    private void ReportCollider(Collider col, Transform localRoot)
    {
        if (col == null)
        {
            return;
        }

        string shapeInfo = GetColliderShapeInfo(col);
        Vector3 localCenter = localRoot != null
            ? localRoot.InverseTransformPoint(col.bounds.center)
            : col.transform.InverseTransformPoint(col.bounds.center);

        Debug.Log(
            $"  {col.name} [{col.GetType().Name}]: " +
            $"enabled={col.enabled}, trigger={col.isTrigger}, " +
            $"worldCenter={FormatVector(col.bounds.center)}, " +
            $"localCenter={FormatVector(localCenter)}, " +
            $"boundsSize={FormatVector(col.bounds.size)}, " +
            shapeInfo
        );
    }

    private string GetColliderShapeInfo(Collider col)
    {
        BoxCollider box = col as BoxCollider;
        if (box != null)
        {
            return $"boxCenter={FormatVector(box.center)}, boxSize={FormatVector(box.size)}";
        }

        SphereCollider sphere = col as SphereCollider;
        if (sphere != null)
        {
            return $"sphereCenter={FormatVector(sphere.center)}, radius={sphere.radius:F4}";
        }

        CapsuleCollider capsule = col as CapsuleCollider;
        if (capsule != null)
        {
            return $"capsuleCenter={FormatVector(capsule.center)}, radius={capsule.radius:F4}, height={capsule.height:F4}, direction={capsule.direction}";
        }

        MeshCollider mesh = col as MeshCollider;
        if (mesh != null)
        {
            string meshName = mesh.sharedMesh != null ? mesh.sharedMesh.name : "none";
            return $"mesh={meshName}, convex={mesh.convex}";
        }

        return "shape=unknown";
    }

    private bool TryGetColliderBounds(GameObject obj, out Bounds bounds)
    {
        bounds = new Bounds();

        if (obj == null)
        {
            return false;
        }

        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        bool hasBounds = false;

        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled || col.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return hasBounds;
    }

    private bool TryGetColliderBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();

        if (root == null)
        {
            return false;
        }

        return TryGetColliderBounds(root.gameObject, out bounds);
    }

    private GameObject GetRootObject(GameObject obj)
    {
        if (obj == null)
        {
            return null;
        }

        Rigidbody rb = obj.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            return rb.gameObject;
        }

        return obj.transform.root.gameObject;
    }

    private Vector3 ToLocal(Transform root, Vector3 worldPosition)
    {
        if (root == null)
        {
            return worldPosition;
        }

        return root.InverseTransformPoint(worldPosition);
    }

    private Vector3 ToExperimentFromM9(Vector3 worldPosition)
    {
        if (m9Origin == null)
        {
            return Vector3.zero;
        }

        Vector3 relativeUnity = worldPosition - m9Origin.position;

        return new Vector3(
            -relativeUnity.y,
            relativeUnity.x,
            relativeUnity.z
        ) * unityToExperimentScale;
    }

    private string FormatVector(Vector3 value)
    {
        return $"({value.x:F4}, {value.y:F4}, {value.z:F4})";
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        DrawPoint(graspCenter, Color.yellow);
        DrawPoint(m9Origin, Color.red);
        DrawPoint(leftFingerTip, Color.cyan);
        DrawPoint(rightFingerTip, Color.cyan);
        DrawPoint(leftContactPoint, Color.green);
        DrawPoint(rightContactPoint, Color.green);

        if (leftContactPoint != null && rightContactPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(leftContactPoint.position, rightContactPoint.position);
        }
    }

    private void DrawPoint(Transform target, Color color)
    {
        if (target == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawSphere(target.position, gizmoSphereRadius);
    }
}
