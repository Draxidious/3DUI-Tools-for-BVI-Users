using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class HapticNavigator : MonoBehaviour
{
    public Transform player;
    private Transform targetPoint;

    private InputDevice leftController;
    private InputDevice rightController;

    public float guidanceAngleThreshold = 15f;
    public float maxVibrationDistance = 8f;

    public void SetTarget(Transform newTarget)
    {
        targetPoint = newTarget;
    }

    void Update()
    {
        EnsureControllersAreValid();

        if (!player || targetPoint == null) return;

        Vector3 toTarget = targetPoint.position - player.position;
        toTarget.y = 0;
        float distance = toTarget.magnitude;

        Vector3 forward = player.forward;
        forward.y = 0;

        float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);

        if (angle > guidanceAngleThreshold)
            SendHaptic(rightController, 0.4f, 0.1f);
        else if (angle < -guidanceAngleThreshold)
            SendHaptic(leftController, 0.4f, 0.1f);
        else
        {
            float intensity = Mathf.Clamp01(1f - (distance / maxVibrationDistance));
            SendHaptic(leftController, intensity, 0.1f);
            SendHaptic(rightController, intensity, 0.1f);
        }
    }

    private void SendHaptic(InputDevice device, float amplitude, float duration)
    {
        if (device.isValid)
        {
            device.SendHapticImpulse(0u, amplitude, duration);
        }
    }

    private void EnsureControllersAreValid()
    {
        if (!leftController.isValid)
        {
            var leftDevices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, leftDevices);
            if (leftDevices.Count > 0) leftController = leftDevices[0];
        }

        if (!rightController.isValid)
        {
            var rightDevices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, rightDevices);
            if (rightDevices.Count > 0) rightController = rightDevices[0];
        }
    }
}
