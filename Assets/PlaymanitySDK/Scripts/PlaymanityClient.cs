using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;

namespace PlaymanitySDK
{
    public static class PlaymanityClient
    {
        public static string AuthToken;
        public static bool IsSessionValid = false;
        private static bool isSessionInitializing = false;
        private static bool isSessionEnding = false;

        /// <summary>
        /// Initiates a session and then starts keep-alive polling.
        /// Retries every 10 seconds if initiation fails.
        /// </summary>
        public static async Task InitSessionAsync()
        {
            if (string.IsNullOrEmpty(AuthToken))
            {
                Debug.LogError("Auth token is required");
                return;
            }

            if (isSessionInitializing || IsSessionValid)
            {
                Debug.Log("Session already initializing or valid, skipping initiation.");
                return;
            }

            isSessionInitializing = true;
            bool initiated = false;

            while (!initiated)
            {
                var initPayload = new { auth = AuthToken };
                string initJson = JsonConvert.SerializeObject(initPayload);

                using (UnityWebRequest request = new UnityWebRequest($"{PSDKConfigManager.ServerURL}/games/sessions/initiate", "POST"))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(initJson);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");

                    Debug.Log("Sending session initiation request...");
                    await request.SendWebRequestAsync();
                    Debug.Log("Session initiation request completed.");

                    string responseText = request.downloadHandler.text;
                    if (request.result == UnityWebRequest.Result.Success && responseText.Contains("\"success\":true"))
                    {
                        Debug.Log("Game session initiated successfully.");
                        IsSessionValid = true;
                        initiated = true;
                        _ = KeepSessionAliveAsync();
                    }
                    else
                    {
                        string errorDetails = (request.result != UnityWebRequest.Result.Success)
                            ? (request.error + " " + responseText)
                            : responseText;
                        Debug.LogWarning("Session initiation failed: " + errorDetails);
                        Debug.Log("Retrying session initiation in 10 seconds...");
                        await Task.Delay(10000);
                    }
                }
            }
            isSessionInitializing = false;
        }

        /// <summary>
        /// Sends a keep-alive request every 9 seconds. Private to ensure it's only called post-initiation.
        /// </summary>
        private static async Task KeepSessionAliveAsync()
        {
            if (!IsSessionValid)
            {
                Debug.LogError("KeepSessionAliveAsync called without a valid session! This should not happen.");
                return;
            }

            while (IsSessionValid)
            {
                if (isSessionEnding)
                {
                    Debug.Log("Session ending, stopping heartbeat.");
                    return;
                }

                var keepAlivePayload = new { authToken = AuthToken };
                string keepAliveJson = JsonConvert.SerializeObject(keepAlivePayload);

                using (UnityWebRequest request = new UnityWebRequest($"{PSDKConfigManager.ServerURL}/games/sessions/heartbeat", "POST"))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(keepAliveJson);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");

                    await request.SendWebRequestAsync();

                    string responseText = request.downloadHandler.text;
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError("Keep-alive request failed: " + request.error + " " + responseText);
                        IsSessionValid = false;
                    }
                    else if (!responseText.Contains("\"success\":true"))
                    {
                        Debug.LogError("Session keep-alive error response: " + responseText);
                        IsSessionValid = false;
                    }
                }
                await Task.Delay(9000);
            }
        }

        /// <summary>
        /// Ends the session when the application quits.
        /// </summary>
        public static async Task EndSessionAsync()
        {
            if (string.IsNullOrEmpty(AuthToken))
            {
                Debug.LogError("Auth token is required for ending session");
                return;
            }

            if (isSessionEnding || !IsSessionValid)
            {
                Debug.Log("Session already ending or not valid, skipping end request.");
                return;
            }

            isSessionEnding = true;

            var endPayload = new { auth = AuthToken };
            string endJson = JsonConvert.SerializeObject(endPayload);

            using (UnityWebRequest request = new UnityWebRequest($"{PSDKConfigManager.ServerURL}/games/sessions/end", "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(endJson);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                await request.SendWebRequestAsync();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("End session request failed: " + request.error + " " + request.downloadHandler.text);
                }
                else
                {
                    string responseText = request.downloadHandler.text;
                    if (responseText.Contains("\"success\":true"))
                    {
                        Debug.Log("Session ended successfully.");
                        IsSessionValid = false;
                    }
                    else
                    {
                        Debug.LogError("Session end error response: " + responseText);
                    }
                }
            }
            isSessionEnding = false;
        }

        public static Task<UnityWebRequest> SendWebRequestAsync(this UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            var operation = request.SendWebRequest();
            operation.completed += _ => tcs.SetResult(request);
            return tcs.Task;
        }

        public static async Task<Advertisement> GetAdvertisementAsync()
        {
            if (!IsSessionValid)
            {
                Debug.Log("Session is not valid.");
                return null;
            }

            var payload = new
            {
                gameUuid = PSDKConfigManager.GameUUID,
                authToken = AuthToken
            };

            string json = JsonConvert.SerializeObject(payload);

            using (UnityWebRequest request = new UnityWebRequest($"{PSDKConfigManager.ServerURL}/advertisements", "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                await request.SendWebRequestAsync();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Failed to fetch ad: " + request.error + " " + request.downloadHandler.text);
                    return null;
                }

                try
                {
                    Debug.Log("Raw response: " + request.downloadHandler.text);

                    var responseWrapper = JsonConvert.DeserializeObject<AdvertisementResponse>(request.downloadHandler.text);
                    if (responseWrapper?.Ad != null)
                    {
                        return responseWrapper.Ad;
                    }
                    else
                    {
                        Debug.LogError("No advertisement data found in response.");
                        return null;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("JSON parse error: " + ex.Message);
                    return null;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Application.quitting += OnApplicationQuit;
        }

        private static async void OnApplicationQuit()
        {
            await EndSessionAsync();
        }
        public class AdvertisementResponse
        {
            [JsonProperty("ad")]
            public Advertisement Ad { get; set; }
        }

        public class Advertisement
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("campaign")]
            public int Campaign { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("media")]
            public string Media { get; set; }

            [JsonProperty("isActive")]
            public bool IsActive { get; set; }
        }
    }
}