using System;
using UnityEngine;
using Oculus.Voice.Dictation;
using UnityEngine.InputSystem;

public class VoiceCaneController : MonoBehaviour
{
    public WhiteCaneController caneController;
    public RaycastPointerController raycastController;
    public AppDictationExperience dictationExperience;

    private InputAction toggleCaneAction;
    private bool isListening = false;

    void Awake()
    {
        // Bind the secondary button on the right-hand controller
        toggleCaneAction = new InputAction(
            type: InputActionType.Button,
            binding: "<XRController>{RightHand}/secondaryButton"
        );
        toggleCaneAction.Enable();

        dictationExperience.DictationEvents.OnFullTranscription.AddListener(OnTranscription);
    }

    void Update()
    {
        //if (!isListening && toggleCaneAction.WasPressedThisFrame())
        //{
        //    dictationExperience.Activate();
        //    isListening = true;
        //    Debug.Log("[VoiceCaneController] Dictation activated.");
        //}
    }

    private void OnTranscription(string transcription)
    {
        string cleaned = transcription.ToLower().Trim();
        Debug.Log("[VoiceCaneController] Transcription: " + cleaned);

        string[] triggers = { "cane", "kane", "came" };
        foreach (string trigger in triggers)
        {
            if (cleaned.Contains(trigger))
            {
                Debug.Log($"[VoiceCaneController] Matched '{trigger}', toggled cane to: " + (!caneController.IsCaneActive() ? "ON" : "OFF"));
                ToggleCane();
                break;
            }
        }
        string[] triggers2 = { "finder","binder"};
        foreach (string trigger in triggers2)
        {
            if (cleaned.Contains(trigger))
            {
                Debug.Log($"[VoiceCaneController] Matched '{trigger}', toggled raycast to: " + (!raycastController.IsRaycastActive() ? "ON" : "OFF"));
                ToggleFinder();
                break;
            }
                
        }

        dictationExperience.Deactivate();
        isListening = false;
    }

    private void OnDestroy()
    {
        toggleCaneAction?.Disable();
        dictationExperience.DictationEvents.OnFullTranscription.RemoveListener(OnTranscription);
    }

    public void ToggleCane()
    {
        bool isCaneActive = caneController.IsCaneActive();
        caneController.ToggleCane(!isCaneActive);
    }

    public void ToggleFinder()
    {
        bool active = raycastController.IsRaycastActive();
        raycastController.ToggleRaycast(!active);
    }
}
