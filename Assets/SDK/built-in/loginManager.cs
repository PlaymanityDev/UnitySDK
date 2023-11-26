using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Playmanity;
using TMPro;
using Newtonsoft.Json.Linq;

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
            playerManager.insertData(token);
            PlayerPrefs.SetString("playmanity-jwt-token", playerManager.token);
        }
    }

    [System.Obsolete]
    private void Start()
    {
        string token = PlayerPrefs.GetString("playmanity-jwt-token");
        bool valid = playerManager.validate(token);
        if (valid)
        {
            playerManager.insertData(token);
        }
        else
        {
            PlayerPrefs.DeleteKey("playmanity-jwt-token");
        }
    }
}
