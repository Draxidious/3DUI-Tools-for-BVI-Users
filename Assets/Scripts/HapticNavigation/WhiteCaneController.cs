using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;

public class WhiteCaneController : MonoBehaviour
{
    public GameObject canePrefab;
    public Transform controllerTransform; // ? Assign your CaneAnchor GameObject here

    private GameObject activeCane;
    private FixedJoint joint;
    private XRInputDevice rightDevice;
    private InputAction toggleCaneAction;

    void Start()
    {
        toggleCaneAction = new InputAction(
            type: InputActionType.Button,
            binding: "<XRController>{RightHand}/secondaryButton"
        );
        toggleCaneAction.Enable();

        TryGetRightController();
    }

    void Update()
    {
        if (toggleCaneAction.WasPressedThisFrame())
        {
            if (activeCane == null)
                SpawnCane();
            else
                RemoveCane();
        }
    }

    private void SpawnCane()
    {
        activeCane = Instantiate(canePrefab);
        Rigidbody caneRb = activeCane.GetComponent<Rigidbody>();
        caneRb.isKinematic = false;
        caneRb.useGravity = true;
        caneRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Snap the cane's AttachPoint to the anchor position
        Transform attachPoint = activeCane.transform.Find("AttachPoint");
        if (attachPoint != null)
        {
            // Match rotation first (optional)
            activeCane.transform.rotation = controllerTransform.rotation;

            // Move cane so AttachPoint lines up with anchor
            Vector3 offset = controllerTransform.position - attachPoint.position;
            activeCane.transform.position += offset;
        }

        StartCoroutine(AttachCaneSafely(caneRb));
    }

    private IEnumerator AttachCaneSafely(Rigidbody caneRb)
    {
        yield return new WaitForFixedUpdate();

        // Reset movement to avoid fighting forces
        caneRb.linearVelocity = Vector3.zero;
        caneRb.angularVelocity = Vector3.zero;

        // Clean up any previous joint
        FixedJoint existingJoint = controllerTransform.GetComponent<FixedJoint>();
        if (existingJoint != null)
            Destroy(existingJoint);

        joint = controllerTransform.gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = caneRb;
        joint.breakForce = float.PositiveInfinity;
        joint.breakTorque = float.PositiveInfinity;

        // Optional: reduce force transfer from collisions
        caneRb.mass = 0.2f;
        caneRb.linearDamping = 1.5f;
        caneRb.angularDamping = 3f;

        // Optional: disable collision between cane and controller
        Collider controllerCol = controllerTransform.GetComponent<Collider>();
        Collider caneCol = activeCane.GetComponent<Collider>();
        if (controllerCol != null && caneCol != null)
            Physics.IgnoreCollision(controllerCol, caneCol, true);
    }

    private void RemoveCane()
    {
        if (joint != null)
            Destroy(joint);

        if (activeCane != null)
        {
            Destroy(activeCane);
            activeCane = null;
        }
    }

    private void TryGetRightController()
    {
        var rightDevices = new List<XRInputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right,
            rightDevices
        );

        if (rightDevices.Count > 0)
            rightDevice = rightDevices[0];
    }

    private void OnDestroy()
    {
        toggleCaneAction?.Disable();
    }
}
