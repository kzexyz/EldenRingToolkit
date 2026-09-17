using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BCnEncoder.Decoder;
using BCnEncoder.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZstdSharp;
using KsEldenRingToolkitManager.Models;
namespace KsEldenRingToolkitManager.Services;

internal static class EldenRingGameIconService
{
    private const string Data0PublicKeyPem = """
-----BEGIN RSA PUBLIC KEY-----
MIIBCwKCAQEA9Rju2whruXDVQZpfylVEPeNxm7XgMHcDyaaRUIpXQE0qEo+6Y36L
P0xpFvL0H0kKxHwpuISsdgrnMHJ/yj4S61MWzhO8y4BQbw/zJehhDSRCecFJmFBz
3I2JC5FCjoK+82xd9xM5XXdfsdBzRiSghuIHL4qk2WZ/0f/nK5VygeWXn/oLeYBL
jX1S8wSSASza64JXjt0bP/i6mpV2SLZqKRxo7x2bIQrR1yHNekSF2jBhZIgcbtMB
xjCywn+7p954wjcfjxB5VWaZ4hGbKhi1bhYPccht4XnGhcUTWO3NmJWslwccjQ4k
sutLq3uRjLMM0IeTkQO6Pv8/R7UNFtdCWwIERzH8IQ==
-----END RSA PUBLIC KEY-----
""";
    private static readonly object Sync = new();
    private static Task<AtlasState?>? warmupTask;
    private static string? warmupFolder;
    private static readonly object LogSync = new();
    private static readonly object AtlasBitmapSync = new();
    private static string? cachedAtlasName;
    private static BitmapSource? cachedAtlasBitmap;
    private static string DebugLogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KsEldenRingToolkitManager", "GameIconDebug.log");
    private static string SignatureIndexPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KsEldenRingToolkitManager", "GameIconSignatureIndexV5.bin");
    private const string FullCacheMarkerFileName = ".native-icons-ready-v1";
    public static bool HasDeterministicIconMapping(EldenRingItem item)
    {
        return EldenRingIconMap.TryGetIconId(item, out int iconId) && iconId > 0;
    }
    public static void StartWarmup(string? gameFolder)
    {
    }
    public static int GetMappedItemCount(IEnumerable<EldenRingItem> items)
    {
        int count = 0;
        foreach (EldenRingItem item in items)
        {
            if (EldenRingIconMap.TryGetIconId(item, out int iconId) && iconId > 0) count++;
        }
        return count;
    }
    public static bool IsFullCacheReady(string gameFolder, IEnumerable<EldenRingItem> items, Func<EldenRingItem, string> outputPathFactory)
    {
        string? cacheDirectory = GetCacheDirectory(items, outputPathFactory);
        if (string.IsNullOrWhiteSpace(cacheDirectory)) return true;
        string markerPath = Path.Combine(cacheDirectory, FullCacheMarkerFileName);
        if (!File.Exists(markerPath)) return false;
        try
        {
            string expected = BuildCacheMarkerSignature(gameFolder);
            string actual = File.ReadAllText(markerPath, Encoding.UTF8).Trim();
            return string.Equals(actual, expected, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
    public static async Task<PrecacheResult> PrecacheAllHighResolutionIconsAsync(string gameFolder, IReadOnlyList<EldenRingItem> items, Func<EldenRingItem, string> outputPathFactory, IProgress<PrecacheProgress>? progress = null)
    {
        if (!IsValidGameFolder(gameFolder)) return new PrecacheResult(0, 0, 0, 0, false);
        List<(EldenRingItem Item, int IconId, string OutputPath)> mapped = new();
        foreach (EldenRingItem item in items)
        {
            if (!EldenRingIconMap.TryGetIconId(item, out int iconId) || iconId <= 0) continue;
            mapped.Add((item, iconId, outputPathFactory(item)));
        }
        if (mapped.Count == 0)
        {
            ReleaseRuntimeCache();
            return new PrecacheResult(0, 0, 0, 0, true);
        }
        AtlasState? state = await EnsureReadyAsync(gameFolder).ConfigureAwait(false);
        if (state == null || state.Cells.Count == 0)
        {
            ReleaseRuntimeCache();
            return new PrecacheResult(mapped.Count, 0, 0, mapped.Count, false);
        }
        Dictionary<int, IconCell> cellById = new();
        foreach (IconCell cell in state.Cells)
        {
            if (cell.IconId > 0 && !cellById.ContainsKey(cell.IconId)) cellById[cell.IconId] = cell;
        }
        List<(EldenRingItem Item, int IconId, string OutputPath, IconCell Cell)> resolvable = mapped.Where(x => cellById.TryGetValue(x.IconId, out _)).Select(x => (x.Item, x.IconId, x.OutputPath, Cell: cellById[x.IconId])).ToList();
        int skippedNoNativeCell = mapped.Count - resolvable.Count;
        int total = resolvable.Count;
        int existing = resolvable.Count(x => File.Exists(x.OutputPath));
        progress?.Report(new PrecacheProgress(existing, total, "Checking icon cache..."));
        var pending = resolvable.Where(x => !File.Exists(x.OutputPath)).OrderBy(x => x.Cell.AtlasName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.IconId).ToList();
        int completed = existing;
        int created = 0;
        string? currentAtlas = null;
        foreach (var entry in pending)
        {
            if (!string.Equals(currentAtlas, entry.Cell.AtlasName, StringComparison.OrdinalIgnoreCase))
            {
                if (currentAtlas != null) ReleaseDecodedAtlas();
                currentAtlas = entry.Cell.AtlasName;
                progress?.Report(new PrecacheProgress(completed, total, "Loading native icon atlas..."));
            }
            if (SaveCellAsPng(state, entry.Cell, entry.OutputPath)) created++;
            completed++;
            if (completed == total || completed % 8 == 0)
            {
                progress?.Report(new PrecacheProgress(completed, total, $"Caching high-resolution icons {completed}/{total}"));
            }
        }
        int failed = resolvable.Count(x => !File.Exists(x.OutputPath));
        bool complete = failed == 0;
        if (complete)
        {
            string? cacheDirectory = GetCacheDirectory(items, outputPathFactory);
            if (!string.IsNullOrWhiteSpace(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
                File.WriteAllText(Path.Combine(cacheDirectory, FullCacheMarkerFileName), BuildCacheMarkerSignature(gameFolder), Encoding.UTF8);
            }
        }
        pending.Clear();
        resolvable.Clear();
        cellById.Clear();
        state = null;
        ReleaseRuntimeCache();
        progress?.Report(new PrecacheProgress(total - failed, total, complete ? "High-resolution icon cache ready" : "Icon cache completed with missing entries"));
        return new PrecacheResult(total, existing, created, skippedNoNativeCell, complete);
    }
    private static string? GetCacheDirectory(IEnumerable<EldenRingItem> items, Func<EldenRingItem, string> outputPathFactory)
    {
        foreach (EldenRingItem item in items)
        {
            if (!HasDeterministicIconMapping(item)) continue;
            string path = outputPathFactory(item);
            return Path.GetDirectoryName(path);
        }
        return null;
    }
    private static string BuildCacheMarkerSignature(string gameFolder)
    {
        string bhd = Path.Combine(gameFolder, "Data0.bhd");
        string bdt = Path.Combine(gameFolder, "Data0.bdt");
        static string FileStamp(string path)
        {
            if (!File.Exists(path)) return "missing";
            FileInfo info = new(path);
            return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
        }
        return $"native-v1|{FileStamp(bhd)}|{FileStamp(bdt)}";
    }
    public static void ReleaseRuntimeCache()
    {
        lock (Sync)
        {
            warmupTask = null;
            warmupFolder = null;
        }
        ReleaseDecodedAtlas();
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true, true);
    }
    private static void ReleaseDecodedAtlas()
    {
        lock (AtlasBitmapSync)
        {
            cachedAtlasName = null;
            cachedAtlasBitmap = null;
        }
    }
    public static async Task<bool> TryCreateHighResolutionIconAsync(string gameFolder, EldenRingItem item, string outputPath)
    {
        try
        {
            if (!IsValidGameFolder(gameFolder)) return false;
            if (File.Exists(outputPath)) return true;
            if (!EldenRingIconMap.TryGetIconId(item, out int iconId) || iconId <= 0)
            {
                Log($"No deterministic iconId mapping for: {item.Name} [{item.Type}] model={item.ModelId}");
                return false;
            }
            AtlasState? state = await EnsureReadyAsync(gameFolder).ConfigureAwait(false);
            if (state == null || state.Cells.Count == 0)
            {
                Log("High-res icon request skipped because native atlas index failed.");
                return false;
            }
            IconCell? cell = null;
            foreach (IconCell candidate in state.Cells)
            {
                if (candidate.IconId == iconId)
                {
                    cell = candidate;
                    break;
                }
            }
            if (cell == null)
            {
                Log($"Native atlas cell MENU_ItemIcon_{iconId} not found for {item.Name}.");
                return false;
            }
            bool written = SaveCellAsPng(state, cell, outputPath);
            if (written) Log($"Native icon resolved deterministically: {item.Name} -> iconId {iconId} ({cell.Rect.Width}x{cell.Rect.Height})");
            return written && File.Exists(outputPath);
        }
        catch (Exception ex)
        {
            Log("High-res icon creation failed: " + ex);
            return false;
        }
    }
    private static Task<AtlasState?> EnsureReadyAsync(string gameFolder)
    {
        lock (Sync)
        {
            string normalized = Path.GetFullPath(gameFolder).TrimEnd(Path.DirectorySeparatorChar);
            if (warmupTask == null || !string.Equals(warmupFolder, normalized, StringComparison.OrdinalIgnoreCase))
            {
                warmupFolder = normalized;
                warmupTask = Task.Run(() => BuildAtlasState(normalized));
            }
            return warmupTask;
        }
    }
    private static AtlasState? BuildAtlasState(string gameFolder)
    {
        try
        {
            Log("Preparing deterministic native item-icon atlas index...");
            string bhdPath = Path.Combine(gameFolder, "Data0.bhd");
            string bdtPath = Path.Combine(gameFolder, "Data0.bdt");
            if (!File.Exists(bhdPath) || !File.Exists(bdtPath))
            {
                Log("Data0.bhd/Data0.bdt not found in configured game folder.");
                return null;
            }
            Dictionary<ulong, BhdEntry> entries = ParseData0Bhd(bhdPath);
            if (entries.Count == 0) return null;
            byte[]? tpfDcx = ReadArchiveEntry(entries, bdtPath, "/menu/hi/01_common.tpf.dcx");
            if (tpfDcx == null) return null;
            byte[]? tpf = DecompressDcx(gameFolder, tpfDcx);
            if (tpf == null) return null;
            Dictionary<string, byte[]> atlasDds = ExtractTpfTextures(tpf);
            if (atlasDds.Count == 0) return null;
            byte[]? layoutDcx = ReadArchiveEntry(entries, bdtPath, "/menu/hi/01_common.sblytbnd.dcx");
            if (layoutDcx == null) return null;
            byte[]? layout = DecompressDcx(gameFolder, layoutDcx);
            if (layout == null) return null;
            List<LayoutCell> layoutCells = ParseLayoutCells(layout);
            if (layoutCells.Count == 0) return null;
            List<IconCell> cells = new(layoutCells.Count);
            HashSet<int> seen = new();
            foreach (LayoutCell source in layoutCells)
            {
                if (source.IconId <= 0 || source.Rect.Width <= 0 || source.Rect.Height <= 0 || !atlasDds.ContainsKey(source.AtlasName) || !seen.Add(source.IconId))
                {
                    continue;
                }
                cells.Add(new IconCell(source.IconId, source.AtlasName, source.Rect, Array.Empty<byte>()));
            }
            Log($"Deterministic icon index ready: {atlasDds.Count} atlas DDS file(s), {cells.Count} MENU_ItemIcon cells.");
            return cells.Count == 0 ? null : new AtlasState(cells, atlasDds);
        }
        catch (Exception ex)
        {
            Log("Deterministic icon index failed: " + ex);
            return null;
        }
    }
    private static List<IconCell>? TryLoadSignatureIndex(FileInfo bhdInfo)
    {
        try
        {
            if (!File.Exists(SignatureIndexPath)) return null;
            using FileStream fs = new(SignatureIndexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader br = new(fs, Encoding.UTF8, false);
            if (br.ReadInt32() != 0x3349534B) return null;
            if (br.ReadInt64() != bhdInfo.Length || br.ReadInt64() != bhdInfo.LastWriteTimeUtc.Ticks) return null;
            int count = br.ReadInt32();
            if (count <= 0 || count > 100000) return null;
            List<IconCell> cells = new(count);
            for (int i = 0; i < count; i++)
            {
                string atlas = br.ReadString();
                Int32Rect rect = new(br.ReadInt32(), br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
                int sigLen = br.ReadInt32();
                if (sigLen <= 0 || sigLen > 1024 * 64) return null;
                byte[] sig = br.ReadBytes(sigLen);
                if (sig.Length != sigLen) return null;
                cells.Add(new IconCell(0, atlas, rect, sig));
            }
            return cells;
        }
        catch
        {
            return null;
        }
    }
    private static void TrySaveSignatureIndex(FileInfo bhdInfo, List<IconCell> cells)
    {
        try
        {
            string? dir = Path.GetDirectoryName(SignatureIndexPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            string temp = SignatureIndexPath + ".tmp";
            using (FileStream fs = new(temp, FileMode.Create, FileAccess.Write, FileShare.None)) using (BinaryWriter bw = new(fs, Encoding.UTF8, false))
            {
                bw.Write(0x3349534B);
                bw.Write(bhdInfo.Length);
                bw.Write(bhdInfo.LastWriteTimeUtc.Ticks);
                bw.Write(cells.Count);
                foreach (IconCell cell in cells)
                {
                    bw.Write(cell.AtlasName);
                    bw.Write(cell.Rect.X);
                    bw.Write(cell.Rect.Y);
                    bw.Write(cell.Rect.Width);
                    bw.Write(cell.Rect.Height);
                    bw.Write(cell.Signature.Length);
                    bw.Write(cell.Signature);
                }
            }
            File.Move(temp, SignatureIndexPath, true);
        }
        catch
        {
        }
    }
    private static bool IsValidGameFolder(string? gameFolder)
    {
        return !string.IsNullOrWhiteSpace(gameFolder) && Directory.Exists(gameFolder) && File.Exists(Path.Combine(gameFolder, "eldenring.exe"));
    }
    private static Dictionary<ulong, BhdEntry> ParseData0Bhd(string bhdPath)
    {
        byte[] encrypted = File.ReadAllBytes(bhdPath);
        byte[] decrypted = RsaDecryptBhd(encrypted);
        if (decrypted.Length < 32 || Encoding.ASCII.GetString(decrypted, 0, 4) != "BHD5") return new Dictionary<ulong, BhdEntry>();
        using MemoryStream ms = new(decrypted, false);
        using BinaryReader br = new(ms);
        br.BaseStream.Position = 4;
        sbyte endian = br.ReadSByte();
        if (endian != -1) return new Dictionary<ulong, BhdEntry>();
        br.ReadByte();
        br.ReadBytes(2);
        if (br.ReadInt32() != 1) return new Dictionary<ulong, BhdEntry>();
        br.ReadInt32();
        int bucketCount = br.ReadInt32();
        int bucketsOffset = br.ReadInt32();
        if (bucketCount <= 0 || bucketCount > 2_000_000 || bucketsOffset < 0 || bucketsOffset >= decrypted.Length) return new Dictionary<ulong, BhdEntry>();
        Dictionary<ulong, BhdEntry> result = new();
        for (int bucket = 0; bucket < bucketCount; bucket++)
        {
            long bucketPos = (long)bucketsOffset + bucket * 8L;
            if (bucketPos < 0 || bucketPos + 8 > decrypted.Length) break;
            br.BaseStream.Position = bucketPos;
            int count = br.ReadInt32();
            int offset = br.ReadInt32();
            if (count < 0 || count > 5_000_000 || offset < 0) continue;
            for (int i = 0; i < count; i++)
            {
                long pos = (long)offset + i * 40L;
                if (pos < 0 || pos + 40 > decrypted.Length) break;
                br.BaseStream.Position = pos;
                ulong hash = br.ReadUInt64();
                int padded = br.ReadInt32();
                int unpadded = br.ReadInt32();
                long fileOffset = br.ReadInt64();
                br.ReadInt64();
                long aesOffset = br.ReadInt64();
                byte[]? aesKey = null;
                List<AesRange>? ranges = null;
                if (aesOffset > 0 && aesOffset + 20 <= decrypted.Length)
                {
                    br.BaseStream.Position = aesOffset;
                    aesKey = br.ReadBytes(16);
                    int rangeCount = br.ReadInt32();
                    if (rangeCount >= 0 && rangeCount <= 64)
                    {
                        ranges = new List<AesRange>(rangeCount);
                        for (int r = 0; r < rangeCount; r++)
                        {
                            if (br.BaseStream.Position + 16 > decrypted.Length) break;
                            ranges.Add(new AesRange(br.ReadInt64(), br.ReadInt64()));
                        }
                    }
                }
                result[hash] = new BhdEntry(fileOffset, padded, unpadded, aesKey, ranges);
            }
        }
        return result;
    }
    private static byte[] RsaDecryptBhd(byte[] encrypted)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(Data0PublicKeyPem);
        RSAParameters p = rsa.ExportParameters(false);
        if (p.Modulus == null || p.Exponent == null) return Array.Empty<byte>();
        BigInteger modulus = new(p.Modulus, isUnsigned: true, isBigEndian: true);
        BigInteger exponent = new(p.Exponent, isUnsigned: true, isBigEndian: true);
        using MemoryStream output = new((encrypted.Length / 256 + 1) * 255);
        for (int offset = 0; offset + 256 <= encrypted.Length; offset += 256)
        {
            BigInteger block = new(encrypted.AsSpan(offset, 256), isUnsigned: true, isBigEndian: true);
            BigInteger value = BigInteger.ModPow(block, exponent, modulus);
            byte[] raw = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (raw.Length > 255) output.Write(raw, raw.Length - 255, 255);
            else
            {
                int padding = 255 - raw.Length;
                for (int i = 0; i < padding; i++) output.WriteByte(0);
                output.Write(raw, 0, raw.Length);
            }
        }
        return output.ToArray();
    }
    private static byte[]? ReadArchiveEntry(IReadOnlyDictionary<ulong, BhdEntry> entries, string bdtPath, string archivePath)
    {
        ulong hash = ErPathHash(archivePath);
        if (!entries.TryGetValue(hash, out BhdEntry? entry)) return null;
        if (entry.PaddedSize <= 0 || entry.PaddedSize > 512 * 1024 * 1024) return null;
        byte[] bytes = new byte[entry.PaddedSize];
        using FileStream fs = new(bdtPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 1024, FileOptions.RandomAccess);
        fs.Position = entry.FileOffset;
        fs.ReadExactly(bytes);
        if (entry.AesKey != null && entry.AesRanges != null && entry.AesRanges.Count > 0) DecryptAesRanges(bytes, entry.AesKey, entry.AesRanges);
        int size = entry.UnpaddedSize > 0 && entry.UnpaddedSize <= bytes.Length ? entry.UnpaddedSize : bytes.Length;
        if (size == bytes.Length) return bytes;
        byte[] trimmed = new byte[size];
        Buffer.BlockCopy(bytes, 0, trimmed, 0, size);
        return trimmed;
    }
    private static void DecryptAesRanges(byte[] bytes, byte[] key, IReadOnlyList<AesRange> ranges)
    {
        using Aes aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        foreach (AesRange range in ranges)
        {
            if (range.Start < 0 || range.End < 0 || range.End <= range.Start || range.Start >= bytes.Length) continue;
            long clampedEnd = Math.Min(range.End, bytes.LongLength);
            int start = checked((int)range.Start);
            int count = checked((int)(clampedEnd - range.Start));
            count -= count % 16;
            if (count <= 0) continue;
            decryptor.TransformBlock(bytes, start, count, bytes, start);
        }
    }
    private static ulong ErPathHash(string path)
    {
        string normalized = path.Trim().Replace('\\', '/').ToLowerInvariant();
        if (!normalized.StartsWith('/')) normalized = "/" + normalized;
        ulong hash = 0;
        foreach (char c in normalized) hash = unchecked(hash * 0x85UL + c);
        return hash;
    }
    private static byte[]? DecompressDcx(string gameFolder, byte[] data)
    {
        if (data.Length < 4 || data[0] != (byte)'D' || data[1] != (byte)'C' || data[2] != (byte)'X' || data[3] != 0) return data;
        if (data.Length < 0x4C) return null;
        int uncompressedSize = ReadBigEndianInt32(data, 0x1C);
        int compressedSize = ReadBigEndianInt32(data, 0x20);
        string algorithm = Encoding.ASCII.GetString(data, 0x28, 4);
        if (uncompressedSize <= 0 || compressedSize <= 0 || 0x4C + compressedSize > data.Length) return null;
        if (algorithm.Equals("ZSTD", StringComparison.Ordinal))
        {
            try
            {
                ReadOnlySpan<byte> compressed = data.AsSpan(0x4C, compressedSize);
                byte[] output = new byte[uncompressedSize];
                using Decompressor decompressor = new();
                bool ok = decompressor.TryUnwrap(compressed, output, out int written);
                return ok && written == uncompressedSize ? output : null;
            }
            catch (Exception ex)
            {
                Log("ZSTD DCX decompression failed: " + ex.Message);
                return null;
            }
        }
        if (!algorithm.Equals("KRAK", StringComparison.Ordinal))
        {
            Log("Unsupported DCX compression: " + algorithm);
            return null;
        }
        string oodlePath = Path.Combine(gameFolder, "oo2core_6_win64.dll");
        if (!File.Exists(oodlePath))
        {
            Log("oo2core_6_win64.dll not found; cannot decode KRAK DCX.");
            return null;
        }
        IntPtr library = NativeLibrary.Load(oodlePath);
        try
        {
            IntPtr proc = NativeLibrary.GetExport(library, "OodleLZ_Decompress");
            OodleLzDecompress decompress = Marshal.GetDelegateForFunctionPointer<OodleLzDecompress>(proc);
            byte[] output = new byte[uncompressedSize];
            GCHandle inputHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            GCHandle outputHandle = GCHandle.Alloc(output, GCHandleType.Pinned);
            try
            {
                IntPtr input = IntPtr.Add(inputHandle.AddrOfPinnedObject(), 0x4C);
                long written = decompress(input, compressedSize, outputHandle.AddrOfPinnedObject(), uncompressedSize, 1, 0, 0, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, 3);
                return written == uncompressedSize ? output : null;
            }
            finally
            {
                if (inputHandle.IsAllocated) inputHandle.Free();
                if (outputHandle.IsAllocated) outputHandle.Free();
            }
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }
    private static int ReadBigEndianInt32(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4));
    }
    private static Dictionary<string, byte[]> ExtractTpfTextures(byte[] tpf)
    {
        Dictionary<string, byte[]> result = new(StringComparer.OrdinalIgnoreCase);
        if (tpf.Length < 0x10 || tpf[0] != (byte)'T' || tpf[1] != (byte)'P' || tpf[2] != (byte)'F' || tpf[3] != 0) return result;
        int count = BinaryPrimitives.ReadInt32LittleEndian(tpf.AsSpan(8, 4));
        byte platform = tpf[0x0C];
        byte encoding = tpf[0x0E];
        if (platform != 0 || count < 0 || count > 2048 || 0x10L + count * 0x14L > tpf.Length) return result;
        for (int i = 0; i < count; i++)
        {
            int entry = 0x10 + i * 0x14;
            int fileOffset = BinaryPrimitives.ReadInt32LittleEndian(tpf.AsSpan(entry, 4));
            int fileSize = BinaryPrimitives.ReadInt32LittleEndian(tpf.AsSpan(entry + 4, 4));
            int nameOffset = BinaryPrimitives.ReadInt32LittleEndian(tpf.AsSpan(entry + 12, 4));
            if (fileOffset < 0 || fileSize <= 0 || nameOffset < 0 || fileOffset + (long)fileSize > tpf.Length) continue;
            string name = StripExtension(ReadTpfName(tpf, nameOffset, encoding));
            if (string.IsNullOrWhiteSpace(name)) continue;
            byte[] dds = new byte[fileSize];
            Buffer.BlockCopy(tpf, fileOffset, dds, 0, fileSize);
            result[name] = dds;
        }
        return result;
    }
    private static string ReadTpfName(byte[] data, int offset, byte encoding)
    {
        if (offset < 0 || offset >= data.Length) return string.Empty;
        StringBuilder sb = new();
        if (encoding == 1)
        {
            for (int i = offset; i + 1 < data.Length; i += 2)
            {
                ushort c = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i, 2));
                if (c == 0) break;
                sb.Append((char)c);
            }
        }
        else
        {
            for (int i = offset; i < data.Length && data[i] != 0; i++) sb.Append((char)data[i]);
        }
        return sb.ToString();
    }
    private static List<LayoutCell> ParseLayoutCells(byte[] bnd)
    {
        List<LayoutCell> cells = new();
        string text = Encoding.UTF8.GetString(bnd);
        const string atlasMarker = "<TextureAtlas imagePath=\"";
        const string iconMarker = "MENU_ItemIcon_";
        int atlasPos = 0;
        while ((atlasPos = text.IndexOf(atlasMarker, atlasPos, StringComparison.Ordinal)) >= 0)
        {
            int imageStart = atlasPos + atlasMarker.Length;
            int imageEnd = text.IndexOf('"', imageStart);
            int atlasEnd = text.IndexOf("</TextureAtlas>", atlasPos, StringComparison.Ordinal);
            if (imageEnd < 0 || atlasEnd < 0) break;
            string atlasName = StripExtension(text.Substring(imageStart, imageEnd - imageStart));
            int pos = atlasPos;
            while ((pos = text.IndexOf("<SubTexture", pos, StringComparison.Ordinal)) >= 0 && pos < atlasEnd)
            {
                int tagEnd = text.IndexOf('>', pos);
                if (tagEnd < 0 || tagEnd > atlasEnd) break;
                string tag = text.Substring(pos, tagEnd - pos + 1);
                string subName = ReadXmlString(tag, "name");
                int marker = subName.IndexOf(iconMarker, StringComparison.Ordinal);
                if (marker >= 0)
                {
                    string idText = subName[(marker + iconMarker.Length)..];
                    int suffix = idText.IndexOfAny(new[] {
                        '.', '_', '-'
                    });
                    if (suffix > 0) idText = idText[..suffix];
                    if (int.TryParse(idText, out int iconId) && iconId > 0)
                    {
                        int x = ReadXmlInt(tag, "x");
                        int y = ReadXmlInt(tag, "y");
                        int w = ReadXmlInt(tag, "width");
                        int h = ReadXmlInt(tag, "height");
                        if (w > 0 && h > 0) cells.Add(new LayoutCell(iconId, atlasName, new Int32Rect(x, y, w, h)));
                    }
                }
                pos = tagEnd + 1;
            }
            atlasPos = atlasEnd + 15;
        }
        return cells;
    }
    private static string ReadXmlString(string tag, string attribute)
    {
        string needle = attribute + "=\"";
        int start = tag.IndexOf(needle, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        start += needle.Length;
        int end = tag.IndexOf('"', start);
        if (end < 0) return string.Empty;
        return tag.Substring(start, end - start);
    }
    private static int ReadXmlInt(string tag, string attribute)
    {
        string needle = attribute + "=\"";
        int start = tag.IndexOf(needle, StringComparison.Ordinal);
        if (start < 0) return 0;
        start += needle.Length;
        int end = tag.IndexOf('"', start);
        if (end < 0) return 0;
        return int.TryParse(tag.AsSpan(start, end - start), out int value) ? value : 0;
    }
    private static BitmapSource? TryDecodeDds(byte[] dds)
    {
        try
        {
            using MemoryStream ms = new(dds, false);
            BitmapDecoder decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch
        {
        }
        try
        {
            using MemoryStream ms = new(dds, false);
            BcDecoder decoder = new();
            using SixLabors.ImageSharp.Image<Rgba32> image = decoder.DecodeToImageRgba32(ms);
            byte[] rgba = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(rgba);
            for (int i = 0; i < rgba.Length; i += 4) (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]);
            BitmapSource bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, rgba, image.Width * 4);
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
    private static BitmapSource LoadBitmap(byte[] bytes)
    {
        using MemoryStream stream = new(bytes, writable: false);
        BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource bitmap = decoder.Frames[0];
        bitmap.Freeze();
        return bitmap;
    }
    private static byte[]? CreateSignature(BitmapSource bitmap, Int32Rect? rect)
    {
        try
        {
            BitmapSource source = rect.HasValue ? new CroppedBitmap(bitmap, rect.Value) : bitmap;
            FormatConvertedBitmap bgra = new(source, PixelFormats.Bgra32, null, 0);
            int width = bgra.PixelWidth;
            int height = bgra.PixelHeight;
            if (width <= 0 || height <= 0) return null;
            byte[] raw = new byte[width * height * 4];
            bgra.CopyPixels(raw, width * 4, 0);
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                int row = y * width * 4;
                for (int x = 0; x < width; x++)
                {
                    int a = raw[row + x * 4 + 3];
                    if (a < 16) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < minX || maxY < minY) return null;
            int fgW = maxX - minX + 1;
            int fgH = maxY - minY + 1;
            int pad = Math.Max(2, (int)Math.Ceiling(Math.Max(fgW, fgH) * 0.06));
            minX = Math.Max(0, minX - pad);
            minY = Math.Max(0, minY - pad);
            maxX = Math.Min(width - 1, maxX + pad);
            maxY = Math.Min(height - 1, maxY + pad);
            CroppedBitmap foreground = new(bgra, new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
            const int size = 48;
            double scale = Math.Min(size / (double)foreground.PixelWidth, size / (double)foreground.PixelHeight);
            int scaledW = Math.Max(1, (int)Math.Round(foreground.PixelWidth * scale));
            int scaledH = Math.Max(1, (int)Math.Round(foreground.PixelHeight * scale));
            DrawingVisual visual = new();
            using (DrawingContext dc = visual.RenderOpen())
            {
                double x = (size - scaledW) / 2.0;
                double y = (size - scaledH) / 2.0;
                dc.DrawImage(foreground, new Rect(x, y, scaledW, scaledH));
            }
            RenderTargetBitmap rendered = new(size, size, 96, 96, PixelFormats.Pbgra32);
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
            rendered.Render(visual);
            rendered.Freeze();
            FormatConvertedBitmap converted = new(rendered, PixelFormats.Bgra32, null, 0);
            byte[] pixels = new byte[size * size * 4];
            converted.CopyPixels(pixels, size * 4, 0);
            return pixels;
        }
        catch
        {
            return null;
        }
    }
    private static long SignatureDistance(byte[] a, byte[] b, long currentBest)
    {
        long score = 0;
        long weightSum = 0;
        int activePixels = 0;
        for (int i = 0; i < a.Length; i += 4)
        {
            int aa = a[i + 3];
            int ba = b[i + 3];
            int maxA = Math.Max(aa, ba);
            if (maxA < 12) continue;
            int alphaDiff = aa - ba;
            int db = a[i] - b[i];
            int dg = a[i + 1] - b[i + 1];
            int dr = a[i + 2] - b[i + 2];
            long weight = 32L + maxA;
            long pixel = (long)db * db + (long)dg * dg + (long)dr * dr + 5L * alphaDiff * alphaDiff;
            score += pixel * weight;
            weightSum += weight;
            activePixels++;
        }
        if (activePixels < 8 || weightSum == 0) return long.MaxValue;
        return score / weightSum;
    }
    private static bool SaveCellAsPng(AtlasState state, IconCell cell, string outputPath)
    {
        try
        {
            if (!state.AtlasDds.TryGetValue(cell.AtlasName, out byte[]? dds)) return false;
            BitmapSource? atlas;
            lock (AtlasBitmapSync)
            {
                if (cachedAtlasBitmap != null && string.Equals(cachedAtlasName, cell.AtlasName, StringComparison.OrdinalIgnoreCase))
                {
                    atlas = cachedAtlasBitmap;
                }
                else
                {
                    atlas = TryDecodeDds(dds);
                    if (atlas != null)
                    {
                        cachedAtlasName = cell.AtlasName;
                        cachedAtlasBitmap = atlas;
                    }
                }
            }
            if (atlas == null) return false;
            Int32Rect rect = ClampRect(cell.Rect, atlas.PixelWidth, atlas.PixelHeight);
            if (rect.Width <= 0 || rect.Height <= 0) return false;
            CroppedBitmap crop = new(atlas, rect);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(crop));
            using FileStream fs = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            encoder.Save(fs);
            return true;
        }
        catch
        {
            return false;
        }
    }
    private static Int32Rect ClampRect(Int32Rect rect, int width, int height)
    {
        int x = Math.Clamp(rect.X, 0, width);
        int y = Math.Clamp(rect.Y, 0, height);
        int right = Math.Clamp(rect.X + rect.Width, 0, width);
        int bottom = Math.Clamp(rect.Y + rect.Height, 0, height);
        return new Int32Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }
    private static string StripExtension(string path)
    {
        string name = path.Replace('\\', '/');
        int slash = name.LastIndexOf('/');
        if (slash >= 0) name = name[(slash + 1)..];
        int dot = name.LastIndexOf('.');
        if (dot > 0) name = name[..dot];
        return name;
    }
    private static void Log(string message)
    {
        try
        {
            lock (LogSync)
            {
                string? dir = Path.GetDirectoryName(DebugLogPath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(DebugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch
        {
        }
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate long OodleLzDecompress(IntPtr compBuf, long compBufSize, IntPtr rawBuf, long rawLen, int fuzzSafe, int checkCrc, int verbosity, IntPtr decBufBase, long decBufSize, IntPtr callback, IntPtr callbackUserData, IntPtr decoderMemory, long decoderMemorySize, int threadPhase);
    private sealed record AtlasState(List<IconCell> Cells, Dictionary<string, byte[]> AtlasDds);
    private sealed record IconCell(int IconId, string AtlasName, Int32Rect Rect, byte[] Signature);
    private sealed record LayoutCell(int IconId, string AtlasName, Int32Rect Rect);
    public readonly record struct PrecacheProgress(int Completed, int Total, string Message);
    public readonly record struct PrecacheResult(int TotalMapped, int AlreadyCached, int Created, int SkippedWithoutNativeCell, bool Complete);
    private sealed record BhdEntry(long FileOffset, int PaddedSize, int UnpaddedSize, byte[]? AesKey, List<AesRange>? AesRanges);
    private readonly record struct AesRange(long Start, long End);
}
