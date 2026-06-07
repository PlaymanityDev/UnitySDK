using System;
using System.Threading.Tasks;
using Playmanity.Unity;
using UnityEngine;

public sealed class PlaymanityExample : MonoBehaviour
{
    [SerializeField] private string gameId = "g_usmkbnwvdttysr8z96waypr7h4";

    private PlaymanityAdController _ads;

    private async void Start()
    {
        // This example creates the controller at runtime.
        // The controller auto-loads Resources/Playmanity/PlaymanityUI unless you
        // assign a PlaymanityAdView or wire the legacy UI fields yourself.
        _ads = gameObject.AddComponent<PlaymanityAdController>();
        _ads.gameId = gameId;
        _ads.useMockInEditor = true;
        _ads.autoCreateUi = true;
        _ads.autoCloseAfterSeconds = 0f;

        Debug.Log("Playmanity HMAC self-test: " + PlaymanityClient.HmacSelfTest());

        try
        {
            PlaymanityResponse user = await _ads.Client.GetUserAsync();
            if (user.success && user.user != null)
                Debug.Log("Playmanity user: " + user.user.username);

            // Full ad flow: request, show it, wait until close/click/autoclose,
            // and report impression automatically once visible long enough.
            PlaymanityAdResult result = await _ads.RequestAndShowAdAsync();

            Debug.Log(
                "Ad finished. reason=" + result.reason +
                " clicked=" + result.clicked +
                " impressionReported=" + result.impressionReported);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
