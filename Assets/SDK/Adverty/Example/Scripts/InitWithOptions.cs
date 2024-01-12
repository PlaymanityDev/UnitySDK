using Adverty;
using UnityEngine;

public class InitWithOptions : MonoBehaviour
{
    private const string ANDROID_API_KEY = "Your_Android_API_Key"; //put your Android API key here
    private const string IOS_API_KEY = "Your_iOS_API_Key"; //put your iOS API key here

    public Camera GameCamera;

    protected void Start()
    {
        //Set up correct GameCamera for Adverty SDK
        if(GameCamera == null)
        {
            GameCamera = Camera.main;
        }
        AdvertySettings.SetMainCamera(GameCamera);

        //Define data and initialize Adverty SDK
        AdvertySettings.SandboxMode = false; //For production we turn off sandbox mode
        AdvertySettings.Platform = AdvertySettings.Mode.Mobile; //define target platform (Mobile, VR, AR)
        AdvertySettings.RestrictUserData = false; //do you disallow collect extra user data?
        UserData userData = new UserData(AgeSegment.Adult, Gender.Male); // define user as adult male

        if(Debug.isDebugBuild || Application.isEditor)
        {
            AdvertySettings.SandboxMode = true; //Sandbox mode enabled if we are using Development build or we are in editor
        }

        AdvertySettings.AndroidAPIKey = ANDROID_API_KEY;
        AdvertySettings.iOSAPIKey = IOS_API_KEY;

        AdvertySDK.Init(userData);
    }
}
