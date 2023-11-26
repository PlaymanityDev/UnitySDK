using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Playmanity;
using TMPro;
using Newtonsoft.Json.Linq;
using System;
using System.Text;

public class loginManager : MonoBehaviour
{
    string username, password;
    public TMP_Text text;

    public void set_username(string value)
    {
        username = value;
    }

    public void set_password(string value)
    {
        password = value;
    }
    public void login()
    {
        response response = playmanity.login(username, password);

        if (response.code == 403 || response.code == 404)
        {
            text.text = (string)JObject.Parse(response.message)["error"];
        }

        if (response.code == 500 || response.code == 502)
        {
            text.text = "Unknown error";
        }

        if (response.code == 200)
        {
            text.text = "Successfully logged in!";
            string token = (string)JObject.Parse(response.message)["value"];
            playerManager.token = token;
            PlayerPrefs.SetString("playmanity-jwt-token", playerManager.token);

            string payload = token.Split(".")[1];
            string paddedPayload = PadBase64Url(payload);
            byte[] decodedBytes = Convert.FromBase64String(paddedPayload);
            string decodedPayload = Encoding.UTF8.GetString(decodedBytes);

            Debug.Log(decodedPayload);
        }
    }

    [System.Obsolete]
    private void Start()
    {
        string token = PlayerPrefs.GetString("playmanity-jwt-token");
        bool valid = playerManager.validate(token);
        if (valid)
        {

        }
        else
        {
            PlayerPrefs.DeleteKey("playmanity-jwt-token");
        }
    }
    static string PadBase64Url(string base64Url)
    {
        int padding = (4 - base64Url.Length % 4) % 4;
        return base64Url + new string('=', padding);
    }
}
