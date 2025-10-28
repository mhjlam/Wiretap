using Microsoft.UI.Xaml.Controls;

namespace Wiretap.Controls
{
    public sealed partial class PlusButton : CircularIconButtonBase
    {
        protected override Symbol IconSymbol => Symbol.Add;

        public PlusButton()
        {
            ToolTipService.SetToolTip(this, "Add Listener");
		}
	}
}
