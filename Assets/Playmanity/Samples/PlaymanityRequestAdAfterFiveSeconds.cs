// Example gameplay script.
// Put PlaymanityManager in your initial scene.
// Put this script in any later scene, or on any gameplay object.
// It waits 5 seconds, requests an ad, shows it, and waits for it to finish.

using System.Collections;
using System.Threading.Tasks;
using Playmanity.Unity;
using UnityEngine;

public sealed class PlaymanityRequestAdAfterFiveSeconds : MonoBehaviour
{
    [Tooltip("Seconds to wait after this object starts before requesting an ad.")]
    public float delaySeconds = 5f;

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delaySeconds));

        if (!PlaymanityManager.HasInstance)
        {
            Debug.LogError(
                "PlaymanityManager is missing. Add PlaymanityManager to a GameObject in your initial/startup scene.");
            yield break;
        }

        Task<PlaymanityAdResult> adTask = PlaymanityManager.Instance.RequestAndShowAdAsync();
        while (!adTask.IsCompleted)
            yield return null;

        if (adTask.IsFaulted)
        {
            Debug.LogException(adTask.Exception.GetBaseException());
            yield break;
        }

        if (adTask.IsCanceled)
        {
            Debug.Log("Playmanity ad request was cancelled.");
            yield break;
        }

        PlaymanityAdResult result = adTask.Result;
        Debug.Log(
            "Playmanity ad finished. reason=" + result.reason +
            " clicked=" + result.clicked +
            " impressionReported=" + result.impressionReported +
            (string.IsNullOrEmpty(result.error) ? "" : " error=" + result.error));
    }
}
