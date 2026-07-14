using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace Image_Printer
{
	public class ImagePrinter
	{
		public AsciiPalette Palette { get; } = new();

		/// <summary>
		/// Active ASCII grayscale characters (shared with <see cref="Palette"/>).
		/// </summary>
		public List<char> ASCIIGrayscaleChars
		{
			get => Palette.Characters;
			set => Palette.ReplaceCharacters(value);
		}

		public bool Invert => Palette.Invert;

		public ASCIISet SelectedASCIISet => Palette.SelectedSet;

		public enum ASCIISet
		{
			Default,
			Numbers,
			Letters,
			Lowercase,
			Uppercase,
			NumbersAndLetters,
			All,
			Custom
		}

		private char[,] _asciiArray;

		public Bitmap Picture { get; private set; }

		private readonly Bitmap _originalPicture;

		public double Resolution { get; private set; } = 1;

		public string Name { get; }

		public string FileName =>
			$"{Name} {Resolution:P}" + (Invert ? " Inverted" : string.Empty) + ".txt";

		public ImagePrinter(string filePath)
		{
			_originalPicture = new(filePath);
			Picture = _originalPicture;
			Name = Path.GetFileNameWithoutExtension(filePath);
		}

		public ImagePrinter(string filePath, double resolution) : this(filePath)
		{
			UpdateResolution(resolution);
		}

		public ImagePrinter(string filePath, double resolution, bool invert) : this(filePath, resolution)
		{
			if (invert)
			{
				ReverseGrayscale();
			}
		}

		public ImagePrinter(string filePath, double resolution, bool invert, ASCIISet set)
			: this(filePath, resolution, invert)
		{
			SetASCIIGrayscaleChars(set);
		}

		public ImagePrinter(Bitmap image, string name = null)
		{
			_originalPicture = image;
			Picture = _originalPicture;
			Name = string.IsNullOrWhiteSpace(name) ? "Gradient Test" : name;
		}

		public ImagePrinter(Bitmap image, double resolution) : this(image)
		{
			UpdateResolution(resolution);
		}

		public ImagePrinter(Bitmap image, double resolution, bool invert) : this(image, resolution)
		{
			if (invert)
			{
				ReverseGrayscale();
			}
		}

		public ImagePrinter(Bitmap image, double resolution, bool invert, ASCIISet set)
			: this(image, resolution, invert)
		{
			SetASCIIGrayscaleChars(set);
		}

		public ImagePrinter(Bitmap image, double resolution, bool invert, ASCIISet set, string name)
			: this(image, name)
		{
			UpdateResolution(resolution);
			if (invert)
			{
				ReverseGrayscale();
			}

			SetASCIIGrayscaleChars(set);
		}

		public void SetASCIIGrayscaleChars(ASCIISet set)
		{
			Palette.SetSet(set);
		}

		public char[,] ToAsciiGrid()
		{
			_asciiArray = GenerateAscii();
			return _asciiArray;
		}

		public override string ToString()
		{
			char[,] asciiArray = ToAsciiGrid();
			StringBuilder stringBuilder = new();
			for (int row = 0; row < asciiArray.GetLength(0); row++)
			{
				for (int col = 0; col < asciiArray.GetLength(1); col++)
				{
					_ = stringBuilder.Append(asciiArray[row, col]);
				}

				_ = stringBuilder.AppendLine();
			}
			return stringBuilder.ToString();
		}

		public void ReverseGrayscale()
		{
			Palette.ToggleInvert();
		}

		public void UpdateResolution(double percent)
		{
			Resolution = percent;
			int newWidth = Math.Max(1, (int)(_originalPicture.Width * percent));
			int newHeight = Math.Max(1, (int)(_originalPicture.Height * percent));
			Picture = ResizeImage(_originalPicture, newWidth, newHeight);
		}

		public static Bitmap ResizeImage(Bitmap originalImage, int newWidth, int newHeight)
		{
			Bitmap newImage = new(newWidth, newHeight);
			using Graphics graphicsHandle = Graphics.FromImage(newImage);
			graphicsHandle.DrawImage(originalImage, 0, 0, newWidth, newHeight);
			return newImage;
		}

		public static Bitmap CreateGradient()
		{
			Bitmap tempBitmap = new(256, 256);
			for (int row = 0; row < 256; row++)
			{
				for (int col = 0; col < 256; col++)
				{
					tempBitmap.SetPixel(row, col, Color.FromArgb(col, col, col));
				}
			}
			return tempBitmap;
		}

		private char[,] GenerateAscii()
		{
			char[,] asciiArray = new char[Picture.Height, Picture.Width];
			for (int i = 0; i < Picture.Height; i++)
			{
				for (int j = 0; j < Picture.Width; j++)
				{
					asciiArray[i, j] = Palette.ColorToChar(Picture.GetPixel(j, i));
				}
			}
			return asciiArray;
		}
	}
}
