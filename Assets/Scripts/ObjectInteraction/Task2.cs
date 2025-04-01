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
    public AudioSource insertKeySound;
    public AudioSource doorLockedSound;
    public AudioSource successSound;
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable doorHandle;
    public Rigidbody doorRigidbody;

    public GameObject task3;

    bool doorLocked = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(WaitAndSpeak());
        doorSocketInteractor.selectEntered.AddListener(OnObjectPlaced);
        doorHandle.selectEntered.AddListener(OnDoorHandleGrab);
        doorRigidbody.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
    }

    private IEnumerator WaitAndSpeak()
    {
        yield return new WaitForSecondsRealtime(2);
        speaker.SpeakQueued("Now, take the key and unlock the door. Make sure to open the door all the way");
        // Add code to make the speaker speak here
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void OnObjectPlaced(SelectEnterEventArgs args)
    {
        GameObject placedObject = args.interactableObject.transform.gameObject;
        if (placedObject.tag == "Key")
        {
            insertKeySound.Play();
            doorLocked = false;
            doorRigidbody.constraints = RigidbodyConstraints.None;
        }
    }

    private void OnDoorHandleGrab(SelectEnterEventArgs args)
    {
        if (doorLocked)
        {
            doorLockedSound.Play();
            return;
        }
    }

    public void CompleteTask()
    {
        successSound.Play();
        speaker.SpeakQueued("Great work!");
        this.gameObject.SetActive(false);
        task3.SetActive(true);

        GameObject[] objects = GameObject.FindGameObjectsWithTag("Door");
        foreach (GameObject obj in objects)
        {
            obj.SetActive(false);
        }

        

    }
}
