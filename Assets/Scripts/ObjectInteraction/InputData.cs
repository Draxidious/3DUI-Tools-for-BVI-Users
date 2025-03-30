using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
public class InputData : MonoBehaviour
{
    public InputDevice _rightController;
    public InputDevice _leftController;
    public InputDevice _headset;

    private void InitializeInputDevices()
    {
        if (!_rightController.isValid)
        {
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, new List<InputDevice>());
        }
    }

    private void InitializeInputDevice(InputDeviceCharacteristics inputDeviceCharacteristics, ref InputDevice inputDevice)
    {
        List<InputDevice> inputDevices = new List<InputDevice>();
        // Call InputDevices with the inputDeviceCharacteristics and the list of inputDevices to fill
        InputDevices.GetDevicesWithCharacteristics(inputDeviceCharacteristics, inputDevices);

        // Here we check if devices are found
        if (inputDevices.Count > 0)
        {
            inputDevice = inputDevices[0];
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!_rightController.isValid)
        {
            InitializeInputDevice(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, ref _rightController);
        }
        if (!_leftController.isValid)
        {
            InitializeInputDevice(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, ref _leftController);
        }
        if (!_headset.isValid)
        {
            InitializeInputDevice(InputDeviceCharacteristics.HeadMounted, ref _headset);
        }
    }
}
