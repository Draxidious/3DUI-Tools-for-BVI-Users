using UnityEngine;

public class DetectCollision : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public Task2 task2;
    public Task3 task3;
    void Start()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Door")
        {
           task2.CompleteTask();
        }

        if (other.gameObject.tag == "Cannonball")
        {
            task3.CompleteTask();
        }
    }
}
