using System.Collections;
using UnityEngine;

public class NavigationTaskManager : MonoBehaviour
{
    public AudioSource arrivalSound;
    public OfflineTTS tts;

    private bool[] pointsReached = new bool[4]; // A = 0, B = 1, C = 2, D = 3

    void Start()
    {
        //tts = GetComponent<OfflineTTS>();
        pointsReached[0] = true; // Start at Point A
        StartCoroutine(WelcomeMessage());
    }

    private IEnumerator WelcomeMessage()
    {
        yield return new WaitForSeconds(2f);
        tts.Speak("Welcome to the navigation task. You are at point A. Move to points B, C, and D.");
    }

    public void OnPointReached(string pointID)
    {
        int index = pointID switch
        {
            "B" => 1,
            "C" => 2,
            "D" => 3,
            _ => -1
        };

        if (index >= 0 && !pointsReached[index])
        {
            pointsReached[index] = true;
            tts.Speak($"You have reached point {pointID}");
            arrivalSound.Play();
        }

        if (AllPointsReached())
        {
            tts.Speak("Great work! You have reached all points.");
        }
    }

    private bool AllPointsReached()
    {
        for (int i = 1; i < pointsReached.Length; i++) // skip A
        {
            if (!pointsReached[i]) return false;
        }
        return true;
    }
}
