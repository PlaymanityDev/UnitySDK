using Newtonsoft.Json;
using Playmanity;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class PlaymanityUI : MonoBehaviour
{
    [SerializeField] private GameObject ViewGroup;
    [SerializeField] private List<GameObject> ViewList;
    [SerializeField] private Image AdImage;
    [SerializeField] private GameObject AdImageClose;
    [SerializeField] private TextMeshProUGUI AdImageCloseCountText;
    string LastAdUrl;
    bool countadtime = false;
    float defaultadtime = 5;
    float adtime;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if(countadtime == true){
            adtime -= Time.deltaTime;
            AdImageCloseCountText.text = Mathf.Round(adtime).ToString();
            if(adtime <= 0){
                countadtime = false;
                AdImageClose.SetActive(true);
                AdImageCloseCountText.text = null;
            }
        }
    }

    public void Show()
    {
        ViewGroup.SetActive(true);
    }

    public void Hide()
    {
        ViewGroup.SetActive(false);
    }

    public void SetCurrentView(int data)
    {
        for (int i = 0; i < ViewList.Count; i++)
        {
            ViewList[i].SetActive(false);
        }
        ViewList[data].SetActive(true);
    }

    public void ShowCurrentView(int data)
    {
        ViewGroup.SetActive(true);
        for (int i = 0; i < ViewList.Count; i++)
        {
            ViewList[i].SetActive(false);
        }
        ViewList[data].SetActive(true);
    }

    public async void RequestAd()
    {
        ShowCurrentView(0);
        AdImageClose.SetActive(false);
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            response res = await playmanity.getAd("static");

            if (res.message == null)
            {
                return;
            }

            ad adTemplate = JsonConvert.DeserializeObject<ad>(res.message);
            LastAdUrl = adTemplate.click_url;
            AdImage.sprite = await LoadImage(adTemplate.url);
        }
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
                SetCurrentView(1);
                return null;
            }
            else{
                SetCurrentView(2);
                adtime = defaultadtime;
                countadtime = true;
            }

            Texture2D texture = ((DownloadHandlerTexture)www.downloadHandler).texture;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
            return sprite;
        }
    }

    public void OpenLastAdUrl(){
        Application.OpenURL(LastAdUrl);
    }
}
