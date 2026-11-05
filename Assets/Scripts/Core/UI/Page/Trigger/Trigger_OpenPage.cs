using UnityEngine;

public class Trigger_OpenPage : MonoBehaviour
{
    [SerializeField] private string _pageName;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Player player = other.GetComponent<Player>();

            if (player.thisRealPlayer)
            {
                PageManager.OpenPageByName(_pageName);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Player player = other.GetComponent<Player>();

            if (player.thisRealPlayer)
            {
                PageManager.ClosePageByName(_pageName);
            }
        }
    }
}
