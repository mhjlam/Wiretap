using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Wiretap.Helpers;
using Wiretap.Models;
using Wiretap.Models.Listeners;
using Wiretap.ViewModels;

namespace Wiretap.Factories
{
    /// <summary>
    /// Factory for creating UI controls and dialog content
    /// </summary>
    public static class UIControlFactory
    {
        public static FrameworkElement CreateAddListenerContent(AddListenerViewModel viewModel, FrameworkElement container)
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.MinWidth = 500;
            mainGrid.MinHeight = 400;
            mainGrid.MaxHeight = 600;

            var propertiesPanel = new StackPanel { Spacing = 15 };

            var protocolGrid = CreateProtocolSelectionGrid(viewModel, propertiesPanel);
            Grid.SetRow(protocolGrid, 0);
            mainGrid.Children.Add(protocolGrid);

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = propertiesPanel
            };
            Grid.SetRow(scrollViewer, 1);
            mainGrid.Children.Add(scrollViewer);

            // Subscribe to property changes
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AddListenerViewModel.SelectedProtocol) ||
                    e.PropertyName == nameof(AddListenerViewModel.CurrentListener))
                {
                    UpdatePropertiesPanel(propertiesPanel, viewModel);
                }
            };

            UpdatePropertiesPanel(propertiesPanel, viewModel);
            return mainGrid;
        }

        private static Grid CreateProtocolSelectionGrid(AddListenerViewModel viewModel, StackPanel propertiesPanel)
        {
            var protocolGrid = new Grid();
            protocolGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            protocolGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            protocolGrid.Margin = new Thickness(0, 0, 0, 20);

            var protocolLabel = new TextBlock
            {
                Text = "Protocol:",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            Grid.SetColumn(protocolLabel, 0);
            protocolGrid.Children.Add(protocolLabel);

            var protocolComboBox = new ComboBox
            {
                ItemsSource = viewModel.AvailableProtocols,
                SelectedItem = viewModel.SelectedProtocol,
                MinWidth = 200,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            protocolComboBox.SelectionChanged += (s, e) =>
            {
                if (protocolComboBox.SelectedItem != null)
                {
                    viewModel.SelectedProtocol = (ListenerProtocol)protocolComboBox.SelectedItem;
                    UpdatePropertiesPanel(propertiesPanel, viewModel);
                }
            };

            Grid.SetColumn(protocolComboBox, 1);
            protocolGrid.Children.Add(protocolComboBox);

            return protocolGrid;
        }

        private static void UpdatePropertiesPanel(StackPanel panel, AddListenerViewModel viewModel)
        {
            try
            {
                panel.Children.Clear();

                if (viewModel.CurrentListener == null)
                    return;

                foreach (var property in viewModel.CurrentProperties)
                {
                    var controlGrid = CreatePropertyControl(property, viewModel);
                    panel.Children.Add(controlGrid);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating properties panel: {ex}");
            }
        }

        private static Grid CreatePropertyControl(PropertyInfo property, AddListenerViewModel viewModel)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Margin = new Thickness(0, 0, 0, 10);

            // Create label
            var label = new TextBlock
            {
                Text = AddListenerViewModel.GetPropertyDisplayName(property) + ":",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = AddListenerViewModel.IsPropertyRequired(property) ?
                    Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                Margin = new Thickness(0, 0, 10, 0)
            };

            Grid.SetColumn(label, 0);
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            // Create input control
            var inputControl = CreateInputControl(property, viewModel);
            Grid.SetColumn(inputControl, 1);
            Grid.SetRow(inputControl, 0);
            grid.Children.Add(inputControl);

            // Create description if available
            var description = AddListenerViewModel.GetPropertyDescription(property);
            if (!string.IsNullOrEmpty(description))
            {
                var descriptionText = new TextBlock
                {
                    Text = description,
                    FontSize = 11,
                    Foreground = ThemeHelper.TextFillColorSecondaryBrush,
                    Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    Name = $"Description_{property.Name}_{Guid.NewGuid().ToString("N")[..8]}"
                };

                Grid.SetColumn(descriptionText, 1);
                Grid.SetRow(descriptionText, 1);
                grid.Children.Add(descriptionText);
            }

            return grid;
        }

        private static FrameworkElement CreateInputControl(PropertyInfo property, AddListenerViewModel viewModel)
        {
            var propertyType = property.PropertyType;
            var currentValue = property.GetValue(viewModel.CurrentListener);

            // Handle specific property types
            if (property.Name == "PortName" && property.DeclaringType == typeof(ComListener))
            {
                return CreateComPortComboBox(property, viewModel, currentValue);
            }

            if (property.Name == "BaudRate" && property.DeclaringType == typeof(ComListener))
            {
                return CreateBaudRateComboBox(property, viewModel, currentValue);
            }

            if (property.Name == "DataBits" && property.DeclaringType == typeof(ComListener))
            {
                return CreateDataBitsComboBox(property, viewModel, currentValue);
            }

            if (property.Name == "PipeName" && property.DeclaringType == typeof(PipeListener))
            {
                return CreatePipeNameControl(property, viewModel, currentValue);
            }

            if (property.Name == "DeviceId" && property.DeclaringType == typeof(UsbListener))
            {
                return CreateUsbDeviceControl(property, viewModel, currentValue);
            }

            // Handle generic types
            if (propertyType == typeof(string))
            {
                return CreateTextBox(property, viewModel, currentValue);
            }
            else if (propertyType == typeof(int))
            {
                return CreateNumberBox(property, viewModel, currentValue);
            }
            else if (propertyType == typeof(bool))
            {
                return CreateCheckBox(property, viewModel, currentValue);
            }
            else if (propertyType.IsEnum)
            {
                return CreateEnumComboBox(property, viewModel, currentValue, propertyType);
            }

            // Fallback
            return CreateTextBox(property, viewModel, currentValue);
        }

        private static ComboBox CreateComPortComboBox(PropertyInfo property, AddListenerViewModel viewModel, object? currentValue)
        {
            var comboBox = new ComboBox
            {
                ItemsSource = AddListenerViewModel.GetAvailableComPorts(),
                SelectedItem = currentValue?.ToString() ?? "",
                MinWidth = 150
            };

            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedItem != null)
                {
                    property.SetValue(viewModel.CurrentListener, comboBox.SelectedItem.ToString());
                }
            };

            return comboBox;
        }

        private static ComboBox CreateBaudRateComboBox(PropertyInfo property, AddListenerViewModel viewModel, object? currentValue)
        {
            var comboBox = new ComboBox
            {
                ItemsSource = AddListenerViewModel.GetValidBaudRates(),
                SelectedItem = Convert.ToInt32(currentValue ?? 9600),
                MinWidth = 150
            };

            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedItem != null)
                {
                    property.SetValue(viewModel.CurrentListener, (int)comboBox.SelectedItem);
                }
            };

            return comboBox;
        }

        private static ComboBox CreateDataBitsComboBox(PropertyInfo property, AddListenerViewModel viewModel, object? currentValue)
        {
            var comboBox = new ComboBox
            {
                ItemsSource = AddListenerViewModel.GetValidDataBits(),
                SelectedItem = Convert.ToInt32(currentValue ?? 8),
                MinWidth = 100
            };

            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedItem != null)
                {
                    property.SetValue(viewModel.CurrentListener, (int)comboBox.SelectedItem);
                }
            };

            return comboBox;
        }

        private static StackPanel CreatePipeNameControl(PropertyInfo property, AddListenerViewModel viewModel, object? currentValue)
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                MinWidth = 200
            };

            var prefixText = new TextBlock
            {
                Text = "\\\\.\\pipe\\",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ThemeHelper.TextFillColorSecondaryBrush,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
            };
            stackPanel.Children.Add(prefixText);

            var textBox = new TextBox
            {
                Text = currentValue?.ToString() ?? string.Empty,
                MinWidth = 150,
                PlaceholderText = "Enter pipe name"
            };

            textBox.TextChanged += (s, e) =>
            {
                property.SetValue(viewModel.CurrentListener, textBox.Text);
            };

            stackPanel.Children.Add(textBox);
            return stackPanel;
        }

        private static StackPanel CreateUsbDeviceControl(PropertyInfo property, AddListenerViewModel viewModel, object? currentValue)
        {
            var stackPanel = new StackPanel { Spacing = 5 };

            var devices = AddListenerViewModel.GetAvailableUsbDevices();
            var comboBox = new ComboBox
            {
                DisplayMemberPath = "Name",
                SelectedValuePath = "DeviceId",
                ItemsSource = devices,
                SelectedValue = currentValue?.ToString() ?? "",
                MinWidth = 250
            };

            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedValue != null)
                {
                    property.SetValue(viewModel.CurrentListener, comboBox.SelectedValue.ToString());
                }
            };
            stackPanel.Children.Add(comboBox);

            var errorTextBlock = new TextBlock
            {
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            stackPanel.Children.Add(errorTextBlock);

            if (devices.Count == 0)
            {
                errorTextBlock.Text = "No USB devices found or enumeration failed";
                errorTextBlock.Visibility = Visibility.Visible;
            }

            // Load devices asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    var loadedDevices = await AddListenerViewModel.GetAvailableUsbDevicesAsync();
                    
                    // Update UI on main thread - using the extension method for cleaner code
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
                    {
                        try
                        {
                            comboBox.ItemsSource = loadedDevices;

                            if (loadedDevices.Count > 0)
                            {
                                errorTextBlock.Visibility = Visibility.Collapsed;

                                if (string.IsNullOrEmpty(comboBox.SelectedValue?.ToString()))
                                {
                                    comboBox.SelectedValue = loadedDevices[0].DeviceId;
                                }
                            }
                            else
                            {
                                errorTextBlock.Text = "No USB devices found";
                                errorTextBlock.Visibility = Visibility.Visible;
                            }
                        }
                        catch
                        {
                            // Silently handle errors updating USB device list
                        }
                    });
                }
                catch
                {
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
                    {
                        errorTextBlock.Text = "USB enumeration failed";
                        errorTextBlock.Visibility = Visibility.Visible;
                    });
                }
            });

            return stackPanel;
        }

        private static TextBox CreateTextBox(PropertyInfo property, AddListenerViewModel viewModel, object? currentValue)
        {
            var textBox = new TextBox
            {
                Text = currentValue?.ToString() ?? string.Empty,
                MinWidth = 200
            };

            textBox.TextChanged += (s, e) =>
            {
                property.SetValue(viewModel.CurrentListener, textBox.Text);
            };

            return textBox;
        }

        private static NumberBox CreateNumberBox(PropertyInfo property, AddListenerViewModel viewModel, object? currentValue)
        {
            var numberBox = new NumberBox
            {
                Value = Convert.ToDouble(currentValue ?? 0),
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                MinWidth = 150
            };

            var range = AddListenerViewModel.GetPropertyRange(property);
            if (range != null)
            {
                numberBox.Minimum = Convert.ToDouble(range.Minimum);
                numberBox.Maximum = Convert.ToDouble(range.Maximum);
            }

            numberBox.ValueChanged += (s, e) =>
            {
                if (!double.IsNaN(numberBox.Value))
                {
                    property.SetValue(viewModel.CurrentListener, (int)numberBox.Value);
                }
            };

            return numberBox;
        }

        private static CheckBox CreateCheckBox(PropertyInfo property, AddListenerViewModel viewModel, object? currentValue)
        {
            var checkBox = new CheckBox
            {
                IsChecked = (bool)(currentValue ?? false)
            };

            checkBox.Checked += (s, e) => property.SetValue(viewModel.CurrentListener, true);
            checkBox.Unchecked += (s, e) => property.SetValue(viewModel.CurrentListener, false);

            return checkBox;
        }

        private static ComboBox CreateEnumComboBox(PropertyInfo property, AddListenerViewModel viewModel, object? currentValue, Type propertyType)
        {
            var comboBox = new ComboBox
            {
                ItemsSource = Enum.GetValues(propertyType),
                SelectedItem = currentValue,
                MinWidth = 150
            };

            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedItem != null)
                {
                    property.SetValue(viewModel.CurrentListener, comboBox.SelectedItem);
                }
            };

            return comboBox;
        }
    }
}
