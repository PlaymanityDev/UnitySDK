using UnityEngine;

namespace PlaymanitySDK
{
    [CreateAssetMenu(fileName = "PSDKConfig", menuName = "Playmanity/Config", order = 1)]
    public class PSDKConfig : ScriptableObject
    {
        public string gameUUID = "404e1b2b-de1b-4988-8cc8-94239dc482b3";
        public string serverURL = "https://app.playmanity.net/api";
        public int postAuthScene = 1;
    }
}
