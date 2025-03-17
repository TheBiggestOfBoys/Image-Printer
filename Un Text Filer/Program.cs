using Image_Printer;
using System;
using System.Drawing;
using System.IO;

namespace Un_Text_Filer
{
	internal class Program
	{
		static void Main()
		{
			ImagePrinter imagePrinter = new(new Bitmap(0, 0));

			Console.Write("Enter path to '.txt' file: ");
			string path = Console.ReadLine();
			string name = path.Split('\\')[^1].Split('.')[0];

			char[,] ASCIIarray = TextFileToArray(path);

			Bitmap image = ASCIItoImage(ASCIIarray);

			image.Save($"C:\\Users\\jrsco\\source\\repos\\Image Printer\\Un Text Filer\\GrayScale Images\\{name}.bmp");
		}

		/// <summary>
		/// Converts all ASCII chars into a grayscale image
		/// </summary>
		/// <param name="array"></param>
		/// <returns></returns>
		public static Bitmap ASCIItoImage(char[,] array)
		{
			Bitmap image = new(array.GetLength(0), array.Length);

			for (int x = 0; x < array.Length; x++)
			{
				for (int y = 0; y < array.GetLength(1); y++)
				{
					Color gray = CharacterToGrayscale(array[x, y]);
					image.SetPixel(y, x, gray);
				}
			}
			return image;
		}

		/// <summary>
		/// Converts a character into a grayscale color.  There will only be the length of the ASCII char array number of gray values.
		/// </summary>
		/// <param name="character">The character to convert</param>
		/// <returns>The stepped grayscale color</returns>
		public static Color CharacterToGrayscale(char character)
		{
			char[] ASCIIGrayscaleChars = [' ', '.', ',', '-', '~', '+', '*', '%', '$', '#', '@'];
			int index = Array.IndexOf(ASCIIGrayscaleChars, character);
			int grayValue = index * ((255 / ASCIIGrayscaleChars.Length) - 1);
			return Color.FromArgb(grayValue, grayValue, grayValue);
		}

		/// <summary>
		/// Gets the lines of the text file and turns it into a 2D char array
		/// </summary>
		/// <param name="filePath">The path of the text file</param>
		/// <returns>2D char array of the text file</returns>
		public static char[,] TextFileToArray(string filePath)
		{
			string[] lines = File.ReadAllLines(filePath);

			int height = lines.Length;
			int width = lines[0].Length;

			char[,] array = new char[height, width];

			for (int x = 0; x < height; x++)
			{
				for (int y = 0; y < height; y++)
				{
					array[x, y] = lines[x][y];
				}
			}

			return array;
		}
	}
}
