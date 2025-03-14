// Copyright (c) 2024 Shay Green & EkeEke & CasualPokePlayer
// SPDX-License-Identifier: LGPL-2.1-or-later

using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Program;

/// <summary>
/// C# implementation of blargg's blip_buf + gpgx's improvements
/// https://github.com/ekeeke/Genesis-Plus-GX/blob/41285e1/core/sound/blip_buf.c
/// </summary>
internal sealed unsafe class BlipBuffer : IDisposable
{
	private const int BlipMaxRatio = 1 << 20;
	// private const int BlipMaxFrame = 4000;

	private const int PreShift = 32;

	private const int TimeBits = PreShift + 20;

	private const ulong TimeUnit = 1L << TimeBits;

	private const int BassShift = 9;
	private const int EndFrameExtra = 2;

	private const int HalfWidth = 8;
	private const int BufExtra = HalfWidth * 2 + EndFrameExtra;
	private const int PhaseBits = 5;
	private const int PhaseCount = 1 << PhaseBits;
	private const int DeltaBits = 15;
	private const int DeltaUnit = 1 << DeltaBits;
	private const int FracBits = TimeBits - PreShift;
	private const int PhaseShift = FracBits - PhaseBits;

	private ulong _factor;
	private ulong _offset;
	private readonly uint _size;
	private int _integrator;
	private readonly void* _sampleBuffer;
	private readonly int* _samples;

	// size is in stereo samples (2 16-bit samples = 1 stereo sample)
	public BlipBuffer(uint size)
	{
		_sampleBuffer = NativeMemory.Alloc(size + BufExtra, sizeof(int));
		_samples = (int*)_sampleBuffer;
		_factor = TimeUnit / BlipMaxRatio;
		_size = size;
		Clear();
	}

	public void Dispose()
	{
		NativeMemory.Free(_sampleBuffer);
	}

	public void SetRates(double clockRate, double sampleRate)
	{
		var factor = TimeUnit * sampleRate / clockRate;
		_factor = (ulong)Math.Ceiling(factor);
	}

	public void Clear()
	{
		_offset = _factor / 2;
		_integrator = 0;
		NativeMemory.Clear(_sampleBuffer, (_size + BufExtra) * sizeof(int));
	}

	public void EndFrame(uint t)
	{
		_offset += t * _factor;
	}

	public uint SamplesAvail => (uint)(_offset >> TimeBits);

	private void RemoveSamples(uint count)
	{
		var remain = SamplesAvail + BufExtra - count;
		_offset -= count * TimeUnit;

		NativeMemory.Copy(_samples + count, _samples, remain * sizeof(int));
		NativeMemory.Clear(_samples + remain, count * sizeof(int));
	}

	public uint ReadSamples(Span<short> output)
	{
		var count = Math.Min((uint)output.Length, SamplesAvail);
		if (count != 0)
		{
			var sum = _integrator;

			for (var i = 0; i < count; i++)
			{
				var s = (short)Math.Clamp(sum >> DeltaBits, short.MinValue, short.MaxValue);
				output[i] = s;
				sum += _samples[i];
				sum -= s << (DeltaBits - BassShift);
			}

			_integrator = sum;
			RemoveSamples(count);
		}

		return count;
	}

