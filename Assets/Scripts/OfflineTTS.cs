using System.Diagnostics;
using UnityEngine;

public class OfflineTTS : MonoBehaviour
{
    public void Speak(string text)
    {
        string escapedText = text.Replace("\"", "\\\"");

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = "C:\\Program Files\\eSpeak NG\\espeak-ng.exe"; // If it's not in PATH, use full path like: "C:\\Program Files\\eSpeak NG\\espeak-ng.exe"
        startInfo.Arguments = $"\"{escapedText}\"";
        startInfo.CreateNoWindow = true;
        startInfo.UseShellExecute = false;

        Process.Start(startInfo);
    }
}
