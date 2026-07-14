using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using OpenCvSharp;

namespace Image_Printer.Video
{
	/// <summary>
	/// Extracts video frames via OpenCvSharp (no UI).
	/// </summary>
	public sealed class VideoFrameSource : IDisposable
	{
		private readonly VideoCapture _capture;

		public string VideoPath { get; }

		public double Fps { get; }

		public int FrameCount { get; }

		public VideoFrameSource(string videoPath)
		{
			if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
			{
				throw new FileNotFoundException("Video file not found.", videoPath);
			}

			VideoPath = videoPath;
			_capture = new VideoCapture(videoPath);
			if (!_capture.IsOpened())
			{
				throw new InvalidOperationException($"Could not open video: {videoPath}");
			}

			Fps = _capture.Fps > 0 ? _capture.Fps : 30;
			FrameCount = Math.Max(0, _capture.FrameCount);
		}

		/// <summary>
		/// Yields frames as bitmaps. Caller owns each returned <see cref="Bitmap"/>.
		/// </summary>
		public IEnumerable<Bitmap> EnumerateFrames()
		{
			_ = _capture.Set(VideoCaptureProperties.PosFrames, 0);
			using Mat mat = new();
			while (_capture.Read(mat) && !mat.Empty())
			{
				_ = Cv2.ImEncode(".png", mat, out byte[] buffer);
				using MemoryStream stream = new(buffer);
				yield return new Bitmap(stream);
			}
		}

		/// <summary>
		/// Writes PNG frames to <paramref name="outputDirectory"/> and returns their paths.
		/// </summary>
		public IReadOnlyList<string> ExtractPngFrames(string outputDirectory, Action<int, int> progress = null)
		{
			_ = Directory.CreateDirectory(outputDirectory);
			List<string> paths = [];
			_ = _capture.Set(VideoCaptureProperties.PosFrames, 0);
			using Mat mat = new();
			int index = 0;
			int total = FrameCount > 0 ? FrameCount : int.MaxValue;

			while (_capture.Read(mat) && !mat.Empty())
			{
				string path = Path.Combine(outputDirectory, $"frame{index}.png");
				_ = Cv2.ImWrite(path, mat);
				paths.Add(path);
				progress?.Invoke(index + 1, total == int.MaxValue ? index + 1 : total);
				index++;
			}

			return paths;
		}

		/// <summary>
		/// Returns a new <see cref="Bitmap"/> for <paramref name="frameIndex"/>. Caller owns it.
		/// </summary>
		public Bitmap GetFrame(int frameIndex)
		{
			int count = FrameCount > 0 ? FrameCount : int.MaxValue;
			if (count != int.MaxValue)
			{
				frameIndex = Math.Clamp(frameIndex, 0, count - 1);
			}

			_ = _capture.Set(VideoCaptureProperties.PosFrames, frameIndex);
			using Mat mat = new();
			if (!_capture.Read(mat) || mat.Empty())
			{
				throw new InvalidOperationException($"Could not read frame {frameIndex}.");
			}

			_ = Cv2.ImEncode(".png", mat, out byte[] buffer);
			using MemoryStream stream = new(buffer);
			return new Bitmap(stream);
		}

		public void Dispose()
		{
			_capture?.Dispose();
		}
	}
}
