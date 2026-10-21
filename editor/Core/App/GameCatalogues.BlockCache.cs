namespace TerrainEditor.App;

/// <summary>
/// A bundle's data through a larger cache of its blocks, read a block at a time.
/// </summary>
public static partial class GameCatalogues
{
    /// <summary>
    /// A bundle's data through a larger cache of its blocks than AssetsTools keeps (it keeps 23 blocks of
    /// 128 KiB; this one 512, 64 MiB):
    /// the readers at the editor's start go back and forth between thousands of them in the largest
    /// bundle, each decompressed again when it is read after leaving the cache. The block used longest
    /// ago goes first.
    /// </summary>
    private sealed class BlockCache : Stream
    {
        /// <summary>
        /// A block's size: 128 KiB.
        /// </summary>
        private const int Size = 1 << 17;
        /// <summary>
        /// How many blocks are kept at most.
        /// </summary>
        private const int Most = 512;

        /// <summary>
        /// How much of a bundle it holds.
        /// </summary>
        public const long Bytes = (long)Size * Most;

        /// <summary>
        /// The bundle's own stream, read a block at a time.
        /// </summary>
        private readonly Stream _inner;
        /// <summary>
        /// The blocks kept, by block index (Position / Size).
        /// </summary>
        private readonly Dictionary<long, LinkedListNode<(long Index, byte[] Data)>> _blocks = [];
        /// <summary>
        /// Last used first.
        /// </summary>
        private readonly LinkedList<(long Index, byte[] Data)> _order = new();

        /// <summary>
        /// A cache over a bundle's data stream.
        /// </summary>
        public BlockCache(Stream inner) => _inner = inner;

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => true;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => _inner.Length;

        /// <inheritdoc/>
        public override long Position { get; set; }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int done = 0;
            while (done < count && Position < Length)
            {
                long index = Position / Size;
                byte[] block = Block(index);
                int at = (int)(Position - index * Size);
                int n = Math.Min(count - done, block.Length - at);
                if (n <= 0)
                {
                    break;
                }
                Buffer.BlockCopy(block, at, buffer, offset + done, n);
                done += n;
                Position += n;
            }
            return done;
        }

        /// <summary>
        /// A block's bytes (the last block may be shorter), read from the inner stream when it is not kept.
        /// </summary>
        private byte[] Block(long index)
        {
            // The last block used again (most reads are a few bytes): already first.
            if (_order.First is { } first && first.Value.Index == index)
            {
                return first.Value.Data;
            }
            if (_blocks.TryGetValue(index, out var kept))
            {
                _order.Remove(kept);
                _order.AddFirst(kept);
                return kept.Value.Data;
            }
            int size = (int)Math.Min(Size, Length - index * Size);
            byte[]? block = null;
            if (_blocks.Count >= Most)
            {
                var old = _order.Last!.Value;
                _order.RemoveLast();
                _blocks.Remove(old.Index);
                // Its memory used again (blocks are large objects, which the runtime frees late).
                block = old.Data.Length == size ? old.Data : null;
            }
            block ??= new byte[size];
            _inner.Position = index * Size;
            _inner.ReadExactly(block);
            _blocks[index] = _order.AddFirst((index, block));
            return block;
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            _ => Length + offset,
        };

        /// <summary>
        /// Nothing to flush: the stream is read only.
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>
        /// Not supported: the stream is read only.
        /// </summary>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>
        /// Not supported: the stream is read only.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
