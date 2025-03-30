using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;

using static Program.InputHelpers;

namespace Program;

[SupportedOSPlatform("windows")]
internal static class MainPayloadGB
{
	private static int _imageIndex;
	private static int _imageByteIndex;
	private static List<byte[]> _imageBGPData;
	private const int BYTES_PER_2BPP_IMAGE = 16 * 144; // 2304

	private static unsafe void LoadReducedImagesAs2BPP()
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
		_imageBGPData = [];
		foreach (var fn in fns)
		{
			var imageTileData = new byte[BYTES_PER_2BPP_IMAGE];
			using var bmp = new Bitmap(fn);
			var bits = bmp.LockBits(new(0, 0, 160, 144), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
			try
			{
				var imagePixelIndex = 0;
				for (var y = 0; y < 144; y++)
				{
					var scanlinePixels = new ReadOnlySpan<uint>((void*)(bits.Scan0 + y * bits.Stride), 160);
					for (var x = 0; x < 160;)
					{
						static byte To2BPP(uint pixel)
						{
							return pixel switch
							{
								0xFF000000 => 0b11,
								0xFF525252 => 0b10,
								0xFFADADAD => 0b01,
								0xFFFFFFFF => 0b00,
								_ => throw new InvalidOperationException("Invalid pixel data")
							};
						}

						static byte RotateRight(byte value, byte offset)
						{
							// BitOperations doesn't have bytewise rotate
							return (byte)((value >> offset) | (value << (8 - offset)));
						}

						// scale down first 8 pixels to 2:1
						byte packedPixels = 0;
						for (var i = 0; i < 4; i++)
						{
							// left pixel would be used for nearest neighbor downscaling
							var x0 = To2BPP(scanlinePixels[x + 0]);
							packedPixels |= x0;
							// BGP is color ids 33221100
							// tile data goes 00112233
							// so this has to be flipped (rotate right instead of shift left)
							packedPixels = RotateRight(packedPixels, 2);
							x += 2;
						}

						imageTileData[imagePixelIndex++] = packedPixels;

						// scale down next 12 pixels to 3:1
						packedPixels = 0;
						for (var i = 0; i < 4; i++)
						{
							// center pixel would be used for nearest neighbor downscaling
							var x1 = To2BPP(scanlinePixels[x + 1]);
							packedPixels |= x1;
							// BGP is color ids 33221100
							// tile data goes 000111222333
							// so this has to be flipped (rotate right instead of shift left)
							packedPixels = RotateRight(packedPixels, 2);
							x += 3;
						}

						imageTileData[imagePixelIndex++] = packedPixels;
					}
				}
			}
			finally
			{
				bmp.UnlockBits(bits);
			}

			_imageBGPData.Add(imageTileData);
		}
	}

	private static byte GetNextBGPByte()
	{
		if (_imageIndex < 0 || // have not yet begun the video ("warmup period" waiting for first frame)
		    _imageIndex >= _imageBGPData.Count || // video is over
		    _imageByteIndex >= BYTES_PER_2BPP_IMAGE) // we overshoot the needed amount of bytes to transfer, so these are dummy bytes
		{
			// all black
			return 0xFF;
		}

		return _imageBGPData[_imageIndex][_imageByteIndex++];
	}

	private static byte PeekBGPByte(int bytesAhead)
	{
		if (_imageIndex < 0 || // have not yet begun the video ("warmup period" waiting for first frame)
		    _imageIndex >= _imageBGPData.Count || // video is over
		    _imageByteIndex + bytesAhead >= BYTES_PER_2BPP_IMAGE) // we overshoot the needed amount of bytes to transfer, so these are dummy bytes
		{
			// all black
			return 0xFF;
		}

		return _imageBGPData[_imageIndex][_imageByteIndex + bytesAhead];
	}

	private static byte[] _audioPcm8Data;
	private static int _audioPcmLength;

	private static byte[] _pulseLut;
	private static byte[] _mvLut;
	private static int _pulseIndex;
	private static int _mvIndex;

