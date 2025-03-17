using System.Drawing;

namespace Image_Resizer
{
	internal class Program
	{
		static void Main()
		{
			Console.Write("Enter Path to image: ");
			string imagePath = Console.ReadLine();

			Bitmap image = new(imagePath);

			string folder = "C:\\Users\\jrsco\\source\\repos\\Image Printer\\Image Resizer\\Resized Images\\";

			for (int x = 2; x <= 256; x *= 2)
			{
				int newWidth = image.Width / x;
				int newHeight = image.Height / x;

				string imageFileName = $"{imagePath.Split('\\')[^1].Split('.')[0]} {newWidth}x{newHeight}.{image.RawFormat}";

				Bitmap resizedBitmap = ResizeImage(image, newWidth, newHeight);

				resizedBitmap.Save(folder + imageFileName, image.RawFormat);

				Console.WriteLine($"Saved: {imageFileName}");
			}
		}

		/// <summary>
		/// Resizes an image to the given dimensions
		/// </summary>
		/// <param name="originalImage">The original image</param>
		/// <param name="newWidth">The width resize to</param>
		/// <param name="newHeight">The height to resize to</param>
		/// <returns>The resized image</returns>
		public static Bitmap ResizeImage(Bitmap originalImage, int newWidth, int newHeight)
		{
			Bitmap newImage = new(newWidth, newHeight);
			using (Graphics graphicsHandle = Graphics.FromImage(newImage))
			{
				graphicsHandle.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				graphicsHandle.DrawImage(originalImage, 0, 0, newWidth, newHeight);
			}
			return newImage;
		}
	}
}