	private static readonly short[,] BlStep =
	{
		{ 43, -115, 350, -488, 1136, -914,  5861, 21022 },
		{ 44, -118, 348, -473, 1076, -799,  5274, 21001 },
		{ 45, -121, 344, -454, 1011, -677,  4706, 20936 },
		{ 46, -122, 336, -431,  942, -549,  4156, 20829 },
		{ 47, -123, 327, -404,  868, -418,  3629, 20679 },
		{ 47, -122, 316, -375,  792, -285,  3124, 20488 },
		{ 47, -120, 303, -344,  714, -151,  2644, 20256 },
		{ 46, -117, 289, -310,  634,  -17,  2188, 19985 },
		{ 46, -114, 273, -275,  553,  117,  1758, 19675 },
		{ 44, -108, 255, -237,  471,  247,  1356, 19327 },
		{ 43, -103, 237, -199,  390,  373,   981, 18944 },
		{ 42, -98,  218, -160,  310,  495,   633, 18527 },
		{ 40, -91,  198, -121,  231,  611,   314, 18078 },
		{ 38, -84,  178,  -81,  153,  722,    22, 17599 },
		{ 36, -76,  157,  -43,   80,  824,  -241, 17092 },
		{ 34, -68,  135,   -3,    8,  919,  -476, 16558 },
		{ 32, -61,  115,   34,  -60, 1006,  -683, 16001 },
		{ 29, -52,   94,   70, -123, 1083,  -862, 15422 },
		{ 27, -44,   73,  106, -184, 1152, -1015, 14824 },
		{ 25, -36,   53,  139, -239, 1211, -1142, 14210 },
		{ 22, -27,   34,  170, -290, 1261, -1244, 13582 },
		{ 20, -20,   16,  199, -335, 1301, -1322, 12942 },
		{ 18, -12,   -3,  226, -375, 1331, -1376, 12293 },
		{ 15, -4,   -19,  250, -410, 1351, -1408, 11638 },
		{ 13, 3,    -35,  272, -439, 1361, -1419, 10979 },
		{ 11, 9,    -49,  292, -464, 1362, -1410, 10319 },
		{ 9,  16,   -63,  309, -483, 1354, -1383, 9660  },
		{ 7,  22,   -75,  322, -496, 1337, -1339, 9005  },
		{ 6,  26,   -85,  333, -504, 1312, -1280, 8355  },
		{ 4,  31,   -94,  341, -507, 1278, -1205, 7713  },
		{ 3,  35,  -102,  347, -506, 1238, -1119, 7082  },
		{ 1,  40,  -110,  350, -499, 1190, -1021, 6464  },
		{ 0,  43,  -115,  350, -488, 1136,  -914, 5861  }
	};

	private static readonly Vector512<int>[] _blStep512 = new Vector512<int>[PhaseCount];
	private static readonly Vector512<int>[] _blStep512HW = new Vector512<int>[PhaseCount];

	static BlipBuffer()
	{
		for (var i = 0; i < PhaseCount; i++)
		{
			fixed (short*
			       input = &BlStep[i, 0],
			       inputHW = &BlStep[i + 1, 0],
			       rev = &BlStep[PhaseCount - i, 0],
			       revHW = &BlStep[PhaseCount - i - 1, 0])
			{
				var input256 = Vector256.Create(input[0], input[1], input[2], input[3], input[4], input[5], input[6], input[7]);
				var rev256 = Vector256.Create(rev[7], rev[6], rev[5], rev[4], rev[3], rev[2], rev[1], rev[0]);
				_blStep512[i] = Vector512.Create(input256, rev256);
				var inputHW256 = Vector256.Create(inputHW[0], inputHW[1], inputHW[2], inputHW[3], inputHW[4], inputHW[5], inputHW[6], inputHW[7]);
				var revHW256 = Vector256.Create(revHW[7], revHW[6], revHW[5], revHW[4], revHW[3], revHW[2], revHW[1], revHW[0]);
				_blStep512HW[i] = Vector512.Create(inputHW256, revHW256);
			}
		}
	}

	public void AddDelta(uint time, int delta)
	{
		if (delta != 0)
		{
			var fixedSample = (uint)((time * _factor + _offset) >> PreShift);
			var phase = fixedSample >> PhaseShift & (PhaseCount - 1);
			var interp = (int)(fixedSample >> (PhaseShift - DeltaBits) & (DeltaUnit - 1));
			var pos = fixedSample >> FracBits;

			var step = _blStep512[phase];
			var stepHW = _blStep512HW[phase];

			var deltaInterp = (delta * interp) >> DeltaBits;
			var deltaInterp512 = Vector512.Create(deltaInterp);
			var delta512 = Vector512.Create(delta - deltaInterp);

			var outS = _samples + pos;
			var out512 = Vector512.Load(outS);
			out512 += step * delta512 + stepHW * deltaInterp512;
			out512.Store(outS);
		}
	}
}
