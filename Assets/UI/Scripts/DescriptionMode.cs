using UnityEngine;
using Oculus.Voice;       // Namespace for AppVoiceExperience
using UnityEngine.Events; // For UnityEvents
using System;             // For StringComparison
using System.Collections; // Required for Coroutines
using System.Collections.Generic; // To use List<>




// AssistanceMode script modified to attempt continuous listening with delayed reactivation
public class DescriptionMode : MonoBehaviour
{
	public RaycastAnnouncer ray;
	public ColliderBasedDescriber flashlight;

	public void raycastOn()
	{
		ray.gameObject.SetActive(true);
		flashlight.gameObject.SetActive(false);
	}
	public void flashlightOn()
	{
		flashlight.gameObject.SetActive(true);
		ray.gameObject.SetActive(false);
	}

	public void describe()
	{
		Debug.LogWarning("describe triggered");
		if(ray.gameObject.activeSelf)
		{
			
			ray.describe = true;
		}
		if (flashlight.gameObject.activeSelf)
		{
			flashlight.describe = true;
		}
	}

}
