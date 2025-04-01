using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Meta.WitAi.TTS.Utilities;
using UnityEngine.InputSystem;

public class BVICollider : MonoBehaviour
{
    // The speaker used to announce the object names.
    public TTSSpeaker speaker;

    // A name that identifies this collider (e.g., "forward", "left").
    // The orientation phrase will be derived from this.
    public string colliderName = "forward";

    // How close in y value objects must be to be considered at the same level.
    // (We use a rounding strategy in this example.)
    public float yTolerance = 0.1f;  // in meters

    // Internal list to keep track of objects currently in the trigger.
    private List<GameObject> enteredObjects = new List<GameObject>();

    // Reference to the XR Origin's main camera.
    private Camera xrCamera;

    private void Start()
    {
        // Attempt to locate the XR Origin's main camera.
        xrCamera = Camera.main;
        if (xrCamera == null)
        {
            Debug.LogWarning("Main Camera (XR Origin's camera) not found in the scene!");
        }
    }

    // Called automatically when another collider enters this trigger.
    private void OnTriggerEnter(Collider other)
    {
        GameObject obj = other.gameObject;
        // Exclude the XR Origin rig and objects named "Collider"
        if (obj.name == "XR Origin" || obj.name == "Collider")
            return;

        if (!enteredObjects.Contains(obj))
        {
            enteredObjects.Add(obj);
        }
    }

    // Called automatically when another collider exits this trigger.
    private void OnTriggerExit(Collider other)
    {
        GameObject obj = other.gameObject;
        if (enteredObjects.Contains(obj))
        {
            enteredObjects.Remove(obj);
        }
    }

    // For testing, press Space to announce.
    private void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            // Example: include all directions.
            AnnounceObjects(true, true, true, true);
        }
    }

    /// <summary>
    /// Announces the objects currently in the collider.
    /// Groups objects by their y value and, for objects within the same group,
    /// includes their individual relative x/z positions (computed relative to the XR Origin's main camera).
    /// Only the directions corresponding to true booleans are included.
    /// </summary>
    /// <param name="includeForward">Include forward distances when object is in front.</param>
    /// <param name="includeBack">Include back distances when object is behind.</param>
    /// <param name="includeLeft">Include left distances when object is to the left.</param>
    /// <param name="includeRight">Include right distances when object is to the right.</param>
    public void AnnounceObjects(bool includeForward, bool includeBack, bool includeLeft, bool includeRight)
    {
        if (enteredObjects.Count == 0)
            return;

        // Group objects by their y value (rounded to 1 decimal place)
        Dictionary<float, List<GameObject>> groups = new Dictionary<float, List<GameObject>>();
        foreach (var obj in enteredObjects)
        {
            float yVal = Mathf.Round(obj.transform.position.y * 10f) / 10f;
            if (!groups.ContainsKey(yVal))
            {
                groups[yVal] = new List<GameObject>();
            }
            groups[yVal].Add(obj);
        }

        // Sort the groups by y value in ascending order.
        var sortedGroups = groups.OrderBy(kvp => kvp.Key).ToList();

        // Build a list of announcement strings—one for each y-level group.
        List<string> announcements = new List<string>();
        bool firstGroup = true;
        foreach (var group in sortedGroups)
        {
            float groupY = group.Key;
            List<GameObject> objectsInGroup = group.Value;

            // Build a list of object descriptions that include their relative x/z details.
            List<string> objectDescriptions = new List<string>();
            foreach (var obj in objectsInGroup)
            {
                string relPos = GetRelativePositionDescription(obj, includeForward, includeBack, includeLeft, includeRight);
                string desc = obj.name;
                if (!string.IsNullOrEmpty(relPos))
                {
                    desc += " (" + relPos + ")";
                }
                objectDescriptions.Add(desc);
            }
            string namesPart = string.Join(", ", objectDescriptions);

            string groupText = "";
            if (firstGroup)
            {
                // For the first group, include the collider's orientation phrase.
                groupText = namesPart + " at y value " + groupY.ToString("F1");
                string orientationPhrase = GetOrientationPhrase();
                if (!string.IsNullOrEmpty(orientationPhrase))
                {
                    int index = groupText.IndexOf(" at y value");
                    if (index != -1)
                    {
                        groupText = groupText.Insert(index, " " + orientationPhrase);
                    }
                }
                announcements.Add(groupText);
                firstGroup = false;
            }
            else
            {
                // For subsequent groups, prefix with "above it at y value ..." and then list the objects.
                groupText = "above it at y value " + groupY.ToString("F1") + " is " + namesPart;
                announcements.Add(groupText);
            }
        }

        // Call SpeakQueued for each announcement.
        foreach (var ann in announcements)
        {
            if (speaker != null)
            {
                speaker.SpeakQueued(ann);
            }
            else
            {
                Debug.LogWarning("Speaker not assigned on " + gameObject.name);
            }
        }
    }

    /// <summary>
    /// Returns an orientation phrase based on the colliderName.
    /// For example, if colliderName is "left", returns "to your left".
    /// </summary>
    private string GetOrientationPhrase()
    {
        string lowerName = colliderName.ToLower();
        if (lowerName.Contains("left"))
            return "to your left";
        else if (lowerName.Contains("right"))
            return "to your right";
        else if (lowerName.Contains("forward") || lowerName.Contains("front"))
            return "in front of you";
        else if (lowerName.Contains("back") || lowerName.Contains("behind"))
            return "behind you";
        else
            return "";
    }

    /// <summary>
    /// Computes a description of the object's relative x and z position with respect to the XR Origin's main camera.
    /// Uses dot products to determine distances in the forward/back and left/right directions.
    /// Only includes directions if the corresponding booleans are true.
    /// </summary>
    private string GetRelativePositionDescription(GameObject obj, bool includeForward, bool includeBack, bool includeLeft, bool includeRight)
    {
        if (xrCamera == null)
            return "";

        // Compute the relative position from the camera.
        Vector3 relPos = obj.transform.position - xrCamera.transform.position;
        // Determine forward/back distance.
        float forwardDist = Vector3.Dot(relPos, xrCamera.transform.forward);
        // Determine right/left distance.
        float rightDist = Vector3.Dot(relPos, xrCamera.transform.right);

        List<string> parts = new List<string>();

        if (forwardDist > 0 && includeForward)
        {
            parts.Add($"{Mathf.Abs(forwardDist):F1} meters in front of you");
        }
        else if (forwardDist < 0 && includeBack)
        {
            parts.Add($"{Mathf.Abs(forwardDist):F1} meters behind you");
        }

        if (rightDist > 0 && includeRight)
        {
            parts.Add($"{Mathf.Abs(rightDist):F1} meters to your right");
        }
        else if (rightDist < 0 && includeLeft)
        {
            parts.Add($"{Mathf.Abs(rightDist):F1} meters to your left");
        }

        return string.Join(", and ", parts);
    }
}
