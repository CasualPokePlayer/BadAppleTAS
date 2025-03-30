using System;

namespace Program;

public static class InputHelpers
{
	public enum InputTransformation
	{
		UpperNybbleXorC, // xor'd with $C, return upper nybble
		LowerNybbleXorC, // xor'd with $C, return lower nybble
		LowerNybbleRaw, // raw lower nybble
		LowerNybbleXor7, // raw lower nybble
	}

	[Flags]
	private enum Buttons : byte
	{
		A = 0x01,
		B = 0x02,
		Select = 0x04,
		Start = 0x08,
	}

	public static string CreateInputStringFromByte(int inputLength, byte b, InputTransformation inputTransformation)
	{
		if (inputLength > 35112)
		{
			throw new InvalidOperationException("Input length is too large!");
		}

		if (inputTransformation == InputTransformation.UpperNybbleXorC)
		{
			b >>= 4;
		}
		else
		{
			b &= 0xF;
		}

		if (inputTransformation is InputTransformation.UpperNybbleXorC or InputTransformation.LowerNybbleXorC)
		{
			b ^= 0xC;
		}

		if (inputTransformation is InputTransformation.LowerNybbleXor7)
		{
			b ^= 0x7;
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
