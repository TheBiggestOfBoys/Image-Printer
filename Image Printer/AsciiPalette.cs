using System;
using System.Collections.Generic;
using System.Drawing;

namespace Image_Printer
{
	/// <summary>
	/// Named ASCII grayscale sets with bidirectional color conversion.
	/// </summary>
	public sealed class AsciiPalette
	{
		private static readonly char[] DefaultChars = [' ', '.', ',', '-', '~', '+', '*', '%', '$', '#', '@'];
		private static readonly char[] NumbersChars = [' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];
		private static readonly char[] LettersChars = [' ', 'i', 'l', 'I', 'L', 't', 'f', 'r', 'x', 'v', 'u', 'j', 'z', 'c', 's', 'e', 'a', 'o', 'n', 'm', 'w', 'k', 'b', 'd', 'p', 'q', 'h', 'y', 'T', 'F', 'J', 'E', 'A', 'O', 'C', 'D', 'P', 'Q', 'H', 'U', 'K', 'R', 'V', 'Y', 'N', 'X', 'M', 'W', 'B', 'G', 'Z'];
		private static readonly char[] LowercaseChars = [' ', 'w', 'k', 'g', 'q', 'p', 'd', 'b', 'y', 'c', 'z', 's', 'e', 'r', 'f', 't', 'l', 'i', 'j', 'u', 'v', 'x', 'n', 'm'];
		private static readonly char[] UppercaseChars = [' ', 'W', 'K', 'G', 'Q', 'P', 'D', 'B', 'Y', 'C', 'Z', 'S', 'E', 'R', 'F', 'T', 'L', 'I', 'J', 'U', 'V', 'X', 'N', 'M'];
		private static readonly char[] NumbersAndLettersChars = ['1', 'i', 'l', 'I', 'j', 't', 'f', 'r', 'x', 'v', 'u', 'z', 'J', 'L', 'c', 's', 'e', 'a', 'o', 'n', 'm', 'w', 'k', 'b', 'd', 'p', 'q', 'h', 'y', 'T', 'F', '2', '3', '4', '5', '6', '7', '8', '9', '0', 'E', 'A', 'O', 'C', 'D', 'P', 'Q', 'H', 'U', 'K', 'R', 'V', 'Y', 'N', 'X', 'M', 'W', 'B', 'G', 'Z'];
		private static readonly char[] AllChars = [' ', '`', '^', '"', ',', ':', ';', '!', '~', '.', '-', '_', '+', '<', '>', 'i', 'l', 'I', 'j', 't', 'f', 'r', 'x', 'v', 'u', 'z', 'J', 'L', 'c', 's', 'e', 'a', 'o', 'n', 'm', 'w', 'k', 'b', 'd', 'p', 'q', 'h', 'y', 'T', 'F', 'E', 'A', 'O', 'C', 'D', 'P', 'Q', 'H', 'U', 'K', 'R', 'V', 'Y', 'N', 'X', 'M', 'W', 'B', 'G', 'Z', '2', '3', '4', '5', '6', '7', '8', '9', '0', 'S', '$', '%', '#', '@'];

		public List<char> Characters { get; private set; } = [.. DefaultChars];

		public ImagePrinter.ASCIISet SelectedSet { get; private set; } = ImagePrinter.ASCIISet.Default;

		public bool Invert { get; private set; }

		public void SetSet(ImagePrinter.ASCIISet set)
		{
			SelectedSet = set;
			Characters = CreateSet(set);
			if (Invert)
			{
				Characters.Reverse();
			}
		}

		/// <summary>
		/// Replaces characters with the caller's final order (does not re-apply invert).
		/// </summary>
		public void ReplaceCharacters(IEnumerable<char> characters)
		{
			SelectedSet = ImagePrinter.ASCIISet.Custom;
			Characters = [.. characters];
			if (Characters.Count == 0)
			{
				Characters.Add(' ');
			}
		}

		public void SetCustom(IEnumerable<char> characters)
		{
			ReplaceCharacters(characters);
			if (Invert)
			{
				Characters.Reverse();
			}
		}

		public void ToggleInvert()
		{
			Invert = !Invert;
			Characters.Reverse();
		}

		public char ColorToChar(Color color)
		{
			int length = Math.Max(1, Characters.Count);
			float grayValue = color.GetBrightness();
			int index = Math.Clamp((int)(grayValue * length), 0, length - 1);
			return Characters[index];
		}

		public Color CharToColor(char character)
		{
			int length = Math.Max(1, Characters.Count);
			int index = Characters.IndexOf(character);
			if (index < 0)
			{
				index = 0;
			}

			int step = Math.Max(1, 255 / length);
			int gray = Math.Clamp(index * step, 0, 255);
			return Color.FromArgb(gray, gray, gray);
		}

		public static List<char> CreateSet(ImagePrinter.ASCIISet set)
		{
			return set switch
			{
				ImagePrinter.ASCIISet.Default => [.. DefaultChars],
				ImagePrinter.ASCIISet.Numbers => [.. NumbersChars],
				ImagePrinter.ASCIISet.Letters => [.. LettersChars],
				ImagePrinter.ASCIISet.Lowercase => [.. LowercaseChars],
				ImagePrinter.ASCIISet.Uppercase => [.. UppercaseChars],
				ImagePrinter.ASCIISet.NumbersAndLetters => [.. NumbersAndLettersChars],
				ImagePrinter.ASCIISet.All => [.. AllChars],
				ImagePrinter.ASCIISet.Custom => [' '],
				_ => [.. DefaultChars]
			};
		}
	}
}
