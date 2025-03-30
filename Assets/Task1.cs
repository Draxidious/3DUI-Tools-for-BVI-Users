using Meta.WitAi.TTS.Utilities;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public class Task1 : MonoBehaviour
{
    public TTSSpeaker speaker;
    public XRSocketInteractor socketInteractor;
    public AudioSource successSound;
    public GameObject task2;
    bool task1Complete = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(WaitAndSpeak());
        socketInteractor.selectEntered.AddListener(OnObjectPlaced);
    }

    private IEnumerator WaitAndSpeak()
    {
        yield return new WaitForSecondsRealtime(5);
        speaker.Speak("Hello, welcome to the Object Interaction Task. Please pick up the key on the table, and put it in the inventory slot on your waist");
        // Add code to make the speaker speak here
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void OnObjectPlaced(SelectEnterEventArgs args)
    {
        if(task1Complete) return;
        task1Complete = true;
        GameObject placedObject = args.interactableObject.transform.gameObject;
        if (placedObject.tag == "Key")
        {
            speaker.Speak("Great job!");
            successSound.Play();
            this.transform.gameObject.SetActive(false); 
            task2.SetActive(true); 
        }
    }
}
