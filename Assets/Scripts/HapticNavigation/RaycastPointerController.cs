using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using XRInputDevice = UnityEngine.XR.InputDevice;

public class RaycastPointerController : MonoBehaviour
{
    public GameObject raycastPrefab;               // Prefab with RaycastDistanceVibration
    public Transform rayOriginTransform;           // Typically left-hand controller or a child
    public GameObject hapticNavigatorObject;       // HapticNavigator GameObject

    private GameObject activeRaycastObject;
    private XRInputDevice leftDevice;

    void Start()
    {
        TryGetLeftController();
    }

    void Update()
    {
        // No manual toggle — handled via voice
    }

    private void SpawnRaycastPointer()
    {
        activeRaycastObject = Instantiate(raycastPrefab);

        // Set ray origin dynamically (assign it to the instantiated script)
        RaycastDistanceVibration rayScript = activeRaycastObject.GetComponent<RaycastDistanceVibration>();
        if (rayScript != null)
        {
            rayScript.rayOrigin = rayOriginTransform;
        }

        if (hapticNavigatorObject != null)
            hapticNavigatorObject.SetActive(false);
    }

    private void RemoveRaycastPointer()
    {
        if (activeRaycastObject != null)
        {
            Destroy(activeRaycastObject);
            activeRaycastObject = null;

            if (hapticNavigatorObject != null)
                hapticNavigatorObject.SetActive(true);
        }
    }

    private void TryGetLeftController()
    {
        var leftDevices = new List<XRInputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left,
            leftDevices
        );

        if (leftDevices.Count > 0)
            leftDevice = leftDevices[0];
    }

    public void ToggleRaycast(bool enable)
    {
        if (enable && activeRaycastObject == null)
        {
            SpawnRaycastPointer();
        }
        else if (!enable && activeRaycastObject != null)
        {
            RemoveRaycastPointer();
        }
    }

    public bool IsRaycastActive()
    {
        return activeRaycastObject != null;
    }

    private void OnDestroy()
    {
        // No InputActions to clean up, but safe for future extensions
    }
}
