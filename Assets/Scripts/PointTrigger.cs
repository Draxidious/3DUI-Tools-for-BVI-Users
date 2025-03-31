using UnityEngine;

public class PointTrigger : MonoBehaviour
{
    public string pointID; // Set to "B", "C", or "D"
    private NavigationTaskManager taskManager;

    void Start()
    {
        taskManager = FindFirstObjectByType<NavigationTaskManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            taskManager.OnPointReached(pointID);
        }
    }
}
