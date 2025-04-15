using Meta.WitAi.TTS.Interfaces;
using Meta.WitAi.TTS.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

public class BVIColliderManager : MonoBehaviour
{
    public TTSSpeaker speaker;
    public ChatGPTClient chatGPTClient;

    // References to the four BVICollider instances.
    public BVICollider forwardCollider;
    public BVICollider backCollider;
    public BVICollider leftCollider;
    public BVICollider rightCollider;

    // Reference to the camera whose forward vector we want to match
    public Camera referenceCamera;

    private void Update()
    {
        // Match forward vector with the camera
        if (referenceCamera != null)
        {
            Vector3 cameraForward = referenceCamera.transform.forward;
            cameraForward.y = 0; // Optional: ignore vertical tilt
            if (cameraForward != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(cameraForward);
            }
        }
        // If the up arrow is pressed, trigger the forward collider announcement.
        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            AnnounceForwardDescription();
        }

        // If the down arrow is pressed, trigger the back collider announcement.
        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            AnnounceBackwardDescription();
        }

        // If the left arrow is pressed, trigger the left collider announcement.
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            AnnounceLeftDescription();
        }

        // If the right arrow is pressed, trigger the right collider announcement.
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            AnnounceRightDescription();
        }
    }

    public void AnnounceForwardDescription()
    {
        if (forwardCollider != null)
        {
            speaker.SpeakQueued("Please wait as I now try to get a feel for what's currently in front of you");
            chatGPTClient.AskChatGPT(chatGPTClient.ColliderPrompt() + forwardCollider.AnnounceObjects(true, true, true, true));
        }
    }

    public void AnnounceBackwardDescription()
    {
        if (backCollider != null)
        {
            speaker.SpeakQueued("Please wait as I now try to get a feel for what's currently behind you");
            chatGPTClient.AskChatGPT(chatGPTClient.ColliderPrompt() + backCollider.AnnounceObjects(true, true, true, true));
        }
    }

    public void AnnounceLeftDescription()
    {
        if (leftCollider != null)
        {
            speaker.SpeakQueued("Please wait as I now try to get a feel for what's currently to your left");
            chatGPTClient.AskChatGPT(chatGPTClient.ColliderPrompt() + leftCollider.AnnounceObjects(true, true, true, true));
        }
    }

    public void AnnounceRightDescription()
    {
        if (rightCollider != null)
        {
            speaker.SpeakQueued("Please wait as I now try to get a feel for what's currently to your right");
            chatGPTClient.AskChatGPT(chatGPTClient.ColliderPrompt() + rightCollider.AnnounceObjects(true, true, true, true));
        }
    }
}
