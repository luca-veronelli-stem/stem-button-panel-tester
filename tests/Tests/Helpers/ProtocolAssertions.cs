namespace Tests.Helpers
{
    /// <summary>
    /// Custom assertions for protocol testing that provide clearer error messages.
    /// </summary>
    public static class ProtocolAssertions
    {
        /// <summary>
        /// Asserts that two byte arrays are equal with a descriptive message.
        /// </summary>
        public static void AssertBytesEqual(byte[] expected, byte[] actual, string? message = null)
        {
            if (expected.Length != actual.Length)
            {
                string msg = message ?? "Byte arrays differ in length";
                throw new Xunit.Sdk.XunitException(
                    $"{msg}: expected length {expected.Length}, actual length {actual.Length}");
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    string msg = message ?? "Byte arrays differ";
                    throw new Xunit.Sdk.XunitException(
                        $"{msg} at index {i}: expected 0x{expected[i]:X2}, actual 0x{actual[i]:X2}\n" +
                        $"Expected: {FormatBytes(expected)}\n" +
                        $"Actual:   {FormatBytes(actual)}");
                }
            }
        }

        /// <summary>
        /// Asserts that a header field at the specified offset matches the expected value.
        /// </summary>
        public static void AssertHeaderField(
            byte[] packet,
            int offset,
            byte expectedValue,
            string fieldName)
        {
            if (packet.Length <= offset)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Packet too short to contain {fieldName} at offset {offset}");
            }

            if (packet[offset] != expectedValue)
            {
                throw new Xunit.Sdk.XunitException(
                    $"{fieldName} mismatch at offset {offset}: expected 0x{expectedValue:X2}, actual 0x{packet[offset]:X2}");
            }
        }

        /// <summary>
        /// Asserts that a big-endian uint at the specified offset matches the expected value.
        /// </summary>
        public static void AssertBigEndianUInt32(
            byte[] packet,
            int offset,
            uint expectedValue,
            string fieldName)
        {
            if (packet.Length < offset + 4)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Packet too short to contain {fieldName} at offset {offset}");
            }

            uint actual = (uint)((packet[offset] << 24) | (packet[offset + 1] << 16) |
                                  (packet[offset + 2] << 8) | packet[offset + 3]);

            if (actual != expectedValue)
            {
                throw new Xunit.Sdk.XunitException(
                    $"{fieldName} mismatch: expected 0x{expectedValue:X8}, actual 0x{actual:X8}");
            }
        }

        /// <summary>
        /// Asserts that a big-endian ushort at the specified offset matches the expected value.
        /// </summary>
        public static void AssertBigEndianUInt16(
            byte[] packet,
            int offset,
            ushort expectedValue,
            string fieldName)
        {
            if (packet.Length < offset + 2)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Packet too short to contain {fieldName} at offset {offset}");
            }

            ushort actual = (ushort)((packet[offset] << 8) | packet[offset + 1]);

            if (actual != expectedValue)
            {
                throw new Xunit.Sdk.XunitException(
                    $"{fieldName} mismatch: expected 0x{expectedValue:X4}, actual 0x{actual:X4}");
            }
        }

        /// <summary>
        /// Formats a byte array as a hex string for display.
        /// </summary>
        public static string FormatBytes(byte[] bytes, int maxLength = 32)
        {
            if (bytes.Length <= maxLength)
            {
                return BitConverter.ToString(bytes);
            }

            return BitConverter.ToString(bytes, 0, maxLength) + $"... ({bytes.Length} bytes total)";
        }
    }
}
