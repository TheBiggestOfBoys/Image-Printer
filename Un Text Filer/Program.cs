using System;
using System.Drawing;
using System.IO;

using Image_Printer;

namespace Un_Text_Filer
{
	internal class Program
	{
		private static void Main()
		{
			Console.Write("Enter path to '.txt' file: ");
			string path = Console.ReadLine();
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				Console.WriteLine("File not found.");
				return;
			}

			AsciiDocument document = AsciiDocument.Load(path);
			using Bitmap image = AsciiImageRebuilder.ToBitmap(document);
			string savePath = Path.Combine(
				Path.GetDirectoryName(path) ?? ".",
				Path.GetFileNameWithoutExtension(path) + ".bmp");
			AsciiImageRebuilder.SaveBitmap(image, savePath);
			Console.WriteLine($"Saved: {savePath}");
		}
	}
}
