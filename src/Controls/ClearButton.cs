using Microsoft.UI.Xaml.Controls;

namespace Wiretap.Controls
{
    public sealed partial class ClearButton : CircularIconButtonBase
    {
        protected override Symbol IconSymbol => Symbol.Delete;

        public ClearButton()
        {
            ToolTipService.SetToolTip(this, "Clear");
		}
	}
}
