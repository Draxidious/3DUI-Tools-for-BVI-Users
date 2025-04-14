using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Meta.WitAi.TTS.Utilities;
using UnityEngine.InputSystem;

public class BVICollider : MonoBehaviour
{
    // The TTSSpeaker used to announce object names.
    public TTSSpeaker speaker;

    // A name that identifies this collider (e.g., "forward", "left").
    // The orientation phrase (e.g., "in front of you") is derived from this.
    public string colliderName = "forward";

    // Tolerance (in meters) for considering two objects as part of the same vertical group.
    public float yTolerance = 0.5f;

    // List to keep track of objects currently in the trigger.
    private List<GameObject> enteredObjects = new List<GameObject>();

    // Reference to the XR Origin's main camera.
    private Camera xrCamera;

    private void Start()
    {
        // Look up the XR Origin's main camera.
        xrCamera = Camera.main;
        if (xrCamera == null)
        {
            Debug.LogWarning("Main Camera (XR Origin's camera) not found in the scene!");
        }
    }

    // Called when another collider enters this trigger.
    private void OnTriggerEnter(Collider other)
    {
        GameObject obj = other.gameObject;
        // Filter out unwanted objects.
        if (obj.name == "XR Origin (XR Rig)" || obj.name.Contains("BVICollider"))
            return;

        if (!enteredObjects.Contains(obj))
        {
            enteredObjects.Add(obj);
        }
    }

    // Called when another collider exits this trigger.
    private void OnTriggerExit(Collider other)
    {
        GameObject obj = other.gameObject;
        if (enteredObjects.Contains(obj))
        {
            enteredObjects.Remove(obj);
        }
    }

    // For testing, press Space to trigger announcements.
    private void Update()
    {
        //if (Keyboard.current.spaceKey.wasPressedThisFrame)
        //{
        //    // For example, include all directions.
        //    AnnounceObjects(true, true, true, true);
        //}
    }

    /// <summary>
    /// Iterates over all objects (sorted by y value) and announces each one individually.
    /// - If an object is not "stacked" with the previous object (i.e. its y value is not within yTolerance or its x/z position isn’t inside the previous object’s bounds), 
    ///   the announcement starts with "At y value ... there is ..."
    /// - If it is considered stacked, the announcement starts with "Above it is ..."
    /// - The first (lowest) object’s announcement also includes the collider’s orientation phrase.
    /// - The relative x/z description is appended (computed relative to the XR Origin's main camera) based on the provided booleans.
    /// </summary>
    public string AnnounceObjects(bool includeForward, bool includeBack, bool includeLeft, bool includeRight)
    {
        if (enteredObjects.Count == 0)
            return "";

        // Sort the objects by their y value (lowest first).
        List<GameObject> sortedObjects = enteredObjects.OrderBy(o => o.transform.position.y).ToList();
        GameObject previous = null;
        string fullDescription = "";

        foreach (var obj in sortedObjects)
        {
            float currentY = obj.transform.position.y;
            string relDescription = GetRelativePositionDescription(obj, includeForward, includeBack, includeLeft, includeRight);
            string message = "";

            if (previous == null)
            {
                // First object overall.
                message = $"At y value {currentY:F1}, there is {obj.name}";
                string orientation = GetOrientationPhrase();
                if (!string.IsNullOrEmpty(orientation))
                    message += " " + orientation;
                if (!string.IsNullOrEmpty(relDescription))
                    message += ", " + relDescription;
            }
            else
            {
                float previousY = previous.transform.position.y;
                if (currentY - previousY < yTolerance && IsWithinXZ(obj, previous))
                {
                    // Current object is close in y and spatially overlapping with the previous: announce as "Above it is..."
                    message = $"Above it, at y value {currentY:F1}, is a {obj.name}";
                    if (!string.IsNullOrEmpty(relDescription))
                        message += ", " + relDescription;
                }
                else
                {
                    float roundedPreviousY = Mathf.Round(previousY * 10.0f) / 10.0f;
                    float roundedCurrentY = Mathf.Round(currentY * 10.0f) / 10.0f;
                    if (roundedPreviousY == roundedCurrentY)
                    {
                        message = $"a {obj.name}";
                    }
                    else
                    {
                        // Not stacked: announce normally.
                        message = $"At y value {currentY:F1}, there is a {obj.name}";
                    }
                    if (!string.IsNullOrEmpty(relDescription))
                        message += ", " + relDescription;
                }
            }

            if (speaker != null)
            {
                Debug.Log("Announcing: " + message);
                fullDescription += message + "\n";
                //speaker.SpeakQueued(message);
            }
            else
            {
                Debug.LogWarning("Speaker not assigned on " + gameObject.name);
            }

            previous = obj;
        }
        Debug.Log("Full Description: " + fullDescription);
        return fullDescription;
    }

    /// <summary>
    /// Returns an orientation phrase based on the colliderName.
    /// For example, if colliderName contains "left", returns "to your left".
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
    /// Uses dot products to determine distances along the forward/back and left/right axes.
    /// Only includes directions for which the corresponding boolean is true.
    /// The distances are now described in centimeters.
    /// </summary>
    private string GetRelativePositionDescription(GameObject obj, bool includeForward, bool includeBack, bool includeLeft, bool includeRight)
    {
        if (xrCamera == null)
            return "";

        Vector3 relPos = obj.transform.position - xrCamera.transform.position;
        float forwardDist = Vector3.Dot(relPos, xrCamera.transform.forward);
        float rightDist = Vector3.Dot(relPos, xrCamera.transform.right);
        List<string> parts = new List<string>();

        // Multiply by 100 to convert from meters to centimeters.
        if (forwardDist > 0 && includeForward)
            parts.Add($"{(Mathf.Abs(forwardDist) * 100):F0} centimeters forward");
        else if (forwardDist < 0 && includeBack)
            parts.Add($"{(Mathf.Abs(forwardDist) * 100):F0} centimeters back");

        if (rightDist > 0 && includeRight)
            parts.Add($"{(Mathf.Abs(rightDist) * 100):F0} centimeters to your right");
        else if (rightDist < 0 && includeLeft)
            parts.Add($"{(Mathf.Abs(rightDist) * 100):F0} centimeters to your left");

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Checks if the 'current' object lies within the x/z bounds of the 'previous' object's collider.
    /// This is used to determine if the current object is stacked (and should be announced with "Above it is...").
    /// </summary>
    private bool IsWithinXZ(GameObject current, GameObject previous)
    {
        Collider prevCollider = previous.GetComponent<Collider>();
        if (prevCollider == null)
            return false;

        Vector3 currPos = current.transform.position;
        Bounds bounds = prevCollider.bounds;
        return (currPos.x >= bounds.min.x && currPos.x <= bounds.max.x &&
                currPos.z >= bounds.min.z && currPos.z <= bounds.max.z);
    }
}
