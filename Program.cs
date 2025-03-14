using System.Runtime.Versioning;

namespace Program;

[SupportedOSPlatform("windows")]
internal static partial class Program
{
	private static void Main()
	{
		//ReduceImages();
		//ResampleAudio();
		//CreateInitialPayload();
		CreateMainPayload();
	}
}
