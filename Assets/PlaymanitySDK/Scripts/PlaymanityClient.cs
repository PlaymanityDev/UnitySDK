using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace PlaymanitySDK
{
    /// <summary>
    /// Client for interacting with the Playmanity backend services.
    /// Handles session management and advertisement retrieval.
    /// </summary>
    public static class PlaymanityClient
    {
        #region Constants
        private const int SESSION_RETRY_DELAY_MS = 10000;
        private const int HEARTBEAT_INTERVAL_MS = 9000;
        private const string CONTENT_TYPE_JSON = "application/json";
        #endregion

        #region Events
        /// <summary>
        /// Triggered when the session state changes.
        /// </summary>
        public static event Action<bool> OnSessionStateChanged;

        /// <summary>
        /// Triggered when an error occurs during API communication.
        /// </summary>
        public static event Action<string, Exception> OnApiError;
        #endregion

        #region Properties
        private static string _authToken;
        /// <summary>
        /// Authentication token used for API requests. Setting a new token invalidates the current session.
        /// </summary>
        public static string AuthToken
        {
            get => _authToken;
            set
            {
                if (_authToken != value)
                {
                    _authToken = value;
                    // Invalidate session if token changes
                    if (IsSessionValid)
                    {
                        IsSessionValid = false;
                        OnSessionStateChanged?.Invoke(false);
                    }
                }
            }
        }

        /// <summary>
        /// Indicates whether the current session is valid.
        /// </summary>
        public static bool IsSessionValid { get; private set; }
        #endregion

        #region Private Fields
        private static readonly object _sessionLock = new object();
        private static bool _isSessionInitializing;
        private static bool _isSessionEnding;
        private static CancellationTokenSource _heartbeatCancellationSource;
        #endregion

        #region Session Management

        /// <summary>
        /// Initiates a session with the Playmanity server and starts keep-alive polling.
        /// Retries automatically if initiation fails.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when AuthToken is missing.</exception>
        public static async Task<bool> InitSessionAsync()
        {
            if (string.IsNullOrEmpty(AuthToken))
            {
                LogError("Auth token is required");
                throw new InvalidOperationException("Auth token is required for session initiation");
            }

            lock (_sessionLock)
            {
                if (_isSessionInitializing || IsSessionValid)
                {
                    Log("Session already initializing or valid, skipping initiation.");
                    return IsSessionValid;
                }
                _isSessionInitializing = true;
            }

            try
            {
                bool initiated = false;
                int attemptCount = 0;

                while (!initiated)
                {
                    attemptCount++;
                    Log($"Attempting session initiation (Attempt {attemptCount})...");

                    var initPayload = new { auth_token = AuthToken };
                    string endpoint = $"{PSDKConfigManager.ServerURL}/games/sessions/initiate";

                    ApiResponse response = await SendApiRequestAsync<ApiResponse>(endpoint, initPayload);

                    if (response != null && response.IsSuccess())
                    {
                        Log("Game session initiated successfully.");

                        // Ensure session is considered valid before starting heartbeat
                        SetSessionValid(true);
                        initiated = true;

                        // First start heartbeat which will send an immediate verification
                        StartHeartbeat();

                        // Wait a moment to ensure immediate heartbeat had time to complete
                        await Task.Delay(1000);

                        // Double check session is still valid after immediate heartbeat
                        if (!IsSessionValid)
                        {
                            LogWarning("Session was invalidated during verification. Will retry initiation.");
                            continue;
                        }
                    }
                    else
                    {
                        string errorDetails = response != null
                            ? $"Server returned failure: {response.GetErrorMessage()}"
                            : "Failed to parse server response";

                        LogWarning($"Session initiation failed: {errorDetails}. Retrying in {SESSION_RETRY_DELAY_MS / 1000} seconds...");
                        await Task.Delay(SESSION_RETRY_DELAY_MS);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError($"Exception during session initiation: {ex.Message}");
                OnApiError?.Invoke("Session initiation failed", ex);
                return false;
            }
            finally
            {
                lock (_sessionLock)
                {
                    _isSessionInitializing = false;
                }
            }
        }

        /// <summary>
        /// Starts the heartbeat process to keep the session alive.
        /// </summary>
        private static void StartHeartbeat()
        {
            // Cancel any existing heartbeat
            StopHeartbeat();

            // Create new cancellation token source
            _heartbeatCancellationSource = new CancellationTokenSource();

            // Send an immediate heartbeat to verify the session is active
            _ = SendImmediateHeartbeatAsync();

            // Start regular heartbeat in background
            _ = KeepSessionAliveAsync(_heartbeatCancellationSource.Token);
        }

        /// <summary>
        /// Sends an immediate heartbeat to verify the session is active.
        /// </summary>
        private static async Task SendImmediateHeartbeatAsync()
        {
            if (!IsSessionValid)
            {
                LogError("SendImmediateHeartbeatAsync called without a valid session!");
                return;
            }

            try
            {
                Log("Sending immediate heartbeat to verify session...");
                var keepAlivePayload = new { auth_token = AuthToken };
                string endpoint = $"{PSDKConfigManager.ServerURL}/games/sessions/heartbeat";

                ApiResponse response = await SendApiRequestAsync<ApiResponse>(endpoint, keepAlivePayload);

                if (response == null || !response.IsSuccess())
                {
                    string errorDetails = response != null
                        ? $"Server returned failure: {response.GetErrorMessage()}"
                        : "Failed to parse server response";

                    LogError($"Immediate heartbeat failed: {errorDetails}");

                    // Check if it's a "NO_ACTIVE_SESSION" error
                    if (response?.Error?.Code == "NO_ACTIVE_SESSION")
                    {
                        LogWarning("Session verification failed: Session not active on server");
                        SetSessionValid(false);

                        // Try to reinitialize
                        _ = Task.Run(async () => {
                            await Task.Delay(1000);
                            await InitSessionAsync();
                        });
                    }
                    else
                    {
                        SetSessionValid(false);
                    }
                }
                else
                {
                    Log("Session verified successfully with immediate heartbeat.");
                }
            }
            catch (Exception ex)
            {
                LogError($"Exception during immediate heartbeat: {ex.Message}");
                OnApiError?.Invoke("Immediate heartbeat failed", ex);
            }
        }

        /// <summary>
        /// Stops the heartbeat process.
        /// </summary>
        private static void StopHeartbeat()
        {
            if (_heartbeatCancellationSource != null)
            {
                if (!_heartbeatCancellationSource.IsCancellationRequested)
                {
                    _heartbeatCancellationSource.Cancel();
                }
                _heartbeatCancellationSource.Dispose();
                _heartbeatCancellationSource = null;
            }
        }

        /// <summary>
        /// Sends a keep-alive request at regular intervals to maintain the session.
        /// </summary>
        private static async Task KeepSessionAliveAsync(CancellationToken cancellationToken)
        {
            if (!IsSessionValid)
            {
                LogError("KeepSessionAliveAsync called without a valid session!");
                return;
            }

            try
            {
                while (IsSessionValid && !cancellationToken.IsCancellationRequested)
                {
                    if (_isSessionEnding)
                    {
                        Log("Session ending, stopping heartbeat.");
                        return;
                    }

                    try
                    {
                        var keepAlivePayload = new { auth_token = AuthToken };
                        string endpoint = $"{PSDKConfigManager.ServerURL}/games/sessions/heartbeat";

                        ApiResponse response = await SendApiRequestAsync<ApiResponse>(endpoint, keepAlivePayload);

                        if (response == null || !response.IsSuccess())
                        {
                            string errorDetails = response != null
                                ? $"Server returned failure: {response.GetErrorMessage()}"
                                : "Failed to parse server response";

                            LogError($"Keep-alive request failed: {errorDetails}");

                            // Check if it's a "NO_ACTIVE_SESSION" error which means we need to reinitialize
                            if (response?.Error?.Code == "NO_ACTIVE_SESSION")
                            {
                                LogWarning("Session no longer active on server, attempting to reinitialize...");
                                SetSessionValid(false);

                                // Try to reinitialize if session was lost on server side
                                _ = Task.Run(async () => {
                                    await Task.Delay(1000); // Brief delay before reinitializing
                                    await InitSessionAsync();
                                });
                            }
                            else
                            {
                                SetSessionValid(false);
                            }
                            return;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // This is expected when cancellation is requested
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogError($"Exception during heartbeat: {ex.Message}");
                        OnApiError?.Invoke("Session heartbeat failed", ex);
                        SetSessionValid(false);
                        return;
                    }

                    // Wait for the next heartbeat interval
                    try
                    {
                        await Task.Delay(HEARTBEAT_INTERVAL_MS, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // This is expected when cancellation is requested
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Unexpected exception in heartbeat loop: {ex.Message}");
                OnApiError?.Invoke("Heartbeat loop failed", ex);
                SetSessionValid(false);
            }
        }

        /// <summary>
        /// Ends the current session with the Playmanity server.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task<bool> EndSessionAsync()
        {
            if (string.IsNullOrEmpty(AuthToken))
            {
                LogError("Auth token is required for ending session");
                return false;
            }

            lock (_sessionLock)
            {
                if (_isSessionEnding || !IsSessionValid)
                {
                    Log("Session already ending or not valid, skipping end request.");
                    return !IsSessionValid;
                }
                _isSessionEnding = true;
            }

            try
            {
                // Stop heartbeat first
                StopHeartbeat();

                var endPayload = new { auth_token = AuthToken };
                string endpoint = $"{PSDKConfigManager.ServerURL}/games/sessions/end";

                ApiResponse response = await SendApiRequestAsync<ApiResponse>(endpoint, endPayload);

                if (response != null && response.IsSuccess())
                {
                    Log("Session ended successfully.");
                    SetSessionValid(false);
                    return true;
                }
                else
                {
                    string errorDetails = response != null
                        ? $"Server returned failure: {response.GetErrorMessage()}"
                        : "Failed to parse server response";

                    // Don't consider NO_ACTIVE_SESSION as an error when ending a session
                    if (response?.Error?.Code == "NO_ACTIVE_SESSION")
                    {
                        Log("Session was already inactive on server.");
                        SetSessionValid(false);
                        return true;
                    }

                    LogError($"End session request failed: {errorDetails}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogError($"Exception during session end: {ex.Message}");
                OnApiError?.Invoke("Session end failed", ex);
                return false;
            }
            finally
            {
                lock (_sessionLock)
                {
                    _isSessionEnding = false;
                }
            }
        }

        /// <summary>
        /// Updates the session valid state and triggers the appropriate event.
        /// </summary>
        private static void SetSessionValid(bool isValid)
        {
            if (IsSessionValid != isValid)
            {
                IsSessionValid = isValid;
                OnSessionStateChanged?.Invoke(isValid);
            }
        }

        #endregion

        #region Advertisements

        /// <summary>
        /// Retrieves an advertisement from the Playmanity server.
        /// </summary>
        /// <returns>An Advertisement object if successful, null otherwise.</returns>
        public static async Task<Advertisement> GetAdvertisementAsync()
        {
            if (!IsSessionValid)
            {
                LogWarning("Attempted to get advertisement without a valid session.");
                return null;
            }

            try
            {
                var payload = new
                {
                    game_uuid = PSDKConfigManager.GameUUID,
                    auth_token = AuthToken
                };

                string endpoint = $"{PSDKConfigManager.ServerURL}/advertisements";

                AdvertisementResponse response = await SendApiRequestAsync<AdvertisementResponse>(endpoint, payload);

                if (response?.Ad != null)
                {
                    return response.Ad;
                }
                else
                {
                    LogWarning("No advertisement data found in response.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogError($"Exception during advertisement fetch: {ex.Message}");
                OnApiError?.Invoke("Advertisement fetch failed", ex);
                return null;
            }
        }

        #endregion

        #region HTTP Helpers

        /// <summary>
        /// Extension method to use SendWebRequest with async/await.
        /// </summary>
        private static Task<UnityWebRequest> SendWebRequestAsync(this UnityWebRequest request, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();

            // If cancellation is requested before we start, cancel immediately
            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            var operation = request.SendWebRequest();

            // Register cancellation
            if (cancellationToken != CancellationToken.None)
            {
                cancellationToken.Register(() =>
                {
                    if (!operation.isDone)
                    {
                        request.Abort();
                        tcs.TrySetCanceled();
                    }
                });
            }

            operation.completed += _ => tcs.TrySetResult(request);
            return tcs.Task;
        }

        /// <summary>
        /// Sends an API request to the Playmanity server and deserializes the response.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response to.</typeparam>
        /// <param name="endpoint">The API endpoint.</param>
        /// <param name="payload">The request payload.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The deserialized response, or null if the request failed.</returns>
        private static async Task<T> SendApiRequestAsync<T>(
            string endpoint,
            object payload,
            CancellationToken cancellationToken = default) where T : class
        {
            string json = JsonConvert.SerializeObject(payload);

            using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", CONTENT_TYPE_JSON);
                request.timeout = 30; // 30 seconds timeout

                try
                {
                    await request.SendWebRequestAsync(cancellationToken);

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        LogError($"API request failed: {request.error}, URL: {endpoint}, Response: {request.downloadHandler?.text ?? "No response"}");
                        return null;
                    }

                    string responseText = request.downloadHandler.text;
                    Log($"API response: {responseText}");

                    try
                    {
                        // First try to handle error responses
                        if (responseText.Contains("\"error\":"))
                        {
                            var errorResponse = JsonConvert.DeserializeObject<T>(responseText);
                            return errorResponse;
                        }

                        // For simple success responses that might just have {"success":true}
                        if (typeof(T) == typeof(ApiResponse) && responseText.Contains("\"success\":true") && !responseText.Contains("\"message\":"))
                        {
                            var successResponse = (T)Activator.CreateInstance(typeof(T));
                            // Set the Success property using reflection
                            typeof(T).GetProperty("Success").SetValue(successResponse, true);
                            return successResponse;
                        }

                        // Normal deserialization
                        return JsonConvert.DeserializeObject<T>(responseText);
                    }
                    catch (JsonException ex)
                    {
                        LogError($"Failed to parse JSON response: {ex.Message}, Response: {responseText}");
                        return null;
                    }
                }
                catch (OperationCanceledException)
                {
                    LogWarning($"API request cancelled: {endpoint}");
                    throw; // Rethrow for proper async cancellation
                }
                catch (Exception ex)
                {
                    LogError($"API request exception: {ex.Message}, URL: {endpoint}");
                    return null;
                }
            }
        }

        #endregion

        #region Logging

        private static void Log(string message) => Debug.Log($"[PlaymanitySDK] {message}");
        private static void LogWarning(string message) => Debug.LogWarning($"[PlaymanitySDK] {message}");
        private static void LogError(string message) => Debug.LogError($"[PlaymanitySDK] {message}");

        #endregion

        #region Initialization and Cleanup

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Application.quitting += OnApplicationQuit;
            Log("PlaymanitySDK initialized");
        }

        private static async void OnApplicationQuit()
        {
            Log("Application quitting, ending session...");
            await EndSessionAsync();
        }

        #endregion

        #region API Models

        /// <summary>
        /// Base response class for API calls.
        /// </summary>
        [Serializable]
        public class ApiResponse
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }

            [JsonProperty("error")]
            public ErrorResponse Error { get; set; }

            /// <summary>
            /// Returns true if this response indicates success
            /// </summary>
            public bool IsSuccess()
            {
                // Success is explicitly true or there's no error object
                return Success && Error == null;
            }

            /// <summary>
            /// Gets the error message from either direct message or error object
            /// </summary>
            public string GetErrorMessage()
            {
                if (Error != null)
                {
                    return $"{Error.Code}: {Error.Message}";
                }
                return Message ?? "Unknown error";
            }
        }

        /// <summary>
        /// Error response structure
        /// </summary>
        [Serializable]
        public class ErrorResponse
        {
            [JsonProperty("code")]
            public string Code { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }
        }

        /// <summary>
        /// Response class for advertisement requests.
        /// </summary>
        [Serializable]
        public class AdvertisementResponse
        {
            [JsonProperty("ad")]
            public Advertisement Ad { get; set; }
        }

        /// <summary>
        /// Model representing an advertisement.
        /// </summary>
        [Serializable]
        public class Advertisement
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("type")]
            public int Type { get; set; }

            [JsonProperty("campaign")]
            public int Campaign { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("media")]
            public string Media { get; set; }

            [JsonProperty("isActive")]
            public bool IsActive { get; set; }
        }
        #endregion
    }
}