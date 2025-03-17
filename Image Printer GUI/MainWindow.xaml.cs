using Image_Printer;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Image_Printer_GUI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		#region File Open & Save
		/// <summary>
		/// Generates a filter for the file selection, sorting by all supported image types in the `Bitmap` class
		/// </summary>
		/// <returns>A string which is used to filter the selectable files</returns>
		static string GenerateFilter()
		{
			string[][] formats = [
				[ImageFormat.Bmp.ToString(), "*.bmp"],
				[ImageFormat.Emf.ToString(), "*.emf"],
				[ImageFormat.Exif.ToString(), "*.exif"],
				[ImageFormat.Gif.ToString(), "*.gif"],
				[ImageFormat.Icon.ToString(), "*.ico"],
				[ImageFormat.Jpeg.ToString(), "*.jpeg;*.jpg;*.jpe;*.jfif;*.jif"],
				[ImageFormat.Png.ToString(), " *.png"],
				[ImageFormat.Tiff.ToString(), "*.tiff;*.tif"],
				[ImageFormat.Wmf.ToString(), "*.wmf"]];

			string codecFilter = string.Empty;
			foreach (string[] format in formats)
			{
				codecFilter += $"{format[0]} files ({format[1]})|{format[1]}|";
			}

			string allFilter = $"All Picture Files|{string.Join(';', formats.Select(x => x[1]))}|";

			codecFilter += "All Files|*.*";

			return allFilter + codecFilter;
		}

		/// <summary>
		/// The 'Open File' dialog parameters
		/// </summary>
		static readonly OpenFileDialog openImageDialog = new()
		{
			Filter = GenerateFilter(),
			Title = "Open image",
			InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
		};

		/// <summary>
		/// The 'Save File' dialog parameters for '.txt' files
		/// </summary>
		static readonly SaveFileDialog saveTextFileDialog = new()
		{
			Filter = "Text File|*.txt",
			Title = "Save the ASCII as a Text File",
			CheckPathExists = true,
			DefaultExt = "txt",
			InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
		};
		#endregion

		/// <summary>
		/// The `ImagePrinter` object which will convert the image to ASCII
		/// </summary>
		ImagePrinter imagePrinter = new(ImagePrinter.CreateGradient());

		public MainWindow()
		{
			InitializeComponent();
			ResetListBox();

			// Add ASCII sets
			ASCIISetPicker.Items.Clear();
			foreach (ImagePrinter.ASCIISet set in Enum.GetValues(typeof(ImagePrinter.ASCIISet)))
			{
				MenuItem item = new() { Header = set };
				item.Click += MenuItem_Click;
				ASCIISetPicker.Items.Add(item);
			}
		}

		private void ResetListBox()
		{
			ASCIIcharsBox.Items.Clear();

			foreach (char character in imagePrinter.ASCIIGrayscaleChars)
			{
				TextBox textBox = new()
				{
					FontSize = 20,
					Width = 25,
					MaxLength = 1,
					Text = character.ToString()
				};
				ASCIIcharsBox.Items.Add(textBox);
			}
		}

		/// <summary>
		/// Updates the preview image based on the resolution
		/// </summary>
		/// <param name="bitmap">The image object</param>
		/// <returns>The scaled image preview</returns>
		private static BitmapSource CreatePreviewImage(Bitmap bitmap)
		{
			// Get a handle to the Bitmap
			IntPtr hBitmap = bitmap.GetHbitmap();

			try
			{
				// Create a BitmapSource from the Bitmap
				return Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			}
			finally
			{
				// Delete the GDI bitmap object
				DeleteObject(hBitmap);
			}
		}

		[LibraryImport("gdi32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static partial bool DeleteObject(IntPtr hObject);

		/// <summary>
		/// When the slider values changes, change the resolution value
		/// </summary>
		private void ResolutionValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			imagePrinter.UpdateResolution(ResolutionValue.Value);
			if (PercentageBox != null) PercentageBox.Text = $"{ResolutionValue.Value:P0}";
			ImagePreview.Source = CreatePreviewImage(imagePrinter.Picture);
		}

		private void UpdateGrayscale(object sender, RoutedEventArgs e)
		{
			imagePrinter.ReverseGrayscale();
		}

		#region Buttons
		/// <summary>
		/// Opens the image file
		/// </summary>
		private void OpenButton_Click(object sender, RoutedEventArgs e)
		{
			// If file has been selected
			if (openImageDialog.ShowDialog() == true)
			{
				string filePath = openImageDialog.FileName;
				imagePrinter = new(filePath);
				ImagePath.Text = filePath;

				// Display the image in the preview box
				ImagePreview.Source = CreatePreviewImage(imagePrinter.Picture);

				// Reset grayscale Inversion
				ReverseGrayscale.IsChecked = false;
			}
		}

		/// <summary>
		/// Writes the ImagePrinter to a .txt file. 
		/// </summary>
		private void SaveButton_Click(object sender, RoutedEventArgs e)
		{
			saveTextFileDialog.FileName = imagePrinter.FileName;

			// If a valid path has been set
			if (saveTextFileDialog.ShowDialog() == true)
			{
				File.WriteAllText(saveTextFileDialog.FileName, imagePrinter.ToString(), Encoding.ASCII);
				ExportPath.Text = saveTextFileDialog.FileName;
			}
		}

		#region File Opening
		/// <summary>
		/// Opens the exported .txt file
		/// </summary>
		private void OpenTextFile_Click(object sender, RoutedEventArgs e)
		{
			Process.Start(new ProcessStartInfo(saveTextFileDialog.FileName) { UseShellExecute = true });
		}
		#endregion

		/// <summary>
		/// Copies the text to the clipboard
		/// </summary>
		private void CopyText_Click(object sender, RoutedEventArgs e)
		{
			Clipboard.SetText(imagePrinter.ToString());
		}

		#region ASCII Chars List
		/// <summary>
		/// Adds a character to use in the ASCII conversion
		/// </summary>
		private void AddCharacter_Click(object sender, RoutedEventArgs e)
		{
			TextBox textBox = new()
			{
				FontSize = 20,
				Width = 25,
				MaxLength = 1
			};
			ASCIIcharsBox.Items.Add(textBox);
		}

		/// <summary>
		/// Removes the last character in the ASCII list
		/// </summary>
		private void SubtractCharacter_Click(object sender, RoutedEventArgs e)
		{
			if (imagePrinter.ASCIIGrayscaleChars.Count > 0)
			{
				int index = ASCIIcharsBox.Items.Count - 1;
				ASCIIcharsBox.Items.RemoveAt(index);
				imagePrinter.ASCIIGrayscaleChars.RemoveAt(index);
			}
		}

		/// <summary>
		/// Resets the custom ASCII chars to the selected menu item
		/// </summary>
		private void DefaultASCII_Click(object sender, RoutedEventArgs e)
		{
			ResetListBox();
		}

		/// <summary>
		/// Changes the ASCII set to use
		/// </summary>
		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			MenuItem item = sender as MenuItem;
			imagePrinter.SetASCIIGrayscaleChars((ImagePrinter.ASCIISet)Enum.Parse(typeof(ImagePrinter.ASCIISet), item.Header.ToString()));
			ResetListBox();
		}

		/// <summary>
		/// Update the ASCII character to match the custom list
		/// </summary>
		private void SyncButton_Click(object sender, RoutedEventArgs e)
		{
			foreach (TextBox item in ASCIIcharsBox.Items)
			{
				imagePrinter.ASCIIGrayscaleChars.Add(item.Text[0]);
			}
		}
		#endregion
		#endregion
	}
}
