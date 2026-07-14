using System;
using System.IO;
using System.Linq;

namespace Image_Printer.Video
{
	/// <summary>
	/// Layout for video → ASCII exports:
	/// {parent}/{VideoName}/Image frames/*.png
	/// {parent}/{VideoName}/Text frames/*.txt
	/// </summary>
	public static class VideoAsciiExportPaths
	{
		public const string ImageFramesFolderName = "Image frames";
		public const string TextFramesFolderName = "Text frames";

		public static string SanitizeFolderName(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return "Video";
			}

			char[] invalid = Path.GetInvalidFileNameChars();
			string cleaned = new(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
			cleaned = cleaned.Trim().TrimEnd('.');
			return string.IsNullOrWhiteSpace(cleaned) ? "Video" : cleaned;
		}

		public static string GetExportRoot(string parentDirectory, string videoPath)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectory);
			ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);

			string stem = SanitizeFolderName(Path.GetFileNameWithoutExtension(videoPath));
			return Path.Combine(parentDirectory, stem);
		}

		public static (string Root, string ImageFramesDir, string TextFramesDir) CreateLayout(
			string parentDirectory,
			string videoPath)
		{
			string root = GetExportRoot(parentDirectory, videoPath);
			string imageFramesDir = Path.Combine(root, ImageFramesFolderName);
			string textFramesDir = Path.Combine(root, TextFramesFolderName);
			_ = Directory.CreateDirectory(imageFramesDir);
			_ = Directory.CreateDirectory(textFramesDir);
			return (root, imageFramesDir, textFramesDir);
		}
	}
}
