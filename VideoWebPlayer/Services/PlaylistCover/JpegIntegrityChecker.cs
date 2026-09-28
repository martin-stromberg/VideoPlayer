using System;

namespace VideoWebPlayer.Services.PlaylistCover
{
    /// <summary>
    /// The verdict of <see cref="JpegIntegrityChecker.Check"/>.
    /// </summary>
    public enum JpegIntegrity
    {
        /// <summary>The JPEG structure is complete and its entropy-coded image data decodes consistently.</summary>
        Intact,

        /// <summary>The JPEG is truncated or its image data is damaged.</summary>
        Corrupt,

        /// <summary>
        /// The checker cannot judge this file (e.g. progressive or arithmetic coding, or an unusual header).
        /// Callers should treat it as acceptable and rely on their other checks.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// A small, strict JPEG integrity check for untrusted uploads. Background: the JPEG decoder of the
    /// image library used for uploads (ImageSharp 3.1) is deliberately lenient - it silently renders a
    /// truncated file, or a file whose entropy-coded body was destroyed, as a partly grey image instead of
    /// failing - so "the file decodes without an exception" does not prove the file is complete. This class
    /// closes that gap: it walks the marker structure (a file without its closing EOI marker is truncated)
    /// and, for the common baseline/extended-sequential Huffman format (which covers virtually all camera
    /// and web JPEGs), it decodes the Huffman-coded scan data symbol by symbol without reconstructing any
    /// pixels (no IDCT, no memory proportional to the image size) and verifies that it is consistent: valid
    /// Huffman codes, coefficient positions within the block, the data covering exactly all MCUs, restart
    /// markers in place. Formats it does not model (progressive, arithmetic coding, 12 bit, DNL) are only
    /// checked for completeness of the marker structure. When in doubt it answers
    /// <see cref="JpegIntegrity.Unknown"/> rather than <see cref="JpegIntegrity.Corrupt"/>, so a valid
    /// photo is never rejected because of an unusual but legal feature.
    /// </summary>
    public static class JpegIntegrityChecker
    {
        // Non-marker bytes tolerated between the end of the decoded MCUs and the next marker (padding
        // written by some encoders); more than this means the data is longer than the image needs.
        private const int MaxTrailingScanBytes = 16;

