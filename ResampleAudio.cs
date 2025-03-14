using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Program;

internal static partial class Program
{
	private static void ResampleAudio()
	{
		using var resampler = new BlipBuffer(4096);
		resampler.SetRates(44100, 2097152.0 / 57.0);
		using var ws = new SDL2WavStream("bad_apple.wav");
		using var br = new BinaryReader(ws);
		using var fs = File.Create("bad_apple_audio.bin");
		using var bw = new BinaryWriter(fs);
		var latch = 0;
		var numSamples = ws.Length / 2;
		var sampleBuffer = new short[4096];
		while (numSamples > 0)
		{
			var sampleBatch = (uint)Math.Min(numSamples, 2205);
			for (uint i = 0; i < sampleBatch; i++)
			{
				var sample = (int)br.ReadInt16();
				var diff = sample - latch;
				latch = sample;
				resampler.AddDelta(i, diff);
			}

			resampler.EndFrame(sampleBatch);
			var numRead = (int)resampler.ReadSamples(sampleBuffer);
			bw.Write(MemoryMarshal.AsBytes(sampleBuffer.AsSpan(0, numRead)));

			numSamples -= sampleBatch;
		}
	}
}
