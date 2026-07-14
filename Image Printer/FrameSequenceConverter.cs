using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace Image_Printer
{
	/// <summary>
	/// Converts a sequence of bitmaps to ASCII text using shared printer settings.
	/// </summary>
	public sealed class FrameSequenceConverter
	{
		public double Resolution { get; set; } = 0.25;

		public bool Invert { get; set; }

		public ImagePrinter.ASCIISet AsciiSet { get; set; } = ImagePrinter.ASCIISet.Default;

		public List<char> CustomCharacters { get; set; }

		public string ConvertFrame(Bitmap frame, string name = null)
		{
			ImagePrinter printer = name is null
				? new ImagePrinter(frame, Resolution, Invert, AsciiSet)
				: new ImagePrinter(frame, Resolution, Invert, AsciiSet, name);

			if (AsciiSet == ImagePrinter.ASCIISet.Custom && CustomCharacters is { Count: > 0 })
			{
				printer.ASCIIGrayscaleChars = CustomCharacters;
			}

			return printer.ToString();
		}

		public IReadOnlyList<string> ConvertAll(IEnumerable<Bitmap> frames, string namePrefix = "frame")
		{
			List<string> results = [];
			int index = 0;
			foreach (Bitmap frame in frames)
			{
				results.Add(ConvertFrame(frame, $"{namePrefix}{index}"));
				index++;
			}
			return results;
		}

		public void WriteAll(
			IEnumerable<Bitmap> frames,
			string outputDirectory,
			string namePrefix = "frame",
			Action<int, int> progress = null,
			int knownTotal = -1)
		{
			_ = Directory.CreateDirectory(outputDirectory);
			int index = 0;
			int total = knownTotal > 0 ? knownTotal : -1;
			foreach (Bitmap frame in frames)
			{
				string ascii = ConvertFrame(frame, $"{namePrefix}{index}");
				string path = Path.Combine(outputDirectory, $"{namePrefix}{index}.txt");
				File.WriteAllText(path, ascii, Encoding.ASCII);
				index++;
				progress?.Invoke(index, total > 0 ? total : index);
			}
		}
	}
}