        /// <summary>
        /// Checks the given JPEG bytes.
        /// </summary>
        /// <param name="data">The complete file content, starting with the SOI marker.</param>
        /// <returns>The integrity verdict.</returns>
        public static JpegIntegrity Check(byte[] data)
        {
            if (data is null || data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
                return JpegIntegrity.Unknown;

            try
            {
                return Walk(data);
            }
            catch (IndexOutOfRangeException)
            {
                // Defensive: a parsing bug must never turn into a rejection.
                return JpegIntegrity.Unknown;
            }
        }

        private static JpegIntegrity Walk(byte[] data)
        {
            var huffmanDc = new HuffmanTable?[4];
            var huffmanAc = new HuffmanTable?[4];
            Frame? frame = null;
            var restartInterval = 0;
            var scansDecoded = 0;
            var pos = 2;

            while (true)
            {
                // Find the next marker (tolerating stray bytes and fill 0xFF bytes between segments).
                while (pos < data.Length && data[pos] != 0xFF)
                    pos++;
                while (pos < data.Length && data[pos] == 0xFF)
                    pos++;
                if (pos >= data.Length)
                    return JpegIntegrity.Corrupt; // no EOI: truncated

                var marker = data[pos++];
                if (marker == 0x00 || marker == 0x01 || marker == 0xD8 || (marker >= 0xD0 && marker <= 0xD7))
                    continue; // stand-alone markers without a length

                if (marker == 0xD9)
                    return scansDecoded > 0 ? JpegIntegrity.Intact : JpegIntegrity.Unknown;

                if (pos + 2 > data.Length)
                    return JpegIntegrity.Corrupt;
                var segmentLength = (data[pos] << 8) | data[pos + 1];
                if (segmentLength < 2 || pos + segmentLength > data.Length)
                    return JpegIntegrity.Corrupt; // segment runs past the end of the file: truncated
                var segmentStart = pos + 2;
                var segmentEnd = pos + segmentLength;
                pos = segmentEnd;

                switch (marker)
                {
                    case 0xC4: // DHT
                        if (!TryReadHuffmanTables(data, segmentStart, segmentEnd, huffmanDc, huffmanAc))
                            return JpegIntegrity.Unknown;
                        break;

                    case 0xDD: // DRI
                        if (segmentEnd - segmentStart < 2)
                            return JpegIntegrity.Unknown;
                        restartInterval = (data[segmentStart] << 8) | data[segmentStart + 1];
                        break;

                    case 0xC0: // SOF0 baseline
                    case 0xC1: // SOF1 extended sequential
                        frame = TryReadFrame(data, segmentStart, segmentEnd);
                        break;

                    case 0xC2: // progressive
                    case 0xC3: // lossless
                    case 0xC5:
                    case 0xC6:
                    case 0xC7:
                    case 0xC9: // arithmetic coding variants
                    case 0xCA:
                    case 0xCB:
                    case 0xCD:
                    case 0xCE:
                    case 0xCF:
                        frame = null;
                        break;

                    case 0xDA: // SOS
                        {
                            var scanResult = frame is null
                                ? SkipScan(data, ref pos)
                                : DecodeScan(data, segmentStart, segmentEnd, frame, huffmanDc, huffmanAc, restartInterval, ref pos);
                            if (scanResult != JpegIntegrity.Intact)
                                return scanResult;
                            scansDecoded++;
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Skips the entropy-coded data of a scan this checker does not model, up to the next real marker.
        /// </summary>
        /// <param name="data">The file content.</param>
        /// <param name="pos">In: start of the entropy-coded data. Out: position of the next marker's 0xFF byte.</param>
        /// <returns><see cref="JpegIntegrity.Intact"/> if a following marker was found, otherwise <see cref="JpegIntegrity.Corrupt"/> (truncated).</returns>
        private static JpegIntegrity SkipScan(byte[] data, ref int pos)
        {
            while (pos < data.Length - 1)
            {
                if (data[pos] == 0xFF)
                {
                    var next = data[pos + 1];
                    if (next != 0x00 && next != 0xFF && !(next >= 0xD0 && next <= 0xD7))
                        return JpegIntegrity.Intact;
                    pos += next == 0xFF ? 1 : 2;
                    continue;
                }

                pos++;
            }

            return JpegIntegrity.Corrupt;
        }

        private static JpegIntegrity DecodeScan(
            byte[] data,
            int headerStart,
            int headerEnd,
            Frame frame,
            HuffmanTable?[] dcTables,
            HuffmanTable?[] acTables,
            int restartInterval,
            ref int pos)
        {
            // SOS header: Ns, then per component (selector, Td/Ta), then Ss, Se, Ah/Al.
            if (headerEnd - headerStart < 1)
                return JpegIntegrity.Unknown;
            var componentCount = data[headerStart];
            if (componentCount < 1 || componentCount > 4 || headerEnd - headerStart < 1 + (componentCount * 2) + 3)
                return JpegIntegrity.Unknown;

            var scanComponents = new ScanComponent[componentCount];
            for (var i = 0; i < componentCount; i++)
            {
                var selector = data[headerStart + 1 + (i * 2)];
                var tables = data[headerStart + 2 + (i * 2)];
                var frameIndex = Array.FindIndex(frame.Components, c => c.Id == selector);
                if (frameIndex < 0)
                    return JpegIntegrity.Unknown;
                var dc = dcTables[(tables >> 4) & 3];
                var ac = acTables[tables & 3];
                if (dc is null || ac is null)
                    return JpegIntegrity.Unknown;
                scanComponents[i] = new ScanComponent(frame.Components[frameIndex], dc, ac);
            }

            int mcuCountX;
            int mcuCountY;
            if (componentCount == 1)
            {
                var component = scanComponents[0].Component;
                var componentWidth = CeilDiv(frame.Width * component.H, frame.MaxH);
                var componentHeight = CeilDiv(frame.Height * component.V, frame.MaxV);
                mcuCountX = CeilDiv(componentWidth, 8);
                mcuCountY = CeilDiv(componentHeight, 8);
            }
            else
            {
                mcuCountX = CeilDiv(frame.Width, 8 * frame.MaxH);
                mcuCountY = CeilDiv(frame.Height, 8 * frame.MaxV);
            }

            var reader = new BitReader(data, pos);
            var totalMcus = (long)mcuCountX * mcuCountY;
            var nextRestart = 0;

            for (long mcu = 0; mcu < totalMcus; mcu++)
            {
                if (restartInterval > 0 && mcu > 0 && mcu % restartInterval == 0)
                {
                    if (!reader.ConsumeRestartMarker(nextRestart))
                        return JpegIntegrity.Corrupt;
                    nextRestart = (nextRestart + 1) & 7;
                }

                foreach (var scanComponent in scanComponents)
                {
                    var blocks = componentCount == 1 ? 1 : scanComponent.Component.H * scanComponent.Component.V;
                    for (var block = 0; block < blocks; block++)
                    {
                        if (!DecodeBlock(reader, scanComponent.Dc, scanComponent.Ac))
                            return JpegIntegrity.Corrupt;
                    }
                }
            }

            // All MCUs are decoded: what follows must be the next marker (a few padding bytes tolerated).
            reader.DiscardBits();
            var trailing = 0;
            while (true)
            {
                if (!reader.TryPeekMarker(out var isMarker))
                    return JpegIntegrity.Corrupt; // end of file without a marker: truncated
                if (isMarker)
                    break;
                reader.SkipByte();
                if (++trailing > MaxTrailingScanBytes)
                    return JpegIntegrity.Corrupt; // scan data much longer than the image needs: destroyed
            }

            pos = reader.Position;
            return JpegIntegrity.Intact;
        }

        private static bool DecodeBlock(BitReader reader, HuffmanTable dc, HuffmanTable ac)
        {
            var dcCategory = dc.Decode(reader);
            if (dcCategory < 0 || dcCategory > 15)
                return false;
            if (dcCategory > 0 && !reader.SkipBits(dcCategory))
                return false;

            var k = 1;
            while (k < 64)
            {
                var symbol = ac.Decode(reader);
                if (symbol < 0)
                    return false;

                var run = symbol >> 4;
                var size = symbol & 0x0F;
                if (size == 0)
                {
                    if (run == 15)
                    {
                        k += 16; // ZRL: 16 zero coefficients
                        continue;
                    }

                    break; // EOB
                }

                k += run;
                if (k > 63)
                    return false; // coefficient index outside the block
                if (!reader.SkipBits(size))
                    return false;
                k++;
            }

            return true;
        }

        private static bool TryReadHuffmanTables(byte[] data, int start, int end, HuffmanTable?[] dcTables, HuffmanTable?[] acTables)
        {
            var pos = start;
            while (pos < end)
            {
                if (pos + 17 > end)
                    return false;
                var tableClass = data[pos] >> 4;
                var tableId = data[pos] & 0x0F;
                var counts = new int[17];
                var total = 0;
                for (var i = 1; i <= 16; i++)
                {
                    counts[i] = data[pos + i];
                    total += counts[i];
                }

                pos += 17;
                if (total > 256 || pos + total > end || tableId > 3 || tableClass > 1)
                    return false;

                var table = HuffmanTable.TryCreate(counts, data, pos, total);
                if (table is null)
                    return false;
                pos += total;

                if (tableClass == 0)
                    dcTables[tableId] = table;
                else
                    acTables[tableId] = table;
            }

            return true;
        }

        private static Frame? TryReadFrame(byte[] data, int start, int end)
        {
            // Precision (1), height (2), width (2), component count (1), then 3 bytes per component.
            if (end - start < 6)
                return null;
            var precision = data[start];
            var height = (data[start + 1] << 8) | data[start + 2];
            var width = (data[start + 3] << 8) | data[start + 4];
            var componentCount = data[start + 5];
            if (precision != 8 || height == 0 || width == 0 || componentCount < 1 || componentCount > 4 || end - start < 6 + (componentCount * 3))
                return null;

            var components = new FrameComponent[componentCount];
            var maxH = 1;
            var maxV = 1;
            for (var i = 0; i < componentCount; i++)
            {
                var offset = start + 6 + (i * 3);
                var h = data[offset + 1] >> 4;
                var v = data[offset + 1] & 0x0F;
                if (h < 1 || h > 4 || v < 1 || v > 4)
                    return null;
                components[i] = new FrameComponent(data[offset], h, v);
                maxH = Math.Max(maxH, h);
                maxV = Math.Max(maxV, v);
            }

            return new Frame(width, height, maxH, maxV, components);
        }

        private static int CeilDiv(int value, int divisor) => (value + divisor - 1) / divisor;

        private sealed record FrameComponent(int Id, int H, int V);

        private sealed record Frame(int Width, int Height, int MaxH, int MaxV, FrameComponent[] Components);

        private sealed record ScanComponent(FrameComponent Component, HuffmanTable Dc, HuffmanTable Ac);

        /// <summary>
        /// A canonical JPEG Huffman table (ITU T.81 Annex C/F), decoding bit by bit.
        /// </summary>
        private sealed class HuffmanTable
        {
            private readonly int[] _maxCode = new int[17];
            private readonly int[] _minCode = new int[17];
            private readonly int[] _valuePointer = new int[17];
            private readonly byte[] _values;

            private HuffmanTable(byte[] values)
            {
                _values = values;
            }

            public static HuffmanTable? TryCreate(int[] counts, byte[] data, int valuesStart, int total)
            {
                var values = new byte[total];
                Array.Copy(data, valuesStart, values, 0, total);
                var table = new HuffmanTable(values);

                var code = 0;
                var index = 0;
                for (var length = 1; length <= 16; length++)
                {
                    table._valuePointer[length] = index;
                    table._minCode[length] = code;
                    code += counts[length];
                    index += counts[length];
                    if (code > (1 << length))
                        return null; // over-subscribed table
                    table._maxCode[length] = counts[length] == 0 ? -1 : code - 1;
                    code <<= 1;
                }

                return table;
            }

            /// <summary>
            /// Decodes one symbol. Returns -1 for an invalid code or when the data ends.
            /// </summary>
            /// <param name="reader">The bit reader positioned at the next code.</param>
            /// <returns>The decoded symbol, or -1.</returns>
            public int Decode(BitReader reader)
            {
                var code = 0;
                for (var length = 1; length <= 16; length++)
                {
                    var bit = reader.ReadBit();
                    if (bit < 0)
                        return -1;
                    code = (code << 1) | bit;
                    if (_maxCode[length] >= 0 && code <= _maxCode[length] && code >= _minCode[length])
                        return _values[_valuePointer[length] + code - _minCode[length]];
                }

                return -1;
            }
        }

        /// <summary>
        /// Reads the entropy-coded scan data bit by bit, handling byte stuffing (0xFF 0x00) and stopping
        /// at real markers. Holds at most the bits of one byte, so the byte position is always exact.
        /// </summary>
        private sealed class BitReader
        {
            private readonly byte[] _data;
            private int _bitsLeft;
            private int _current;

            public BitReader(byte[] data, int position)
            {
                _data = data;
                Position = position;
            }

            /// <summary>Gets the index of the next byte that has not been loaded yet.</summary>
            public int Position { get; private set; }

            /// <summary>
            /// Reads the next bit.
            /// </summary>
            /// <returns>0 or 1, or -1 when a marker or the end of the file is reached.</returns>
            public int ReadBit()
            {
                if (_bitsLeft == 0)
                {
                    if (!TryLoadByte())
                        return -1;
                }

                _bitsLeft--;
                return (_current >> _bitsLeft) & 1;
            }

            /// <summary>
            /// Skips <paramref name="count"/> bits.
            /// </summary>
            /// <param name="count">The number of bits to skip.</param>
            /// <returns><see langword="false"/> if the data ends before that many bits are available.</returns>
            public bool SkipBits(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    if (ReadBit() < 0)
                        return false;
                }

                return true;
            }

            /// <summary>Discards the remaining bits of the current byte (padding before a marker).</summary>
            public void DiscardBits() => _bitsLeft = 0;

            /// <summary>
            /// Consumes the restart marker <c>RSTn</c> expected at the current byte position.
            /// </summary>
            /// <param name="expectedIndex">The expected restart index (0-7).</param>
            /// <returns><see langword="true"/> if the expected marker was found and consumed.</returns>
            public bool ConsumeRestartMarker(int expectedIndex)
            {
                DiscardBits();
                var pos = Position;
                while (pos < _data.Length && _data[pos] == 0xFF && pos + 1 < _data.Length && _data[pos + 1] == 0xFF)
                    pos++;
                if (pos + 1 >= _data.Length || _data[pos] != 0xFF || _data[pos + 1] != 0xD0 + expectedIndex)
                    return false;
                Position = pos + 2;
                return true;
            }

            /// <summary>
            /// Looks at the byte at the current position without consuming it.
            /// </summary>
            /// <param name="isMarker">Whether it starts a real marker (0xFF followed by neither 0x00, 0xFF nor RSTn).</param>
            /// <returns><see langword="false"/> if the end of the file was reached.</returns>
            public bool TryPeekMarker(out bool isMarker)
            {
                isMarker = false;
                if (Position >= _data.Length)
                    return false;
                if (_data[Position] != 0xFF)
                    return true;
                if (Position + 1 >= _data.Length)
                    return false;
                var next = _data[Position + 1];

                // A stuffed 0xFF, fill byte or restart marker after the last MCU counts as a stray byte.
                isMarker = next != 0x00 && next != 0xFF && !(next >= 0xD0 && next <= 0xD7);
                return true;
            }

            /// <summary>Skips one byte.</summary>
            public void SkipByte() => Position++;

            private bool TryLoadByte()
            {
                while (Position < _data.Length && _data[Position] == 0xFF && Position + 1 < _data.Length && _data[Position + 1] == 0xFF)
                    Position++; // fill byte

                if (Position >= _data.Length)
                    return false;

                var value = _data[Position];
                if (value == 0xFF)
                {
                    if (Position + 1 >= _data.Length || _data[Position + 1] != 0x00)
                        return false; // a marker (or the end): no more scan data
                    Position += 2; // stuffed 0xFF
                }
                else
                {
                    Position++;
                }

                _current = value;
                _bitsLeft = 8;
                return true;
            }
        }
    }
}
