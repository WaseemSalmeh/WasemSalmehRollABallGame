using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class PushableObstacle : MonoBehaviour
{
    [SerializeField] private float mass = 1f;
    [SerializeField] private float impactImpulseMultiplier = 0.35f;

    private Rigidbody obstacleRigidbody;
    private bool isActivated;

    private void Awake()
    {
        ConfigurePhysicsBody();
    }

    public void Initialize(float obstacleMass)
    {
        mass = obstacleMass;
        ConfigurePhysicsBody();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isActivated || !WasHitByPlayer(collision))
        {
            return;
        }

        Activate(collision);
    }

    private void ConfigurePhysicsBody()
    {
        if (TryGetComponent(out MeshCollider meshCollider) && !meshCollider.convex)
        {
            meshCollider.convex = true;
        }

        obstacleRigidbody = GetComponent<Rigidbody>();
        if (obstacleRigidbody == null)
        {
            obstacleRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        obstacleRigidbody.mass = mass;
        obstacleRigidbody.linearDamping = 0.05f;
        obstacleRigidbody.angularDamping = 0.05f;
        obstacleRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        obstacleRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        obstacleRigidbody.useGravity = false;
        obstacleRigidbody.isKinematic = true;
    }

    private static bool WasHitByPlayer(Collision collision)
    {
        if (collision.rigidbody != null && collision.rigidbody.GetComponent<PlayerController>() != null)
        {
            return true;
        }

        return collision.transform.GetComponentInParent<PlayerController>() != null;
    }

    private void Activate(Collision collision)
    {
        isActivated = true;
        obstacleRigidbody.isKinematic = false;
        obstacleRigidbody.useGravity = true;
        obstacleRigidbody.WakeUp();

        Vector3 impactVelocity = collision.rigidbody != null ? collision.rigidbody.linearVelocity : collision.relativeVelocity;
        if (collision.contactCount > 0)
        {
            obstacleRigidbody.AddForceAtPosition(impactVelocity * impactImpulseMultiplier, collision.GetContact(0).point, ForceMode.Impulse);
            return;
        }

        obstacleRigidbody.AddForce(impactVelocity * impactImpulseMultiplier, ForceMode.Impulse);
    }
}
