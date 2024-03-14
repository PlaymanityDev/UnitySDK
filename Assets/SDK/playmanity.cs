using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        public static async Task<response> getAd(string type)
        {
            playmanitySettings settings = playmanitySettings.Instance;
            string token = playerManager.token;

            if (token == null)
            {
                token = PlayerPrefs.GetString("playmanity-jwt-token");
            }

            UnityWebRequest uwr = UnityWebRequest.Get(settings.api_base + $"/ads/game/{settings.UUID}/{type}");
            uwr.SetRequestHeader("Authorization", token);
            var asyncOperation = uwr.SendWebRequest();

            // Wait until the operation is done
            while (!asyncOperation.isDone)
            {
                await Task.Yield();
            }
            Debug.Log(uwr.responseCode + " " + uwr.downloadHandler.text);
            response response = new response(uwr.responseCode, uwr.downloadHandler.text);
            UnityWebRequest.Result result = uwr.result;
            

            // Check for errors
            if (result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error: {response.code}, {response.message}");
                uwr.Dispose();
                return response;
            }
            else
            {
                // Print status code
                Debug.Log($"Status Code: {response.code}");
                Debug.Log($"Response Body: {response.message}");
                uwr.Dispose();
                return response;
            }
        }
        public static response login(string username, string password)
        {
            playmanitySettings settings = playmanitySettings.Instance;
            WWWForm form = new WWWForm();
            form.AddField("username", username);
            form.AddField("password", password);
            //form.AddField("gameid", 0);

            UnityWebRequest uwr = UnityWebRequest.Post(settings.api_base + "/auth/sign-in", form);
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