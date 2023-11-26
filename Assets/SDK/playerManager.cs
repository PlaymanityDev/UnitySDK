using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;

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
            // NOT IMPLEMENTED.
            return true;
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
            expire = (int)payloadJSON["exp"];

            SceneManager.LoadScene(playmanity.startLevel);
        }

        static string PadBase64Url(string base64Url)
        {
            int padding = (4 - base64Url.Length % 4) % 4;
            return base64Url + new string('=', padding);
        }
    }
}