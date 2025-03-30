using System.IO;

using static Program.InputHelpers;

namespace Program;

internal static partial class Program
{
	private static void CreateInitialPayloadGBC()
	{
		using var fs = File.OpenRead("bad_apple.gbc");
		fs.Seek(25, SeekOrigin.Begin); // first 25 bytes are $C000 payload, that's manually done
		using var br = new BinaryReader(fs);
		using var sw = new StreamWriter("bad_apple_initial_payload_gbc.txt");
		for (var i = 0; i < 0x1000; i++)
		{
			var b = br.ReadByte();
			var upperInput = CreateInputStringFromByte(5 * 2, b, InputTransformation.UpperNybbleXorC);
			var lowerInput = CreateInputStringFromByte(10 * 2, b, InputTransformation.LowerNybbleXorC);
			sw.WriteLine(upperInput);
			sw.WriteLine(lowerInput);
		}
	}

	private static void CreateInitialPayloadGB()
	{
		var rom = File.ReadAllBytes("bad_apple.gb");
		using var sw = new StreamWriter("bad_apple_initial_payload_gb.txt");
		// bytes are copied backwards here
		for (var i = 0xB07E; i > 0xA000; i--)
		{
			var b = i >= 0xA949
				? rom[i - 0xA949 + 0x2A]
				: (byte)0xFF;
			var upperInput = CreateInputStringFromByte(5 * 2, b, InputTransformation.UpperNybbleXorC);
			var lowerInput = CreateInputStringFromByte(10 * 2, b, InputTransformation.LowerNybbleXorC);
			sw.WriteLine(upperInput);
			sw.WriteLine(lowerInput);
		}
	}
}
