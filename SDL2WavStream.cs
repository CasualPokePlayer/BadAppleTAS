using System;
using System.IO;

using static SDL2.SDL;

namespace Program;

internal sealed class SDL2WavStream : Stream
{
	private IntPtr _wav;
	private readonly uint _len;
	private uint _pos;

	public SDL2WavStream(string wavFile)
	{
		if (SDL_LoadWAV(wavFile, out _, out var wav, out var len) == IntPtr.Zero)
		{
			throw new($"Could not load WAV file! SDL error: {SDL_GetError()}");
		}

		_wav = wav;
		_len = len;
	}

	protected override void Dispose(bool disposing)
	{
		SDL_FreeWAV(_wav);
		_wav = IntPtr.Zero;

		base.Dispose(disposing);
	}

	public override bool CanRead => true;
	public override bool CanSeek => true;
	public override bool CanWrite => false;
	public override long Length => _len;

	public override long Position
	{
		get => _pos;
		set
		{
			if (value < 0 || value > _len)
			{
				throw new ArgumentOutOfRangeException(paramName: nameof(value), value, message: "index out of range");
			}

			_pos = (uint)value;
		}
	}

	public override void Flush()
	{
	}

	public override unsafe int Read(Span<byte> buffer)
	{
		ObjectDisposedException.ThrowIf(_wav == IntPtr.Zero, typeof(SDL2WavStream));

		var count = Math.Min((uint)buffer.Length, _len - _pos);
		var countSigned = unchecked((int)count);
		new ReadOnlySpan<byte>((void*)(_wav + _pos), countSigned).CopyTo(buffer);
		_pos += count;
		return countSigned;
	}

	public override int Read(byte[] buffer, int offset, int count)
		=> Read(new(buffer, offset, count));

	public override long Seek(long offset, SeekOrigin origin)
	{
		var newpos = origin switch
		{
			SeekOrigin.Begin => offset,
			SeekOrigin.Current => _pos + offset,
			SeekOrigin.End => _len + offset,
			_ => offset
		};

		Position = newpos;
		return newpos;
	}

	public override void SetLength(long value)
		=> throw new NotSupportedException();

	public override void Write(ReadOnlySpan<byte> buffer)
		=> throw new NotSupportedException();

	public override void Write(byte[] buffer, int offset, int count)
		=> throw new NotSupportedException();
}
