using System;
using System.Runtime.InteropServices;

namespace SlimBus.Implementations
{
    public readonly ref struct ReadOnlySpanOrMemory<T> where T: unmanaged
    {
        private readonly ReadOnlySpan<T> _span;

        private readonly ReadOnlyMemory<T>? _memory;

        public ReadOnlySpan<T> Span => IsSpan ? _span : throw new Exception();

        public ReadOnlyMemory<T> Memory => IsMemory ? _memory.Value : throw new Exception();

        public bool IsSpan => _memory == null;

        public bool IsMemory => _memory != null;

        public ReadOnlySpanOrMemory(ReadOnlySpan<T> span)
        {
            _span = span;
            _memory = null;
        }

        public ReadOnlySpanOrMemory(ReadOnlyMemory<T> memory)
        {
            _span = ReadOnlySpan<T>.Empty;
            _memory = memory;
        }

        public static implicit operator ReadOnlySpanOrMemory<T>(ReadOnlySpan<T> span) => new ReadOnlySpanOrMemory<T>(span);

        public static implicit operator ReadOnlySpanOrMemory<T>(ReadOnlyMemory<T> memory) => new ReadOnlySpanOrMemory<T>(memory);
    }
}
