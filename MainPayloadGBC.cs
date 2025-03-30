using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;

using static Program.InputHelpers;

namespace Program;

[SupportedOSPlatform("windows")]
internal static class MainPayloadGBC
{
	private static int _imageIndex;
	private static int _imageByteIndex;
	private static List<byte[]> _imageTileData;
	private const int BYTES_PER_2BPP_IMAGE = 20 * 18 * 16; // 5760

	private static void LoadReducedImagesAs2BPP()
	{
		var fns = Directory.GetFiles("./reduced_bad_apple");
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

		_imageIndex = -1;
		_imageTileData = [];
		Span<byte> packed2bppPixels = stackalloc byte[8];
		foreach (var fn in fns)
		{
			var imageTileData = new byte[BYTES_PER_2BPP_IMAGE];
			using var bmp = new Bitmap(fn);
			var bits = bmp.LockBits(new(0, 0, 160, 144), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
			try
			{
				for (var i = 0; i < BYTES_PER_2BPP_IMAGE; i += 2)
				{
					var tileIndex = i / 16;
					var tileX = tileIndex % 20;
					var tileY = tileIndex / 20;
					var tileInnerIndex = i % 16;
					var tileInnerY = tileInnerIndex / 2;
					var tilePixelX = tileX * 8;
					var tilePixelY = tileY * 8 + tileInnerY;

					static byte To2BPP(uint pixel)
					{
						return pixel switch
						{
							0xFF000000 => 0b00,
							0xFF525252 => 0b01,
							0xFFADADAD => 0b10,
							0xFFFFFFFF => 0b11,
							_ => throw new InvalidOperationException("Invalid pixel data")
						};
					}

					static byte Unpack2BPP(ReadOnlySpan<byte> packed2bpp, bool first)
					{
						byte ret = 0;
						for (var i = 0; i < 8; i++)
						{
							var bit = first ? (packed2bpp[i] & 0b01) : ((packed2bpp[i] & 0b10) >> 1);
							ret |= (byte)(bit << (7 - i));
						}

						return ret;
					}

					unsafe
					{
						var tilePixels = new ReadOnlySpan<uint>((void*)(bits.Scan0 + tilePixelY * bits.Stride + tilePixelX * 4), 8);
						for (var j = 0; j < 8; j++)
						{
							packed2bppPixels[j] = To2BPP(tilePixels[j]);
						}

						imageTileData[i + 0] = Unpack2BPP(packed2bppPixels, true);
						imageTileData[i + 1] = Unpack2BPP(packed2bppPixels, false);
					}
				}
			}
			finally
			{
				bmp.UnlockBits(bits);
			}

			_imageTileData.Add(imageTileData);
		}
	}

	private static byte GetNextTileDataByte()
	{
		if (_imageIndex < 0 || // have not yet begun the video ("warmup period" before HL is reset)
		    _imageIndex >= _imageTileData.Count || // video is over
		    _imageByteIndex >= BYTES_PER_2BPP_IMAGE) // we overshoot the needed amount of bytes to transfer, so these are dummy bytes
		{
			return 0;
		}

		return _imageTileData[_imageIndex][_imageByteIndex++];
	}

	private static byte[] _audioPcm8Data;
	private static int _audioPcmLength;

	private static byte[] _pulseLut;
	private static byte[] _mvLut;
	private static int _pulseIndex;
	private static int _mvIndex;

	private static void LoadAudio()
	{
		_audioPcm8Data = File.ReadAllBytes("bad_apple_audio_8bit.bin");
		_audioPcmLength = _audioPcm8Data.Length & ~1; // should be a multiple of 2 (wave channel holds 2 samples at a time)
		_pulseLut = new byte[256];
		_mvLut = new byte[256];

		// taken from https://github.com/jbshelton/GBAudioPlayerV3/blob/469ba0a/encoder.c
		// see generate_hq_lut (follows same principle as wave ram + volume changes, but the approach used is more expensive compared to such)
		var ampLut = new int[256];
		ampLut.AsSpan().Fill(-1);

		for (var m = 0; m < 8; m++)
		{
			for (var p = 0; p < 8; p++)
			{
				var pulse = p * -1 - 1;
				pulse = pulse * 2 + 1;
				var outamp = (int)(128.0 + 128.0 / 120.0 * (pulse * (m + 1)));

				if (ampLut[outamp] == -1)
				{
					_pulseLut[outamp] = (byte)(7 - p);
					_mvLut[outamp] = (byte)m;
					ampLut[outamp] = outamp;
				}

				pulse = p + 1;
				pulse = pulse * 2 - 1;
				outamp = (int)(127.0 + 128.0 / 120.0 * (pulse * (m + 1)));

				if (ampLut[outamp] == -1)
				{
					_pulseLut[outamp] = (byte)(p + 8);
					_mvLut[outamp] = (byte)m;
					ampLut[outamp] = outamp;
				}
			}
		}

		byte tempPulse = 0;
		byte tempMv = 0;
		var tempAmp = 0;

		for (var i = 0; i < 128; i++)
		{
			if (ampLut[i] == i)
			{
				tempPulse = _pulseLut[i];
				tempMv = _mvLut[i];
				tempAmp = ampLut[i];
			}
			else
			{
				_pulseLut[i] = tempPulse;
				_mvLut[i] = tempMv;
				ampLut[i] = tempAmp;
			}
		}

		for (var i = 255; i >= 128; i--)
		{
			if (ampLut[i] == i)
			{
				tempPulse = _pulseLut[i];
				tempMv = _mvLut[i];
				tempAmp = ampLut[i];
			}
			else
			{
				_pulseLut[i] = tempPulse;
				_mvLut[i] = tempMv;
				ampLut[i] = tempAmp;
			}
		}
	}

	private static byte GetWaveChannelByte()
	{
		if (_imageIndex < 0 || // have not yet begun the video ("warmup period" before HL is reset)
		    _pulseIndex >= _audioPcmLength) // audio is over
		{
			return (byte)((_pulseLut[0x80] << 4) | (_pulseLut[0x80] & 0xF));
		}

		var sample0 = _audioPcm8Data[_pulseIndex++];
		var sample1 = _audioPcm8Data[_pulseIndex++];
		return (byte)((_pulseLut[sample0] << 4) | (_pulseLut[sample1] & 0xF));
	}

	private static byte GetVolumeNybble()
	{
		if (_imageIndex < 0 || // have not yet begun the video ("warmup period" before HL is reset)
		    _mvIndex >= _audioPcmLength) // audio is over
		{
			return (byte)(_mvLut[0x80] & 0x7);
		}

		var sample = _audioPcm8Data[_mvIndex++];
		return (byte)(_mvLut[sample] & 0x7);
	}

	private static void AdjustVol(StreamWriter sw, int extraCycles)
	{
		var vol = GetVolumeNybble();
		var f = CreateInputStringFromByte(extraCycles + 10, vol, InputTransformation.LowerNybbleRaw);
		sw.WriteLine(f);
	}

	private static void LoadSample(StreamWriter sw, int initialCycles, int endCycles)
	{
		var samples = GetWaveChannelByte();
		var f1 = CreateInputStringFromByte(initialCycles + 5, samples, InputTransformation.UpperNybbleXorC);
		var f2 = CreateInputStringFromByte(6 + endCycles, samples, InputTransformation.LowerNybbleXorC);
		sw.WriteLine(f1);
		sw.WriteLine(f2);
	}

	private static void JoypadToHli(StreamWriter sw, int initialCycles, int endCycles)
	{
		var tileData = GetNextTileDataByte();
		var f1 = CreateInputStringFromByte(initialCycles + 5, tileData, InputTransformation.UpperNybbleXorC);
		var f2 = CreateInputStringFromByte(5 + endCycles, tileData, InputTransformation.LowerNybbleXorC);
		sw.WriteLine(f1);
		sw.WriteLine(f2);
	}

	private static byte JoypadFirstHalfToE(StreamWriter sw, int extraCycles)
	{
		var tileData = GetNextTileDataByte();
		var f = CreateInputStringFromByte(extraCycles + 5, tileData, InputTransformation.UpperNybbleXorC);
		sw.WriteLine(f);
		return tileData;
	}

	private static void JoypadEToHli(StreamWriter sw, int extraCycles, byte tileData)
	{
		var f = CreateInputStringFromByte(extraCycles + 5, tileData, InputTransformation.LowerNybbleXorC);
		sw.WriteLine(f);
	}

	private static void UnrolledScanlinePrepLcdc(StreamWriter sw)
	{
		AdjustVol(sw, 0);
		LoadSample(sw, 0, 2+3+1);

		for (var i = 0; i < 3; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVol(sw, 0);

		for (var i = 0; i < 4; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVol(sw, 7);
		LoadSample(sw, 0, 0);

		for (var i = 0; i < 3; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		var tileData = JoypadFirstHalfToE(sw, 1);
		AdjustVol(sw, 0);
		JoypadEToHli(sw, 0, tileData);

		for (var i = 0; i < 3; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		JoypadToHli(sw, 0, 2);
	}

	private static void LoopedScanline(StreamWriter sw)
	{
		AdjustVol(sw, 0);
		LoadSample(sw, 0, 0);

		for (var i = 0; i < 3; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		var tileData = JoypadFirstHalfToE(sw, 1);
		AdjustVol(sw, 0);
		JoypadEToHli(sw, 0, tileData);

		for (var i = 0; i < 4; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVol(sw, 2);
		LoadSample(sw, 0, 0);

		for (var i = 0; i < 3; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVol(sw, 2+3+1);

		for (var i = 0; i < 3; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		JoypadToHli(sw, 0, 1+2+4);
	}

	private static void LoopedScanlineFinal(StreamWriter sw)
	{
		AdjustVol(sw, 0);
		LoadSample(sw, 0, 0);

		for (var i = 0; i < 3; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVol(sw, 6);

		for (var i = 0; i < 4; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVol(sw, 7);
		LoadSample(sw, 0, 0);

		for (var i = 0; i < 3; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVol(sw, 2+4);

		for (var i = 0; i < 4; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		byte length, b;
		if (_imageIndex >= _imageTileData.Count)
		{
			length = 2;
			b = 1 ^ 0xF;
		}
		else
		{
			length = 2 + 1 + 4;
			b = 0 ^ 0xF;
		}

		var f = CreateInputStringFromByte(length, b, InputTransformation.LowerNybbleRaw);
		sw.WriteLine(f);
	}

	private static void UnrolledScanlinePrepGdma(StreamWriter sw)
	{
		AdjustVol(sw, 0);
		LoadSample(sw, 0, 0);

		for (var i = 0; i < 3; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVol(sw, 2+3+1);

		for (var i = 0; i < 4; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVol(sw, 1+3+3);
		LoadSample(sw, 0, 0);

		for (var i = 0; i < 3; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVol(sw, 2+3+1);

		for (var i = 0; i < 3; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		JoypadToHli(sw, 0, 1+3+3);

		// HL reset, new frame
		_imageIndex++;
		_imageByteIndex = 0;
		if (_imageIndex == 0)
		{
			// we do wave ram writes on odd indexes
			// as such the samples written are 31 and 32 samples ahead respectively
			// pulseIndex needs to be a multiple of 2 here however, so
			_pulseIndex = 31+1;
			_mvIndex = 0+1;
		}
	}

	private static void LoopedScanlineGdma(StreamWriter sw)
	{
		AdjustVol(sw, 1+3+2+16);

		LoadSample(sw, 0, 0);
		JoypadToHli(sw, 0, 0);

		AdjustVol(sw, 4);

		for (var i = 0; i < 4; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVol(sw, 7);
		LoadSample(sw, 0, 1+3+2+16);

		JoypadToHli(sw, 0, 0);

		AdjustVol(sw, 3+1);

		for (var i = 0; i < 3; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		JoypadToHli(sw, 0, 1+2+4);
	}

	public static void CreateMainPayload()
	{
		LoadReducedImagesAs2BPP();
		LoadAudio();

		//using var sw = new StreamWriter("bad_apple_main_payload.txt");
		using var sw = new StreamWriter("./bad_apple_complete/Input Log.txt", append: true);
		while (_imageIndex < _imageTileData.Count)
		{
			// the main loop comprises of three 60 FPS frames to form one 20 FPS frame
			// the loop is broken down by scanlines, all self-contained, allowing for easily splitting the load here

			// frame 0

			// LY 0
			UnrolledScanlinePrepLcdc(sw);

			// LY 1-71
			for (var ly = 1; ly < 72; ly++)
			{
				LoopedScanline(sw);
			}

			// LY 72
			UnrolledScanlinePrepLcdc(sw);

			// LY 73-153
			for (var ly = 73; ly < 154; ly++)
			{
				LoopedScanline(sw);
			}

			// frame 1

			// LY 0
			UnrolledScanlinePrepLcdc(sw);

			// LY 1-6
			for (var ly = 1; ly < 7; ly++)
			{
				LoopedScanline(sw);
			}

			// LY 7
			UnrolledScanlinePrepGdma(sw);

			// LY 8-71
			for (var ly = 8; ly < 72; ly++)
			{
				LoopedScanlineGdma(sw);
			}

			// LY 72
			UnrolledScanlinePrepLcdc(sw);

			// LY 73-153
			for (var ly = 73; ly < 154; ly++)
			{
				LoopedScanlineGdma(sw);
			}

			// frame 2

			// LY 0
			UnrolledScanlinePrepLcdc(sw);

			// LY 1-35
			for (var ly = 1; ly < 36; ly++)
			{
				LoopedScanlineGdma(sw);
			}

			// LY 36-71
			for (var ly = 36; ly < 72; ly++)
			{
				LoopedScanline(sw);
			}

			// LY 72
			UnrolledScanlinePrepLcdc(sw);

			// LY 73-152
			for (var ly = 73; ly < 153; ly++)
			{
				LoopedScanline(sw);
			}

			// LY 153
			LoopedScanlineFinal(sw);
		}

		sw.WriteLine("[/Input]");
	}
}
