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
            Vector3 cameraPos = Camera.main.transform.position;
            Vector3 cameraForward = referenceCamera.transform.forward;
            cameraForward.y = 0; // Optional: ignore vertical tilt
            if (cameraForward != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(cameraForward);
            }
            if (transform.position != cameraPos)
            {
                transform.position = cameraPos;
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
            string response = forwardCollider.AnnounceObjects(true, true, true, true);
            if(response.Length == 0)
            {
                speaker.SpeakQueued("Sorry looks like there isn't anything in front of you");
                return;
            }
            chatGPTClient.AskChatGPT(chatGPTClient.ColliderPrompt() + response);
        }
    }

    public void AnnounceBackwardDescription()
    {
        if (backCollider != null)
        {
            speaker.SpeakQueued("Please wait as I now try to get a feel for what's currently behind you");
            string response = backCollider.AnnounceObjects(true, true, true, true);
            if (response.Length == 0)
            {
                speaker.SpeakQueued("Sorry looks like there isn't anything behind you");
                return;
            }
            chatGPTClient.AskChatGPT(chatGPTClient.ColliderPrompt() + response);
        }
    }

    public void AnnounceLeftDescription()
    {
        if (leftCollider != null)
        {
            speaker.SpeakQueued("Please wait as I now try to get a feel for what's currently to your left");
            string response = leftCollider.AnnounceObjects(true, true, true, true);
            if (response.Length == 0)
            {
                speaker.SpeakQueued("Sorry looks like there isn't anything to your left");
                return;
            }
            chatGPTClient.AskChatGPT(chatGPTClient.ColliderPrompt() + response);
        }
    }

    public void AnnounceRightDescription()
    {
        if (rightCollider != null)
        {
            speaker.SpeakQueued("Please wait as I now try to get a feel for what's currently to your right");
            string response = rightCollider.AnnounceObjects(true, true, true, true);
            if (response.Length == 0)
            {
                speaker.SpeakQueued("Sorry looks like there isn't anything to your right");
                return;
            }
            chatGPTClient.AskChatGPT(chatGPTClient.ColliderPrompt() + response);
        }
    }
}
