using System.Buffers.Binary;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    internal static partial class Layout
    {
        /// <summary>
        /// A bundle's tree, read raw: each GameObject's name and components (file id, path id) and each
        /// Transform's GameObject, whether it is a root (no parent) and its children, by file and path id.
        /// </summary>
        /// <param name="GameObjects">Each GameObject's name and components, by file and path id.</param>
        /// <param name="Transforms">Each Transform's GameObject, whether it is a root and its children, by file and
        /// path id.</param>
        public sealed record Tree(Dictionary<(Bundle.Inner, long), (string Name, (int File, long Pid)[] Components)> GameObjects, Dictionary<(Bundle.Inner, long), (long Go, bool Root, long[] Children)> Transforms)
        {
            /// <summary>
            /// An empty tree.
            /// </summary>
            public Tree()
                : this([], [])
            {
            }

            /// <summary>
            /// A GameObject's or Transform's numbers, from its bytes. A GameObject has its components first
            /// (a count, then each a file id and a path id), then its layer and name; a Transform its
            /// GameObject, rotation, position and scale, then its children and its parent.
            /// </summary>
            public void Add(Bundle.Inner f, long pid, int type, Bytes d)
            {
                if (type == GameObjectType)
                {
                    var comps = new (int, long)[d.I32(0)];
                    for (int i = 0; i < comps.Length; i++)
                    {
                        comps[i] = (d.I32(4 + 12 * i), d.I64(8 + 12 * i));
                    }
                    int name = 8 + 12 * comps.Length;
                    GameObjects[(f, pid)] = (d.Text(name), comps);
                }
                else if (type == TransformType)
                {
                    long[] children = d.ChildIds(52);
                    int father = 56 + 12 * children.Length;
                    Transforms[(f, pid)] = (d.I64(4), d.I32(father) == 0 && d.I64(father + 4) == 0, children);
                }
            }
        }

        /// <summary>
        /// An object's bytes, with the file's byte order.
        /// </summary>
        /// <param name="data">The object's bytes.</param>
        /// <param name="bigEndian">Whether the file is big-endian.</param>
        public readonly ref struct Bytes(ReadOnlySpan<byte> data, bool bigEndian)
        {
            /// <summary>
            /// The bytes.
            /// </summary>
            private readonly ReadOnlySpan<byte> _data = data;

            /// <summary>
            /// A 32-bit integer at a byte offset.
            /// </summary>
            public int I32(int at) => bigEndian ? BinaryPrimitives.ReadInt32BigEndian(_data[at..]) : BinaryPrimitives.ReadInt32LittleEndian(_data[at..]);

            /// <summary>
            /// A 64-bit integer at a byte offset.
            /// </summary>
            public long I64(int at) => bigEndian ? BinaryPrimitives.ReadInt64BigEndian(_data[at..]) : BinaryPrimitives.ReadInt64LittleEndian(_data[at..]);

            /// <summary>
            /// A 32-bit float at a byte offset.
            /// </summary>
            public float F32(int at) => BitConverter.Int32BitsToSingle(I32(at));

            /// <summary>
            /// A byte at an offset.
            /// </summary>
            public byte U8(int at) => _data[at];

            /// <summary>
            /// A Transform's child ids: a count at <paramref name="countAt"/>, then each child's path id, 12 bytes apart
            /// from the first (after its file id).
            /// </summary>
            /// <param name="countAt">The byte offset of the child count.</param>
            public long[] ChildIds(int countAt)
            {
                long[] children = new long[I32(countAt)];
                for (int i = 0; i < children.Length; i++)
                {
                    children[i] = I64(countAt + 8 + 12 * i);
                }
                return children;
            }

            /// <summary>
            /// A string: its length, then its UTF-8 bytes.
            /// </summary>
            public string Text(int at) => System.Text.Encoding.UTF8.GetString(_data.Slice(at + 4, I32(at)));
        }
    }
}
