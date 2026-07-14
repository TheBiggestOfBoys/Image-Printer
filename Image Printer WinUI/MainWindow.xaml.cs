using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading.Tasks;

using Image_Printer;
using Image_Printer.Video;

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

		/// <summary>Filesystem path of the PDF last written by Save as PDF.</summary>
		private string lastExportedPdfPath;

		private StorageFile savedPdfFile;
		#endregion

		private ImagePrinter imagePrinter = new(ImagePrinter.CreateGradient());

		private FrameScrubSource _frameScrub;
		private int _currentFrameIndex;
		private int _frameLoadVersion;
		private bool _suppressInvertToggle;
		private bool _suppressAsciiSetChange;
		private bool _suppressResolutionSync;
		private bool _suppressFrameScrub;

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

			IReadOnlyList<StorageFile> files = await openPicker.PickMultipleFilesAsync();
			if (files is null || files.Count == 0)
			{
				return;
			}

			List<string> paths = [];
			foreach (StorageFile file in files)
			{
				if (!string.IsNullOrWhiteSpace(file.Path) && File.Exists(file.Path))
				{
					paths.Add(file.Path);
				}
			}

			if (paths.Count == 0)
			{
				ShowStatus("Could not access a filesystem path for the selected image(s).", InfoBarSeverity.Error);
				return;
			}

			openedFile = files[0];

			try
			{
				if (paths.Count == 1 && !FrameScrubSource.IsGifPath(paths[0]))
				{
					ClearFrameScrub();
					ImagePathText.Text = paths[0];
					await LoadSingleImageAsync(paths[0]);
					ShowStatus("Image opened. Adjust resolution, then save, copy, or export as PDF.", InfoBarSeverity.Informational);
					return;
				}

				FrameScrubSource scrub = FrameScrubSource.FromImages(paths);
				if (scrub.FrameCount <= 1 && scrub.Kind == FrameScrubKind.Images)
				{
					ClearFrameScrub();
					scrub.Dispose();
					ImagePathText.Text = paths[0];
					await LoadSingleImageAsync(paths[0]);
					ShowStatus("Image opened. Adjust resolution, then save, copy, or export as PDF.", InfoBarSeverity.Informational);
					return;
				}

				await BeginFrameScrubAsync(scrub, startIndex: 0);
				string kindLabel = scrub.Kind == FrameScrubKind.Gif ? "GIF frames" : "images";
				ShowStatus(
					$"Opened {scrub.FrameCount} {kindLabel}. Scrub to pick a frame, then save/export that one.",
					InfoBarSeverity.Informational);
			}
			catch (Exception ex)
			{
				ShowStatus($"Could not open images: {ex.Message}", InfoBarSeverity.Error);
			}
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

			string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
			try
			{
				string text = imagePrinter.ToString();
				await File.WriteAllTextAsync(tempPath, text);
				await CommitTempFileToStorageAsync(savedFile, tempPath);

				lastExportedPath = !string.IsNullOrWhiteSpace(savedFile.Path) ? savedFile.Path : tempPath;
				ExportPathText.Text = !string.IsNullOrWhiteSpace(savedFile.Path)
					? savedFile.Path
					: savedFile.Name;
				UpdateActionAvailability();
				ShowStatus("Saved as text.", InfoBarSeverity.Success);
			}
			catch (Exception ex)
			{
				bool wrote = IsNonEmptyFile(tempPath)
					|| IsNonEmptyFile(savedFile.Path)
					|| savedFile is not null;
				if (wrote)
				{
					lastExportedPath = !string.IsNullOrWhiteSpace(savedFile.Path) ? savedFile.Path : tempPath;
					UpdateActionAvailability();
					ShowStatus("Saved as text.", InfoBarSeverity.Success);
				}
				else
				{
					ShowStatus($"Could not save text: {ex.Message}", InfoBarSeverity.Error);
				}
			}
			finally
			{
				TryDeleteTempFile(tempPath, lastExportedPath);
			}
		}

		private async void OpenTextFile_Click(object sender, RoutedEventArgs e)
		{
			if (await TryOpenExportedFileAsync(savedFile, lastExportedPath))
			{
				return;
			}

			ShowStatus("Save as text first, then open the exported file.", InfoBarSeverity.Informational);
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

		private async void SavePdfButton_Click(object sender, RoutedEventArgs e)
		{
			if (imagePrinter is null)
			{
				return;
			}

			ApplyAsciiCharsFromUi();
			string ascii = imagePrinter.ToString();

			FileSavePicker pdfPicker = new()
			{
				SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
				SuggestedFileName = Path.ChangeExtension(imagePrinter.FileName, ".pdf"),
				DefaultFileExtension = ".pdf"
			};
			pdfPicker.FileTypeChoices.Add("PDF", [".pdf"]);

			IWindowNative window = this.As<IWindowNative>();
			pdfPicker.As<IInitializeWithWindow>().Initialize(window.WindowHandle);

			StorageFile pdfFile = await pdfPicker.PickSaveFileAsync();
			if (pdfFile is null)
			{
				return;
			}

			string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
			AsciiPdfExportResult result = null;
			try
			{
				// Always write via temp so QuestPDF never fights the picker file lock.
				result = await Task.Run(() => AsciiPdfExporter.Save(ascii, tempPath));
				await CommitTempFileToStorageAsync(pdfFile, tempPath);

				savedPdfFile = pdfFile;
				lastExportedPdfPath = !string.IsNullOrWhiteSpace(pdfFile.Path) ? pdfFile.Path : tempPath;
				ExportPathText.Text = !string.IsNullOrWhiteSpace(pdfFile.Path) ? pdfFile.Path : pdfFile.Name;
				UpdateActionAvailability();

				string orientation = result.IsLandscape ? "landscape" : "portrait";
				ShowStatus(
					$"PDF saved ({orientation}, {result.FontSizePt:0.##}pt, {result.ScalePercent:0.#}% of 12pt).",
					InfoBarSeverity.Success);
			}
			catch (Exception ex)
			{
				bool exported = IsNonEmptyFile(tempPath) || IsNonEmptyFile(pdfFile.Path);
				if (exported)
				{
					savedPdfFile = pdfFile;
					lastExportedPdfPath = !string.IsNullOrWhiteSpace(pdfFile.Path) ? pdfFile.Path : tempPath;
					try { UpdateActionAvailability(); } catch { /* ignore */ }

					if (result is not null)
					{
						string orientation = result.IsLandscape ? "landscape" : "portrait";
						ShowStatus(
							$"PDF saved ({orientation}, {result.FontSizePt:0.##}pt, {result.ScalePercent:0.#}% of 12pt).",
							InfoBarSeverity.Success);
					}
					else
					{
						ShowStatus("PDF saved.", InfoBarSeverity.Success);
					}
				}
				else
				{
					ShowStatus($"PDF export failed: {ex.Message}", InfoBarSeverity.Error);
				}
			}
			finally
			{
				TryDeleteTempFile(tempPath, lastExportedPdfPath);
			}
		}

		private async void OpenPdfButton_Click(object sender, RoutedEventArgs e)
		{
			if (await TryOpenExportedFileAsync(savedPdfFile, lastExportedPdfPath))
			{
				return;
			}

			ShowStatus("Save as PDF first, then open the exported file.", InfoBarSeverity.Informational);
		}

		private async void TextToImageButton_Click(object sender, RoutedEventArgs e)
		{
			FileOpenPicker textPicker = new()
			{
				SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
				FileTypeFilter = { ".txt" }
			};
			IWindowNative window = this.As<IWindowNative>();
			textPicker.As<IInitializeWithWindow>().Initialize(window.WindowHandle);

			StorageFile textFile = await textPicker.PickSingleFileAsync();
			if (textFile is null)
			{
				return;
			}

			try
			{
				string content = await FileIO.ReadTextAsync(textFile);
				AsciiDocument document = AsciiDocument.FromText(content);
				Bitmap rebuilt = await Task.Run(() => AsciiImageRebuilder.ToBitmap(document, imagePrinter.Palette));

				FileSavePicker imageSave = new()
				{
					SuggestedStartLocation = PickerLocationId.PicturesLibrary,
					SuggestedFileName = Path.GetFileNameWithoutExtension(textFile.Name) + ".bmp",
					DefaultFileExtension = ".bmp"
				};
				imageSave.FileTypeChoices.Add("BMP", [".bmp"]);
				imageSave.FileTypeChoices.Add("PNG", [".png"]);
				imageSave.As<IInitializeWithWindow>().Initialize(window.WindowHandle);

				StorageFile outFile = await imageSave.PickSaveFileAsync();
				if (outFile is null)
				{
					rebuilt.Dispose();
					return;
				}

				string tempImage = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{outFile.FileType}");
				await Task.Run(() => AsciiImageRebuilder.SaveBitmap(rebuilt, tempImage));
				CachedFileManager.DeferUpdates(outFile);
				await FileIO.WriteBytesAsync(outFile, await File.ReadAllBytesAsync(tempImage));
				_ = await CachedFileManager.CompleteUpdatesAsync(outFile);

				imagePrinter = new ImagePrinter((Bitmap)rebuilt.Clone(), Path.GetFileNameWithoutExtension(textFile.Name));
				rebuilt.Dispose();
				imagePrinter.UpdateResolution(1);
				ImagePathText.Text = !string.IsNullOrWhiteSpace(outFile.Path) ? outFile.Path : outFile.Name;
				ExportPathText.Text = ImagePathText.Text;
				await RefreshPreviewAsync();
				UpdateActionAvailability();
				ShowStatus("Text rebuilt to image.", InfoBarSeverity.Success);

				if (File.Exists(tempImage))
				{
					File.Delete(tempImage);
				}
			}
			catch (Exception ex)
			{
				ShowStatus($"Text to image failed: {ex.Message}", InfoBarSeverity.Error);
			}
		}

		private async void OpenVideoButton_Click(object sender, RoutedEventArgs e)
		{
			FileOpenPicker videoPicker = new()
			{
				SuggestedStartLocation = PickerLocationId.VideosLibrary,
				FileTypeFilter = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".webm", ".gif" }
			};
			IWindowNative window = this.As<IWindowNative>();
			videoPicker.As<IInitializeWithWindow>().Initialize(window.WindowHandle);

			StorageFile videoFile = await videoPicker.PickSingleFileAsync();
			if (videoFile is null || string.IsNullOrWhiteSpace(videoFile.Path))
			{
				if (videoFile is not null)
				{
					ShowStatus("Could not access a filesystem path for that video.", InfoBarSeverity.Error);
				}

				return;
			}

			try
			{
				await BeginFrameScrubAsync(FrameScrubSource.FromVideo(videoFile.Path), startIndex: 0);
				ShowStatus(
					"Video opened. Scrub to a frame, then save/export that one — or use Export all frames.",
					InfoBarSeverity.Informational);
			}
			catch (Exception ex)
			{
				ShowStatus($"Could not open video: {ex.Message}", InfoBarSeverity.Error);
			}
		}

		private async void ExportAllFramesButton_Click(object sender, RoutedEventArgs e)
		{
			if (_frameScrub is null || !_frameScrub.SupportsExportAll
				|| string.IsNullOrWhiteSpace(_frameScrub.SourcePath))
			{
				ShowStatus("Open a video or GIF first to export all frames.", InfoBarSeverity.Informational);
				return;
			}

			FolderPicker folderPicker = new()
			{
				SuggestedStartLocation = PickerLocationId.DocumentsLibrary
			};
			folderPicker.FileTypeFilter.Add("*");
			IWindowNative window = this.As<IWindowNative>();
			folderPicker.As<IInitializeWithWindow>().Initialize(window.WindowHandle);
			StorageFolder folder = await folderPicker.PickSingleFolderAsync();
			if (folder is null || string.IsNullOrWhiteSpace(folder.Path))
			{
				ShowStatus("Choose a parent folder. A folder named after the source will be created inside it.", InfoBarSeverity.Informational);
				return;
			}

			ApplyAsciiCharsFromUi();
			double resolution = ResolutionSlider.Value / 100;
			bool invert = InvertGrayscaleBox.IsChecked == true;
			ImagePrinter.ASCIISet set = imagePrinter.SelectedASCIISet;
			List<char> customChars = [.. imagePrinter.ASCIIGrayscaleChars];
			string sourcePath = _frameScrub.SourcePath;
			FrameScrubKind kind = _frameScrub.Kind;
			string parentDir = folder.Path;

			SetVideoExportProgress(0, "Preparing frame export…", visible: true);
			ShowStatus("Extracting and converting frames…", InfoBarSeverity.Informational);
			ExportAllFramesButton.IsEnabled = false;
			OpenVideoButton.IsEnabled = false;
			string exportRoot = null;
			try
			{
				Microsoft.UI.Dispatching.DispatcherQueue dispatcher = DispatcherQueue;

				await Task.Run(() =>
				{
					(string Root, string ImageFramesDir, string TextFramesDir) = VideoAsciiExportPaths.CreateLayout(parentDir, sourcePath);
					exportRoot = Root;

					List<string> pngs;
					if (kind == FrameScrubKind.Gif)
					{
						using FrameScrubSource gifSource = FrameScrubSource.FromGif(sourcePath);
						pngs = ExtractScrubPngs(gifSource, ImageFramesDir, (done, total) =>
						{
							double pct = total > 0 ? done * 50.0 / total : 0;
							ReportVideoExportProgress(dispatcher, pct, $"Extracting GIF frames… {done}/{total}");
						});
					}
					else
					{
						using VideoFrameSource source = new(sourcePath);
						int frameHint = source.FrameCount > 0 ? source.FrameCount : 0;
						pngs = [.. source.ExtractPngFrames(ImageFramesDir, (done, total) =>
						{
							double pct = total > 0 ? done * 50.0 / total : 0;
							ReportVideoExportProgress(dispatcher, pct, $"Extracting image frames… {done}/{total}");
						})];
						if (pngs.Count == 0 && frameHint > 0)
						{
							pngs = [];
						}
					}

					FrameSequenceConverter converter = new()
					{
						Resolution = resolution,
						Invert = invert,
						AsciiSet = set,
						CustomCharacters = set == ImagePrinter.ASCIISet.Custom ? customChars : null
					};

					int textTotal = Math.Max(1, pngs.Count);
					converter.WriteAll(
						EnumerateBitmaps(pngs),
						TextFramesDir,
						progress: (done, total) =>
						{
							double pct = 50 + (total > 0 ? done * 50.0 / total : 0);
							ReportVideoExportProgress(dispatcher, pct, $"Converting text frames… {done}/{total}");
						},
						knownTotal: textTotal);
				});

				ExportPathText.Text = exportRoot;
				SetVideoExportProgress(100, "Frame export complete.", visible: true);
				ShowStatus($"Frames exported to {exportRoot}", InfoBarSeverity.Success);
			}
			catch (Exception ex)
			{
				if (DirectoryHasFiles(exportRoot))
				{
					ShowStatus($"Frames exported to {exportRoot}", InfoBarSeverity.Success);
				}
				else
				{
					ShowStatus($"Frame export failed: {ex.Message}", InfoBarSeverity.Error);
				}
			}
			finally
			{
				OpenVideoButton.IsEnabled = true;
				UpdateActionAvailability();
				SetVideoExportProgress(0, null, visible: false);
			}
		}

		private async void FrameScrubSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
		{
			if (_suppressFrameScrub || _frameScrub is null)
			{
				return;
			}

			int index = (int)Math.Round(e.NewValue);
			await LoadScrubFrameAsync(index);
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
			ImagePathText.Text = source.SourcePath;
			await LoadScrubFrameAsync((int)FrameScrubSlider.Value);
			UpdateActionAvailability();
		}

		private void ClearFrameScrub()
		{
			_frameScrub?.Dispose();
			_frameScrub = null;
			_currentFrameIndex = 0;
			FrameScrubberPanel.Visibility = Visibility.Collapsed;
		}

		private async Task LoadSingleImageAsync(string path)
		{
			ImagePrinter.ASCIISet previousSet = imagePrinter.SelectedASCIISet;
			List<char> previousChars = imagePrinter.ASCIIGrayscaleChars;

			imagePrinter = new(path);
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
		}

		private async Task LoadScrubFrameAsync(int index)
		{
			if (_frameScrub is null)
			{
				return;
			}

			index = Math.Clamp(index, 0, _frameScrub.FrameCount - 1);
			int version = ++_frameLoadVersion;

			ApplyAsciiCharsFromUi();
			ImagePrinter.ASCIISet set = imagePrinter.SelectedASCIISet;
			List<char> customChars = [.. imagePrinter.ASCIIGrayscaleChars];
			double resolution = ResolutionSlider.Value / 100;
			bool invert = InvertGrayscaleBox.IsChecked == true;
			FrameScrubSource source = _frameScrub;
			string displayName = source.GetFrameDisplayName(index);

			Bitmap frame = await Task.Run(() => source.GetFrame(index));
			if (version != _frameLoadVersion || _frameScrub != source)
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

			ResetAsciiList();
			await RefreshPreviewAsync();
			UpdateActionAvailability();
		}

		private void ReportVideoExportProgress(Microsoft.UI.Dispatching.DispatcherQueue dispatcher, double percent, string message)
		{
			_ = dispatcher.TryEnqueue(() => SetVideoExportProgress(percent, message, visible: true));
		}

		private void SetVideoExportProgress(double percent, string message, bool visible)
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

		private static IEnumerable<Bitmap> EnumerateBitmaps(IEnumerable<string> paths)
		{
			foreach (string path in paths)
			{
				using Bitmap bmp = new(path);
				yield return (Bitmap)bmp.Clone();
			}
		}

		private static List<string> ExtractScrubPngs(FrameScrubSource source, string outputDirectory, Action<int, int> progress)
		{
			_ = Directory.CreateDirectory(outputDirectory);
			List<string> paths = [];
			for (int i = 0; i < source.FrameCount; i++)
			{
				using Bitmap frame = source.GetFrame(i);
				string path = Path.Combine(outputDirectory, $"frame{i}.png");
				frame.Save(path, DrawingImageFormat.Png);
				paths.Add(path);
				progress?.Invoke(i + 1, source.FrameCount);
			}
			return paths;
		}

		/// <summary>
		/// Writes a completed temp file into a picker <see cref="StorageFile"/> without treating
		/// CachedFileManager incomplete/failed follow-up status as an export failure.
		/// </summary>
		private static async Task CommitTempFileToStorageAsync(StorageFile file, string tempPath)
		{
			ArgumentNullException.ThrowIfNull(file);
			if (!File.Exists(tempPath))
			{
				throw new FileNotFoundException("Temporary export file was not created.", tempPath);
			}

			if (!string.IsNullOrWhiteSpace(file.Path))
			{
				try
				{
					File.Copy(tempPath, file.Path, overwrite: true);
					return;
				}
				catch
				{
					// Fall through to StorageFile APIs when the path is locked by the picker.
				}
			}

			byte[] bytes = await File.ReadAllBytesAsync(tempPath);
			try { CachedFileManager.DeferUpdates(file); }
			catch { /* optional */ }

			await FileIO.WriteBytesAsync(file, bytes);

			try { _ = await CachedFileManager.CompleteUpdatesAsync(file); }
			catch { /* file bytes are already written */ }
		}

		private static async Task<bool> TryOpenExportedFileAsync(StorageFile storageFile, string path)
		{
			if (storageFile is not null)
			{
				try
				{
					if (await Launcher.LaunchFileAsync(storageFile))
					{
						return true;
					}
				}
				catch { /* try path next */ }
			}

			if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
			{
				try
				{
					_ = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
					return true;
				}
				catch { /* try LaunchFileAsync next */ }

				try
				{
					StorageFile file = await StorageFile.GetFileFromPathAsync(path);
					if (await Launcher.LaunchFileAsync(file))
					{
						return true;
					}
				}
				catch { /* give up */ }
			}

			return false;
		}

		private static bool IsNonEmptyFile(string path)
		{
			return !string.IsNullOrWhiteSpace(path) && File.Exists(path) && new FileInfo(path).Length > 0;
		}

		private static bool DirectoryHasFiles(string directory)
		{
			return !string.IsNullOrWhiteSpace(directory)
			&& Directory.Exists(directory)
			&& Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any();
		}

		private static void TryDeleteTempFile(string tempPath, string keepPath)
		{
			try
			{
				if (File.Exists(tempPath)
					&& !string.Equals(tempPath, keepPath, StringComparison.OrdinalIgnoreCase))
				{
					File.Delete(tempPath);
				}
			}
			catch { /* ignore cleanup failures */ }
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

			bool hasExportedPdf = savedPdfFile is not null
				|| (!string.IsNullOrWhiteSpace(lastExportedPdfPath) && File.Exists(lastExportedPdfPath));

			SaveButton.IsEnabled = hasPrinter;
			CopyText.IsEnabled = hasPrinter;
			SavePdfButton.IsEnabled = hasPrinter;
			InvertGrayscaleBox.IsEnabled = hasPrinter;
			OpenTextFile.IsEnabled = hasExportedFile;
			OpenPdfButton.IsEnabled = hasExportedPdf;
			ExportAllFramesButton.IsEnabled = _frameScrub is not null && _frameScrub.SupportsExportAll;
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
