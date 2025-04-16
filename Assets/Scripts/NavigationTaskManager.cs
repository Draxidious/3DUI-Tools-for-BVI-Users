using Meta.WitAi.TTS.Utilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class NavigationTaskManager : MonoBehaviour
{

    public TTSSpeaker speaker;
    //public OfflineTTS tts;
    public AudioSource arrivalSound;
    public Transform pointB;
    public Transform pointC;
    public Transform pointD;

    public Transform pointA1;
    public Transform pointB1;
    public Transform pointB2;
    public Transform pointC1;
    
    public HapticNavigator hapticNavigator;

    private bool[] pointsReached = new bool[8]; // A = 0, B = 1, C = 2, D = 3
    private InputDevice leftController;
    private InputDevice rightController;

    void Start()
    {
        pointsReached[0] = true; // Start at A
        EnsureControllersAreValid();
        hapticNavigator.SetTarget(pointA1); // Start guiding to B
        StartCoroutine(WelcomeMessage());
    }

    private IEnumerator WelcomeMessage()
    {
        yield return new WaitForSecondsRealtime(5);
        speaker.SpeakQueued("Welcome to the navigation task. You are currenty standing at point A. " +
            "" +
            "Next you will Move to points B, then to C, and finally to D which is your goal.");
    }

    public void OnPointReached(string pointID)
    {
        EnsureControllersAreValid();

        int index = pointID switch
        {
            "A1"=>1,
            "B" => 2,
            "B1"=>3,
            "B2" => 4,
            "C" => 5,
            "C1" => 6,
            "D" => 7,
            _ => -1
        };

        if (index >= 0 && !pointsReached[index])
        {
            pointsReached[index] = true;
            if (pointID == "B"| pointID == "C" | pointID == "D")
            {
                speaker.SpeakQueued($"You have reached point {pointID}");
                arrivalSound.Play();
            }
            

            SendHaptic(leftController, 0.8f, 0.2f);
            SendHaptic(rightController, 0.8f, 0.2f);

            // Set new navigation target
            if (pointID == "A1") hapticNavigator.SetTarget(pointB);
            if (pointID == "B") hapticNavigator.SetTarget(pointB1);
            if (pointID == "B1") hapticNavigator.SetTarget(pointB2);
            if (pointID == "B2") hapticNavigator.SetTarget(pointC);
            if (pointID == "C") hapticNavigator.SetTarget(pointC1);
            if (pointID == "C1") hapticNavigator.SetTarget(pointD);
            if (pointID == "D") hapticNavigator.SetTarget(null);
        }

        if (AllPointsReached())
        {
            //yield return new WaitForSeconds(2f);
            new WaitForSecondsRealtime(2);
            speaker.SpeakQueued("Great work! You have reached all points.");
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
