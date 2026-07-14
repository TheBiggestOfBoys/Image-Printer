using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

using OpenCvSharp;

namespace Image_Printer.Video
{
	/// <summary>
	/// Random-access frames from a video, animated GIF, or ordered image list for UI scrubbing.
	/// </summary>
	public sealed class FrameScrubSource : IDisposable
	{
		private readonly VideoCapture _capture;
		private readonly Image _gif;
		private readonly FrameDimension _gifDimension;
		private readonly IReadOnlyList<string> _imagePaths;
		private readonly bool _ownsCapture;
		private readonly bool _ownsGif;

		private FrameScrubSource(VideoCapture capture, string sourcePath, int frameCount, bool ownsCapture)
		{
			_capture = capture ?? throw new ArgumentNullException(nameof(capture));
			_ownsCapture = ownsCapture;
			_gif = null;
			_ownsGif = false;
			_imagePaths = null;
			SourcePath = sourcePath;
			FrameCount = Math.Max(1, frameCount);
			Kind = FrameScrubKind.Video;
		}

		private FrameScrubSource(Image gif, FrameDimension dimension, string sourcePath, int frameCount)
		{
			_gif = gif ?? throw new ArgumentNullException(nameof(gif));
			_gifDimension = dimension;
			_ownsGif = true;
			_capture = null;
			_ownsCapture = false;
			_imagePaths = null;
			SourcePath = sourcePath;
			FrameCount = Math.Max(1, frameCount);
			Kind = FrameScrubKind.Gif;
		}

		private FrameScrubSource(IReadOnlyList<string> imagePaths, string sourcePath)
		{
			_imagePaths = imagePaths ?? throw new ArgumentNullException(nameof(imagePaths));
			if (_imagePaths.Count == 0)
			{
				throw new ArgumentException("At least one image path is required.", nameof(imagePaths));
			}

			_capture = null;
			_ownsCapture = false;
			_gif = null;
			_ownsGif = false;
			SourcePath = sourcePath;
			FrameCount = _imagePaths.Count;
			Kind = FrameScrubKind.Images;
		}

		public FrameScrubKind Kind { get; }

		public string SourcePath { get; }

		public int FrameCount { get; }

		public bool SupportsExportAll => Kind is FrameScrubKind.Video or FrameScrubKind.Gif;

		public static bool IsGifPath(string path)
		{
			return !string.IsNullOrWhiteSpace(path)
			&& string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Opens a multi-frame GIF for scrubbing, or a single-frame GIF as a one-image sequence.
		/// </summary>
		public static FrameScrubSource FromGif(string gifPath)
		{
			if (string.IsNullOrWhiteSpace(gifPath) || !File.Exists(gifPath))
			{
				throw new FileNotFoundException("GIF file not found.", gifPath);
			}

			Image gif = Image.FromFile(gifPath);
			try
			{
				if (gif.FrameDimensionsList.Length == 0)
				{
					gif.Dispose();
					return FromImages([gifPath], gifPath);
				}

				FrameDimension dimension = new(gif.FrameDimensionsList[0]);
				int count = gif.GetFrameCount(dimension);
				if (count <= 1)
				{
					gif.Dispose();
					return FromImages([gifPath], gifPath);
				}

				return new FrameScrubSource(gif, dimension, gifPath, count);
			}
			catch
			{
				gif.Dispose();
				throw;
			}
		}

		public static FrameScrubSource FromVideo(string videoPath)
		{
			if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
			{
				throw new FileNotFoundException("Video file not found.", videoPath);
			}

			if (IsGifPath(videoPath))
			{
				return FromGif(videoPath);
			}

			VideoCapture capture = new(videoPath);
			if (!capture.IsOpened())
			{
				capture.Dispose();
				throw new InvalidOperationException($"Could not open video: {videoPath}");
			}

			int count = Math.Max(1, capture.FrameCount);
			return new FrameScrubSource(capture, videoPath, count, ownsCapture: true);
		}

		public static FrameScrubSource FromImages(IEnumerable<string> imagePaths, string sourceLabel = null)
		{
			List<string> paths = [];
			HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
			foreach (string path in imagePaths)
			{
				if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !seen.Add(path))
				{
					continue;
				}

				paths.Add(path);
			}

			if (paths.Count == 0)
			{
				throw new FileNotFoundException("No image files found.");
			}

			// A lone animated GIF should scrub its frames, not only the first one.
			if (paths.Count == 1 && IsGifPath(paths[0]))
			{
				return FromGif(paths[0]);
			}

			string label = sourceLabel
				?? (paths.Count == 1 ? paths[0] : Path.GetDirectoryName(paths[0]) ?? paths[0]);
			return new FrameScrubSource(paths, label);
		}

		/// <summary>
		/// Returns a new <see cref="Bitmap"/> for the frame. Caller owns the bitmap.
		/// </summary>
		public Bitmap GetFrame(int index)
		{
			index = Math.Clamp(index, 0, FrameCount - 1);

			if (Kind == FrameScrubKind.Images)
			{
				return new Bitmap(_imagePaths[index]);
			}

			if (Kind == FrameScrubKind.Gif)
			{
				_ = _gif.SelectActiveFrame(_gifDimension, index);
				return new Bitmap(_gif);
			}

			_ = _capture.Set(VideoCaptureProperties.PosFrames, index);
			using Mat mat = new();
			if (!_capture.Read(mat) || mat.Empty())
			{
				throw new InvalidOperationException($"Could not read frame {index}.");
			}

			_ = Cv2.ImEncode(".png", mat, out byte[] buffer);
			using MemoryStream stream = new(buffer);
			return new Bitmap(stream);
		}

		public string GetFrameDisplayName(int index)
		{
			index = Math.Clamp(index, 0, FrameCount - 1);
			if (Kind == FrameScrubKind.Images)
			{
				return Path.GetFileNameWithoutExtension(_imagePaths[index]);
			}

			string stem = Path.GetFileNameWithoutExtension(SourcePath);
			return $"{stem}_frame{index}";
		}

		public void Dispose()
		{
			if (_ownsCapture)
			{
				_capture?.Dispose();
			}

			if (_ownsGif)
			{
				_gif?.Dispose();
			}
		}
	}

	public enum FrameScrubKind
	{
		Video,
		Images,
		Gif
	}
}
