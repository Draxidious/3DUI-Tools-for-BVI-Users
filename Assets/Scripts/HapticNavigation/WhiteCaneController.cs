using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;

public class WhiteCaneController : MonoBehaviour
{
    public GameObject canePrefab;
    public Transform controllerTransform; // Assign CaneAnchor
    public GameObject hapticNavigatorObject; // Assign HapticNavigator GameObject in Inspector

    private GameObject activeCane;
    private XRInputDevice rightDevice;
    private InputAction toggleCaneAction;

    private Vector3 localOffset = new Vector3(0f, 0f, 0.6f);
    private Quaternion localRotation = Quaternion.Euler(90f, 0f, 0f);

    void Start()
    {
        //toggleCaneAction = new InputAction(
        //    type: InputActionType.Button,
        //    binding: "<XRController>{RightHand}/secondaryButton"
        //);
        //toggleCaneAction.Enable();

        TryGetRightController();

    }

    void Update()
    {
        //if (toggleCaneAction.WasPressedThisFrame())
        //{
        //    if (activeCane == null)
        //        SpawnCane();
        //    else
        //        RemoveCane();
        //}
    }

    private void SpawnCane()
    {
        activeCane = Instantiate(canePrefab);
        UpdateCaneTransform();

        // Disable haptic navigation system
        if (hapticNavigatorObject != null)
            hapticNavigatorObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (activeCane != null)
        {
            UpdateCaneTransform();
        }
    }

    private void UpdateCaneTransform()
    {
        if (controllerTransform == null || activeCane == null) return;

        activeCane.transform.position = controllerTransform.TransformPoint(localOffset);
        activeCane.transform.rotation = controllerTransform.rotation * localRotation;
    }

    private void RemoveCane()
    {
        if (activeCane != null)
        {
            Destroy(activeCane);
            activeCane = null;

            // Reactivate haptic navigation system
            if (hapticNavigatorObject != null)
                hapticNavigatorObject.SetActive(true);
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

    public void ToggleCane(bool enable)
    {
        if (enable && activeCane == null)
        {
            SpawnCane();
        }
        else if (!enable && activeCane != null)
        {
            RemoveCane();
        }
    }

    public bool IsCaneActive()
    {
        return activeCane != null;
    }
}
