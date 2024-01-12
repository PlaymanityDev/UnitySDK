using Adverty;
using Newtonsoft.Json.Linq;
using Playmanity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class gameManager : MonoBehaviour
{
    public Camera mainCamera;
    void Start()
    {
        UserData userData = new UserData(AgeSegment.Unknown, Gender.Unknown);

        playmanitySettings settings = playmanitySettings.Instance;
        if (settings.forceKeyOverride == "")
        {
            response response = playmanity.getKey();

            if (response.code != 200)
            {
                Debug.LogWarning(response.message);
                return;
            }

            string key = (string)JObject.Parse(response.message)["key"];
            Debug.Log(key);

            AdvertySettings.APIKey = key;
        }
        else
        {
            AdvertySettings.APIKey = settings.forceKeyOverride;
        }

        AdvertySDK.Init(userData);

        AdvertySettings.SetMainCamera(mainCamera);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
