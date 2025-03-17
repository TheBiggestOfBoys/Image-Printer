using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace Image_Printer
{
	public class ImagePrinter
	{
		#region ASCII Info
		/// <summary>
		/// The original grayscale characters
		/// </summary>
		private static readonly char[] OriginalASCIIGrayscaleChars = [' ', '.', ',', '-', '~', '+', '*', '%', '$', '#', '@'];

		#region Alternate Grayscale Rankings
		/// <summary>
		/// A grayscale ranking using only digits
		/// </summary>
		private static readonly char[] ASCIIGrayscaleNumbers = [' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

		#region Letters
		/// <summary>
		/// A grayscale ranking using both lowercase and uppercase letters
		/// </summary>
		private static readonly char[] ASCIIGrayscaleLetters = [' ', 'i', 'l', 'I', 'L', 't', 'f', 'r', 'x', 'v', 'u', 'j', 'z', 'c', 's', 'e', 'a', 'o', 'n', 'm', 'w', 'k', 'b', 'd', 'p', 'q', 'h', 'y', 'T', 'F', 'J', 'E', 'A', 'O', 'C', 'D', 'P', 'Q', 'H', 'U', 'K', 'R', 'V', 'Y', 'N', 'X', 'M', 'W', 'B', 'G', 'Z'];

		/// <summary>
		/// A grayscale ranking using only lowercase letters
		/// </summary>
		private static readonly char[] ASCIIGrayscaleLowercaseLetters = [' ', 'w', 'k', 'g', 'q', 'p', 'd', 'b', 'y', 'c', 'z', 's', 'e', 'r', 'f', 't', 'l', 'i', 'j', 'u', 'v', 'x', 'n', 'm'];

		/// <summary>
		/// A grayscale ranking using only uppercase letters
		/// </summary>
		private static readonly char[] ASCIIGrayscaleUppercaseLetters = [' ', 'W', 'K', 'G', 'Q', 'P', 'D', 'B', 'Y', 'C', 'Z', 'S', 'E', 'R', 'F', 'T', 'L', 'I', 'J', 'U', 'V', 'X', 'N', 'M'];
		#endregion

		#region Mix
		/// <summary>
		/// A grayscale ranking using uppercase and lowercase letters, and numbers
		/// </summary>
		private static readonly char[] ASCIIGrayscaleNumbersAndLetters = ['1', 'i', 'l', 'I', 'j', 't', 'f', 'r', 'x', 'v', 'u', 'z', 'J', 'L', 'c', 's', 'e', 'a', 'o', 'n', 'm', 'w', 'k', 'b', 'd', 'p', 'q', 'h', 'y', 'T', 'F', '2', '3', '4', '5', '6', '7', '8', '9', '0', 'E', 'A', 'O', 'C', 'D', 'P', 'Q', 'H', 'U', 'K', 'R', 'V', 'Y', 'N', 'X', 'M', 'W', 'B', 'G', 'Z'];

		/// <summary>
		/// A grayscale ranking using all keyboard characters
		/// </summary>
		private static readonly char[] ASCIIGrayscaleAll = [' ', '`', '^', '"', ',', ':', ';', '!', '~', '.', '-', '_', '+', '<', '>', 'i', 'l', 'I', 'j', 't', 'f', 'r', 'x', 'v', 'u', 'z', 'J', 'L', 'c', 's', 'e', 'a', 'o', 'n', 'm', 'w', 'k', 'b', 'd', 'p', 'q', 'h', 'y', 'T', 'F', 'E', 'A', 'O', 'C', 'D', 'P', 'Q', 'H', 'U', 'K', 'R', 'V', 'Y', 'N', 'X', 'M', 'W', 'B', 'G', 'Z', '2', '3', '4', '5', '6', '7', '8', '9', '0', 'S', '$', '%', '#', '@'];
		#endregion
		#endregion

		/// <summary>
		/// Array of ASCII characters representing different levels of grayscale
		/// </summary>
		public List<char> ASCIIGrayscaleChars = [.. OriginalASCIIGrayscaleChars];

		/// <summary>
		/// Whether to invert the grayscale values.  Meant to fix white text on a black background & vice versa
		/// </summary>
		public bool Invert { get; private set; }
		#endregion

		/// <summary>
		/// The array of ASCII characters representing the image in grayscale
		/// </summary>
		private char[,] ASCIIArray;

		/// <summary>
		/// The current picture, using the given resolution
		/// </summary>
		public Bitmap Picture { get; private set; }

		/// <summary>
		/// The original picture, to use when resizing
		/// </summary>
		private readonly Bitmap OriginalPicture;

		public double Resolution { get; private set; }

		public readonly string Name;

		public string FileName => $"{Name} {Resolution:P}" + (Invert ? " Inverted" : "") + ".txt";

		#region ASCII Set
		/// <summary>
		/// The header of the selected ASCII set
		/// </summary>
		public ASCIISet selectedASCIISet { get; private set; } = ASCIISet.Default;

		private List<char> ChangeASCIISet() => selectedASCIISet switch
		{
			ASCIISet.Default => [.. OriginalASCIIGrayscaleChars],
			ASCIISet.Numbers => [.. ASCIIGrayscaleNumbers],
			ASCIISet.Letters => [.. ASCIIGrayscaleLetters],
			ASCIISet.Lowercase => [.. ASCIIGrayscaleLowercaseLetters],
			ASCIISet.Uppercase => [.. ASCIIGrayscaleUppercaseLetters],
			ASCIISet.NumbersAndLetters => [.. ASCIIGrayscaleNumbersAndLetters],
			ASCIISet.All => [.. ASCIIGrayscaleAll],
			ASCIISet.Custom => [' '],
			_ => []
		};

		public void SetASCIIGrayscaleChars(ASCIISet set)
		{
			selectedASCIISet = set;
			ASCIIGrayscaleChars = ChangeASCIISet();
		}
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs an `ImagePrinter` with the given file path
		/// </summary>
		/// <param name="path">The file path</param>
		public ImagePrinter(string filePath)
		{
			OriginalPicture = new(filePath);
			Picture = OriginalPicture;

			Name = Path.GetFileNameWithoutExtension(filePath);
		}

		public ImagePrinter(string filePath, double resolution) : this(filePath)
		{
			UpdateResolution(resolution);
		}

		public ImagePrinter(string filePath, double resolution, bool invert) : this(filePath, resolution)
		{
			if (invert) ReverseGrayscale();
		}

		public ImagePrinter(string filePath, double resolution, bool invert, ASCIISet set) : this(filePath, resolution, invert)
		{
			selectedASCIISet = set;
		}

		#region From Bitmap
		public ImagePrinter(Bitmap image)
		{
			OriginalPicture = image;
			Picture = OriginalPicture;

			Name = "Gradient Test";
		}
		public ImagePrinter(Bitmap image, double resolution) : this(image)
		{
			UpdateResolution(resolution);
		}

		public ImagePrinter(Bitmap image, double resolution, bool invert) : this(image, resolution)
		{
			if (invert) ReverseGrayscale();
		}

		public ImagePrinter(Bitmap image, double resolution, bool invert, ASCIISet set) : this(image, resolution, invert)
		{
			selectedASCIISet = set;
		}
		#endregion
		#endregion

		/// <summary>
		/// Converts each image's pixel to an ASCII character of appropriate darkness.
		/// </summary>
		/// <param name="array">The 2D array of colors to get Color data from.</param>
		/// <returns>A 2D array of ASCII character representing the image in grayscale.</returns>
		private char[,] GenerateASCII()
		{
			char[,] asciiArray = new char[Picture.Height, Picture.Width];
			for (int i = 0; i < Picture.Height; i++)
			{
				for (int j = 0; j < Picture.Width; j++)
				{
					asciiArray[i, j] = ColorToASCII(Picture.GetPixel(j, i));
				}
			}
			return asciiArray;
		}

		/// <summary>
		/// Converts a Color to an ASCII character representing its grayscale value.
		/// </summary>
		/// <param name="color">The Color to convert.</param>
		/// <returns>An ASCII character representing the grayscale value of the input Color.</returns>
		private char ColorToASCII(Color color)
		{
			// Calculate the grayscale value of the input color using a weighted average formula
			float grayValue = color.GetBrightness();

			// Calculate the index of the ASCII character representing the grayscale value
			int index = (int)(grayValue * OriginalASCIIGrayscaleChars.Length);
			if (index >= OriginalASCIIGrayscaleChars.Length)
			{
				index = OriginalASCIIGrayscaleChars.Length - 1;
			}

			// Return the ASCII character representing the grayscale value
			return ASCIIGrayscaleChars[index];
		}

		/// <summary>
		/// Converts the 2D ASCII array to a string, as the ToString method
		/// </summary>
		/// <returns>The string of all the characters together.</returns>
		public override string ToString()
		{
			// ASCII isn't generated until this method is called to save on performance when moving the slider
			ASCIIArray = GenerateASCII();
			StringBuilder stringBuilder = new();
			for (int row = 0; row < ASCIIArray.GetLength(0); row++)
			{
				for (int col = 0; col < ASCIIArray.GetLength(1); col++)
				{
					stringBuilder.Append(ASCIIArray[row, col]);
				}
				stringBuilder.AppendLine();
			}
			return stringBuilder.ToString();
		}

		/// <summary>
		/// Reverses the order if the ASCII Grayscale array
		/// </summary>
		public void ReverseGrayscale()
		{
			Invert = !Invert;
			ASCIIGrayscaleChars.Reverse();
		}

		/// <summary>
		/// Updates the image's resolution
		/// </summary>
		public void UpdateResolution(double percent)
		{
			Resolution = percent;
			int newWidth = (int)(OriginalPicture.Width * percent);
			int newHeight = (int)(OriginalPicture.Height * percent);

			if (newWidth < 1) newWidth = 1;
			if (newHeight < 1) newHeight = 1;

			Picture = ResizeImage(OriginalPicture, newWidth, newHeight);
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
			Graphics graphicsHandle = Graphics.FromImage(newImage);

			graphicsHandle.DrawImage(originalImage, 0, 0, newWidth, newHeight);

			return newImage;
		}

		public static Bitmap CreateGradient()
		{
			Color[,] tempArray = new Color[256, 256];
			for (int row = 0; row < tempArray.GetLength(0); row++)
			{
				for (int col = 0; col < tempArray.GetLength(1); col++)
				{
					tempArray[row, col] = Color.FromArgb(col, col, col);
				}
			}

			Bitmap tempBitmap = new(256, 256);
			for (int row = 0; row < tempArray.GetLength(0); row++)
			{
				for (int col = 0; col < tempArray.GetLength(1); col++)
				{
					tempBitmap.SetPixel(row, col, tempArray[row, col]);
				}
			}
			return tempBitmap;
		}

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
	}
}
