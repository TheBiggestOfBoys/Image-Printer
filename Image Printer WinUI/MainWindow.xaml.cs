using Image_Printer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Image_Printer_WinUI
{
	/// <summary>
	/// An empty window that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainWindow : Window
	{
		#region File Open & Save
		/// <summary>
		/// The 'Open File' dialog parameters
		/// </summary>
		public FileOpenPicker openPicker = new()
		{
			SuggestedStartLocation = PickerLocationId.PicturesLibrary,
			// Filter for the file selection, sorting by all supported image types in the `Bitmap` class
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

		/// <summary>
		/// The 'Save File' dialog parameters
		/// </summary>
		public FileSavePicker savePicker = new()
		{
			SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
			DefaultFileExtension = ".txt"
		};

		/// <summary>
		/// The opened file from the dialog
		/// </summary>
		public StorageFile openedFile;

		/// <summary>
		/// The saved file from the dialog
		/// </summary>
		public StorageFile savedFile;
		#endregion

		/// <summary>
		/// The `ImagePrinter` object which will convert the image to ASCII
		/// </summary>
		ImagePrinter imagePrinter;

		public MainWindow()
		{
			InitializeComponent();
		}

		/// <summary>
		/// When the slider values changes, change the resolution value
		/// </summary>
		private async void ResolutionValue_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
		{
			if (openedFile != null)
			{
				imagePrinter.UpdateResolution(e.NewValue / 100);
				// Create a BitmapDecoder
				Stream stream = await openedFile.OpenStreamForReadAsync();
				IRandomAccessStream ras = stream.AsRandomAccessStream();
				BitmapDecoder decoder = await BitmapDecoder.CreateAsync(ras);

				// Create a bitmap transform
				BitmapTransform transform = new()
				{
					InterpolationMode = BitmapInterpolationMode.NearestNeighbor,
					ScaledWidth = (uint)(decoder.PixelWidth * (e.NewValue / 100)),
					ScaledHeight = (uint)(decoder.PixelHeight * (e.NewValue / 100))
				};

				// Load the bitmap & bitmap source using transform
				SoftwareBitmap bmp = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
				SoftwareBitmapSource source = new();
				await source.SetBitmapAsync(bmp);
				PreviewImage.Source = source;
			}
		}

		#region Interface Imports
		[ComImport]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
		internal interface IWindowNative
		{
			IntPtr WindowHandle { get; }
		}

		[ComImport]
		[Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		public interface IInitializeWithWindow
		{
			void Initialize(IntPtr hwnd);
		}
		#endregion

		#region Buttons
		private async void OpenButton_Click(object sender, RoutedEventArgs e)
		{
			// Get the window handle in terms of IntPtr
			IWindowNative window = this.As<IWindowNative>();
			IntPtr hwnd = window.WindowHandle;

			// Initialize openPicker with the window handle
			IInitializeWithWindow initializeWithWindowWrapper = openPicker.As<IInitializeWithWindow>();
			initializeWithWindowWrapper.Initialize(hwnd);

			openedFile = await openPicker.PickSingleFileAsync();
			if (openedFile != null)
			{
				IRandomAccessStream stream = await openedFile.OpenReadAsync();
				BitmapImage bitmapImage = new();
				await bitmapImage.SetSourceAsync(stream);
				PreviewImage.Source = bitmapImage;

				// Initialize imagePrinter
				imagePrinter = new(openedFile.Path);
				imagePrinter.UpdateResolution(ResolutionSlider.Value / 100);
				if (InvertGrayscaleBox.IsChecked == true)
					imagePrinter.ReverseGrayscale();
			}
		}

		private async void SaveButton_Click(object sender, RoutedEventArgs e)
		{
			if (PreviewImage.Source != null)
			{
				// Dropdown of file types the user can save the file as
				if (!savePicker.FileTypeChoices.ContainsKey("Plain Text"))
					savePicker.FileTypeChoices.Add("Plain Text", [".txt"]);

				// Default file name if the user does not type one in or select a file to replace
				savePicker.SuggestedFileName = imagePrinter.FileName;

				// Get the window handle in terms of IntPtr
				IWindowNative window = this.As<IWindowNative>();
				IntPtr hwnd = window.WindowHandle;

				// Initialize savePicker with the window handle
				IInitializeWithWindow initializeWithWindowWrapper = savePicker.As<IInitializeWithWindow>();
				initializeWithWindowWrapper.Initialize(hwnd);

				// Show the dialog for the user to specify the target file
				savedFile = await savePicker.PickSaveFileAsync();

				if (savedFile != null)
				{
					// Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
					CachedFileManager.DeferUpdates(savedFile);

					// Write your string to the file.
					await FileIO.WriteTextAsync(savedFile, imagePrinter.ToString());

					// Let Windows know that we're finished changing the file so the other app can update the remote version of the file.
					await CachedFileManager.CompleteUpdatesAsync(savedFile);
				}
			}
		}

		private void OpenTextFile_Click(object sender, RoutedEventArgs e)
		{
			if (savedFile != null && savedFile.Path != null)
				Process.Start("notepad.exe", savedFile.Path);
		}

		private void CopyText_Click(object sender, RoutedEventArgs e)
		{
			DataPackage dataPackage = new()
			{
				RequestedOperation = DataPackageOperation.Copy
			};
			dataPackage.SetText(imagePrinter.ToString());
			Clipboard.SetContent(dataPackage);
		}

		private void ToggleGrayScale(object sender, RoutedEventArgs e)
		{
			imagePrinter.ReverseGrayscale();
		}
		#endregion
	}
}
