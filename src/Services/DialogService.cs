using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Wiretap.Factories;
using Wiretap.Models;
using Wiretap.ViewModels;

namespace Wiretap.Services
{
	public class DialogService : IDialogService
	{
		public async Task<ContentDialogResult> ShowAddListenerDialogAsync(XamlRoot xamlRoot)
		{
			var viewModel = new AddListenerViewModel();
			var dialogContent = UIControlFactory.CreateAddListenerContent(viewModel, null!);

			var dialog = new ContentDialog
			{
				Title = "Add New Listener",
				Content = dialogContent,
				PrimaryButtonText = "Add",
				CloseButtonText = "Cancel",
				DefaultButton = ContentDialogButton.Primary,
				XamlRoot = xamlRoot,
				IsPrimaryButtonEnabled = viewModel.IsValid
			};

			viewModel.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == nameof(AddListenerViewModel.IsValid))
				{
					dialog.IsPrimaryButtonEnabled = viewModel.IsValid;
				}
			};

			var result = await dialog.ShowAsync().AsTask().ConfigureAwait(false);

			// Store the result listener for retrieval
			if (result == ContentDialogResult.Primary && viewModel.CurrentListener != null)
			{
				LastCreatedListener = viewModel.CurrentListener;
			}
			else
			{
				LastCreatedListener = null;
			}

			return result;
		}

		public async Task<ContentDialogResult> ShowRemoveListenerConfirmationAsync(string listenerName, XamlRoot xamlRoot)
		{
			var dialog = new ContentDialog()
			{
				Title = "Remove Listener",
				Content = $"Are you sure you want to remove the listener '{listenerName}'?",
				PrimaryButtonText = "Remove",
				CloseButtonText = "Cancel",
				DefaultButton = ContentDialogButton.Close,
				XamlRoot = xamlRoot
			};

			return await dialog.ShowAsync().AsTask().ConfigureAwait(false);
		}

		public async Task<bool> ShowSettingsDialogAsync(Window owner)
		{
			try
			{
				var settingsService = ServiceContainer.Instance.SettingsService;
				if (settingsService == null) return false;

				// Load settings on background thread
				var currentSettings = await settingsService.LoadSettingsAsync().ConfigureAwait(false);

				// Use TaskCompletionSource to properly marshal dialog to UI thread
				var tcs = new TaskCompletionSource<ContentDialogResult>();

				owner.DispatcherQueue.TryEnqueue(async () =>
				{
					try
					{
						var settingsContent = CreateSettingsDialogContent(currentSettings);

						var dialog = new ContentDialog()
						{
							Title = "Settings",
							Content = settingsContent,
							PrimaryButtonText = "Save",
							CloseButtonText = "Cancel",
							DefaultButton = ContentDialogButton.Primary,
							XamlRoot = owner.Content.XamlRoot
						};

						// Show dialog on UI thread
						var result = await dialog.ShowAsync();
						tcs.SetResult(result);
					}
					catch (Exception ex)
					{
						tcs.SetException(ex);
					}
				});

				var dialogResult = await tcs.Task;

				if (dialogResult == ContentDialogResult.Primary)
				{
					// Save settings on background thread
					await settingsService.SaveSettingsAsync(currentSettings).ConfigureAwait(false);
					return true;
				}

				return false;
			}
			catch
			{
				return false;
			}
		}

		private FrameworkElement CreateSettingsDialogContent(AppSettings settings)
		{
			var mainGrid = new Grid
			{
				MinWidth = 400,
				MinHeight = 150
			};

			var stackPanel = new StackPanel { Spacing = 20, Margin = new Thickness(20) };

			// Connection Status Section
			var connectionSection = new StackPanel { Spacing = 10 };

			var connectionCheckBox = new CheckBox
			{
				Content = "Show connection status messages",
				IsChecked = settings.Listeners.ShowConnectionStatus
			};

			connectionCheckBox.Checked += (s, e) => settings.Listeners.ShowConnectionStatus = true;
			connectionCheckBox.Unchecked += (s, e) => settings.Listeners.ShowConnectionStatus = false;

			connectionSection.Children.Add(connectionCheckBox);

			var connectionDescription = new TextBlock
			{
				Text = "When enabled, displays messages when clients connect or disconnect from TCP, Named Pipe, and COM listeners.",
				FontSize = 12,
				Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(20, 5, 0, 0)
			};

			connectionSection.Children.Add(connectionDescription);
			stackPanel.Children.Add(connectionSection);
			mainGrid.Children.Add(stackPanel);

			return mainGrid;
		}

		public BaseListener? LastCreatedListener { get; private set; }
	}
}
