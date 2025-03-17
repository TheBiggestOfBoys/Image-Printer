using Image_Printer;
using OpenCvSharp;
using System;
using System.Drawing;
using System.Threading;

namespace Video_Printer
{
	internal class Program
	{
		static void Main()
		{
			// Path is relative to working directory of the app
			string outputPath = "C:\\Users\\jrsco\\source\\repos\\Image Printer\\Video Printer\\Frames";

			Console.Write("Enter path to video file: ");
			string videoFile = Console.ReadLine();
			Console.Clear();

			VideoCapture capture = new(videoFile);
			Mat image = new();

			Bitmap[] frames = new Bitmap[capture.FrameCount];
			string[] stringFrames = new string[capture.FrameCount];

			Console.WriteLine("Begin extracting frames from video file...");
			for (int i = 0; capture.IsOpened(); i++)
			{
				// Read next frame in video file
				if (!capture.Read(image))
				{
					break;
				}
				// Save image to disk.
				string exportPath = $"{outputPath}\\frame{i}.png";
				Cv2.ImWrite(exportPath, image);
				Console.WriteLine($"Successfully saved frame {i} to disk.");

				frames[i] = Bitmap.FromFile(exportPath) as Bitmap;
			}
			Console.WriteLine($"Finished, check output at: {outputPath}.");
			Console.Clear();

			double pause = capture.Fps / capture.FrameCount;

			for (int x = 0; x < capture.FrameCount; x++)
			{
				ImagePrinter imagePrinter = new(frames[x]);
				imagePrinter.UpdateResolution(0.25);
				stringFrames[x] = imagePrinter.ToString();
				Console.WriteLine(stringFrames[x]);
				Thread.Sleep((int)(pause * 1000));
				Console.Clear();
			}
		}
	}
}
