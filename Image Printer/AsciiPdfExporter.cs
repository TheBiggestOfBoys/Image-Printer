using System;
using System.IO;
using System.Text.RegularExpressions;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Image_Printer
{
	public sealed class AsciiPdfExportResult
	{
		public bool IsLandscape { get; init; }
		public float FontSizePt { get; init; }
		public float ScalePercent { get; init; }
		public string Path { get; init; }
	}

	/// <summary>
	/// Writes ASCII art as real selectable monospace text on exactly one Letter page
	/// with auto orientation and fitted scale.
	/// </summary>
	public static class AsciiPdfExporter
	{
		private const float MarginInches = 0.35f;
		private const float PointsPerInch = 72f;
		private const float BaselineFontPt = 12f;
		private const float MinFontPt = 1f;
		private const float MaxFontPt = 48f;
		/// <summary>Consolas advance width relative to font size (generous → smaller font, stays on one page).</summary>
		private const float GlyphWidthFactor = 0.68f;
		/// <summary>Slightly above LineHeight(1) so QuestPDF line metrics do not overflow.</summary>
		private const float LineHeightFactor = 1.2f;
		private const float FitSafety = 0.9f;

		private static readonly Regex PdfPageRegex = new(@"/Type\s*/Page(?!\s*/)", RegexOptions.Compiled);

		static AsciiPdfExporter()
		{
			QuestPDF.Settings.License = LicenseType.Community;
		}

		public static AsciiPdfExportResult Save(string asciiText, string pdfPath)
		{
			AsciiDocument document = AsciiDocument.FromText(asciiText ?? string.Empty);
			return Save(document, pdfPath);
		}

		public static AsciiPdfExportResult Save(AsciiDocument document, string pdfPath)
		{
			ArgumentNullException.ThrowIfNull(document);
			ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

			int rows = Math.Max(1, document.Rows);
			int cols = Math.Max(1, document.Columns);
			string[] lines = new string[rows];
			for (int r = 0; r < rows; r++)
			{
				char[] rowChars = new char[cols];
				for (int c = 0; c < cols; c++)
				{
					rowChars[c] = document.Grid[r, c];
				}

				lines[r] = new string(rowChars);
			}

			float marginPt = MarginInches * PointsPerInch;
			float pageW = PageSizes.Letter.Width;
			float pageH = PageSizes.Letter.Height;

			float contentAspect = cols / (float)rows;
			float portraitPrintableW = pageW - (2 * marginPt);
			float portraitPrintableH = pageH - (2 * marginPt);
			bool landscape = contentAspect > (portraitPrintableW / portraitPrintableH);

			float printableW = landscape ? pageH - (2 * marginPt) : portraitPrintableW;
			float printableH = landscape ? pageW - (2 * marginPt) : portraitPrintableH;

			PageSize pageSize = landscape
				? new PageSize(pageH, pageW)
				: PageSizes.Letter;

			float fontSizePt = FitFontSize(cols, rows, printableW * FitSafety, printableH * FitSafety);
			string body = string.Join("\n", lines);

			// Shrink until the generated PDF is forced to a single page.
			for (int attempt = 0; attempt < 12; attempt++)
			{
				byte[] pdfBytes = GenerateSinglePageCandidate(pageSize, marginPt, printableW, printableH, fontSizePt, body);
				int pages = CountPdfPages(pdfBytes);
				if (pages <= 1)
				{
					File.WriteAllBytes(pdfPath, pdfBytes);
					return new AsciiPdfExportResult
					{
						IsLandscape = landscape,
						FontSizePt = fontSizePt,
						ScalePercent = fontSizePt / BaselineFontPt * 100f,
						Path = pdfPath
					};
				}

				fontSizePt = Math.Max(MinFontPt, (float)Math.Round(fontSizePt * 0.85f, 2));
			}

			// Last resort: generate at minimum size and keep only page 1.
			byte[] fallback = GenerateSinglePageCandidate(pageSize, marginPt, printableW, printableH, MinFontPt, body);
			string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
			try
			{
				File.WriteAllBytes(tempPath, fallback);
				DocumentOperation.LoadFile(tempPath).TakePages("1").Save(pdfPath);
			}
			finally
			{
				try
				{
					if (File.Exists(tempPath))
					{
						File.Delete(tempPath);
					}
				}
				catch { /* ignore */ }
			}

			return new AsciiPdfExportResult
			{
				IsLandscape = landscape,
				FontSizePt = MinFontPt,
				ScalePercent = MinFontPt / BaselineFontPt * 100f,
				Path = pdfPath
			};
		}

		private static byte[] GenerateSinglePageCandidate(
			PageSize pageSize,
			float marginPt,
			float printableW,
			float printableH,
			float fontSizePt,
			string body)
		{
			return Document.Create(container =>
			{
				_ = container.Page(page =>
				{
					page.Size(pageSize);
					page.Margin(marginPt);
					page.DefaultTextStyle(x => x
						.FontFamily("Consolas")
						.FontSize(fontSizePt)
						.FontColor(Colors.Black)
						.LineHeight(1f));

					// Fixed printable box + ScaleToFit keeps content from flowing to page 2.
					_ = page.Content()
						.Width(printableW)
						.Height(printableH)
						.AlignCenter()
						.AlignMiddle()
						.ScaleToFit()
						.Text(body);
				});
			}).GeneratePdf();
		}

		private static float FitFontSize(int cols, int rows, float printableWPt, float printableHPt)
		{
			float byWidth = printableWPt / (cols * GlyphWidthFactor);
			float byHeight = printableHPt / (rows * LineHeightFactor);
			float size = Math.Min(byWidth, byHeight);
			size = Math.Clamp(size, MinFontPt, MaxFontPt);
			return (float)Math.Round(size, 2);
		}

		private static int CountPdfPages(byte[] pdfBytes)
		{
			if (pdfBytes is null || pdfBytes.Length == 0)
			{
				return 0;
			}

			// Match dictionary entries for page objects, not the /Pages tree node.
			return PdfPageRegex.Matches(System.Text.Encoding.Latin1.GetString(pdfBytes)).Count;
		}
	}
}
