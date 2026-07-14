using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading.Tasks;

using Image_Printer;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

using WinRT;

using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;

namespace Image_Printer_WinUI
{
	/// <summary>
	/// Main window for converting images to ASCII text (feature parity with Image Printer GUI).
	/// </summary>
	public sealed partial class MainWindow : Window
	{
		#region File Open & Save
		public FileOpenPicker openPicker = new()
		{
			SuggestedStartLocation = PickerLocationId.PicturesLibrary,
			FileTypeFilter = {
				".bmp",
				".emf",
				".exif",
				".gif",
				".ico",
				".jpeg", ".jpg", ".jpe", ".jfif", ".jif",
				".png",
				".tiff", ".tif",
				".wmf"
			}
		};

		public FileSavePicker savePicker = new()
		{
			SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
			DefaultFileExtension = ".txt"
		};

		public StorageFile openedFile;
		public StorageFile savedFile;

		/// <summary>Filesystem path of the .txt last written by Save as text.</summary>
		private string lastExportedPath;
		#endregion

		private ImagePrinter imagePrinter = new(ImagePrinter.CreateGradient());

		private bool _suppressInvertToggle;
		private bool _suppressAsciiSetChange;
		private bool _suppressResolutionSync;

		public MainWindow()
		{
			InitializeComponent();
			PopulateAsciiSets();
			ResetAsciiList();
			_ = InitializePreviewAsync();
			UpdateActionAvailability();
		}

		private async Task InitializePreviewAsync()
		{
			imagePrinter.UpdateResolution(ResolutionSlider.Value / 100);
			await RefreshPreviewAsync();
			ImagePathText.Text = string.Empty;
			ExportPathText.Text = string.Empty;
		}

		private void PopulateAsciiSets()
		{
			_suppressAsciiSetChange = true;
			AsciiSetComboBox.Items.Clear();
			foreach (ImagePrinter.ASCIISet set in Enum.GetValues<ImagePrinter.ASCIISet>())
			{
				AsciiSetComboBox.Items.Add(set.ToString());
			}

			AsciiSetComboBox.SelectedItem = imagePrinter.SelectedASCIISet.ToString();
			_suppressAsciiSetChange = false;
		}

		private void ResetAsciiList()
		{
			AsciiCharsPanel.Children.Clear();
			foreach (char character in imagePrinter.ASCIIGrayscaleChars)
			{
				AsciiCharsPanel.Children.Add(CreateAsciiCharBox(character.ToString()));
			}
		}

		private static TextBox CreateAsciiCharBox(string text = "")
		{
			TextBox box = new()
			{
				Text = text,
				MaxLength = 1,
				Width = 40,
				FontSize = 20,
				FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
				HorizontalAlignment = HorizontalAlignment.Left
			};
			AutomationProperties.SetName(box, "ASCII character");
			return box;
		}

		private async Task RefreshPreviewAsync()
		{
			if (imagePrinter?.Picture is null)
			{
				return;
			}

			BitmapImage bitmapImage = await CreatePreviewImageAsync(imagePrinter.Picture);
			PreviewImage.Source = bitmapImage;
			EmptyStateText.Visibility = Visibility.Collapsed;
		}

		private static async Task<BitmapImage> CreatePreviewImageAsync(Bitmap bitmap)
		{
			MemoryStream stream = new();
			bitmap.Save(stream, DrawingImageFormat.Png);
			stream.Position = 0;

			BitmapImage bitmapImage = new();
			await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
			return bitmapImage;
		}

		#region Interface Imports
		[ComImport]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
		internal interface IWindowNative
		{
			IntPtr WindowHandle { get; }
		}

