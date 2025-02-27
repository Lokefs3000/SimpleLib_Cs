namespace SimpleLib.Utility
{
    public class SpanStream<T> : Stream
        where T : unmanaged
    {
        private readonly ReadOnlyMemory<T> _rawStream;
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _rawStream.Length;
        public override long Position { get => _position; set => _position = Math.Clamp(value, 0, _rawStream.Length); }

        public SpanStream(ReadOnlyMemory<T> rawStream)
        {
            _rawStream = rawStream;
        }

        public override void Flush()
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
