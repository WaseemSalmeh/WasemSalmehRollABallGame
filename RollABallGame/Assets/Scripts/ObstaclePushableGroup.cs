using UnityEngine;

[DisallowMultipleComponent]
public class ObstaclePushableGroup : MonoBehaviour
{
    [SerializeField] private float barrelMass = 2.2f;
    [SerializeField] private float lidMass = 0.75f;

    private void Awake()
    {
        ConfigureGroup();
    }

    [ContextMenu("Configure Pushable Obstacles")]
    public void ConfigureGroup()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform || !TryGetMass(child.name, out float mass))
            {
                continue;
            }

            if (!child.TryGetComponent(out Collider _))
            {
                continue;
            }

            PushableObstacle pushableObstacle = child.GetComponent<PushableObstacle>();
            if (pushableObstacle == null)
            {
                pushableObstacle = child.gameObject.AddComponent<PushableObstacle>();
            }

            pushableObstacle.Initialize(mass);
        }
    }

    private bool TryGetMass(string objectName, out float mass)
    {
        string normalizedName = objectName.ToLowerInvariant();
        bool isLid = normalizedName.Contains("crate_lid") || normalizedName.Contains("barrel_lid") || normalizedName.EndsWith("lid");
        bool isBarrel = normalizedName.Contains("barrel") && !normalizedName.Contains("stand");

        if (isLid)
        {
            mass = lidMass;
            return true;
        }

        if (isBarrel)
        {
            mass = barrelMass;
            return true;
        }

        mass = 0f;
        return false;
    }
}
