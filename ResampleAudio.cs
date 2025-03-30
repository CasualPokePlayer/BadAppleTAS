using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;

namespace Program;

internal static partial class Program
{
	private static readonly byte[] _baseWavHeader =
	[
		0x52, 0x49, 0x46, 0x46, // 0-3 / "RIFF"
		0x00, 0x00, 0x00, 0x00, // 4-7 / filesize - 8 (filled in)
		0x57, 0x41, 0x56, 0x45, // 8-11 / "WAVE"
		0x66, 0x6D, 0x74, 0x20, // 12-15 / "fmt "
		0x10, 0x00, 0x00, 0x00, // 16-19 / chunk size (16 byte format used)
		0x01, 0x00, // 20-21 / PCM format
		0x01, 0x00, // 22-23 / 1 channel
		0x00, 0x00, 0x00, 0x00, // 24-27 / sample rate (filled in)
		0x00, 0x00, 0x00, 0x00, // 28-31 / byte rate (filled in) (sample rate * channels * bytes per sample)
		0x00, 0x00, // 32-33 / block align (filled in) (channels * bytes per sample)
		0x00, 0x00, // 34-35 / valid bits per sample (filled in)
		0x64, 0x61, 0x74, 0x61, // 36-39 / "data"
		0x00, 0x00, 0x00, 0x00, // 40-43 / chunk size (filled in)
	];

	// for input into ffmpeg
	public static void ConvertToWav(string outFileName, Stream rawAudio, uint sampleRate, uint sampleSize)
	{
		using var fs = File.Create(outFileName);
		var wavHeader = _baseWavHeader.AsSpan().ToArray();
		BinaryPrimitives.WriteUInt32LittleEndian(wavHeader.AsSpan(4), (uint)(rawAudio.Length + 44 - 8));
		BinaryPrimitives.WriteUInt32LittleEndian(wavHeader.AsSpan(24), sampleRate);
		BinaryPrimitives.WriteUInt32LittleEndian(wavHeader.AsSpan(28), sampleRate * sampleSize);
		BinaryPrimitives.WriteUInt16LittleEndian(wavHeader.AsSpan(32), (ushort)sampleSize);
		BinaryPrimitives.WriteUInt16LittleEndian(wavHeader.AsSpan(34), (ushort)(sampleSize * 8));
		BinaryPrimitives.WriteUInt32LittleEndian(wavHeader.AsSpan(40), (uint)rawAudio.Length);
		fs.Write(wavHeader);
		rawAudio.Seek(0, SeekOrigin.Begin);
		rawAudio.CopyTo(fs);
	}

	private static void ResampleAudioGBC()
	{
		using var resampler = new BlipBuffer(4096);
		// resample to ~36792Hz
		resampler.SetRates(44100, 2097152.0 / 57.0);
		using var ws = new SDL2WavStream("bad_apple.wav");
		using var br = new BinaryReader(ws);
		using var fs = File.Create("bad_apple_audio_gbc.bin");
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
		
		ConvertToWav("bad_apple_audio_gbc.wav", fs, sampleRate: (uint)(2097152.0 / 57.0), sampleSize: 2);
	}

	private static void ResampleAudioGB()
	{
		using var resampler = new BlipBuffer(4096);
		// resample to ~18396Hz
		resampler.SetRates(44100, 2097152.0 / 114.0);
		using var ws = new SDL2WavStream("bad_apple.wav");
		using var br = new BinaryReader(ws);
		using var fs = File.Create("bad_apple_audio_gb.bin");
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

		ConvertToWav("bad_apple_audio_gb.wav", fs, sampleRate: (uint)(2097152.0 / 114.0), sampleSize: 2);
	}
}