		[GeneratedComInterface]
		[Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		public partial interface IInitializeWithWindow
		{
			void Initialize(IntPtr hwnd);
		}
		#endregion

		#region Resolution
		private async void ResolutionValue_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
		{
			if (_suppressResolutionSync || imagePrinter is null || PercentageBox is null)
			{
				return;
			}

			_suppressResolutionSync = true;
			PercentageBox.Text = Math.Round(e.NewValue).ToString();
			_suppressResolutionSync = false;

			imagePrinter.UpdateResolution(e.NewValue / 100);
			await RefreshPreviewAsync();
		}

		private async void PercentageBox_LostFocus(object sender, RoutedEventArgs e)
		{
			if (imagePrinter is null)
			{
				return;
			}

			if (double.TryParse(PercentageBox.Text, out double value))
			{
				value = Math.Clamp(value, 1, 100);
				_suppressResolutionSync = true;
				ResolutionSlider.Value = Math.Round(value);
				_suppressResolutionSync = false;
				imagePrinter.UpdateResolution(value / 100);
				PercentageBox.Text = Math.Round(value).ToString();
				await RefreshPreviewAsync();
			}
			else
			{
				_suppressResolutionSync = true;
				ResolutionSlider.Value = 100;
				PercentageBox.Text = "100";
				_suppressResolutionSync = false;
				imagePrinter.UpdateResolution(1);
				await RefreshPreviewAsync();
			}
		}
		#endregion

		#region Buttons
		private async void OpenButton_Click(object sender, RoutedEventArgs e)
		{
			IWindowNative window = this.As<IWindowNative>();
			IntPtr hwnd = window.WindowHandle;

			IInitializeWithWindow initializeWithWindowWrapper = openPicker.As<IInitializeWithWindow>();
			initializeWithWindowWrapper.Initialize(hwnd);

			StorageFile file = await openPicker.PickSingleFileAsync();
			if (file is null)
			{
				return;
			}

			openedFile = file;
			ImagePathText.Text = openedFile.Path;

			ImagePrinter.ASCIISet previousSet = imagePrinter.SelectedASCIISet;
			List<char> previousChars = imagePrinter.ASCIIGrayscaleChars;

			imagePrinter = new(openedFile.Path);
			imagePrinter.SetASCIIGrayscaleChars(previousSet);
			if (previousSet == ImagePrinter.ASCIISet.Custom)
			{
				imagePrinter.ASCIIGrayscaleChars = [.. previousChars];
			}

			imagePrinter.UpdateResolution(ResolutionSlider.Value / 100);

			_suppressInvertToggle = true;
			InvertGrayscaleBox.IsChecked = false;
			_suppressInvertToggle = false;

			ResetAsciiList();
			await RefreshPreviewAsync();
			UpdateActionAvailability();
			ShowStatus("Image opened. Adjust resolution, then save or copy as text.", InfoBarSeverity.Informational);
		}

		private async void SaveButton_Click(object sender, RoutedEventArgs e)
		{
			if (imagePrinter is null)
			{
				return;
			}

			ApplyAsciiCharsFromUi();

			if (!savePicker.FileTypeChoices.ContainsKey("Plain Text"))
			{
				savePicker.FileTypeChoices.Add("Plain Text", [".txt"]);
			}

			savePicker.SuggestedFileName = imagePrinter.FileName;

			IWindowNative window = this.As<IWindowNative>();
			IntPtr hwnd = window.WindowHandle;

			IInitializeWithWindow initializeWithWindowWrapper = savePicker.As<IInitializeWithWindow>();
			initializeWithWindowWrapper.Initialize(hwnd);

			savedFile = await savePicker.PickSaveFileAsync();
			if (savedFile is null)
			{
				return;
			}

			CachedFileManager.DeferUpdates(savedFile);
			await FileIO.WriteTextAsync(savedFile, imagePrinter.ToString());
			_ = await CachedFileManager.CompleteUpdatesAsync(savedFile);

			lastExportedPath = savedFile.Path;
			ExportPathText.Text = !string.IsNullOrWhiteSpace(lastExportedPath)
				? lastExportedPath
				: savedFile.Name;
			UpdateActionAvailability();
			ShowStatus("Saved as text.", InfoBarSeverity.Success);
		}

		private async void OpenTextFile_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// Open only the .txt this app just wrote via Save as text (same as WPF GUI).
				if (savedFile is not null && await Launcher.LaunchFileAsync(savedFile))
				{
					return;
				}

				if (!string.IsNullOrWhiteSpace(lastExportedPath) && File.Exists(lastExportedPath))
				{
					StorageFile file = await StorageFile.GetFileFromPathAsync(lastExportedPath);
					if (await Launcher.LaunchFileAsync(file))
					{
						return;
					}

					_ = Process.Start(new ProcessStartInfo
					{
						FileName = lastExportedPath,
						UseShellExecute = true
					});
					return;
				}

				ShowStatus("Save as text first, then open the exported file.", InfoBarSeverity.Informational);
			}
			catch (Exception ex)
			{
				ShowStatus($"Could not open text file: {ex.Message}", InfoBarSeverity.Error);
			}
		}

