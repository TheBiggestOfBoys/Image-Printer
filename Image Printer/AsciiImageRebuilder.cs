using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Image_Printer
{
	/// <summary>
	/// Rebuilds a grayscale <see cref="Bitmap"/> from ASCII text (UnText path).
	/// </summary>
	public static class AsciiImageRebuilder
	{
		public static Bitmap ToBitmap(AsciiDocument document, AsciiPalette palette = null)
		{
			palette ??= new AsciiPalette();
			char[,] grid = document.Grid;
			int height = document.Rows;
			int width = document.Columns;
			Bitmap bitmap = new(width, height);

			for (int row = 0; row < height; row++)
			{
				for (int col = 0; col < width; col++)
				{
					bitmap.SetPixel(col, row, palette.CharToColor(grid[row, col]));
				}
			}

			return bitmap;
		}

		public static Bitmap FromFile(string textPath, AsciiPalette palette = null)
		{
			return ToBitmap(AsciiDocument.Load(textPath), palette);
		}

		public static void SaveBitmap(Bitmap bitmap, string path)
		{
			ImageFormat format = Path.GetExtension(path).ToLowerInvariant() switch
			{
				".png" => ImageFormat.Png,
				".jpg" or ".jpeg" => ImageFormat.Jpeg,
				_ => ImageFormat.Bmp
			};
			bitmap.Save(path, format);
		}
	}
}
