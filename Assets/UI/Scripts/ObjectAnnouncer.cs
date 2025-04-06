using UnityEngine;
using Meta.WitAi;
using Meta.WitAi.TTS.Utilities;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Collections;
using Meta.WitAi.TTS.Data;

public class ObjectAnnouncer : MonoBehaviour
{
	public TTSSpeaker witTTS;
	private List<string> ObjectNames = new List<string>();
	private Queue<string> speechQueue = new Queue<string>();
	private bool isSpeaking = false; // Add a flag to track speech status.

	void Start()
	{
		if (witTTS == null)
		{
			Debug.LogError("WitTextToSpeech component is not assigned to ObjectAnnouncer on " + gameObject.name);
		}
	}

	void OnTriggerEnter(Collider other)
	{
		string objectName = other.gameObject.name;

		if (witTTS != null)
		{
			objectName = other.name;
			string speechText = "You have found " + objectName;

			speechQueue.Enqueue(speechText);

			if (!isSpeaking) // Use the flag instead of witTTS.IsSpeaking
			{
				StartCoroutine(SpeakNextCoroutine()); // Use a coroutine
			}

			Debug.Log(speechText);
		}
	}

	IEnumerator SpeakNextCoroutine()
	{
		isSpeaking = true; // Set the flag to true at the start of speech.
		while (speechQueue.Count > 0)
		{
			string speechText = speechQueue.Dequeue();
			TTSSpeakerClipEvents clipEvents = new TTSSpeakerClipEvents();
			clipEvents.OnComplete.AddListener(OnSpeechComplete);
			witTTS.Speak(speechText, clipEvents);

			// Wait until the speech is complete.
			while (witTTS.IsSpeaking)
			{
				yield return null;
			}
		}
		isSpeaking = false; // Set the flag to false when the queue is empty.
	}

	void OnSpeechComplete(TTSSpeaker speaker, TTSClipData clipData)
	{
		// No need to call SpeakNext here, the coroutine handles it.
	}
}