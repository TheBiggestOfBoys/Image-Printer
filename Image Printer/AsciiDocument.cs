using System;
using System.IO;
using System.Text;

namespace Image_Printer
{
	/// <summary>
	/// Rectangular ASCII text documents (load/save and grid helpers).
	/// </summary>
	public sealed class AsciiDocument(char[,] grid)
	{
		public char[,] Grid { get; } = grid ?? throw new ArgumentNullException(nameof(grid));

		public int Rows => Grid.GetLength(0);

		public int Columns => Grid.GetLength(1);

		public static AsciiDocument FromText(string text)
		{
			string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
				.Split('\n', StringSplitOptions.None);

			// Drop a trailing empty line from final newline.
			if (lines.Length > 0 && lines[^1].Length == 0)
			{
				Array.Resize(ref lines, lines.Length - 1);
			}

			if (lines.Length == 0)
			{
				return new AsciiDocument(new char[1, 1] { { ' ' } });
			}

			int width = 0;
			foreach (string line in lines)
			{
				width = Math.Max(width, line.Length);
			}

			width = Math.Max(1, width);

			char[,] grid = new char[lines.Length, width];
			for (int row = 0; row < lines.Length; row++)
			{
				string line = lines[row];
				for (int col = 0; col < width; col++)
				{
					grid[row, col] = col < line.Length ? line[col] : ' ';
				}
			}

			return new AsciiDocument(grid);
		}

		public static AsciiDocument Load(string path)
		{
			return FromText(File.ReadAllText(path));
		}

		public void Save(string path)
		{
			File.WriteAllText(path, ToString(), Encoding.ASCII);
		}

		public override string ToString()
		{
			StringBuilder sb = new();
			for (int row = 0; row < Rows; row++)
			{
				for (int col = 0; col < Columns; col++)
				{
					_ = sb.Append(Grid[row, col]);
				}

				_ = sb.AppendLine();
			}
			return sb.ToString();
		}
	}
}