		private void CopyText_Click(object sender, RoutedEventArgs e)
		{
			if (imagePrinter is null)
			{
				return;
			}

			ApplyAsciiCharsFromUi();

			DataPackage dataPackage = new()
			{
				RequestedOperation = DataPackageOperation.Copy
			};
			dataPackage.SetText(imagePrinter.ToString());
			Clipboard.SetContent(dataPackage);
			ShowStatus("Copied text to the clipboard.", InfoBarSeverity.Success);
		}

		private void ToggleGrayScale(object sender, RoutedEventArgs e)
		{
			if (_suppressInvertToggle || imagePrinter is null)
			{
				return;
			}

			imagePrinter.ReverseGrayscale();
			ResetAsciiList();
		}
		#endregion

		#region ASCII Chars List
		private void AddCharacter_Click(object sender, RoutedEventArgs e)
		{
			AsciiCharsPanel.Children.Add(CreateAsciiCharBox());
		}

		private void SubtractCharacter_Click(object sender, RoutedEventArgs e)
		{
			if (AsciiCharsPanel.Children.Count == 0)
			{
				return;
			}

			int index = AsciiCharsPanel.Children.Count - 1;
			AsciiCharsPanel.Children.RemoveAt(index);
			if (imagePrinter.ASCIIGrayscaleChars.Count > index)
			{
				imagePrinter.ASCIIGrayscaleChars.RemoveAt(index);
			}
		}

		private void DefaultAscii_Click(object sender, RoutedEventArgs e)
		{
			ResetAsciiList();
		}

		private void AsciiSetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_suppressAsciiSetChange || imagePrinter is null || AsciiSetComboBox.SelectedItem is null)
			{
				return;
			}

			if (!Enum.TryParse(AsciiSetComboBox.SelectedItem.ToString(), out ImagePrinter.ASCIISet set))
			{
				return;
			}

			imagePrinter.SetASCIIGrayscaleChars(set);
			_suppressInvertToggle = true;
			InvertGrayscaleBox.IsChecked = false;
			_suppressInvertToggle = false;
			ResetAsciiList();
		}

		private void SyncButton_Click(object sender, RoutedEventArgs e)
		{
			ApplyAsciiCharsFromUi();
			ShowStatus("ASCII characters applied.", InfoBarSeverity.Success);
		}

		private void ApplyAsciiCharsFromUi()
		{
			if (imagePrinter is null)
			{
				return;
			}

			System.Collections.Generic.List<char> chars = [];
			foreach (UIElement child in AsciiCharsPanel.Children)
			{
				if (child is TextBox box && box.Text.Length > 0)
				{
					chars.Add(box.Text[0]);
				}
			}

			if (chars.Count == 0)
			{
				chars.Add(' ');
			}

			// SetASCIIGrayscaleChars(Custom) resets to [' ']; replace with the edited list afterward.
			imagePrinter.SetASCIIGrayscaleChars(ImagePrinter.ASCIISet.Custom);
			imagePrinter.ASCIIGrayscaleChars = chars;

			_suppressAsciiSetChange = true;
			AsciiSetComboBox.SelectedItem = ImagePrinter.ASCIISet.Custom.ToString();
			_suppressAsciiSetChange = false;
		}
		#endregion

		private void UpdateActionAvailability()
		{
			bool hasPrinter = imagePrinter is not null;
			bool hasExportedFile = savedFile is not null
				|| (!string.IsNullOrWhiteSpace(lastExportedPath) && File.Exists(lastExportedPath));

			SaveButton.IsEnabled = hasPrinter;
			CopyText.IsEnabled = hasPrinter;
			InvertGrayscaleBox.IsEnabled = hasPrinter;
			OpenTextFile.IsEnabled = hasExportedFile;
		}

		private void ShowStatus(string message, InfoBarSeverity severity)
		{
			StatusInfoBar.Severity = severity;
			StatusInfoBar.Title = severity switch
			{
				InfoBarSeverity.Success => "Done",
				InfoBarSeverity.Error => "Error",
				_ => "Image Printer"
			};
			StatusInfoBar.Message = message;
			StatusInfoBar.IsOpen = true;
		}
	}
}
