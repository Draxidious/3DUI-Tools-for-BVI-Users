using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class HapticNavigator : MonoBehaviour
{
    public Transform player;
    private Transform targetPoint;

    private InputDevice leftController;
    private InputDevice rightController;

    public float maxVibrationAngle = 180f;
    public float vibrationStrength = 0.5f;

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
        toTarget.Normalize();

        Vector3 forward = player.forward;
        forward.y = 0;
        forward.Normalize();

        float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up); // Left  Right 

        float absAngle = Mathf.Abs(angle);
        float intensity = Mathf.Clamp01(absAngle / maxVibrationAngle) * vibrationStrength;

        if (Mathf.Abs(angle) < 5f)
        {
            // Facing correct direction no vibration
            SendHaptic(leftController, 0f, 0.05f);
            SendHaptic(rightController, 0f, 0.05f);
        }
        else if (angle > 0)
        {
            // Turning right right vibrates, left fades out
            SendHaptic(rightController, intensity, 0.1f);
            SendHaptic(leftController, 0f, 0.05f);
        }
        else
        {
            // Turning left left vibrates, right fades out
            SendHaptic(leftController, intensity, 0.1f);
            SendHaptic(rightController, 0f, 0.05f);
        }
    }

    private void SendHaptic(InputDevice device, float amplitude, float duration)
    {
        if (device.isValid && amplitude > 0f)
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
