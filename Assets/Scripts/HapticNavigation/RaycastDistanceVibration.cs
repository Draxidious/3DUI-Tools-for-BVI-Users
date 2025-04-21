using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(LineRenderer))]
public class RaycastDistanceVibration : MonoBehaviour
{
    public Transform rayOrigin;
    public float maxRayDistance = 5f;
    public string detectableTag = "Key";

    private InputDevice leftController;
    private LineRenderer lineRenderer;

    void Start()
    {
        TryGetLeftController();

        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;

        Material rayMat = new Material(Shader.Find("Unlit/Color"));
        rayMat.color = Color.cyan;
        lineRenderer.material = rayMat;
        lineRenderer.enabled = true;
    }

    void Update()
    {
        if (!leftController.isValid)
            TryGetLeftController();

        if (rayOrigin == null) return;

        Vector3 start = rayOrigin.position;
        Vector3 direction = rayOrigin.forward;
        Vector3 end = start + direction * maxRayDistance;

        bool hitKey = false;

        if (Physics.Raycast(start, direction, out RaycastHit hit, maxRayDistance))
        {
            end = hit.point;

            //if (hit.collider.CompareTag(detectableTag))
            //{
            hitKey = true;
            float distance = hit.distance;
            float intensity = Mathf.Clamp01(1f - (distance / maxRayDistance));
            SendHaptic(intensity, 0.1f);

            lineRenderer.material.color = Color.green;
            //}
        }

        if (!hitKey)
        {
            SendHaptic(0f, 0.05f);
            lineRenderer.material.color = Color.cyan;
        }

        DrawRay(start, end);
    }

    void DrawRay(Vector3 start, Vector3 end)
    {
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    void SendHaptic(float amplitude, float duration)
    {
        if (leftController.isValid && amplitude > 0f)
        {
            bool success = leftController.SendHapticImpulse(0u, amplitude, duration);
            Debug.Log($"Haptic sent to left controller: {success} | amplitude: {amplitude}");
        }
    }

    void TryGetLeftController()
    {
        var leftDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, leftDevices);
        if (leftDevices.Count > 0)
        {
            leftController = leftDevices[0];
            Debug.Log(" Left controller re-acquired");
        }
        else
        {
            Debug.LogWarning(" Could not find left controller");
        }
    }
}
