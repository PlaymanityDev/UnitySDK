using Adverty.AdUnit;
using UnityEngine;

public class DynamicCreationOfUnits : MonoBehaviour
{
    private const string IN_PLAY_UNIT_NAME = "InPlayUnit_example";
    private const string IN_MENU_UNIT_NAME = "InMenuUnit_example";

    protected void Start()
    {
        CreateInMenuUnit();
        CreateInPlayUnit();
    }

    private static GameObject CreateInPlayUnit()
    {
        InPlayUnitConfiguration config = new InPlayUnitConfiguration();
        config.ViewData.Size = 4f;
        config.ViewData.Ratio = UnitRatio.Box;
        config.Behavior.AllowAnimation = true;
        return UnitFactory.Create(config, IN_PLAY_UNIT_NAME);
    }

    private static GameObject CreateInMenuUnit()
    {
        InMenuUnitConfiguration menuConfig = new InMenuUnitConfiguration();
        menuConfig.ViewData.Size = 1f;
        menuConfig.ViewData.MatchWidthOrHeight = WidthHeight.Width;
        return UnitFactory.Create(menuConfig, IN_MENU_UNIT_NAME);
    }
}
