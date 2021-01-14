using System;

namespace Autobus.Types
{
    public readonly ref struct ReadOnlySpanOrMemory<T> where T: unmanaged
    {
        private enum Type
        {
            Span,
            Memory
        }

        private readonly Type _type;
        
        private readonly ReadOnlySpan<T> _span;

        private readonly ReadOnlyMemory<T> _memory;

        public ReadOnlySpan<T> Span => IsSpan ? _span : throw new Exception();

        public ReadOnlyMemory<T> Memory => IsMemory ? _memory : throw new Exception();

        public bool IsSpan => _type == Type.Span;

        public bool IsMemory => _type == Type.Memory;

        public ReadOnlySpanOrMemory(ReadOnlySpan<T> span)
        {
            _type = Type.Span;
            _span = span;
            _memory = ReadOnlyMemory<T>.Empty;
        }

        public ReadOnlySpanOrMemory(ReadOnlyMemory<T> memory)
        {
            _type = Type.Memory;
            _span = ReadOnlySpan<T>.Empty;
            _memory = memory;
        }

        public static implicit operator ReadOnlySpanOrMemory<T>(ReadOnlySpan<T> span) => new (span);

        public static implicit operator ReadOnlySpanOrMemory<T>(ReadOnlyMemory<T> memory) => new (memory);
    }
}
