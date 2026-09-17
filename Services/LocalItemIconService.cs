using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace KsEldenRingToolkitManager.Services
{
    internal static class LocalItemIconService
    {
        private const string PackMagic = "KSIPACK1";
        private static readonly object Sync = new object();
        private static Dictionary<string, PackEntry>? _entries;
        private static string? _packPath;
        private static FileStream? _packStream;
        private readonly struct PackEntry
        {
            public PackEntry(long offset, int length)
            {
                Offset = offset;
                Length = length;
            }
            public long Offset
            {
                get;
            }
            public int Length
            {
                get;
            }
        }
        public static bool IsAvailable
        {
            get
            {
                EnsureIndexLoaded();
                return _entries != null && _entries.Count > 0;
            }
        }
        public static bool TryGetIconBytes(string itemName, out byte[]? bytes)
        {
            bytes = null;
            if (string.IsNullOrWhiteSpace(itemName)) return false;
            EnsureIndexLoaded();
            if (_entries == null || string.IsNullOrWhiteSpace(_packPath)) return false;
            foreach (string key in GetLookupKeys(itemName))
            {
                if (!_entries.TryGetValue(key, out PackEntry entry)) continue;
                try
                {
                    byte[] data = new byte[entry.Length];
                    lock (Sync)
                    {
                        if (_packStream == null) return false;
                        _packStream.Position = entry.Offset;
                        int read = 0;
                        while (read < data.Length)
                        {
                            int current = _packStream.Read(data, read, data.Length - read);
                            if (current <= 0) return false;
                            read += current;
                        }
                    }
                    bytes = data;
                    return true;
                }
                catch
                {
                    bytes = null;
                    return false;
                }
            }
            return false;
        }
        public static bool TryMaterializeIcon(string itemName, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(outputPath)) return false;
            if (File.Exists(outputPath)) return true;
            if (!TryGetIconBytes(itemName, out byte[]? bytes) || bytes == null) return false;
            try
            {
                string? folder = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
                string tempPath = outputPath + ".tmp";
                File.WriteAllBytes(tempPath, bytes);
                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(tempPath, outputPath);
                return true;
            }
            catch
            {
                return false;
            }
        }
        private static void EnsureIndexLoaded()
        {
            if (_entries != null) return;
            lock (Sync)
            {
                if (_entries != null) return;
                string candidate = Path.Combine(AppContext.BaseDirectory, "Data", "ItemIcons.kspack");
                if (!File.Exists(candidate))
                {
                    _entries = new Dictionary<string, PackEntry>(StringComparer.Ordinal);
                    _packPath = null;
                    _packStream = null;
                    return;
                }
                try
                {
                    using FileStream stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
                    using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
                    string magic = Encoding.ASCII.GetString(reader.ReadBytes(8));
                    if (!string.Equals(magic, PackMagic, StringComparison.Ordinal)) throw new InvalidDataException("Unsupported item icon pack.");
                    int count = reader.ReadInt32();
                    if (count < 0 || count > 100_000) throw new InvalidDataException("Invalid item icon pack index.");
                    Dictionary<string, PackEntry> loaded = new Dictionary<string, PackEntry>(count, StringComparer.Ordinal);
                    for (int i = 0; i < count; i++)
                    {
                        ushort keyLength = reader.ReadUInt16();
                        string key = Encoding.UTF8.GetString(reader.ReadBytes(keyLength));
                        long offset = reader.ReadInt64();
                        int length = reader.ReadInt32();
                        if (key.Length > 0 && offset >= 0 && length > 0) loaded[key] = new PackEntry(offset, length);
                    }
                    _entries = loaded;
                    _packPath = candidate;
                    _packStream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.RandomAccess);
                }
                catch
                {
                    _entries = new Dictionary<string, PackEntry>(StringComparer.Ordinal);
                    _packPath = null;
                    _packStream = null;
                }
            }
        }
        private static IEnumerable<string> GetLookupKeys(string itemName)
        {
            HashSet<string> yielded = new HashSet<string>(StringComparer.Ordinal);
            string exact = Normalize(itemName);
            if (exact.Length > 0 && yielded.Add(exact)) yield return exact;
            string withoutAltered = System.Text.RegularExpressions.Regex.Replace(itemName, @"\s*\(Altered\)\s*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            string baseKey = Normalize(withoutAltered);
            if (baseKey.Length > 0 && yielded.Add(baseKey)) yield return baseKey;
        }
        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string text = value.Replace("_s", "'s", StringComparison.OrdinalIgnoreCase).Trim();
            int extensionIndex = text.LastIndexOf('.');
            if (extensionIndex > 0 && extensionIndex >= text.Length - 6) text = text.Substring(0, extensionIndex);
            StringBuilder builder = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
            }
            return builder.ToString();
        }
    }
}
