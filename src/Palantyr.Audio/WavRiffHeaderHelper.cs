using System.Buffers.Binary;
using System.Text;

namespace Palantyr.Audio;

/// <summary>
/// Standard 44-byte RIFF/WAVE PCM header writer, in-place crash header repairer (ADR-003, FR-REC-006),
/// and 10-second validation audio clip slicer (FR-VAL-002).
/// Audio format: 16,000 Hz, 16-bit signed PCM, 1 channel (mono), 32,000 bytes/second.
/// </summary>
public static class WavRiffHeaderHelper
{
    public const int HeaderSizeBytes = 44;
    public const int SampleRateHz = 16000;
    public const short Channels = 1;
    public const short BitsPerSample = 16;
    public const short BlockAlign = Channels * (BitsPerSample / 8); // 2 bytes per frame
    public const int BytesPerSecond = SampleRateHz * BlockAlign;     // 32,000 bytes/sec
    public const int TwoSecondChunkBytes = BytesPerSecond * 2;       // 64,000 bytes per 2s flush

    /// <summary>
    /// Creates a standard 44-byte RIFF/WAVE PCM header for 16 kHz, 16-bit mono audio.
    /// </summary>
    public static byte[] CreateWavHeader(long pcmDataLengthBytes)
    {
        long clampedDataBytes = Math.Clamp(pcmDataLengthBytes, 0L, uint.MaxValue - 36L);
        uint dataChunkSize = (uint)clampedDataBytes;
        uint riffChunkSize = 36u + dataChunkSize;

        var header = new byte[HeaderSizeBytes];
        var span = header.AsSpan();

        // "RIFF" chunk descriptor
        Encoding.ASCII.GetBytes("RIFF", span[0..4]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..8], riffChunkSize);
        Encoding.ASCII.GetBytes("WAVE", span[8..12]);

