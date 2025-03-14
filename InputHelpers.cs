using System;

namespace Program;

internal static partial class Program
{
	private enum InputTransformation
	{
		UpperNybbleXor, // xor'd with $C, return upper nybble
		LowerNybbleXor, // xor'd with $C, return lower nybble
		LowerNybbleRaw, // raw lower nybble
	}

	[Flags]
	private enum Buttons : byte
	{
		A = 0x01,
		B = 0x02,
		Select = 0x04,
		Start = 0x08,
	}

	private static string CreateInputStringFromByte(int inputLength, byte b, InputTransformation inputTransformation)
	{
		if (inputLength > 35112)
		{
			throw new InvalidOperationException("Input length is too large!");
		}

		if (inputTransformation == InputTransformation.UpperNybbleXor)
		{
			b >>= 4;
		}
		else
		{
			b &= 0xF;
		}

		if (inputTransformation is InputTransformation.UpperNybbleXor or InputTransformation.LowerNybbleXor)
		{
			b ^= 0xC;
		}

		// note: 0 is pressed
		var buttons = (Buttons)(b ^ 0xF);
		var aInput = (buttons & Buttons.A) != 0 ? 'A' : '.';
		var bInput = (buttons & Buttons.B) != 0 ? 'B' : '.';
		var selectInput = (buttons & Buttons.Select) != 0 ? 's' : '.';
		var startInput = (buttons & Buttons.Start) != 0 ? 'S' : '.';

		return $"|{inputLength.ToString(),5},....{startInput}{selectInput}{bInput}{aInput}.|";
	}
}
