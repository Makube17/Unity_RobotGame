using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [Header("生成する物体Prefab")]
    public GameObject[] objectPrefabs;

    [Header("生成数")]
    public int spawnCount = 10;

    [Header("生成範囲")]
    public Vector3 spawnCenter = new Vector3(0f, 2.5f, 0f);
    public Vector3 spawnAreaSize = new Vector3(1.0f, 0.5f, 1.0f);

    [Header("落下設定")]
    public float spawnInterval = 0.15f;
    public float randomTorquePower = 2.0f;

    [Header("親オブジェクト")]
    public Transform spawnedParent;

    [Header("デバッグ")]
    public bool spawnOnStart = true;

    public bool IsSpawning { get; private set; }

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private Coroutine spawnCoroutine;

    void Start()
    {
        if (spawnOnStart)
        {
            SpawnObjects();
        }
    }

    public void SpawnObjects()
    {
        if (objectPrefabs == null || objectPrefabs.Length == 0)
        {
            Debug.LogError("ObjectSpawner: objectPrefabs が設定されていません。");
            return;
        }

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        ClearObjects();
        spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        IsSpawning = true;

        for (int i = 0; i < spawnCount; i++)
        {
            SpawnOneObject();
            yield return new WaitForSeconds(spawnInterval);
        }

        IsSpawning = false;
        spawnCoroutine = null;

        Debug.Log("ObjectSpawner: 物体生成が完了しました。");
    }

    void SpawnOneObject()
    {
        GameObject prefab = objectPrefabs[Random.Range(0, objectPrefabs.Length)];

        Vector3 randomOffset = new Vector3(
            Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f),
            Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f),
            Random.Range(-spawnAreaSize.z / 2f, spawnAreaSize.z / 2f)
        );

        Vector3 spawnPosition = spawnCenter + randomOffset;
        Quaternion spawnRotation = Random.rotation;

        GameObject obj = Instantiate(
            prefab,
            spawnPosition,
            spawnRotation,
            spawnedParent
        );

        spawnedObjects.Add(obj);

        Rigidbody rb = obj.GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = obj.AddComponent<Rigidbody>();
        }

        rb.mass = 1.0f;
        rb.useGravity = true;
        rb.isKinematic = false;

        Vector3 randomTorque = new Vector3(
            Random.Range(-randomTorquePower, randomTorquePower),
            Random.Range(-randomTorquePower, randomTorquePower),
            Random.Range(-randomTorquePower, randomTorquePower)
        );

        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }

    public void ClearObjects()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        IsSpawning = false;

        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        spawnedObjects.Clear();
    }

    private void ClearObjectsOnly()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        spawnedObjects.Clear();
    }

    public List<GameObject> GetSpawnedObjects()
    {
        return spawnedObjects;
    }

    public float GetEstimatedSpawnDuration()
    {
        return spawnCount * spawnInterval;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(spawnCenter, spawnAreaSize);
    }
}