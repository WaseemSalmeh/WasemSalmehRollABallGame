using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class GroundVegetationScatter : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject grassPrefab;
    [SerializeField] private GameObject flowerPrefab;

    [Header("Distribution")]
    [Min(0.5f)]
    [SerializeField] private float cellSize = 1.25f;

    [Range(0f, 1f)]
    [SerializeField] private float coverage = 0.92f;

    [Range(0f, 1f)]
    [SerializeField] private float flowerChance = 0.18f;

    [SerializeField] private int seed = 17;

    [Min(0f)]
    [SerializeField] private float edgePadding = 0.35f;

    [Header("Variation")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.85f, 1.15f);
    [SerializeField] private float surfaceOffset = 0.02f;
    [SerializeField] private float clearanceRadius = 0.18f;
    [SerializeField] private float clearanceHeight = 1.25f;

    [Header("Editor")]
    [SerializeField] private bool generateInEditor = true;

    private const string ContainerName = "_VegetationScatterInstances";

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            EnsureGenerated();
            return;
        }

        if (generateInEditor)
        {
            Rebuild();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying || !generateInEditor)
        {
            return;
        }

        // Delay rebuild to avoid calling during validation
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && generateInEditor && !Application.isPlaying)
            {
                Rebuild();
            }
        };
    }
#endif

    [ContextMenu("Rebuild Vegetation")]
    public void Rebuild()
    {
        if (!TryGetScatterSurface(out Mesh mesh, out Collider groundCollider))
        {
            return;
        }

        Transform container = GetOrCreateContainer();
        RefreshContainer(container);
        ClearContainer(container);

        Random.State previousState = Random.state;
        Random.InitState(seed);

        Bounds localBounds = mesh.bounds;
        float minX = localBounds.min.x + edgePadding;
        float maxX = localBounds.max.x - edgePadding;
        float minZ = localBounds.min.z + edgePadding;
        float maxZ = localBounds.max.z - edgePadding;

        if (minX >= maxX || minZ >= maxZ)
        {
            Random.state = previousState;
            return;
        }

        int cellsX = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / cellSize));
        int cellsZ = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / cellSize));

        float minScale = Mathf.Min(scaleRange.x, scaleRange.y);
        float maxScale = Mathf.Max(scaleRange.x, scaleRange.y);
        Vector3 rayLift = transform.up * 10f;

        // Jittered cells keep the fill random while still covering the whole ground evenly.
        for (int x = 0; x < cellsX; x++)
        {
            for (int z = 0; z < cellsZ; z++)
            {
                if (Random.value > coverage)
                {
                    continue;
                }

                float cellMinX = minX + (x * cellSize);
                float cellMinZ = minZ + (z * cellSize);
                float cellMaxX = Mathf.Min(cellMinX + cellSize, maxX);
                float cellMaxZ = Mathf.Min(cellMinZ + cellSize, maxZ);

                float localX = Random.Range(cellMinX, cellMaxX);
                float localZ = Random.Range(cellMinZ, cellMaxZ);
                Vector3 worldSample = transform.TransformPoint(new Vector3(localX, localBounds.center.y, localZ));
                Ray ray = new Ray(worldSample + rayLift, -transform.up);

                if (!groundCollider.Raycast(ray, out RaycastHit hit, 20f))
                {
                    continue;
                }

                Vector3 spawnPosition = hit.point + (hit.normal * surfaceOffset);

                if (IsBlocked(spawnPosition, groundCollider))
                {
                    continue;
                }

                GameObject prefab = ChoosePrefab();
                if (prefab == null)
                {
                    continue;
                }

                GameObject instance = CreateInstance(prefab, container);
                float scale = Random.Range(minScale, maxScale);
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                instance.transform.SetPositionAndRotation(spawnPosition, rotation);
                instance.transform.localScale *= scale;
            }
        }

        Random.state = previousState;
    }

    [ContextMenu("Clear Vegetation")]
    public void ClearGeneratedVegetation()
    {
        Transform container = GetContainer();
        if (container == null)
        {
            return;
        }

        ClearContainer(container);
    }

    private void EnsureGenerated()
    {
        Transform container = GetOrCreateContainer();
        RefreshContainer(container);

        if (container.childCount == 0)
        {
            Rebuild();
        }
    }

    private bool TryGetScatterSurface(out Mesh mesh, out Collider groundCollider)
    {
        mesh = null;
        groundCollider = GetComponent<Collider>();

        if (!TryGetComponent(out MeshFilter meshFilter) || groundCollider == null)
        {
            return false;
        }

        mesh = meshFilter.sharedMesh;
        return mesh != null;
    }

    private GameObject ChoosePrefab()
    {
        bool hasGrass = grassPrefab != null;
        bool hasFlower = flowerPrefab != null;

        if (!hasGrass && !hasFlower)
        {
            return null;
        }

        if (!hasGrass)
        {
            return flowerPrefab;
        }

        if (!hasFlower)
        {
            return grassPrefab;
        }

        return Random.value < flowerChance ? flowerPrefab : grassPrefab;
    }

    private bool IsBlocked(Vector3 spawnPosition, Collider groundCollider)
    {
        Vector3 checkCenter = spawnPosition + (transform.up * (clearanceHeight * 0.5f));
        Collider[] overlaps = Physics.OverlapSphere(checkCenter, clearanceRadius, Physics.AllLayers, QueryTriggerInteraction.Ignore);

        foreach (Collider overlap in overlaps)
        {
            if (overlap == null || overlap == groundCollider)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private Transform GetOrCreateContainer()
    {
        Transform existing = GetContainer();
        if (existing != null)
        {
            return existing;
        }

        GameObject containerObject = new GameObject(ContainerName);
        Transform container = containerObject.transform;
        container.SetParent(transform, false);
        RefreshContainer(container);
        return container;
    }

    private Transform GetContainer()
    {
        return transform.Find(ContainerName);
    }

    private void RefreshContainer(Transform container)
    {
        container.localPosition = Vector3.zero;
        container.localRotation = Quaternion.identity;
        container.localScale = GetInverseLossyScale();
    }

    private Vector3 GetInverseLossyScale()
    {
        Vector3 worldScale = transform.lossyScale;
        return new Vector3(InverseScale(worldScale.x), InverseScale(worldScale.y), InverseScale(worldScale.z));
    }

    private static float InverseScale(float value)
    {
        return Mathf.Approximately(value, 0f) ? 1f : 1f / value;
    }

    private void ClearContainer(Transform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            GameObject child = container.GetChild(i).gameObject;

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private GameObject CreateInstance(GameObject prefab, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject editorInstance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (editorInstance != null)
            {
                return editorInstance;
            }
        }
#endif

        return Instantiate(prefab, parent);
    }
}
