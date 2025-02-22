#if UNITY_STANDALONE_WIN
using System;
using System.IO;
using UnityEngine;
#endif

namespace PlaymanitySDK
{
    public class DeviceID
    {
        public static string GetDeviceID()
        {
            string deviceId = "";

#if UNITY_STANDALONE_WIN
            deviceId = GetWindowsUniqueID();
#elif UNITY_ANDROID
            deviceId = GetAndroidID();
#elif UNITY_IOS
            deviceId = GetiOSVendorID();
#endif

            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = GetOrCreateUUID();
            }

            return deviceId;
        }

#if UNITY_STANDALONE_WIN
        private static string GetWindowsUniqueID()
        {
            string filePath = Path.Combine(Application.persistentDataPath, "device_id.txt");

            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }

            string newDeviceID = Guid.NewGuid().ToString();
            File.WriteAllText(filePath, newDeviceID);
            return newDeviceID;
        }
#endif

#if UNITY_ANDROID
        private static string GetAndroidID()
        {
            try
            {
                using (AndroidJavaClass cls = new AndroidJavaClass("android.provider.Settings$Secure"))
                {
                    using (AndroidJavaObject context = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                        .GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        return cls.CallStatic<string>("getString", context.Call<AndroidJavaObject>("getContentResolver"), "android_id");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to get Android ID: " + ex.Message);
                return null;
            }
        }
#endif

#if UNITY_IOS
        [DllImport("__Internal")]
        private static extern string _GetVendorID();

        private static string GetiOSVendorID()
        {
            try
            {
                return _GetVendorID();
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to get iOS Vendor ID: " + ex.Message);
                return null;
            }
        }
#endif

        private static string GetOrCreateUUID()
        {
            string id = PlayerPrefs.GetString("device_uuid", "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("device_uuid", id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }
}
