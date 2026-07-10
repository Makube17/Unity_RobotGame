using System.Collections.Generic;
using UnityEngine;

public class SpawnedObjectPhysicsStabilizer : MonoBehaviour
{
    [Header("References")]
    public ObjectSpawner objectSpawner;

    [Header("Apply Timing")]
    public bool applyContinuously = true;
    public float applyInterval = 0.25f;

    [Header("Rigidbody Stabilization")]
    public bool stabilizeRigidbody = true;
    public float mass = 2.0f;
    public float drag = 1.5f;
    public float angularDrag = 6.0f;
    public float maxAngularVelocity = 3.0f;
    public int solverIterations = 12;
    public int solverVelocityIterations = 4;
    public CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    public RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;

    [Header("Collider Bounce And Friction")]
    public bool applyColliderMaterial = true;
    [Range(0f, 1f)] public float staticFriction = 0.8f;
    [Range(0f, 1f)] public float dynamicFriction = 0.8f;
    [Range(0f, 1f)] public float bounciness = 0.0f;
    public PhysicMaterialCombine frictionCombine = PhysicMaterialCombine.Maximum;
    public PhysicMaterialCombine bounceCombine = PhysicMaterialCombine.Minimum;

    private readonly HashSet<int> configuredRigidbodies = new HashSet<int>();
    private PhysicMaterial stableMaterial;
    private float nextApplyTime;

    void Awake()
    {
        if (objectSpawner == null)
        {
            objectSpawner = GetComponent<ObjectSpawner>();
        }
    }

    void Update()
    {
        if (!applyContinuously)
        {
            return;
        }

        if (Time.time < nextApplyTime)
        {
            return;
        }

        nextApplyTime = Time.time + Mathf.Max(0.02f, applyInterval);
        ApplyToSpawnedObjects(false);
    }

    public void ApplyToSpawnedObjects(bool forceReapply = true)
    {
        if (objectSpawner == null)
        {
            return;
        }

        List<GameObject> spawnedObjects = objectSpawner.GetSpawnedObjects();

        if (spawnedObjects == null)
        {
            return;
        }

        foreach (GameObject obj in spawnedObjects)
        {
            ApplyToObject(obj, forceReapply);
        }
    }

    public void ClearConfiguredCache()
    {
        configuredRigidbodies.Clear();
    }

    private void ApplyToObject(GameObject obj, bool forceReapply)
    {
        if (obj == null)
        {
            return;
        }

        Rigidbody rb = obj.GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = obj.GetComponentInChildren<Rigidbody>();
        }

        if (rb != null)
        {
            int rbId = rb.GetInstanceID();

            if (forceReapply || !configuredRigidbodies.Contains(rbId))
            {
                ApplyToRigidbody(rb);
                configuredRigidbodies.Add(rbId);
            }
        }

        if (applyColliderMaterial)
        {
            ApplyStableMaterial(obj);
        }
    }

    private void ApplyToRigidbody(Rigidbody rb)
    {
        if (!stabilizeRigidbody || rb == null)
        {
            return;
        }

        rb.mass = mass;
        rb.drag = drag;
        rb.angularDrag = angularDrag;
        rb.maxAngularVelocity = maxAngularVelocity;
        rb.solverIterations = solverIterations;
        rb.solverVelocityIterations = solverVelocityIterations;
        rb.collisionDetectionMode = collisionDetectionMode;
        rb.interpolation = interpolation;
    }

    private void ApplyStableMaterial(GameObject obj)
    {
        if (stableMaterial == null)
        {
            stableMaterial = new PhysicMaterial("RuntimeSpawnedObjectStableMaterial");
        }

        stableMaterial.staticFriction = staticFriction;
        stableMaterial.dynamicFriction = dynamicFriction;
        stableMaterial.bounciness = bounciness;
        stableMaterial.frictionCombine = frictionCombine;
        stableMaterial.bounceCombine = bounceCombine;

        Collider[] colliders = obj.GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
        {
            if (col == null)
            {
                continue;
            }

            col.material = stableMaterial;
        }
    }
}
