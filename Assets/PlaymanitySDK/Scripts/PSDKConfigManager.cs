using UnityEngine;

namespace PlaymanitySDK
{
    public static class PSDKConfigManager
    {
        private static PSDKConfig _config;

        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            LoadConfig();
        }

        private static void LoadConfig()
        {
            _config = Resources.Load<PSDKConfig>("PSDKConfig");
            if (_config == null)
            {
                Debug.LogWarning("PSDKConfig not found! Make sure it's in a 'Resources' folder.");
            }
        }

        public static string GameUUID => _config?.gameUUID;
        public static string ServerURL => _config?.serverURL;
        
        public static int PostAuthScene => (int)(_config?.postAuthScene);
    }
}