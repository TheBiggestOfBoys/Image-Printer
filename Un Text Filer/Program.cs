using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Un_Text_Filer
{
	internal class Program
	{
		private static readonly char[] ASCIIGrayscaleChars = [' ', '.', ',', '-', '~', '+', '*', '%', '$', '#', '@'];

		static void Main()
		{
			Console.Write("Enter path to '.txt' file: ");
			string path = Console.ReadLine();
			string name = path.Split('\\')[^1].Split('.')[0];

			char[][] ASCIIarray = TextFileToArray(path);

			Bitmap image = ASCIItoImage(ASCIIarray);

			image.Save($"C:\\Users\\jrsco\\source\\repos\\Image Printer\\Un Text Filer\\GrayScale Images\\{name}.bmp");
		}

		/// <summary>
		/// Converts all ASCII chars into a grayscale image
		/// </summary>
		/// <param name="array"></param>
		/// <returns></returns>
		public static Bitmap ASCIItoImage(char[][] array)
		{
			Bitmap image = new(array[0].Length, array.Length);

			for (int x = 0; x < array.Length; x++)
			{
				for (int y = 0; y < array[x].Length; y++)
				{
					Color gray = CharacterToGrayscale(array[x][y]);
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
			int index = Array.IndexOf(ASCIIGrayscaleChars, character);
			int grayValue = index * ((255 / ASCIIGrayscaleChars.Length) - 1);
			return Color.FromArgb(grayValue, grayValue, grayValue);
		}

		/// <summary>
		/// Gets the lines of the text file and turns it into a 2D char array
		/// </summary>
		/// <param name="filePath">The path of the text file</param>
		/// <returns>2D char array of the text file</returns>
		public static char[][] TextFileToArray(string filePath)
		{
			List<char[]> ASCIIlist = [];
			StreamReader reader = new(filePath);

			while (!reader.EndOfStream)
			{
				string line = reader.ReadLine();
				ASCIIlist.Add(line.ToCharArray());
			}
			reader.Close();

			return ASCIIlist.ToArray();
		}
	}
}
