using Meta.WitAi.TTS.Utilities;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public class Task3 : MonoBehaviour
{
    public TTSSpeaker speaker;
    public AudioSource successSound;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(WaitAndSpeak());
    }

    private IEnumerator WaitAndSpeak()
    {
        yield return new WaitForSecondsRealtime(2);
        speaker.Speak("For the final task, please put the cannonball into the cannon");
        GameObject[] objects = GameObject.FindGameObjectsWithTag("Key");
        foreach (GameObject obj in objects)
        {
            obj.SetActive(false);
        }
        // Add code to make the speaker speak here
    }

    public void CompleteTask()
    {
        successSound.Play();
        speaker.Speak("Great work! You've completed all of the tasks!");
        this.gameObject.SetActive(false);

        GameObject[] objects = GameObject.FindGameObjectsWithTag("Cannonball");
        foreach (GameObject obj in objects)
        {
            obj.SetActive(false);
        }

    }


}
