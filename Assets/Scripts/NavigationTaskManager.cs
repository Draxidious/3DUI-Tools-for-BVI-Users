using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class NavigationTaskManager : MonoBehaviour
{
    public OfflineTTS tts;
    public AudioSource arrivalSound;
    public Transform pointB;
    public Transform pointC;
    public Transform pointD;
    public HapticNavigator hapticNavigator;

    private bool[] pointsReached = new bool[4]; // A = 0, B = 1, C = 2, D = 3
    private InputDevice leftController;
    private InputDevice rightController;

    void Start()
    {
        pointsReached[0] = true; // Start at A
        EnsureControllersAreValid();
        hapticNavigator.SetTarget(pointB); // Start guiding to B
        StartCoroutine(WelcomeMessage());
    }

    private IEnumerator WelcomeMessage()
    {
        yield return new WaitForSeconds(2f);
        tts.Speak("Welcome to the navigation task. You are at point A. Move to points B, C, and D.");
    }

    public void OnPointReached(string pointID)
    {
        EnsureControllersAreValid();

        int index = pointID switch
        {
            "B" => 1,
            "C" => 2,
            "D" => 3,
            _ => -1
        };

        if (index >= 0 && !pointsReached[index])
        {
            pointsReached[index] = true;
            tts.Speak($"You have reached point {pointID}");
            arrivalSound.Play();

            SendHaptic(leftController, 0.8f, 0.2f);
            SendHaptic(rightController, 0.8f, 0.2f);

            // Set new navigation target
            if (pointID == "B") hapticNavigator.SetTarget(pointC);
            if (pointID == "C") hapticNavigator.SetTarget(pointD);
            if (pointID == "D") hapticNavigator.SetTarget(null);
        }

        if (AllPointsReached())
        {
            tts.Speak("Great work! You have reached all points.");
            SendHaptic(leftController, 1f, 0.3f);
            SendHaptic(rightController, 1f, 0.3f);
        }
    }

    private bool AllPointsReached()
    {
        for (int i = 1; i < pointsReached.Length; i++) // skip A
        {
            if (!pointsReached[i]) return false;
        }
        return true;
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

    private void SendHaptic(InputDevice device, float amplitude, float duration)
    {
        if (device.isValid)
        {
            device.SendHapticImpulse(0u, amplitude, duration);
        }
    }
}
