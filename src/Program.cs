namespace Wiretap
{
	public class Program
	{
		[System.STAThread]
		public static void Main()
		{
			// The MddBootstrapAutoInitializer handles WinRT initialization automatically
			Microsoft.UI.Xaml.Application.Start((p) => new App());
		}
	}
}
