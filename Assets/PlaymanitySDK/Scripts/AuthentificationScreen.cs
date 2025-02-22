using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

namespace PlaymanitySDK
{
    public class AuthenticationScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text infoText;

        void Start()
        {
            StartCoroutine(HandleAuth());
        }

        IEnumerator HandleAuth()
        {
            infoText.text = "Authenticating...";
            string gameUUID = PSDKConfigManager.GameUUID;
            string deviceID = DeviceID.GetDeviceID();

            var jsonPayload = new
            {
                game_uuid = gameUUID,
                device_id = deviceID
            };

            string jsonString = JsonConvert.SerializeObject(jsonPayload);

            string initUrl = $"{PSDKConfigManager.ServerURL}/games/auth/initiate";

            UnityWebRequest initRequest = new UnityWebRequest(initUrl, "POST");
            byte[] jsonData = System.Text.Encoding.UTF8.GetBytes(jsonString);
            initRequest.uploadHandler = new UploadHandlerRaw(jsonData);
            initRequest.downloadHandler = new DownloadHandlerBuffer();
            initRequest.SetRequestHeader("Content-Type", "application/json");

            yield return initRequest.SendWebRequest();
            infoText.text = "Processing...";

            if (initRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Initial auth error: " + initRequest.error + " | " + initRequest.downloadHandler.text);
                infoText.text = "Error: " + initRequest.error;
                yield break;
            }

            GameAuthInitResponse initResponse;
            try
            {
                initResponse = JsonConvert.DeserializeObject<GameAuthInitResponse>(initRequest.downloadHandler.text);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("JSON parse error: " + ex.Message);
                infoText.text = "Error parsing server response.";
                yield break;
            }

            if (initResponse == null || string.IsNullOrEmpty(initResponse.AuthUrl) || string.IsNullOrEmpty(initResponse.AuthId))
            {
                Debug.LogError("Invalid auth initiation response.");
                infoText.text = "Invalid response from server.";
                yield break;
            }

            infoText.text = "Awaiting authorization...";
            Application.OpenURL(initResponse.AuthUrl);

            while (true)
            {
                yield return new WaitForSeconds(1);
                string statusUrl = $"{PSDKConfigManager.ServerURL}/games/auth/status/{initResponse.AuthId}";

                UnityWebRequest statusRequest = UnityWebRequest.Get(statusUrl);
                statusRequest.SetRequestHeader("Content-Type", "application/json");

                yield return statusRequest.SendWebRequest();

                if (statusRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Auth status error: " + statusRequest.error);
                    infoText.text = "Error: " + statusRequest.error;
                    continue;
                }

                GameAuthStatusResponse statusResponse;
                try
                {
                    statusResponse = JsonConvert.DeserializeObject<GameAuthStatusResponse>(statusRequest.downloadHandler.text);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("JSON parse error (status): " + ex.Message);
                    infoText.text = "Error parsing auth status.";
                    continue;
                }

                if (statusResponse == null)
                {
                    infoText.text = "Invalid status response.";
                    continue;
                }

                infoText.text = "Verifying...";

                if (statusResponse.status == AuthStatus.valid)
                {
                    infoText.text = "Enjoy!";
                    PlaymanityClient.AuthToken = statusResponse.token;
                    yield return PlaymanityClient.InitSessionAsync();
                    SceneManager.LoadSceneAsync(PSDKConfigManager.PostAuthScene);
                    yield break;
                }
                else if (statusResponse.status == AuthStatus.denied)
                {
                    infoText.text = "Authorization denied.";
                    yield break;
                }
                else
                {
                    infoText.text = statusResponse.status.ToString();
                }
            }
        }
    }

    public class GameAuthInitResponse
    {
        [JsonProperty("auth_id")]
        public string AuthId { get; set; }

        [JsonProperty("auth_url")]
        public string AuthUrl { get; set; }
    }

    public enum AuthStatus
    {
        valid,
        denied,
        unresolved
    }

    public class GameAuthStatusResponse
    {
        [JsonProperty("status")]
        public AuthStatus status { get; set; }

        [JsonProperty("token")]
        public string token { get; set; }

        [JsonProperty("error")]
        public GameAuthStatusError error { get; set; }
    }

    public class GameAuthStatusError
    {
        [JsonProperty("code")]
        public string code { get; set; }

        [JsonProperty("message")]
        public string message { get; set; }
    }
}