	private static void LoadAudio()
	{
		_audioPcm8Data = File.ReadAllBytes("bad_apple_audio_gb_u8.bin");
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

#if false
		for (var i = 0; i < _audioPcm8Data.Length; i++)
		{
			var pulse = _pulseLut[_audioPcm8Data[i]];
			var mv = _mvLut[_audioPcm8Data[i]];
			var amp = (pulse * 2 - 15) * 64 * (mv + 1);
			var reducedAmp = (byte)((amp + 32768) / 65536.0 * 256);
			_audioPcm8Data[i] = reducedAmp;
		}

		using var ms = new MemoryStream(_audioPcm8Data, writable: false);
		Program.ConvertToWav("bad_apple_audio_gb_u8_converted.wav", ms, 18396, 1);
#endif
	}

	private static byte GetWaveChannelByte()
	{
		if (_imageIndex < 0 || // have not yet begun the video ("warmup period" waiting for first frame)
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
		if (_imageIndex < 0 || // have not yet begun the video ("warmup period" waiting for first frame)
		    _mvIndex >= _audioPcmLength) // audio is over
		{
			return (byte)(_mvLut[0x80] & 0x7);
		}

		var sample = _audioPcm8Data[_mvIndex++];
		return (byte)(_mvLut[sample] & 0x7);
	}

	// 1 m-cycle = 2 samples

	private static byte StoreVol(StreamWriter sw, int extraCycles)
	{
		var curVolNybble = GetVolumeNybble(); // store vol stores the next nybble rather than the current one
		var nextVolNybble = GetVolumeNybble();
		var f = CreateInputStringFromByte((extraCycles + 10) * 2, nextVolNybble, InputTransformation.LowerNybbleXor7);
		sw.WriteLine(f);
		return curVolNybble;
	}

	private static void AdjustVolDraw(StreamWriter sw, byte volumeNybble, int extraCycles)
	{
		var f = CreateInputStringFromByte((extraCycles + 10) * 2, volumeNybble, InputTransformation.LowerNybbleXor7);
		sw.WriteLine(f);
	}

	private static void AdjustVolNonDraw(StreamWriter sw, int extraCycles)
	{
		var vol = GetVolumeNybble();
		var f = CreateInputStringFromByte((extraCycles + 10) * 2, vol, InputTransformation.LowerNybbleRaw);
		sw.WriteLine(f);
	}

	private static void LoadSample(StreamWriter sw, int initialCycles, int endCycles)
	{
		var samples = GetWaveChannelByte();
		var f1 = CreateInputStringFromByte((initialCycles + 5) * 2, samples, InputTransformation.UpperNybbleXorC);
		var f2 = CreateInputStringFromByte((6 + endCycles) * 2, samples, InputTransformation.LowerNybbleXorC);
		sw.WriteLine(f1);
		sw.WriteLine(f2);
	}

	private static void JoypadToHli(StreamWriter sw, int initialCycles, int endCycles)
	{
		var bgpByte = GetNextBGPByte();
		var f1 = CreateInputStringFromByte((initialCycles + 5) * 2, bgpByte, InputTransformation.UpperNybbleXorC);
		var f2 = CreateInputStringFromByte((5 + endCycles) * 2, bgpByte, InputTransformation.LowerNybbleXorC);
		sw.WriteLine(f1);
		sw.WriteLine(f2);
	}

	private static void JoypadFirstHalfToE(StreamWriter sw, int extraCycles, int bytesAhead)
	{
		var bgpByte = PeekBGPByte(bytesAhead);
		var f = CreateInputStringFromByte((extraCycles + 5) * 2, bgpByte, InputTransformation.UpperNybbleXorC);
		sw.WriteLine(f);
	}

	private static void JoypadEToHli(StreamWriter sw, int extraCycles)
	{
		var bgpByte = GetNextBGPByte();
		var f = CreateInputStringFromByte((extraCycles + 5) * 2, bgpByte, InputTransformation.LowerNybbleXorC);
		sw.WriteLine(f);
	}

	private static void UnrolledScanlineDraw(StreamWriter sw)
	{
		var volNybble = StoreVol(sw, 5);
		AdjustVolDraw(sw, volNybble, 78);
		LoadSample(sw, 0, 0);
	}

	private static void LoopedScanlineDraw(StreamWriter sw)
	{
		var volNybble = StoreVol(sw, 5);
		AdjustVolDraw(sw, volNybble, 57);
		LoadSample(sw, 0, 21);
	}

	private static void UnrolledScanlinePrepNonDraw(StreamWriter sw)
	{
		JoypadToHli(sw, 5, 0);
		AdjustVolNonDraw(sw, 0);

		for (var i = 0; i < 4; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVolNonDraw(sw, 7);
		LoadSample(sw, 0, 0);

		JoypadToHli(sw, 0, 0);
		JoypadToHli(sw, 0, 1);
	}

	private static void UnrolledScanlineNonDraw(StreamWriter sw)
	{
		JoypadToHli(sw, 0, 0);
		JoypadFirstHalfToE(sw, 0, 0);

		AdjustVolNonDraw(sw, 0);

		JoypadEToHli(sw, 0);

		for (var i = 0; i < 4; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVolNonDraw(sw, 2);
		LoadSample(sw, 0, 0);

		JoypadToHli(sw, 0, 0);
		JoypadToHli(sw, 0, 1);
	}

	private static void LoopedScanlineNonDraw(StreamWriter sw)
	{
		JoypadToHli(sw, 0, 0);
		JoypadFirstHalfToE(sw, 0, 0);

		AdjustVolNonDraw(sw, 0);

		JoypadEToHli(sw, 0);

		for (var i = 0; i < 4; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVolNonDraw(sw, 2);
		LoadSample(sw, 0, 0);

		JoypadToHli(sw, 0, 11);
	}

	private static void UnrolledScanlineToggleLcdc(StreamWriter sw)
	{
		JoypadToHli(sw, 0, 0);
		JoypadFirstHalfToE(sw, 0, 0);

		AdjustVolNonDraw(sw, 0);

		JoypadEToHli(sw, 0);

		for (var i = 0; i < 4; i++)
		{
			JoypadToHli(sw, 0, 0);
		}

		AdjustVolNonDraw(sw, 2);
		LoadSample(sw, 0, 0);

		JoypadToHli(sw, 0, 11);
	}

	private static void LoopedScanlineFinal(StreamWriter sw)
	{
		AdjustVolNonDraw(sw, 15);
		AdjustVolNonDraw(sw, 47);
		LoadSample(sw, 0, 13);

		byte length, b;
		if (_imageIndex >= _imageBGPData.Count - 1)
		{
			length = 2;
			b = 1 ^ 0xF;
		}
		else
		{
			length = 2 + 2 + 4;
			b = 0 ^ 0xF;
		}

		var f = CreateInputStringFromByte(length * 2, b, InputTransformation.LowerNybbleRaw);
		sw.WriteLine(f);

		// Starting new draw frame
		_imageIndex++;
		_imageByteIndex = 0;
		if (_imageIndex == 0)
		{
			_pulseIndex = 32;
			_mvIndex = 0;
		}
	}

	public static void CreateMainPayload()
	{
		LoadReducedImagesAs2BPP();
		LoadAudio();

		//using var sw = new StreamWriter("bad_apple_main_payload.txt");
		using var sw = new StreamWriter("./bad_apple_gb_complete/Input Log.txt", append: true);
		while (_imageIndex < _imageBGPData.Count)
		{
			// the main loop comprises of three 60 FPS frames to form one 20 FPS frame
			// the loop is broken down by scanlines, all self-contained, allowing for easily splitting the load here

			// frame 0

			// LY 0-143
			for (var ly = 0; ly < 144; ly += 3)
			{
				UnrolledScanlineDraw(sw);
				UnrolledScanlineDraw(sw);
				LoopedScanlineDraw(sw);
			}

			// LY 144
			UnrolledScanlinePrepNonDraw(sw);

			// LY 145-152
			for (var ly = 145; ly < 153; ly += 2)
			{
				UnrolledScanlineNonDraw(sw);
				LoopedScanlineNonDraw(sw);
			}

			// LY 153
			UnrolledScanlineToggleLcdc(sw);

			// frame 1

			// LY 0-151
			for (var ly = 0; ly < 152; ly += 2)
			{
				UnrolledScanlineNonDraw(sw);
				LoopedScanlineNonDraw(sw);
			}

			// LY 152
			UnrolledScanlineNonDraw(sw);

			// LY 153
			UnrolledScanlineToggleLcdc(sw);

			// frame 2

			// LY 0-151
			for (var ly = 0; ly < 152; ly += 2)
			{
				UnrolledScanlineNonDraw(sw);
				LoopedScanlineNonDraw(sw);
			}

			// LY 152
			UnrolledScanlineNonDraw(sw);

			// LY 153
			LoopedScanlineFinal(sw);
		}

		sw.WriteLine("[/Input]");
	}
}
