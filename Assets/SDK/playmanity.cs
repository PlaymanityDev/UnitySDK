using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Playmanity {

    public class response
    {
        public long code;
        public string message;

        public response(long code, string message)
        {
            this.code = code;
            this.message = message;
        }
    }

    public class playmanity : MonoBehaviour
    {
        public static string api_base = "https://api.playmanity.net";
        public static int startLevel = 1;

        public static response login(string username, string password)
        {
            WWWForm form = new WWWForm();
            form.AddField("username", username);
            form.AddField("password", password);
            form.AddField("gameid", 0);

            UnityWebRequest uwr = UnityWebRequest.Post(api_base + "/sign-in", form);
            uwr.SendWebRequest();


            while (!uwr.isDone)
            {

            }
            response response = new response(uwr.responseCode, uwr.downloadHandler.text);
            UnityWebRequest.Result result = uwr.result;
            uwr.Dispose();
            // Check for errors
            if (result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error: {response.code}, {response.message}");
                return response;
            }
            else
            {
                // Print status code
                Debug.Log($"Status Code: {response.code}");
                Debug.Log($"Response Body: {response.message}");
                return response;
            }
        }
    }
}