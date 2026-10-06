namespace Visualizer.App.Audio;

public sealed class AudioRingBuffer
{
    private readonly float[] _buffer;
    private readonly object _gate = new();
    private long _writeIndex;
    private bool _filled;

    public AudioRingBuffer(int capacity)
    {
        _buffer = new float[capacity];
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        lock (_gate)
        {
            foreach (var sample in samples)
            {
                _buffer[_writeIndex % _buffer.Length] = sample;
                _writeIndex++;
                if (_writeIndex >= _buffer.Length)
                {
                    _filled = true;
                }
            }
        }
    }

    public int ReadLatest(Span<float> destination)
    {
        lock (_gate)
        {
            var available = _filled ? _buffer.Length : (int)Math.Min(_writeIndex, _buffer.Length);
            var count = Math.Min(destination.Length, available);
            destination.Clear();

            var start = _writeIndex - count;
            var offset = destination.Length - count;
            for (var i = 0; i < count; i++)
            {
                var index = (int)((start + i) % _buffer.Length);
                if (index < 0)
                {
                    index += _buffer.Length;
                }

                destination[offset + i] = _buffer[index];
            }

            return count;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_buffer);
            _writeIndex = 0;
            _filled = false;
        }
    }
}
