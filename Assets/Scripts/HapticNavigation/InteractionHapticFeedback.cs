using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class InteractionHapticFeedback : MonoBehaviour
{
    [Header("Haptics Settings")]
    public float hoverAmplitude = 0.2f;
    public float hoverDuration = 0.05f;

    public float grabAmplitude = 0.6f;
    public float grabDuration = 0.1f;

    public float dropAmplitude = 0.4f;
    public float dropDuration = 0.08f;

    private void OnEnable()
    {
        var interactable = GetComponent<XRGrabInteractable>();
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        var interactable = GetComponent<XRGrabInteractable>();
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        TriggerHaptics(args.interactorObject, hoverAmplitude, hoverDuration);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        TriggerHaptics(args.interactorObject, grabAmplitude, grabDuration);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        TriggerHaptics(args.interactorObject, dropAmplitude, dropDuration);
    }

    private void TriggerHaptics(IXRInteractor interactorObject, float amplitude, float duration)
    {
        InputDeviceCharacteristics hand = GetHandFromInteractor(interactorObject);

        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(hand | InputDeviceCharacteristics.Controller, devices);

        if (devices.Count > 0 && devices[0].TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
        {
            devices[0].SendHapticImpulse(0, amplitude, duration);
        }
    }

    private InputDeviceCharacteristics GetHandFromInteractor(IXRInteractor interactor)
    {
        if (interactor.transform.name.ToLower().Contains("left"))
            return InputDeviceCharacteristics.Left;
        if (interactor.transform.name.ToLower().Contains("right"))
            return InputDeviceCharacteristics.Right;

        // Default fallback
        return InputDeviceCharacteristics.Left;
    }
}
