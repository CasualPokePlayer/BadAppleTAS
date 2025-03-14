using System.IO;

namespace Program;

internal static partial class Program
{
	private static void CreateInitialPayload()
	{
		using var fs = File.OpenRead("bad_apple.gbc");
		fs.Seek(25, SeekOrigin.Begin); // first 25 bytes are $C000 payload, that's manually done
		using var br = new BinaryReader(fs);
		using var sw = new StreamWriter("bad_apple_initial_payload.txt");
		for (var i = 0; i < 0x1000; i++)
		{
			var b = br.ReadByte();
			var upperInput = CreateInputStringFromByte(5 * 2, b, InputTransformation.UpperNybbleXor);
			var lowerInput = CreateInputStringFromByte(10 * 2, b, InputTransformation.LowerNybbleXor);
			sw.WriteLine(upperInput);
			sw.WriteLine(lowerInput);
		}
	}
}
