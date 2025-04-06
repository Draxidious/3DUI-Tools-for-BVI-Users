using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class WhiteCaneTip : MonoBehaviour
{
    private InputDevice rightHand;

    void Start()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
            devices
        );

        if (devices.Count > 0)
            rightHand = devices[0];
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (rightHand.isValid)
        {
            rightHand.SendHapticImpulse(0u, 0.6f, 0.1f);
            Debug.Log("Cane hit: " + collision.gameObject.name);
        }
    }
}
