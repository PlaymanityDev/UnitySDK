using Adverty;
using UnityEngine;
using UnityEngine.UI;

public class RewardedInMenu : MonoBehaviour
{
    private const int YOUR_REWARD = 500;//set reward as 500 coins

    public Text rewardText;//text where we show reward balance

    [HideInInspector]
    public int rewardBalance;

    public BaseUnit targetUnit;// here we add target unit (additional functionality)

    private bool canInteract;//flag when we can unit can be interactable (additional functionality)
    private bool interacting;//flag when we call interact funcion and expect logic call in OnApplicationPause method. (additional functionality)

    protected void Start()
    {
        //Required subscription
        AdvertyEvents.AdCompleted += OnRewarded;

        //Optional subscriptions. Additional handling
        AdvertyEvents.UnitActivated += OnUnitActivated;
        AdvertyEvents.UnitDeactivated += OnUnitDeactivated;
    }

    protected void OnDestroy()
    {
        AdvertyEvents.AdCompleted -= OnRewarded;
        AdvertyEvents.UnitActivated -= OnUnitActivated;
        AdvertyEvents.UnitDeactivated -= OnUnitDeactivated;
    }

    protected void OnApplicationPause(bool paused)
    {
        if(interacting)
        {
            if(paused)
            {
                OnRewardedAdOpened();
            }
            else
            {
                OnRewarededAdClosed();
                interacting = false;
            }
        }
    }

    /// <summary>
    /// Handle rewarding
    /// </summary>
    /// <param name="completedUnit">Unit, what ad was clicked and rewarded</param>
    private void OnRewarded(BaseUnit completedUnit)
    {
        Debug.Log("Ad was completed on " + completedUnit.name + ". Add reward: " + YOUR_REWARD);

        rewardBalance += YOUR_REWARD;
        rewardText.text = rewardBalance.ToString();
    }

    // ----- ADDITIONAL FUNCTIONALITY ------ //

    /// <summary>
    /// Handle unit deactivation
    /// </summary>
    /// <param name="deactivatedUnit"></param>
    private void OnUnitDeactivated(BaseUnit deactivatedUnit)
    {
        canInteract = targetUnit != deactivatedUnit;//when we deactivated, no reason to interact
    }

    /// <summary>
    /// Handle unit activation
    /// </summary>
    /// <param name="activatedUnit"></param>
    private void OnUnitActivated(BaseUnit activatedUnit)
    {
        canInteract = targetUnit == activatedUnit;//when unit activated we can interact
    }

    /// <summary>
    /// Manual interaction method with MenuUnit.
    /// To use automated interaction use MenuUnitClickHandler script on MenuUnit
    /// </summary>
    public void InteractWithTargetUnit()
    {
        if(canInteract)
        {
            if(Application.isEditor)
            {
                OnRewarded(targetUnit);//simulate rewarding in editor
            }
            else
            {
                interacting = true;
                targetUnit.Interact();//manually interact with unit on build.
            }
        }
    }

    /// <summary>
    /// Handle reward ad opening
    /// </summary>
    private static void OnRewardedAdOpened()
    {
        Debug.Log("Reward Ad was opened.");
    }

    /// <summary>
    /// Handle reward ad closingopening
    /// </summary>
    private static void OnRewarededAdClosed()
    {
        Debug.Log("Reward Ad was closed.");
    }
}
