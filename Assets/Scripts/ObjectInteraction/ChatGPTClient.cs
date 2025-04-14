using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;

public class ChatGPTClient : MonoBehaviour
{
    [Header("OpenAI Settings")]
    private string openAIKey;
    public string model = "gpt-4"; // or "gpt-3.5-turbo"

    public void AskChatGPT(string userMessage)
    {
        StartCoroutine(SendChatRequest(userMessage));
    }

    public void Start()
    {
        openAIKey = OpenAIAPIKey.key; // Assuming you have a static class to store your API key
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
                Debug.Log("ChatGPT says: " + response.choices[0].message.content);
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
