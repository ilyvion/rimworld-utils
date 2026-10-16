using System.Buffers.Binary;
using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;
using ZstdSharp;

namespace AssetPackBaker;

/// <summary>
/// Writes RimWorld's mod asset pack (<c>Assets.zip</c>): a ZIP archive whose entries are
/// Zstandard-compressed (method 93) or stored, plus a <c>bake.manifest</c> entry mapping every
/// entry name to the SHA-256 of its uncompressed content and a <c>textures.info</c> entry
/// describing every texture. Entries with identical content and codec are stored once, under
/// the ordinally smallest name; the game resolves the other names through the manifest.
/// </summary>
internal sealed class AssetPackWriter
{
    public const string ManifestEntryName = "bake.manifest";
    public const string TextureInfoEntryName = "textures.info";
    public const int CompressionLevel = 19;

    private const ushort MethodStore = 0;
    private const ushort MethodZstd = 93;
    private const ushort VersionNeeded = 20;
    private const ushort VersionNeededZip64 = 45;
    private const ushort VersionMadeBy = 0x0314; // Unix, ZIP 2.0
    private const ushort Utf8NamesFlag = 0x0800;
    private const uint DosTime = 0x00210000; // 1980-01-01 00:00
    private const uint ExternalAttributes = 0x01A40000; // -rw-r--r--

    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> hashes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BakedTexture> textures = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    public int Count => hashes.Count;

    public int Deduplicated => hashes.Count - entries.Count;

    private sealed record Entry(string Name, byte[] Payload, uint Crc, int Length, bool Stored);

    public void AddTexture(string name, BakedTexture texture)
    {
        Add(name, texture.Dds, stored: false);
        lock (gate)
        {
            textures[name] = texture;
        }
    }

    public void AddRaw(string name, byte[] content, bool stored) => Add(name, content, stored);

