using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;

using Image_Printer;
using Image_Printer.Video;

namespace Video_Printer
{
	internal class Program
	{
		private static void Main()
		{
			Console.Write("Enter path to video file: ");
			string videoFile = Console.ReadLine();
			if (string.IsNullOrWhiteSpace(videoFile) || !File.Exists(videoFile))
			{
				Console.WriteLine("Video file not found.");
				return;
			}

			string parentDir = Path.Combine(Directory.GetCurrentDirectory(), "Frames");
			(string Root, string ImageFramesDir, string TextFramesDir) = VideoAsciiExportPaths.CreateLayout(parentDir, videoFile);

			Console.Clear();
			using VideoFrameSource source = new(videoFile);
			Console.WriteLine($"Export root: {Root}");
			Console.WriteLine("Extracting image frames...");
			IReadOnlyList<string> pngPaths = source.ExtractPngFrames(ImageFramesDir, (done, total) =>
			{
				Console.WriteLine($"Saved image frame {done}/{total}");
			});
			Console.WriteLine($"Finished extracting to: {ImageFramesDir}");

			FrameSequenceConverter converter = new()
			{
				Resolution = 0.25,
				Invert = false,
				AsciiSet = ImagePrinter.ASCIISet.Default
			};

			int delayMs = (int)Math.Max(1, 1000.0 / source.Fps);
			int index = 0;
			foreach (string png in pngPaths)
			{
				using Bitmap frame = new(png);
				string ascii = converter.ConvertFrame(frame, $"frame{index}");
				string txtPath = Path.Combine(TextFramesDir, $"frame{index}.txt");
				File.WriteAllText(txtPath, ascii);
				Console.Clear();
				Console.WriteLine(ascii);
				Thread.Sleep(delayMs);
				index++;
			}

			Console.WriteLine($"Text frames written to: {TextFramesDir}");
		}
	}
}
