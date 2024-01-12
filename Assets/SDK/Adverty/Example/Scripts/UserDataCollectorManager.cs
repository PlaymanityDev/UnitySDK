using Adverty;
using System;
using UnityEngine;
using UnityEngine.UI;

public class UserDataCollectorManager : MonoBehaviour
{
    private const string PRIVACY_POLICY_URL = "https://adverty.com/privacy-policy/";

    [SerializeField]
    private Dropdown ageGroupDropdown;

    [SerializeField]
    private Dropdown genderDropdown;

    [SerializeField]
    private Button privacyPolicyButton;

    [SerializeField]
    private Button acceptButton;

    [SerializeField]
    private Button disagreeButton;

    public UserData GeneratedUserData { get; private set; }
    public event Action Closed;

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);

        if(Closed != null)
        {
            Closed();
        }
    }

    private void Awake()
    {
        GeneratedUserData = null;
        privacyPolicyButton.onClick.AddListener(OnPrivacyPolicyLinkClicked);
        acceptButton.onClick.AddListener(OnAccepted);
        disagreeButton.onClick.AddListener(OnDisagreed);
    }

    private void OnDisagreed()
    {
        AdvertySettings.RestrictUserData = true;
        GeneratedUserData = new UserData(AgeSegment.Unknown, Gender.Unknown);
        Hide();
    }

    private void OnAccepted()
    {
        AdvertySettings.RestrictUserData = false;
        GeneratedUserData = new UserData((AgeSegment)(ageGroupDropdown.value + 1), (Gender)(genderDropdown.value + 1));
        Hide();
    }

    private void OnPrivacyPolicyLinkClicked()
    {
        Application.OpenURL(PRIVACY_POLICY_URL);
    }
}
