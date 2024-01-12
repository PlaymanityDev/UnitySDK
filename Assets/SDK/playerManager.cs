using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;

namespace Playmanity
{
    public class playerManager : MonoBehaviour
    {
        public static string token;
        public static string id;
        public static string[] roles;
        public static string[] beta;
        public static int expire;

        [System.Obsolete]
        public static bool validate(string token)
        {
            playmanitySettings settings = playmanitySettings.Instance;
            UnityWebRequest uwr = UnityWebRequest.Post(settings.api_base + $"/user/validate", $"{{ \"token\": {token}}}");
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
                return false;
            }
            else
            {
                return (bool)JObject.Parse(response.message)["status"];
            }
        }

        public static void insertData(string token)
        {
            playerManager.token = token;
            string payload = token.Split(".")[1];
            string paddedPayload = PadBase64Url(payload);
            byte[] decodedBytes = Convert.FromBase64String(paddedPayload);
            string decodedPayload = Encoding.UTF8.GetString(decodedBytes);

            JObject payloadJSON = JObject.Parse(decodedPayload);
            id = (string)payloadJSON["iss"];
            roles = payloadJSON["roles"].ToString().Split(',');
            id = (string)payloadJSON["iss"];

            playmanitySettings settings = playmanitySettings.Instance;
            SceneManager.LoadScene(settings.startLevel);
        }

        static string PadBase64Url(string base64Url)
        {
            int padding = (4 - base64Url.Length % 4) % 4;
            return base64Url + new string('=', padding);
        }
    }
}
