using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Program;

[SupportedOSPlatform("windows")]
internal static partial class Program
{
	private const string BASE_NAME = "bad_apple";

	private static unsafe void ReduceImages()
	{
		var fns = Directory.GetFiles($"./{BASE_NAME}");
		Array.Sort(fns, (fn1, fn2) =>
		{
			static int GetNumber(string fn)
			{
				var filename = Path.GetFileNameWithoutExtension(fn);
				var num = filename.Replace(BASE_NAME, "");
				return int.Parse(num);
			}

			var num1 = GetNumber(fn1);
			var num2 = GetNumber(fn2);
			if (num1 < num2)
			{
				return -1;
			}

			// ReSharper disable once ConvertIfStatementToReturnStatement
			if (num1 > num2)
			{
				return 1;
			}

			return 0;
		});

		Directory.CreateDirectory($"./reduced_{BASE_NAME}");
		foreach (var fn in fns)
		{
			using var bmp = new Bitmap(fn);
			using var newBmp = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format16bppRgb555);
			var bits = bmp.LockBits(new(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
			var newBits = newBmp.LockBits(new(0, 0, newBmp.Width, newBmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb555);
			try
			{
				var bitSpan = new ReadOnlySpan<byte>(bits.Scan0.ToPointer(), bits.Stride * bits.Height);
				var newBitSpan = new Span<byte>(newBits.Scan0.ToPointer(), newBits.Stride * newBits.Height);
				for (var y = 0; y < bits.Height; y++)
				{
					var scanline = MemoryMarshal.Cast<byte, uint>(bitSpan.Slice(y * bits.Stride, bits.Width * sizeof(uint)));
					var newScanline = MemoryMarshal.Cast<byte, ushort>(newBitSpan.Slice(y * newBits.Stride, newBits.Width * sizeof(ushort)));
#if true
					for (var x = 0; x < bits.Width; x++)
					{
						var pixel = scanline[x];
						var r = (pixel >> 16) & 0xFF;
						var g = (pixel >> 8) & 0xFF;
						var b = (pixel >> 0) & 0xFF;
						var averageCol = Math.Round((r + g + b) / 3.0, MidpointRounding.AwayFromZero);
						ushort grayscalePixel = averageCol switch
						{
							< 0x2B => 0x0000,
							< 0x80 => 0x294A,
							< 0xD5 => 0x56B5,
							_ => 0x7FFF
						};

						newScanline[x] = grayscalePixel;
					}
#else
					if (bits.Width % 40 != 0)
					{
						throw new InvalidOperationException("Not a multiple of 40");
					}

					for (var x = 0; x < bits.Width;)
					{
						static ushort ToGrayscalePixel(uint pixel)
						{
							var r = (pixel >> 16) & 0xFF;
							var g = (pixel >> 8) & 0xFF;
							var b = (pixel >> 0) & 0xFF;
							var averageCol = Math.Round((r + g + b) / 3.0, MidpointRounding.AwayFromZero);
							return averageCol switch
							{
								< 0x2B => 0x0000,
								< 0x80 => 0x294A,
								< 0xD5 => 0x56B5,
								_ => 0x7FFF
							};
						}

						// scale down first 8 pixels to 2:1
						for (var i = 0; i < 4; i++)
						{
							var x0 = ToGrayscalePixel(scanline[x + 0]);
							var x1 = ToGrayscalePixel(scanline[x + 1]);
							if (x0 != x1)
							{
								// pixels are not the the same, can't use them as is
								// nearest neighbor is just left pixel, I think?
								x1 = x0;
							}

							newScanline[x + 0] = x0;
							newScanline[x + 1] = x1;
							x += 2;
						}

						// scale down next 12 pixels to 3:1
						for (var i = 0; i < 4; i++)
						{
							var x0 = ToGrayscalePixel(scanline[x + 0]);
							var x1 = ToGrayscalePixel(scanline[x + 1]);
							var x2 = ToGrayscalePixel(scanline[x + 2]);
							if (x0 != x1 || x2 != x1)
							{
								// pixels are not the the same, can't use them as is
								// nearest neighbor is just center pixel, I think?
								x0 = x1;
								x2 = x1;
							}

							newScanline[x + 0] = x0;
							newScanline[x + 1] = x1;
							newScanline[x + 2] = x2;
							x += 3;
						}
					}
#endif
				}
			}
			finally
			{
				bmp.UnlockBits(bits);
				newBmp.UnlockBits(newBits);
			}

			var filename = Path.GetFileName(fn);
			var newPath = $"./reduced_{BASE_NAME}/{filename}";
			if (File.Exists(newPath))
			{
				File.Delete(newPath);
			}

			newBmp.Save(newPath, ImageFormat.Png);
		}
	}
}
