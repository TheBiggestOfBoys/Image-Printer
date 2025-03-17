using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;

namespace Image_Printer
{
	internal class Program
	{
		/// <summary>
		/// The Main method which reads the file's path and type and calls the appropriate methods
		/// </summary>
		static void Main(string[] args)
		{
			string path;
			double resolution;
			bool invert;
			ImagePrinter.ASCIISet set;

			if (args.Length == 0)
			{
				do
				{
					Console.Write("Please paste the path to the image you want to print: ");
					path = Console.ReadLine();
				}
				while (!File.Exists(path));

				do { Console.Write("What resolution? (1-100%): "); }
				while (!double.TryParse(Console.ReadLine(), out resolution));

				do { Console.Write("Invert Grayscale?: "); }
				while (!bool.TryParse(Console.ReadLine(), out invert));

				do
				{
					foreach (ImagePrinter.ASCIISet setOfASCII in Enum.GetValues(typeof(ImagePrinter.ASCIISet)))
					{
						Console.WriteLine(setOfASCII);
					}
					Console.Write("ASCII set (ENTER for Default): ");
				}
				while (!Enum.TryParse(Console.ReadLine(), out set));
			}
			else
			{
				path = args[0];
				resolution = double.Parse(args[1]);
				invert = bool.Parse(args[2]);
				set = (ImagePrinter.ASCIISet)Enum.Parse(typeof(ImagePrinter.ASCIISet), args[3]);
			}

			string name = Path.GetFileNameWithoutExtension(path);
			string parentDirectory = Path.GetDirectoryName(path) + '\\';
			// Construct a new Bitmap Object using the path
			Bitmap img = new(path);

			if (img.RawFormat.Equals(ImageFormat.Gif))
			{
				string folder = parentDirectory + name + '\\';
				Directory.CreateDirectory(folder);

				// Number of frames
				int frameCount = img.GetFrameCount(FrameDimension.Time);
				// Initialize the array of frames with the correct length
				Bitmap[] gifFrames = new Bitmap[frameCount];

				// Collect and assign the frames
				for (int i = 0; i < frameCount; i++)
				{
					// Return an Image at a certain index
					img.SelectActiveFrame(FrameDimension.Time, i);
					gifFrames[i] = img.Clone() as Bitmap;
				}

				string[] framesAsStrings = new string[frameCount];
				for (int x = 0; x < frameCount; x++)
				{
					ImagePrinter gifPrinter = new(gifFrames[x], resolution, invert, set);
					framesAsStrings[x] = gifPrinter.ToString();
					File.WriteAllText(folder + name + " #" + x + ".txt", framesAsStrings[x], Encoding.ASCII);
				}
				foreach (string frame in framesAsStrings)
				{
					Console.Clear();
					Console.WriteLine(frame);
					Thread.Sleep(75);
				}
			}

			else
			{
				ImagePrinter imagePrinter = new(path, resolution, invert, set);

				string text = imagePrinter.ToString();
				Console.WriteLine(text);
				File.WriteAllText(parentDirectory + name + ".txt", text, Encoding.ASCII);
			}
		}
	}
}
