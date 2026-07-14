using System;
using System.Collections.Generic;
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

using Image_Printer;

using Microsoft.Win32;

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
		private static string GenerateFilter()
		{
			Dictionary<ImageFormat, string> formats = new()
			{
				{ ImageFormat.Bmp, "*.bmp" },
				{ ImageFormat.Emf, "*.emf" },
				{ ImageFormat.Exif, "*.exif" },
				{ ImageFormat.Gif, "*.gif" },
				{ ImageFormat.Icon, "*.ico" },
				{ ImageFormat.Jpeg, "*.jpeg;*.jpg;*.jpe;*.jfif;*.jif" },
				{ ImageFormat.Png, "*.png" },
				{ ImageFormat.Tiff, "*.tiff;*.tif" },
				{ ImageFormat.Wmf, "*.wmf" }
			};

			// Generate individual filters
			string codecFilter = string.Join('|', formats.Select(format => $"{format.Key} files ({format.Value})|{format.Value}"));

			// Generate "All Picture Files" filter
			string allFilter = $"All Picture Files|{string.Join(';', formats.Values)}";

			// Add "All Files" filter
			return $"{allFilter}|{codecFilter}|All Files|*.*";
		}

		/// <summary>
		/// The 'Open File' dialog parameters
		/// </summary>
		private static readonly OpenFileDialog openImageDialog = new()
		{
			Filter = GenerateFilter(),
			Title = "Open image",
			InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
		};

		/// <summary>
		/// The 'Save File' dialog parameters for '.txt' files
		/// </summary>
		private static readonly SaveFileDialog saveTextFileDialog = new()
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
		private ImagePrinter imagePrinter = new(ImagePrinter.CreateGradient());

		public MainWindow()
		{
			InitializeComponent();
			ResetListBox();

			// Add ASCII sets
			ASCIISetPicker.Items.Clear();
			foreach (ImagePrinter.ASCIISet set in Enum.GetValues<ImagePrinter.ASCIISet>())
			{
				MenuItem item = new() { Header = set };
				item.Click += MenuItem_Click;
				_ = ASCIISetPicker.Items.Add(item);
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
				_ = ASCIIcharsBox.Items.Add(textBox);
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
				_ = DeleteObject(hBitmap);
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
			ResolutionValue.Value = Math.Round(ResolutionValue.Value, 2);
			_ = (PercentageBox?.Text = (ResolutionValue.Value * 100).ToString());
			imagePrinter.UpdateResolution(ResolutionValue.Value);
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
		/// Opens the exported .txt file created by Save as text
		/// </summary>
		private void OpenTextFile_Click(object sender, RoutedEventArgs e)
		{
			string path = ExportPath.Text;
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				_ = MessageBox.Show(
					"Save as text first, then open the exported file.",
					"Image Printer",
					MessageBoxButton.OK,
					MessageBoxImage.Information);
				return;
			}

			_ = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
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
			_ = ASCIIcharsBox.Items.Add(textBox);
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
			MenuItem? item = sender as MenuItem;
			imagePrinter.SetASCIIGrayscaleChars(Enum.Parse<ImagePrinter.ASCIISet>(item.Header.ToString()));
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

		private void PercentageBox_LostFocus(object sender, RoutedEventArgs e)
		{
			if (double.TryParse(PercentageBox.Text, out double value))
			{
				ResolutionValue.Value = Math.Round(value / 100, 2);
				imagePrinter.UpdateResolution(ResolutionValue.Value);
				ImagePreview.Source = CreatePreviewImage(imagePrinter.Picture);
			}
			else
			{
				ResolutionValue.Value = 1;
			}
		}
	}
}
