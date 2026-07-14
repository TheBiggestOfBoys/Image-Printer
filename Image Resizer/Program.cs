using System;
using System.Drawing;
using System.IO;

using Image_Printer;

namespace Image_Resizer
{
	internal class Program
	{
		private static void Main()
		{
			Console.Write("Enter Path to image: ");
			string imagePath = Console.ReadLine();

			Bitmap image = new(imagePath);

			string folder = Path.GetDirectoryName(imagePath);

			for (int x = 2; x <= 256; x *= 2)
			{
				int newWidth = image.Width / x;
				int newHeight = image.Height / x;

				float percentage = newWidth / image.Width;

				string imageFileName = $"{imagePath.Split('\\')[^1].Split('.')[0]} {newWidth}x{newHeight}.{image.RawFormat}";

				ImagePrinter imagePrinter = new(imagePath);
				imagePrinter.UpdateResolution(percentage);

				Bitmap resizedBitmap = imagePrinter.Picture;

				resizedBitmap.Save(folder + imageFileName, image.RawFormat);

				Console.WriteLine($"Saved: {imageFileName}");
			}
		}
	}
}
