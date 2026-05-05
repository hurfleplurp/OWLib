using System;
using System.Collections.Generic;
using System.IO;
using TankLib.Math;

namespace TankLib.Replay.Frames
{
    /// <summary>
    /// Reads and parses replay frames from decompressed replay data.
    /// This is the main entry point for frame-by-frame replay iteration.
    /// </summary>
    public class ReplayFrameReader : IDisposable
    {
        private readonly BinaryReader _reader;
        private readonly MemoryStream _stream;
        private readonly long _dataLength;

        public int CurrentPosition => (int)_stream.Position;
        public int RemainingBytes => (int)(_dataLength - _stream.Position);
        public bool HasMoreData => _stream.Position < _dataLength;

        // Frame reading state
        private uint _lastTickNumber;
        private uint _frameCount;

        /// <summary>
        /// Detected frame format version (discovered during parsing)
        /// </summary>
        public int DetectedFormatVersion { get; private set; }

        /// <summary>
        /// List of detected frame boundaries
        /// </summary>
        public List<FrameBoundary> DetectedFrames { get; } = new List<FrameBoundary>();

        public ReplayFrameReader(byte[] decompressedData)
        {
            if (decompressedData == null || decompressedData.Length == 0)
                throw new ArgumentException("Decompressed data is null or empty", nameof(decompressedData));

            _stream = new MemoryStream(decompressedData);
            _reader = new BinaryReader(_stream);
            _dataLength = decompressedData.Length;
        }

        /// <summary>
        /// Analyze the data to detect frame format and boundaries
        /// </summary>
        public void AnalyzeStructure()
        {
            long startPos = _stream.Position;

            try
            {
                // Strategy 1: Look for length-prefixed frames
                DetectLengthPrefixedFrames();

                // Strategy 2: Look for tick number sequences
                if (DetectedFrames.Count < 10)
                {
                    DetectTickSequenceFrames();
                }

                // Strategy 3: Look for fixed-interval patterns
                if (DetectedFrames.Count < 10)
                {
                    DetectFixedIntervalFrames();
                }
            }
            finally
            {
                _stream.Position = startPos;
            }
        }

        private void DetectLengthPrefixedFrames()
        {
            _stream.Position = 0;
            var candidates = new List<FrameBoundary>();

            int offset = 0;
            int consecutiveValid = 0;
            int lastFrameEnd = 0;

            while (offset < _dataLength - 8)
            {
                _stream.Position = offset;

                // Try reading as uint16 length first (more common for small frames)
                ushort len16 = _reader.ReadUInt16();
                if (len16 > 4 && len16 < 2048 && offset + 2 + len16 <= _dataLength)
                {
                    // Check if this could be followed by another valid frame
                    int nextOffset = offset + 2 + len16;
                    if (nextOffset < _dataLength - 2)
                    {
                        _stream.Position = nextOffset;
                        ushort nextLen = _reader.ReadUInt16();
                        if (nextLen > 4 && nextLen < 2048)
                        {
                            if (offset == lastFrameEnd || offset == 0)
                            {
                                consecutiveValid++;
                                candidates.Add(new FrameBoundary
                                {
                                    Offset = offset,
                                    HeaderSize = 2,
                                    DataSize = len16,
                                    Confidence = 0.5f + (consecutiveValid * 0.1f)
                                });
                                lastFrameEnd = offset + 2 + len16;
                                offset = lastFrameEnd;
                                continue;
                            }
                        }
                    }
                }

                // Try uint32 length
                _stream.Position = offset;
                uint len32 = _reader.ReadUInt32();
                if (len32 > 8 && len32 < 65536 && offset + 4 + len32 <= _dataLength)
                {
                    int nextOffset = offset + 4 + (int)len32;
                    if (nextOffset < _dataLength - 4)
                    {
                        _stream.Position = nextOffset;
                        uint nextLen = _reader.ReadUInt32();
                        if (nextLen > 8 && nextLen < 65536)
                        {
                            if (offset == lastFrameEnd || offset == 0)
                            {
                                consecutiveValid++;
                                candidates.Add(new FrameBoundary
                                {
                                    Offset = offset,
                                    HeaderSize = 4,
                                    DataSize = (int)len32,
                                    Confidence = 0.5f + (consecutiveValid * 0.1f)
                                });
                                lastFrameEnd = offset + 4 + (int)len32;
                                offset = lastFrameEnd;
                                continue;
                            }
                        }
                    }
                }

                consecutiveValid = 0;
                offset++;
            }

            // Keep candidates with good confidence
            foreach (var candidate in candidates)
            {
                if (candidate.Confidence > 0.6f)
                    DetectedFrames.Add(candidate);
            }
        }

