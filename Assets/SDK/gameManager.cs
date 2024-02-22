using Newtonsoft.Json;
using Playmanity;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class gameManager : MonoBehaviour
{
    public async void showImageAd()
    {
        response res = await playmanity.getAd("static");

        if (res.message == null)
        {
            return;
        }

        ad adTemplate = JsonConvert.DeserializeObject<ad>(res.message);
        Debug.Log(adTemplate);
        //Debug.Log(adTemplate.url);

        GameObject defaultCanvas = Resources.Load<GameObject>("playmanityAdCanvas");
        //Sprite imageTexture = await LoadImage(adTemplate.url);

        GameObject adCanvas = Instantiate(defaultCanvas);

        Image image = adCanvas.transform.GetChild(0).GetChild(0).GetChild(0).gameObject.GetComponent<Image>();

        image.sprite = await LoadImage(adTemplate.url);
    }


    async Task<Sprite> LoadImage(string url)
    {
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
        {
            var asyncOperation = www.SendWebRequest();

            while (!asyncOperation.isDone)
            {
                await Task.Yield();
            }

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to load image from URL: " + url + ", error: " + www.error + ", url: " + url);
                return null;
            }

            Texture2D texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
            return sprite;
        }
    }
}

class ad
{
    public readonly int id;
    public readonly string type;
    public readonly string url;
    public readonly int advertiser;
    public readonly string click_url;

    // Constructor
    public ad(int id, string type, string url, int advertiser, string click_url)
    {
        this.id = id;
        this.type = type;
        this.url = url;
        this.advertiser = advertiser;
        this.click_url = click_url;
    }
}
