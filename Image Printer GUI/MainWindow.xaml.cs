using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

using Image_Printer;
using Image_Printer.Video;

using Microsoft.Win32;

namespace Image_Printer_GUI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		/// <summary>Filesystem path of the PDF last written by Save as PDF.</summary>
		private string? lastExportedPdfPath;

		private FrameScrubSource? _frameScrub;
		private int _currentFrameIndex;
		private int _frameLoadVersion;
		private bool _suppressFrameScrub;
		private bool _suppressResolutionSync;

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
			Title = "Open image(s)",
			Multiselect = true,
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

			_suppressResolutionSync = true;
			imagePrinter.UpdateResolution(ResolutionValue.Value);
			PercentageBox.Text = Math.Round(ResolutionValue.Value * 100).ToString();
			_suppressResolutionSync = false;
			ImagePreview.Source = CreatePreviewImage(imagePrinter.Picture);
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
			// ValueChanged fires while XAML is still loading (before named controls exist).
			if (_suppressResolutionSync || !IsLoaded || imagePrinter?.Picture is null || ImagePreview is null)
			{
				return;
			}

			_suppressResolutionSync = true;
			ResolutionValue.Value = Math.Round(ResolutionValue.Value, 2);
			_ = PercentageBox?.Text = Math.Round(ResolutionValue.Value * 100).ToString();
			_suppressResolutionSync = false;

			imagePrinter.UpdateResolution(ResolutionValue.Value);
			ImagePreview.Source = CreatePreviewImage(imagePrinter.Picture);
		}

		private void UpdateGrayscale(object sender, RoutedEventArgs e)
		{
			imagePrinter.ReverseGrayscale();
		}

		#region Buttons
		/// <summary>
		/// Opens one or more image files (multi-select enables frame scrubbing).
		/// </summary>
		private async void OpenButton_Click(object sender, RoutedEventArgs e)
		{
			if (openImageDialog.ShowDialog() != true || openImageDialog.FileNames.Length == 0)
			{
				return;
			}

			string[] paths = openImageDialog.FileNames;
			try
			{
				if (paths.Length == 1 && !FrameScrubSource.IsGifPath(paths[0]))
				{
					ClearFrameScrub();
					imagePrinter = new(paths[0]);
					ImagePath.Text = paths[0];
					ImagePreview.Source = CreatePreviewImage(imagePrinter.Picture);
					ReverseGrayscale.IsChecked = false;
					return;
				}

				FrameScrubSource scrub = FrameScrubSource.FromImages(paths);
				if (scrub.FrameCount <= 1 && scrub.Kind == FrameScrubKind.Images)
				{
					scrub.Dispose();
					ClearFrameScrub();
					imagePrinter = new(paths[0]);
					ImagePath.Text = paths[0];
					ImagePreview.Source = CreatePreviewImage(imagePrinter.Picture);
					ReverseGrayscale.IsChecked = false;
					return;
				}

				await BeginFrameScrubAsync(scrub, startIndex: 0);
			}
			catch (Exception ex)
			{
				_ = MessageBox.Show(ex.Message, "Could not open images", MessageBoxButton.OK, MessageBoxImage.Error);
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

		private void SavePdfButton_Click(object sender, RoutedEventArgs e)
		{
			SaveFileDialog pdfDialog = new()
			{
				Filter = "PDF|*.pdf",
				Title = "Save as PDF",
				FileName = Path.ChangeExtension(imagePrinter.FileName, ".pdf"),
				DefaultExt = "pdf",
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
			};
			if (pdfDialog.ShowDialog() != true)
			{
				return;
			}

			try
			{
				AsciiPdfExportResult result = AsciiPdfExporter.Save(imagePrinter.ToString(), pdfDialog.FileName);
				lastExportedPdfPath = pdfDialog.FileName;
				OpenPdfButton.IsEnabled = true;
				string orientation = result.IsLandscape ? "landscape" : "portrait";
				_ = MessageBox.Show(
					$"PDF saved ({orientation}, {result.FontSizePt:0.##}pt, {result.ScalePercent:0.#}% of 12pt).",
					"Image Printer",
					MessageBoxButton.OK,
					MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				_ = MessageBox.Show(ex.Message, "PDF export failed", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void OpenPdfButton_Click(object sender, RoutedEventArgs e)
		{
			string? path = lastExportedPdfPath;
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				_ = MessageBox.Show(
					"Save as PDF first, then open the exported file.",
					"Image Printer",
					MessageBoxButton.OK,
					MessageBoxImage.Information);
				return;
			}

			_ = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
		}

		private void TextToImageButton_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog textDialog = new()
			{
				Filter = "Text File|*.txt",
				Title = "Text to image",
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
			};
			if (textDialog.ShowDialog() != true)
			{
				return;
			}

			try
			{
				AsciiDocument document = AsciiDocument.Load(textDialog.FileName);
				using Bitmap rebuilt = AsciiImageRebuilder.ToBitmap(document, imagePrinter.Palette);

				SaveFileDialog imageDialog = new()
				{
					Filter = "BMP|*.bmp|PNG|*.png",
					Title = "Save rebuilt image",
					FileName = Path.GetFileNameWithoutExtension(textDialog.FileName) + ".bmp",
					InitialDirectory = Path.GetDirectoryName(textDialog.FileName)
				};
				if (imageDialog.ShowDialog() != true)
				{
					return;
				}

				AsciiImageRebuilder.SaveBitmap(rebuilt, imageDialog.FileName);
				imagePrinter = new ImagePrinter((Bitmap)rebuilt.Clone(), Path.GetFileNameWithoutExtension(textDialog.FileName));
				imagePrinter.UpdateResolution(1);
				ImagePath.Text = imageDialog.FileName;
				ExportPath.Text = imageDialog.FileName;
				ImagePreview.Source = CreatePreviewImage(imagePrinter.Picture);
				ResetListBox();
			}
			catch (Exception ex)
			{
				_ = MessageBox.Show(ex.Message, "Text to image failed", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private async void OpenVideoButton_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog videoDialog = new()
			{
				Filter = "Video / GIF|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm;*.gif|All Files|*.*",
				Title = "Open video or GIF",
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
			};
			if (videoDialog.ShowDialog() != true)
			{
				return;
			}

			try
			{
				await BeginFrameScrubAsync(FrameScrubSource.FromVideo(videoDialog.FileName), startIndex: 0);
			}
			catch (Exception ex)
			{
				_ = MessageBox.Show(ex.Message, "Could not open video", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private async void ExportAllFramesButton_Click(object sender, RoutedEventArgs e)
		{
			if (_frameScrub is null || !_frameScrub.SupportsExportAll
				|| string.IsNullOrWhiteSpace(_frameScrub.SourcePath))
			{
				_ = MessageBox.Show("Open a video or GIF first to export all frames.", "Image Printer", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			OpenFolderDialog folderDialog = new()
			{
				Title = "Choose parent folder for frame export",
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
			};
			if (folderDialog.ShowDialog() != true)
			{
				return;
			}

			string sourcePath = _frameScrub.SourcePath;
			FrameScrubKind kind = _frameScrub.Kind;
			string parentDir = folderDialog.FolderName;
			double resolution = ResolutionValue.Value;
			bool invert = ReverseGrayscale.IsChecked == true;
			ImagePrinter.ASCIISet set = imagePrinter.SelectedASCIISet;
			List<char> customChars = [.. imagePrinter.ASCIIGrayscaleChars];

			SetVideoExportProgress(0, "Preparing frame export…", visible: true);
			OpenVideoButton.IsEnabled = false;
			ExportAllFramesButton.IsEnabled = false;
			try
			{
				string? exportRoot = null;

				await Task.Run(() =>
				{
					(string? Root, string? ImageFramesDir, string? TextFramesDir) = VideoAsciiExportPaths.CreateLayout(parentDir, sourcePath);
					exportRoot = Root;

					IReadOnlyList<string> pngs;
					if (kind == FrameScrubKind.Gif)
					{
						using FrameScrubSource gifSource = FrameScrubSource.FromGif(sourcePath);
						List<string> gifPngs = [];
						for (int i = 0; i < gifSource.FrameCount; i++)
						{
							using Bitmap frame = gifSource.GetFrame(i);
							string path = Path.Combine(ImageFramesDir, $"frame{i}.png");
							frame.Save(path, ImageFormat.Png);
							gifPngs.Add(path);
							double pct = gifSource.FrameCount > 0 ? (i + 1) * 50.0 / gifSource.FrameCount : 0;
							ReportVideoExportProgress(pct, $"Extracting GIF frames… {i + 1}/{gifSource.FrameCount}");
						}
						pngs = gifPngs;
					}
					else
					{
						using VideoFrameSource source = new(sourcePath);
						int frameHint = source.FrameCount > 0 ? source.FrameCount : 0;
						pngs = source.ExtractPngFrames(ImageFramesDir, (done, total) =>
						{
							double pct = total > 0 ? done * 50.0 / total : 0;
							ReportVideoExportProgress(pct, $"Extracting image frames… {done}/{total}");
						});
						_ = frameHint;
					}

					FrameSequenceConverter converter = new()
					{
						Resolution = resolution,
						Invert = invert,
						AsciiSet = set,
						CustomCharacters = set == ImagePrinter.ASCIISet.Custom ? customChars : null
					};

					List<Bitmap> frames = [];
					foreach (string png in pngs)
					{
						frames.Add(new Bitmap(png));
					}

					try
					{
						int textTotal = Math.Max(1, frames.Count);
						converter.WriteAll(
							frames,
							TextFramesDir,
							progress: (done, total) =>
							{
								double pct = 50 + (total > 0 ? done * 50.0 / total : 0);
								ReportVideoExportProgress(pct, $"Converting text frames… {done}/{total}");
							},
							knownTotal: textTotal);
					}
					finally
					{
						foreach (Bitmap b in frames)
						{
							b.Dispose();
						}
					}
				});

				ExportPath.Text = exportRoot;
				_ = MessageBox.Show($"Frames exported to:\n{exportRoot}", "Image Printer", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				_ = MessageBox.Show(ex.Message, "Frame export failed", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally
			{
				OpenVideoButton.IsEnabled = true;
				ExportAllFramesButton.IsEnabled = _frameScrub is not null && _frameScrub.SupportsExportAll;
				SetVideoExportProgress(0, null, visible: false);
			}
		}

		private async void FrameScrubSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if (_suppressFrameScrub || _frameScrub is null)
			{
				return;
			}

			await LoadScrubFrameAsync((int)Math.Round(e.NewValue));
		}

		private async void PrevFrameButton_Click(object sender, RoutedEventArgs e)
		{
			if (_frameScrub is null)
			{
				return;
			}

			await LoadScrubFrameAsync(_currentFrameIndex - 1);
		}

		private async void NextFrameButton_Click(object sender, RoutedEventArgs e)
		{
			if (_frameScrub is null)
			{
				return;
			}

			await LoadScrubFrameAsync(_currentFrameIndex + 1);
		}

		private async Task BeginFrameScrubAsync(FrameScrubSource source, int startIndex)
		{
			ClearFrameScrub();
			_frameScrub = source;
			_suppressFrameScrub = true;
			FrameScrubSlider.Minimum = 0;
			FrameScrubSlider.Maximum = Math.Max(0, source.FrameCount - 1);
			FrameScrubSlider.Value = Math.Clamp(startIndex, 0, source.FrameCount - 1);
			_suppressFrameScrub = false;
			FrameScrubberPanel.Visibility = Visibility.Visible;
			ExportAllFramesButton.IsEnabled = source.SupportsExportAll;
			ImagePath.Text = source.SourcePath;
			await LoadScrubFrameAsync((int)FrameScrubSlider.Value);
		}

		private void ClearFrameScrub()
		{
			_frameScrub?.Dispose();
			_frameScrub = null;
			_currentFrameIndex = 0;
			FrameScrubberPanel.Visibility = Visibility.Collapsed;
			ExportAllFramesButton.IsEnabled = false;
		}

		private async Task LoadScrubFrameAsync(int index)
		{
			if (_frameScrub is null)
			{
				return;
			}

			index = Math.Clamp(index, 0, _frameScrub.FrameCount - 1);
			int version = ++_frameLoadVersion;

			ImagePrinter.ASCIISet set = imagePrinter.SelectedASCIISet;
			List<char> customChars = [.. imagePrinter.ASCIIGrayscaleChars];
			double resolution = ResolutionValue.Value;
			bool invert = ReverseGrayscale.IsChecked == true;
			FrameScrubSource source = _frameScrub;
			string displayName = source.GetFrameDisplayName(index);

			Bitmap frame = await Task.Run(() => source.GetFrame(index));
			if (version != _frameLoadVersion || !ReferenceEquals(_frameScrub, source))
			{
				frame.Dispose();
				return;
			}

			imagePrinter = new ImagePrinter(frame, displayName);
			imagePrinter.SetASCIIGrayscaleChars(set);
			if (set == ImagePrinter.ASCIISet.Custom)
			{
				imagePrinter.ASCIIGrayscaleChars = customChars;
			}

			imagePrinter.UpdateResolution(resolution);
			if (invert)
			{
				imagePrinter.ReverseGrayscale();
			}

			_currentFrameIndex = index;
			_suppressFrameScrub = true;
			FrameScrubSlider.Value = index;
			_suppressFrameScrub = false;
			FrameScrubLabel.Text = $"Frame {index + 1} / {_frameScrub.FrameCount}";
			ImagePreview.Source = CreatePreviewImage(imagePrinter.Picture);
			ResetListBox();
		}

		private void ReportVideoExportProgress(double percent, string message)
		{
			_ = Dispatcher.InvokeAsync(() => SetVideoExportProgress(percent, message, visible: true));
		}

		private void SetVideoExportProgress(double percent, string? message, bool visible)
		{
			VideoExportProgressBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
			VideoExportProgressText.Visibility = visible && !string.IsNullOrWhiteSpace(message)
				? Visibility.Visible
				: Visibility.Collapsed;
			VideoExportProgressBar.Value = Math.Clamp(percent, 0, 100);
			if (!string.IsNullOrWhiteSpace(message))
			{
				VideoExportProgressText.Text = message;
			}
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
			if (_suppressResolutionSync || !IsLoaded || imagePrinter?.Picture is null || ImagePreview is null || ResolutionValue is null)
			{
				return;
			}

			if (double.TryParse(PercentageBox.Text, out double value))
			{
				value = Math.Clamp(value, 1, 100);
				_suppressResolutionSync = true;
				ResolutionValue.Value = Math.Round(value / 100, 2);
				PercentageBox.Text = Math.Round(value).ToString();
				_suppressResolutionSync = false;
				imagePrinter.UpdateResolution(ResolutionValue.Value);
				ImagePreview.Source = CreatePreviewImage(imagePrinter.Picture);
			}
			else
			{
				_suppressResolutionSync = true;
				ResolutionValue.Value = 1;
				PercentageBox.Text = "100";
				_suppressResolutionSync = false;
				imagePrinter.UpdateResolution(1);
				ImagePreview.Source = CreatePreviewImage(imagePrinter.Picture);
			}
		}
	}
}