        private void DetectTickSequenceFrames()
        {
            _stream.Position = 0;

            // Look for incrementing uint32 values that could be tick numbers
            var tickCandidates = new Dictionary<int, List<(int offset, uint value)>>();

            for (int fieldOffset = 0; fieldOffset < 32; fieldOffset += 4)
            {
                tickCandidates[fieldOffset] = new List<(int, uint)>();

                // Sample values at this field offset across the data
                for (int dataOffset = 0; dataOffset < _dataLength - 64; dataOffset += 64)
                {
                    _stream.Position = dataOffset + fieldOffset;
                    if (_stream.Position + 4 <= _dataLength)
                    {
                        uint value = _reader.ReadUInt32();
                        // Tick numbers should be reasonable (1 to millions)
                        if (value > 0 && value < 10000000)
                        {
                            tickCandidates[fieldOffset].Add((dataOffset + fieldOffset, value));
                        }
                    }
                }
            }

            // Find field offset with most monotonically increasing values
            int bestOffset = -1;
            int bestScore = 0;

            foreach (var kvp in tickCandidates)
            {
                int score = 0;
                var values = kvp.Value;
                for (int i = 1; i < values.Count; i++)
                {
                    if (values[i].value > values[i - 1].value &&
                        values[i].value - values[i - 1].value < 1000)
                    {
                        score++;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestOffset = kvp.Key;
                }
            }

            if (bestOffset >= 0 && bestScore > 5)
            {
                // Use this to establish frame boundaries
                // Each detected tick value location is start of a frame
                foreach (var (offset, value) in tickCandidates[bestOffset])
                {
                    if (offset >= bestOffset)
                    {
                        DetectedFrames.Add(new FrameBoundary
                        {
                            Offset = offset - bestOffset,
                            HeaderSize = bestOffset + 4,
                            DataSize = -1, // Unknown
                            Confidence = 0.4f,
                            TickNumber = value
                        });
                    }
                }
            }
        }

        private void DetectFixedIntervalFrames()
        {
            // Look for repeating byte patterns at fixed intervals
            var intervalScores = new Dictionary<int, int>();

            for (int interval = 32; interval < 512; interval += 4)
            {
                int matches = 0;
                for (int offset = 0; offset + interval < _dataLength && offset < 4096; offset += interval)
                {
                    // Check if similar structure at each interval
                    byte b0 = _stream.Position < _dataLength ? GetByteAt(offset) : (byte)0;
                    byte b1 = _stream.Position < _dataLength ? GetByteAt(offset + interval) : (byte)0;

                    // Very rough heuristic: first few bytes might be similar for frames
                    if (System.Math.Abs(b0 - b1) < 16)
                        matches++;;
                }

                if (matches > 5)
                    intervalScores[interval] = matches;
            }

            // If we found a likely interval, generate frame boundaries
            if (intervalScores.Count > 0)
            {
                var bestInterval = 0;
                var bestScore = 0;
                foreach (var kvp in intervalScores)
                {
                    if (kvp.Value > bestScore)
                    {
                        bestScore = kvp.Value;
                        bestInterval = kvp.Key;
                    }
                }

                if (bestInterval > 0)
                {
                    for (int offset = 0; offset < _dataLength; offset += bestInterval)
                    {
                        DetectedFrames.Add(new FrameBoundary
                        {
                            Offset = offset,
                            HeaderSize = -1,
                            DataSize = bestInterval,
                            Confidence = 0.3f
                        });
                    }
                }
            }
        }

        private byte GetByteAt(int offset)
        {
            if (offset >= 0 && offset < _dataLength)
            {
                _stream.Position = offset;
                return _reader.ReadByte();
            }
            return 0;
        }

        /// <summary>
        /// Read raw bytes from current position
        /// </summary>
        public byte[] ReadBytes(int count)
        {
            return _reader.ReadBytes(count);
        }

        /// <summary>
        /// Read a single value at current position
        /// </summary>
        public T Read<T>() where T : unmanaged
        {
            return _reader.Read<T>();
        }

        /// <summary>
        /// Seek to a specific offset
        /// </summary>
        public void Seek(int offset)
        {
            _stream.Position = offset;
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _stream?.Dispose();
        }
    }

    public class FrameBoundary
    {
        public int Offset { get; set; }
        public int HeaderSize { get; set; }
        public int DataSize { get; set; }
        public float Confidence { get; set; }
        public uint TickNumber { get; set; }
    }
}
