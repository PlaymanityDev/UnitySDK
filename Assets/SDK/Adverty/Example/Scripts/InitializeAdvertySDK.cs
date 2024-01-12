using Adverty;
using UnityEngine;

public class InitializeAdvertySDK : MonoBehaviour
{
    public Camera GameCamera;

    protected void Start()
    {
        //Set up correct GameCamera for Adverty SDK
        if(GameCamera == null)
        {
            GameCamera = Camera.main;
        }
        Adverty.AdvertySettings.SetMainCamera(GameCamera);

        //Define data and initialize Adverty SDK
        /*
        //for Android use android api key or empty string if app will not be build for that platform
        string androidAPIKey = "YOUR_ANDROID_API_KEY";
        //for iOS platform use ios api key or empty string if app will not be build for that platform
        string iosAPIKey = "YOUR_IOS_API_KEY";
        //for other platforms and editor use general api key
        string apiKey = "YOUR_API_KEY";
        AdvertySettings.Mode platform = AdvertySettings.Mode.Mobile; //define target platform (Mobile, VR, AR)
        bool restrictUserDataCollection = false; //do you disallow collect extra user data?
        UserData userData = new UserData(AgeSegment.Adult, Gender.Male); // define user as adult male
        AdvertySDK.Init(apiKey, androidAPIKey, iosAPIKey, platform, restrictUserDataCollection, userData);
        */

        //User Data update example:
        /*
        string consentString = "IAB TCF CONSENT STRING"; //put your consent string here
        UserData extendedUserData = new UserData(AgeSegment.Unknown, Gender.Unknown, consentString); //define user data with consent string
        AdvertySDK.UpdateUserData(extendedUserData); //Assign to Adverty new user data
        */

        //Use predefined data from Adverty settings window
        UserData userData = new UserData(AgeSegment.Unknown, Gender.Unknown); //define user data (user and gender are unknown)
        AdvertySDK.Init(userData);
    }
}
