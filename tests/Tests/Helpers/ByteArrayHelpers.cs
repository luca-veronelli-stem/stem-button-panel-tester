namespace Tests.Helpers
{
    /// <summary>
    /// Equality comparer for byte arrays, useful for collection assertions.
    /// </summary>
    public sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        /// <summary>
        /// Singleton instance for convenience.
        /// </summary>
        public static ByteArrayComparer Instance { get; } = new();

        public bool Equals(byte[]? x, byte[]? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.SequenceEqual(y);
        }

        public int GetHashCode(byte[] obj)
        {
            if (obj is null || obj.Length == 0)
            {
                return 0;
            }

            // Simple hash combining first few bytes
            int hash = 17;
            int maxBytes = Math.Min(16, obj.Length);
            for (int i = 0; i < maxBytes; i++)
            {
                hash = hash * 31 + obj[i];
            }
            return hash ^ obj.Length;
        }
    }

    /// <summary>
    /// Extension methods for byte array operations in tests.
    /// </summary>
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Creates a copy of the byte array with a bit flipped at the specified position.
        /// </summary>
        public static byte[] WithBitFlip(this byte[] original, int byteIndex, byte mask = 0x01)
        {
            byte[] copy = original.ToArray();
            copy[byteIndex] ^= mask;
            return copy;
        }

        /// <summary>
        /// Creates a copy of the byte array with the last byte corrupted (useful for CRC tests).
        /// </summary>
        public static byte[] WithCorruptedCrc(this byte[] original)
        {
            return original.WithBitFlip(original.Length - 1, 0xFF);
        }

        /// <summary>
        /// Extracts a slice of the byte array.
        /// </summary>
        public static byte[] Slice(this byte[] original, int offset, int length)
        {
            return original.AsSpan(offset, length).ToArray();
        }

        /// <summary>
        /// Concatenates multiple byte arrays.
        /// </summary>
        public static byte[] Concat(this byte[] first, params byte[][] others)
        {
            int totalLength = first.Length + others.Sum(a => a.Length);
            byte[] result = new byte[totalLength];
            int offset = 0;

            Buffer.BlockCopy(first, 0, result, offset, first.Length);
            offset += first.Length;

            foreach (byte[] arr in others)
            {
                Buffer.BlockCopy(arr, 0, result, offset, arr.Length);
                offset += arr.Length;
            }

            return result;
        }
    }
}
