using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Program;

internal static partial class Program
{
	private static unsafe void ReduceImages()
	{
		var fns = Directory.GetFiles("./bad_apple");
		Array.Sort(fns, (fn1, fn2) =>
		{
			static int GetNumber(string fn)
			{
				var filename = Path.GetFileNameWithoutExtension(fn);
				var num = filename.Replace("bad_apple", "");
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

		Directory.CreateDirectory("./reduced_bad_apple");
		foreach (var fn in fns)
		{
			using var bmp = new Bitmap(fn);
			using var newBmp = new Bitmap(160, 144, PixelFormat.Format16bppRgb555);
			var bits = bmp.LockBits(new(0, 0, 160, 144), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
			var newBits = newBmp.LockBits(new(0, 0, 160, 144), ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb555);
			try
			{
				var bitSpan = new ReadOnlySpan<byte>(bits.Scan0.ToPointer(), bits.Stride * bits.Height);
				var newBitSpan = new Span<byte>(newBits.Scan0.ToPointer(), newBits.Stride * newBits.Height);
				for (var y = 0; y < 144; y++)
				{
					var scanline = MemoryMarshal.Cast<byte, uint>(bitSpan.Slice(y * bits.Stride, 160 * 4));
					var newScanline = MemoryMarshal.Cast<byte, ushort>(newBitSpan.Slice(y * newBits.Stride, 160 * 2));
					for (var x = 0; x < 160; x++)
					{
						var pixel = scanline[x];
						var r = (pixel >> 16) & 0xFF;
						var g = (pixel >> 8) & 0xFF;
						var b = (pixel >> 0) & 0xFF;
						var averageCol = Math.Round((r + g + b) / 3.0, MidpointRounding.AwayFromZero);
						ushort grayscalePixel;
						if (averageCol < 0x2B)
						{
							grayscalePixel = 0x0000;
						}
						else if (averageCol < 0x80)
						{
							grayscalePixel = 0x294A;
						}
						else if (averageCol < 0xD5)
						{
							grayscalePixel = 0x56B5;
						}
						else
						{
							grayscalePixel = 0x7FFF;
						}

						newScanline[x] = grayscalePixel;
					}
				}
			}
			finally
			{
				bmp.UnlockBits(bits);
				newBmp.UnlockBits(newBits);
			}

			var filename = Path.GetFileName(fn);
			var newPath = $"./reduced_bad_apple/{filename}";
			if (File.Exists(newPath))
			{
				File.Delete(newPath);
			}

			newBmp.Save(newPath, ImageFormat.Bmp);
		}
	}
}
