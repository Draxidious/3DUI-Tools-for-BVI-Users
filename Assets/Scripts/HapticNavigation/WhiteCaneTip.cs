using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class WhiteCaneTip : MonoBehaviour
{
    public AudioSource stickhit;
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

    private void OnTriggerEnter(Collider other)
    {
        if (rightHand.isValid)
        {
            rightHand.SendHapticImpulse(0u, 0.7f, 0.1f);
            stickhit.Play();
            Debug.Log("Cane touched: " + other.gameObject.name);
        }
    }
}