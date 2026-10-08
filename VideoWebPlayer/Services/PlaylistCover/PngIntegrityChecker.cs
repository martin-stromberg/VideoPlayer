using System;
using System.Buffers.Binary;

namespace VideoWebPlayer.Services.PlaylistCover
{
    /// <summary>
    /// The result of a structural PNG integrity check.
    /// </summary>
    public enum PngIntegrity
    {
        /// <summary>
        /// The bytes are a complete PNG: valid signature, well-formed chunks with matching CRCs,
        /// terminated by an IEND chunk.
        /// </summary>
        Intact,

        /// <summary>
        /// The bytes look like a PNG but are truncated or structurally damaged.
        /// </summary>
        Corrupt,

        /// <summary>
        /// The bytes do not carry the PNG signature - the check does not apply.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// A small, structural PNG integrity check for untrusted uploads. Background: SkiaSharp's PNG decoder
    /// is lenient towards truncation - a file cut off mid-stream still produces a partially rendered
    /// bitmap instead of failing - so "the bytes decode without an exception" does not prove the file is
    /// complete. This class closes that gap without decoding any pixel data: it walks the chunk list,
    /// verifies each chunk's length and CRC32 and requires the terminating IEND chunk.
    /// </summary>
    public static class PngIntegrityChecker
    {
        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>
        /// Checks the structural integrity of a PNG file.
        /// </summary>
        /// <param name="data">The raw uploaded file bytes.</param>
        /// <returns>
        /// <see cref="PngIntegrity.Unknown"/> when the bytes do not carry a PNG signature,
        /// <see cref="PngIntegrity.Corrupt"/> when a chunk is truncated, malformed or its CRC mismatches
        /// or the terminating IEND chunk is missing, and <see cref="PngIntegrity.Intact"/> otherwise.
        /// Trailing bytes after IEND are tolerated (container formats may append payloads).
        /// </returns>
        public static PngIntegrity Check(byte[] data)
        {
            if (data is null || data.Length < Signature.Length || !data.AsSpan(0, Signature.Length).SequenceEqual(Signature))
                return PngIntegrity.Unknown;

            var position = Signature.Length;
            var sawIhdr = false;

            while (true)
            {
                // Each chunk is [4-byte length][4-byte type][data][4-byte CRC32].
                if (position + 8 > data.Length)
                    return PngIntegrity.Corrupt;

                var length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position, 4));
                if (length > int.MaxValue - 8)
                    return PngIntegrity.Corrupt;

                var chunkType = data.AsSpan(position + 4, 4);
                var dataStart = position + 8;
                var chunkEnd = dataStart + (int)length + 4;
                if (chunkEnd > data.Length)
                    return PngIntegrity.Corrupt;

                var declaredCrc = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(dataStart + (int)length, 4));
                var computedCrc = Crc32(data.AsSpan(position + 4, 4 + (int)length));
                if (computedCrc != declaredCrc)
                    return PngIntegrity.Corrupt;

                if (!sawIhdr)
                {
                    // The first chunk must be IHDR, and its data is exactly 13 bytes.
                    if (!chunkType.SequenceEqual("IHDR"u8) || length != 13)
                        return PngIntegrity.Corrupt;
                    sawIhdr = true;
                }

                position = chunkEnd;

                if (chunkType.SequenceEqual("IEND"u8))
                    return PngIntegrity.Intact;
            }
        }

        /// <summary>
        /// The CRC32 (IEEE 802.3, polynomial 0xEDB88320) PNG uses over chunk type plus chunk data.
        /// </summary>
        /// <param name="bytes">The bytes to checksum.</param>
        /// <returns>The CRC32 checksum.</returns>
        private static uint Crc32(ReadOnlySpan<byte> bytes)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in bytes)
            {
                crc ^= b;
                for (var bit = 0; bit < 8; bit++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }

            return ~crc;
        }
    }
}
