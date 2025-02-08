using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace PlaymanitySDK
{
    public static class PlaymanityClient
    {
        public static string AuthToken;

        public static bool IsSessionValid = false;

        /// <summary>
        /// Initiates a session and then starts keep‐alive polling.
        /// Retries every 10 seconds if initiation fails.
        /// </summary>
        public static async Task InitSessionAsync()
        {
            if (string.IsNullOrEmpty(AuthToken))
            {
                Debug.LogError("Auth token is required");
                return;
            }

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

                    await request.SendWebRequestAsync();

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
                        Debug.LogError("Session initiation failed: " + errorDetails);

                        Debug.Log("Retrying session initiation in 10 seconds...");
                        await Task.Delay(10000);
                    }
                }
            }
        }

        /// <summary>
        /// Sends a keep-alive request every 9 seconds.
        /// </summary>
        public static async Task KeepSessionAliveAsync()
        {
            while (IsSessionValid)
            {
                await Task.Delay(9000);
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
                    }
                    else if (!responseText.Contains("\"success\":true"))
                    {
                        Debug.LogError("Session keep-alive error response: " + responseText);
                    }
                }
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
                    Debug.LogError("End session request failed: " + request.error);
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
        }

        public static Task<UnityWebRequest> SendWebRequestAsync(this UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            var operation = request.SendWebRequest();
            operation.completed += _ => tcs.SetResult(request);
            return tcs.Task;
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
    }
}
