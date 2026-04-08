using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerFallReset : MonoBehaviour
{
    private const float ResetBelowGroundDistance = 0.01f;
    private const float ResetCooldownSeconds = 0.2f;
    private const string GroundObjectName = "Ground";
    private const string PickUpParentName = "PickUp Parent";
    private const string PickUpTag = "PickUp";

    private Collider playerCollider;
    private Rigidbody playerRigidbody;
    private PlayerController playerController;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float resetSurfaceHeight;
    private PickupState[] pickupStates;
    private float resetCooldownRemaining;

    private sealed class PickupState
    {
        public PickupState(GameObject pickup)
        {
            Pickup = pickup;
            Parent = pickup.transform.parent;
            LocalPosition = pickup.transform.localPosition;
            LocalRotation = pickup.transform.localRotation;
            LocalScale = pickup.transform.localScale;
        }

        private GameObject Pickup { get; }
        private Transform Parent { get; }
        private Vector3 LocalPosition { get; }
        private Quaternion LocalRotation { get; }
        private Vector3 LocalScale { get; }

        public void Restore()
        {
            if (Pickup == null)
            {
                return;
            }

            Transform pickupTransform = Pickup.transform;
            pickupTransform.SetParent(Parent, false);
            pickupTransform.localPosition = LocalPosition;
            pickupTransform.localRotation = LocalRotation;
            pickupTransform.localScale = LocalScale;
            Pickup.SetActive(true);

            if (Pickup.TryGetComponent(out Rigidbody pickupRigidbody))
            {
                pickupRigidbody.linearVelocity = Vector3.zero;
                pickupRigidbody.angularVelocity = Vector3.zero;
                pickupRigidbody.Sleep();
            }
        }
    }

    private void Awake()
    {
        playerCollider = GetComponent<Collider>();
        playerRigidbody = GetComponent<Rigidbody>();
        playerController = GetComponent<PlayerController>();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        resetSurfaceHeight = ResolveResetHeight();
        pickupStates = CachePickupStates();
    }

    private void Update()
    {
        if (resetCooldownRemaining > 0f)
        {
            resetCooldownRemaining -= Time.deltaTime;
            return;
        }

        if (playerCollider.bounds.max.y < resetSurfaceHeight)
        {
            ResetLevel();
        }
    }

    private void ResetLevel()
    {
        resetCooldownRemaining = ResetCooldownSeconds;

        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        playerRigidbody.position = spawnPosition;
        playerRigidbody.rotation = spawnRotation;
        Physics.SyncTransforms();

        foreach (PickupState pickupState in pickupStates)
        {
            pickupState.Restore();
        }

        playerController.ResetRun();
        playerRigidbody.WakeUp();
    }

    private static PickupState[] CachePickupStates()
    {
        List<PickupState> states = new List<PickupState>();
        GameObject pickUpParent = GameObject.Find(PickUpParentName);

        if (pickUpParent != null)
        {
            foreach (Transform child in pickUpParent.GetComponentsInChildren<Transform>(true))
            {
                if (child == pickUpParent.transform || !child.gameObject.CompareTag(PickUpTag))
                {
                    continue;
                }

                states.Add(new PickupState(child.gameObject));
            }
        }

        if (states.Count > 0)
        {
            return states.ToArray();
        }

        foreach (GameObject pickup in GameObject.FindGameObjectsWithTag(PickUpTag))
        {
            states.Add(new PickupState(pickup));
        }

        return states.ToArray();
    }

    private static float ResolveResetHeight()
    {
        GameObject groundObject = GameObject.Find(GroundObjectName);
        if (groundObject == null)
        {
            return -ResetBelowGroundDistance;
        }

        if (groundObject.TryGetComponent(out Collider groundCollider))
        {
            return groundCollider.bounds.max.y - ResetBelowGroundDistance;
        }

        return groundObject.transform.position.y - ResetBelowGroundDistance;
    }
}
