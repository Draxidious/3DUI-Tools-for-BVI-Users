using UnityEngine;
using UnityEngine.InputSystem;

public class BVIColliderManager : MonoBehaviour
{
    // References to the four BVICollider instances.
    public BVICollider forwardCollider;
    public BVICollider backCollider;
    public BVICollider leftCollider;
    public BVICollider rightCollider;

    private void Update()
    {
        // If the up arrow is pressed, trigger the forward collider announcement.
        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            if (forwardCollider != null)
            {
                forwardCollider.AnnounceObjects(true, true, true, true);
            }
        }

        // If the down arrow is pressed, trigger the back collider announcement.
        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            if (backCollider != null)
            {
                backCollider.AnnounceObjects(true, true, true, true);
            }
        }

        // If the left arrow is pressed, trigger the left collider announcement.
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            if (leftCollider != null)
            {
                leftCollider.AnnounceObjects(true, true, true, true);
            }
        }

        // If the right arrow is pressed, trigger the right collider announcement.
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            if (rightCollider != null)
            {
                rightCollider.AnnounceObjects(true, true, true, true);
            }
        }
    }
}