    public void Write(string path)
    {
        var manifest = new StringBuilder();
        foreach (var (name, hash) in hashes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            _ = manifest.Append(name).Append(' ').Append(hash).Append('\n');
        }
        var textureInfo = new StringBuilder();
        foreach (var (name, t) in textures.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            _ = textureInfo
                .Append(name)
                .Append(' ')
                .Append(t.Width)
                .Append(' ')
                .Append(t.Height)
                .Append(' ')
                .Append(t.MipCount)
                .Append(' ')
                .Append((int)t.Format)
                .Append(' ')
                .Append(t.Opaque ? 1 : 0)
                .Append(' ')
                .Append(t.Swizzle)
                .Append('\n');
        }

        List<Entry> all =
        [
            .. entries.Values,
            CreateEntry(ManifestEntryName, Encoding.UTF8.GetBytes(manifest.ToString()), false),
            CreateEntry(
                TextureInfoEntryName,
                Encoding.UTF8.GetBytes(textureInfo.ToString()),
                false
            ),
        ];
        all.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        using var stream = File.Create(path);
        List<(Entry Entry, long Offset)> central = new(all.Count);
        Span<byte> local = stackalloc byte[30];
        foreach (var entry in all)
        {
            var name = Encoding.UTF8.GetBytes(entry.Name);
            central.Add((entry, stream.Position));
            BinaryPrimitives.WriteUInt32LittleEndian(local, 0x04034B50);
            WriteSharedFields(local[4..], VersionNeeded, entry, name.Length, 0);
            stream.Write(local);
            stream.Write(name);
            stream.Write(entry.Payload);
        }

        var directoryOffset = stream.Position;
        Span<byte> header = stackalloc byte[46];
        Span<byte> zip64Offset = stackalloc byte[12];
        foreach (var (entry, offset) in central)
        {
            var name = Encoding.UTF8.GetBytes(entry.Name);
            var zip64 = offset >= uint.MaxValue;
            header.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(header, 0x02014B50);
            BinaryPrimitives.WriteUInt16LittleEndian(header[4..], VersionMadeBy);
            WriteSharedFields(
                header[6..],
                zip64 ? VersionNeededZip64 : VersionNeeded,
                entry,
                name.Length,
                zip64 ? zip64Offset.Length : 0
            );
            BinaryPrimitives.WriteUInt32LittleEndian(header[38..], ExternalAttributes);
            BinaryPrimitives.WriteUInt32LittleEndian(
                header[42..],
                zip64 ? uint.MaxValue : (uint)offset
            );
            stream.Write(header);
            stream.Write(name);
            if (zip64)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(zip64Offset, 1);
                BinaryPrimitives.WriteUInt16LittleEndian(zip64Offset[2..], 8);
                BinaryPrimitives.WriteInt64LittleEndian(zip64Offset[4..], offset);
                stream.Write(zip64Offset);
            }
        }
        WriteEnd(stream, central.Count, stream.Position - directoryOffset, directoryOffset);
    }

    private void Add(string name, byte[] content, bool stored)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content));
        var key = stored ? hash + ":store" : hash;
        var entry = CreateEntry(name, content, stored);
        lock (gate)
        {
            hashes[name] = hash;
            if (
                !entries.TryGetValue(key, out var owner)
                || string.CompareOrdinal(name, owner.Name) < 0
            )
            {
                entries[key] = entry;
            }
        }
    }

    private static Entry CreateEntry(string name, byte[] content, bool stored)
    {
        var crc = BinaryPrimitives.ReadUInt32LittleEndian(Crc32.Hash(content));
        byte[] payload;
        if (stored)
        {
            payload = content;
        }
        else
        {
            using Compressor compressor = new(CompressionLevel);
            payload = compressor.Wrap(content).ToArray();
        }
        return new Entry(name, payload, crc, content.Length, stored);
    }

    private static void WriteSharedFields(
        Span<byte> fields,
        ushort versionNeeded,
        Entry entry,
        int nameLength,
        int extraLength
    )
    {
        BinaryPrimitives.WriteUInt16LittleEndian(fields, versionNeeded);
        BinaryPrimitives.WriteUInt16LittleEndian(fields[2..], Utf8NamesFlag);
        BinaryPrimitives.WriteUInt16LittleEndian(
            fields[4..],
            entry.Stored ? MethodStore : MethodZstd
        );
        BinaryPrimitives.WriteUInt32LittleEndian(fields[6..], DosTime);
        BinaryPrimitives.WriteUInt32LittleEndian(fields[10..], entry.Crc);
        BinaryPrimitives.WriteUInt32LittleEndian(fields[14..], (uint)entry.Payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(fields[18..], (uint)entry.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(fields[22..], (ushort)nameLength);
        BinaryPrimitives.WriteUInt16LittleEndian(fields[24..], (ushort)extraLength);
    }

    private static void WriteEnd(Stream stream, long count, long size, long offset)
    {
        if (count >= ushort.MaxValue || offset >= uint.MaxValue || size >= uint.MaxValue)
        {
            Span<byte> zip64 = stackalloc byte[76];
            zip64.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(zip64, 0x06064B50);
            BinaryPrimitives.WriteInt64LittleEndian(zip64[4..], 44);
            BinaryPrimitives.WriteUInt16LittleEndian(zip64[12..], VersionMadeBy);
            BinaryPrimitives.WriteUInt16LittleEndian(zip64[14..], VersionMadeBy);
            BinaryPrimitives.WriteInt64LittleEndian(zip64[24..], count);
            BinaryPrimitives.WriteInt64LittleEndian(zip64[32..], count);
            BinaryPrimitives.WriteInt64LittleEndian(zip64[40..], size);
            BinaryPrimitives.WriteInt64LittleEndian(zip64[48..], offset);
            var locator = zip64[56..];
            BinaryPrimitives.WriteUInt32LittleEndian(locator, 0x07064B50);
            BinaryPrimitives.WriteInt64LittleEndian(locator[8..], offset + size);
            BinaryPrimitives.WriteUInt32LittleEndian(locator[16..], 1);
            stream.Write(zip64);
        }
        Span<byte> end = stackalloc byte[22];
        end.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(end, 0x06054B50);
        BinaryPrimitives.WriteUInt16LittleEndian(
            end[8..],
            (ushort)Math.Min(count, ushort.MaxValue)
        );
        BinaryPrimitives.WriteUInt16LittleEndian(
            end[10..],
            (ushort)Math.Min(count, ushort.MaxValue)
        );
        BinaryPrimitives.WriteUInt32LittleEndian(end[12..], (uint)Math.Min(size, uint.MaxValue));
        BinaryPrimitives.WriteUInt32LittleEndian(end[16..], (uint)Math.Min(offset, uint.MaxValue));
        stream.Write(end);
    }
}
