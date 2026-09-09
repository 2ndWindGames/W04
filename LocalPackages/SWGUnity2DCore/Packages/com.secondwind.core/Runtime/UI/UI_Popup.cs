using System.Collections;
using System.Collections.Generic;
using SWGUnity2DCore.Manager;
using SWGUnity2DCore.UI;
using UnityEngine;

public class UI_Popup : UI_Base
{
    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        CoreServices.UI.SetCanvas(gameObject, true);
        return true;
    }

    public virtual void ClosePopupUI()
    {
        CoreServices.UI.ClosePopupUI(this);
    }
}
