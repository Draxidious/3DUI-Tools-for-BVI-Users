using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;
using Meta.WitAi.TTS.Utilities;

public class ChatGPTClient : MonoBehaviour
{
    // The TTSSpeaker used to announce object names.
    public TTSSpeaker speaker;

    [Header("OpenAI Settings")]
    private string openAIKey;
    public string model = "gpt-4"; // or "gpt-3.5-turbo"

    public void AskChatGPT(string userMessage)
    {
        StartCoroutine(SendChatRequest(userMessage));
    }

    public string ColliderPrompt()
    {
        return "You are a tool for a BVI user using VR in Unity. There is a collider attached to the VR rig that intersects with objects in a scene." +
            " It prints out the name of the object, as well as its x & y coordinates. " +
            "It also specifies how far forward/backward and left/right items are from the user. " +
            "It also groups items, so if items are directly above others, it says things like \"item 1 at y value 1.0, above it, " +
            "at y value 1.1 is item 2\". This indicates that item 2 is ontop of item 1. \r\n\r\n" +
            "Given this description of objects in the scene detected by the collider, " +
            "I want you to provide a general description of what the room has. The goal is to reduce cognitive load for the BVI user. " +
            "There can be a lot of items, and it can be intense to have to listen to all of the coordinates of the items. " +
            "You should keep the description relatively short, but it should encompass the objects in the room, and use information from " +
            "the names to formulate the situation.\r\n\r\n" +
            "For example, if a room has several weapons that are ontop of a table, you should say something like " +
            "\"You seem to be in a weapon room, with several objects placed on the table in front of you and a bit to your right/left\" " +
            "\r\n\r\nAs another example, if there is an object that says table with a book ontop and chair, " +
            "and the coordinates of the chair are near the table, you can say something like: \"There seems to be a desk for reading a book " +
            "placed on a table with a nearby chair\" \r\n\r\nThe coordinates of the objects should inform your description. Nearby objects should have " +
            "logical groupings. If a torch is on a wall, then you should say something like \"You seem to be in a torch-lit room.\"\r\n\r\n" +
            "Only give the description I ask of you. No other text. If some object seems to not make sense based on its name, such as 'Collider', " +
            "then don't mention it. If you are given less than 3 items, simply list them and their coordinates. If you recieve no items, " +
            "DO NOT make things up. Just say the following sentence word for word: Sorry looks like there isn't anything in front of you. " +
            "If you are given planes that seem to be a floor, just mention that the floor is flat, don't mention multiple planes or their locations." +
            "Now, here is the description: \n";
    }

    public void Start()
    {
        openAIKey = OpenAIAPIKey.key; // Assuming you have a static class to store your API key
        // Optionally, you can uncomment and use the AskChatGPT below:
        // AskChatGPT("Hello, ChatGPT! How are you today?");
    }

    IEnumerator SendChatRequest(string message)
    {
        string url = "https://api.openai.com/v1/chat/completions";

        var requestData = new
        {
            model = model,
            messages = new object[]
            {
                new { role = "user", content = message }
            }
        };

        string jsonData = JsonConvert.SerializeObject(requestData);
        byte[] postData = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(postData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + openAIKey);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Response: " + request.downloadHandler.text);
                var response = JsonConvert.DeserializeObject<ChatGPTResponse>(request.downloadHandler.text);
                string fullResponse = response.choices[0].message.content;

                // Split the response into sentences using period as a delimiter.
                string[] sentences = fullResponse.Split('.');

                // Queue each sentence using TTSSpeaker.SpeakQueued
                foreach (string sentence in sentences)
                {
                    string trimmedSentence = sentence.Trim();
                    if (!string.IsNullOrEmpty(trimmedSentence))
                    {
                        // Append the period back for natural speech cadence.
                        speaker.SpeakQueued(trimmedSentence + ".");
                    }
                }
            }
            else
            {
                Debug.LogError("Error: " + request.error);
            }
        }
    }
}

[System.Serializable]
public class ChatGPTResponse
{
    public Choice[] choices;

    [System.Serializable]
    public class Choice
    {
        public Message message;
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }
}