        // "fmt " sub-chunk (16 bytes for PCM)
        Encoding.ASCII.GetBytes("fmt ", span[12..16]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..20], 16u); // Subchunk1Size (16 for PCM)
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..22], 1);   // AudioFormat (1 = PCM)
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..24], (ushort)Channels);
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..28], (uint)SampleRateHz);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..32], (uint)BytesPerSecond);
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..34], (ushort)BlockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(span[34..36], (ushort)BitsPerSample);

        // "data" sub-chunk
        Encoding.ASCII.GetBytes("data", span[36..40]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[40..44], dataChunkSize);

        return header;
    }

    /// <summary>
    /// Writes a 44-byte RIFF/WAVE header at the beginning of the stream.
    /// </summary>
    public static void WriteWavHeader(Stream stream, long pcmDataLengthBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = CreateWavHeader(pcmDataLengthBytes);
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }
        stream.Write(header, 0, header.Length);
    }

    /// <summary>
    /// Updates the RIFF chunk size (offset 4) and data subchunk size (offset 40) in-place on a seekable stream.
    /// </summary>
    public static void UpdateWavHeaderSizes(Stream stream, long pcmDataLengthBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        long clampedDataBytes = Math.Clamp(pcmDataLengthBytes, 0L, uint.MaxValue - 36L);
        uint dataChunkSize = (uint)clampedDataBytes;
        uint riffChunkSize = 36u + dataChunkSize;

        Span<byte> buffer = stackalloc byte[4];

        stream.Seek(4, SeekOrigin.Begin);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, riffChunkSize);
        stream.Write(buffer);

        stream.Seek(40, SeekOrigin.Begin);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, dataChunkSize);
        stream.Write(buffer);
    }

    /// <summary>
    /// Inspects the actual file size on disk (<c>length - 44</c>), updates the RIFF chunk size at byte offset 4
    /// and <c>data</c> subchunk size at byte offset 40 in-place with <see cref="FileOptions.WriteThrough"/>,
    /// and returns the recovered duration in milliseconds (<c>(pcmBytes * 1000L) / 32000L</c>).
    /// </summary>
    public static long RepairUnclosedWavHeader(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("WAV segment file not found for header repair.", filePath);
        }

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.WriteThrough);

        if (stream.Length < HeaderSizeBytes)
        {
            return 0L;
        }

        long pcmBytes = stream.Length - HeaderSizeBytes;
        UpdateWavHeaderSizes(stream, pcmBytes);
        stream.Flush(flushToDisk: true);

        return CalculateDurationMs(pcmBytes);
    }

    /// <summary>
    /// Extracts a standalone WAV byte array containing ~<paramref name="windowSeconds"/> seconds (±half window around
    /// <paramref name="centerTimestampMs"/>) of PCM frames from a recorded segment file (FR-VAL-002).
    /// </summary>
    public static byte[] ExtractPcmSliceAsWav(string filePath, long centerTimestampMs, int windowSeconds = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("WAV segment file not found for clip extraction.", filePath);
        }

        if (windowSeconds <= 0)
        {
            windowSeconds = 10;
        }

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            options: FileOptions.SequentialScan);

        long totalPcmBytes = Math.Max(0L, stream.Length - HeaderSizeBytes);
        // Align to 16-bit mono frame boundary (2 bytes)
        totalPcmBytes -= totalPcmBytes % BlockAlign;

        if (totalPcmBytes <= 0)
        {
            return CreateWavHeader(0);
        }

        long windowMs = windowSeconds * 1000L;
        long halfWindowMs = windowMs / 2L;
        long clampedCenterMs = Math.Max(0L, centerTimestampMs);

        long startMs = Math.Max(0L, clampedCenterMs - halfWindowMs);
        long requestedBytes = (windowMs * BytesPerSecond) / 1000L;
        requestedBytes -= requestedBytes % BlockAlign;

        long startByteOffset = (startMs * BytesPerSecond) / 1000L;
        startByteOffset -= startByteOffset % BlockAlign;

        if (startByteOffset >= totalPcmBytes)
        {
            startByteOffset = Math.Max(0L, totalPcmBytes - requestedBytes);
            startByteOffset -= startByteOffset % BlockAlign;
        }

        long availableBytesFromStart = totalPcmBytes - startByteOffset;
        int slicePcmBytes = (int)Math.Min(requestedBytes, availableBytesFromStart);
        slicePcmBytes -= slicePcmBytes % BlockAlign;

        var output = new byte[HeaderSizeBytes + slicePcmBytes];
        var header = CreateWavHeader(slicePcmBytes);
        Buffer.BlockCopy(header, 0, output, 0, HeaderSizeBytes);

        if (slicePcmBytes > 0)
        {
            stream.Seek(HeaderSizeBytes + startByteOffset, SeekOrigin.Begin);
            int totalRead = 0;
            while (totalRead < slicePcmBytes)
            {
                int read = stream.Read(output, HeaderSizeBytes + totalRead, slicePcmBytes - totalRead);
                if (read == 0)
                {
                    break;
                }
                totalRead += read;
            }

            if (totalRead < slicePcmBytes)
            {
                totalRead -= totalRead % BlockAlign;
                var resized = new byte[HeaderSizeBytes + totalRead];
                var adjustedHeader = CreateWavHeader(totalRead);
                Buffer.BlockCopy(adjustedHeader, 0, resized, 0, HeaderSizeBytes);
                Buffer.BlockCopy(output, HeaderSizeBytes, resized, HeaderSizeBytes, totalRead);
                return resized;
            }
        }

        return output;
    }

    /// <summary>
    /// Computes audio duration in milliseconds from PCM byte count (<c>(pcmBytes * 1000L) / 32000L</c>).
    /// </summary>
    public static long CalculateDurationMs(long pcmBytes)
    {
        if (pcmBytes <= 0)
        {
            return 0L;
        }

        return (pcmBytes * 1000L) / BytesPerSecond;
    }
}
