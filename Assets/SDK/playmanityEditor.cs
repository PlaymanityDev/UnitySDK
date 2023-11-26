using UnityEngine;
using UnityEditor;

namespace Playmanity
{
    public class playmanityEditor : EditorWindow
    {

        // Add menu item named "Settings" to the Playmanity menu
        [MenuItem("Playmanity/Settings")]
        public static void ShowWindow()
        {
            // Show existing window instance. If one doesn't exist, create one.
            EditorWindow.GetWindow(typeof(playmanityEditor), false, "Settings");
        }

        void OnGUI()
        {
            // API Base Input Field
            GUILayout.Label("API Base:", EditorStyles.boldLabel);
            playmanity.api_base = EditorGUILayout.TextField("API Base", playmanity.api_base);

            // Starting Scene Input Field
            playmanity.startLevel = EditorGUILayout.IntField("Start Scene Index", playmanity.startLevel);

            // Save and Close buttons
            GUILayout.Space(10);
            if (GUILayout.Button("Save"))
            {
                // You can save the settings to PlayerPrefs or a configuration file here
                // For simplicity, let's just print the values for now
                Debug.Log("API Base: " + playmanity.api_base);
                Debug.Log("Start Scene: " + playmanity.startLevel);
            }

            if (GUILayout.Button("Close"))
            {
                this.Close();
            }
        }
    }
}