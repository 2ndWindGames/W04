using SWGUnity2DCore.Manager;

namespace SWGUnity2DCore.UI.Scene
{
	public class UI_Scene : UI_Base
	{
		public override bool Init()
		{
			if (!base.Init())
				return false;

			CoreServices.UI.SetCanvas(gameObject, false);
			return true;
		}
	}
}
