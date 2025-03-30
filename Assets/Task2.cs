using Meta.WitAi.TTS.Utilities;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public class Task2 : MonoBehaviour
{
    public TTSSpeaker speaker;
    public XRSocketInteractor doorSocketInteractor;
    public AudioSource successSound;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(WaitAndSpeak());
        doorSocketInteractor.selectEntered.AddListener(OnObjectPlaced);
    }

    private IEnumerator WaitAndSpeak()
    {
        yield return new WaitForSecondsRealtime(2);
        speaker.Speak("Now, take the key and unlock the door");
        // Add code to make the speaker speak here
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void OnObjectPlaced(SelectEnterEventArgs args)
    {
        GameObject placedObject = args.interactableObject.transform.gameObject;
      
    }
}
