using Adverty;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuSampleScript : MonoBehaviour
{
    [SerializeField]
    private UserDataCollectorManager userDataCollectorManager;

    private void Start()
    {
        userDataCollectorManager.Closed += OnUserDataCollectorClosed;
        userDataCollectorManager.Show();
    }

    public void StartGame()
    {        
        SceneManager.LoadScene("InPlaySampleScene");
    }

    private void OnUserDataCollectorClosed()
    {
        AdvertySDK.Init(userDataCollectorManager.GeneratedUserData);
    }
}
