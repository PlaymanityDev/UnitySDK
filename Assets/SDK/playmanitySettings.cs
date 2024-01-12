using UnityEngine;

[System.Serializable]
public class playmanitySettings : ScriptableObject
{
    public string api_base = "https://api.playmanity.net";
    public int startLevel = 1;
    public string UUID = "";
    public string forceKeyOverride = "";

    // Static property to get or create the instance
    private static playmanitySettings _instance;
    public static playmanitySettings Instance
    {
        get
        {
            if (_instance == null)
            {
                // If the instance is null, try to load it from the "settings" folder
                _instance = Resources.Load<playmanitySettings>("playmanity/playmanitySettings");

                // If it still doesn't exist, create a new instance
                if (_instance == null)
                {
                    _instance = ScriptableObject.CreateInstance<playmanitySettings>();
                    _instance.api_base = "https://api.playmanity.net";
                    _instance.startLevel = 1;
                    _instance.UUID = "";
                    _instance.forceKeyOverride = "";

                    // Save the instance as an asset in the "settings" folder
                    UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
                    UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "playmanity");
                    UnityEditor.AssetDatabase.CreateAsset(_instance, "Assets/Resources/playmanity/playmanitySettings.asset");
                    UnityEditor.AssetDatabase.SaveAssets();
                }
                else
                {
                    Debug.Log("playmanitySettings.asset loaded from settings folder.");
                }
            }

            return _instance;
        }
    }
}
