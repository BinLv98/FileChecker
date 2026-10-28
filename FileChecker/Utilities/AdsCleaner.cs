using FileChecker.Data;
using FileChecker.Definitions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileChecker.Utilities
{
    internal static class AdsCleaner
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Win32FindStreamData
        {
            public long StreamSize;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
            public string StreamName;
        }

        public const string ZONE_STREAM_NAME = "Zone.Identifier";

        private static readonly IntPtr _invalidHandle = new(-1);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFileW(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        public static AdsRemoveResult RemoveZoneIdentifier(string path, out string error)
        {
            error = null;
            if (DeleteFileW(path + ":Zone.Identifier"))
            {
                return AdsRemoveResult.Removed;
            }
            var lastWin32Error = Marshal.GetLastWin32Error();
            if (lastWin32Error is 2 or 3)
            {
                return AdsRemoveResult.NoStream;
            }
            error = new Win32Exception(lastWin32Error).Message;
            return AdsRemoveResult.Failed;
        }

        public static AdsRemoveResult RemoveAllStreams(string path, out int removedCount, out string error)
        {
            removedCount = 0;
            error = null;
            List<string> list;
            try
            {
                list = CollectNamedStreamNames(path);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return AdsRemoveResult.Failed;
            }
            if (list == null)
            {
                error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return AdsRemoveResult.Failed;
            }
            if (list.Count == 0)
            {
                return AdsRemoveResult.NoStream;
            }
            foreach (var item in list)
            {
                if (DeleteFileW(path + ":" + item))
                {
                    removedCount++;
                    continue;
                }
                var lastWin32Error = Marshal.GetLastWin32Error();
                if (lastWin32Error is 2 or 3)
                {
                    removedCount++;
                    continue;
                }
                error ??= new Win32Exception(lastWin32Error).Message;
                return AdsRemoveResult.Failed;
            }
            return AdsRemoveResult.Removed;
        }

        private static List<string> CollectNamedStreamNames(string path)
        {
            var list = new List<string>();
            var intPtr = FindFirstStreamW(path, 0, out var findStreamData, 0u);
            if (intPtr == _invalidHandle)
            {
                return null;
            }
            try
            {
                do
                {
                    var text = NormalizeStreamName(findStreamData.StreamName, out var isMain);
                    if (!isMain && text.Length > 0)
                    {
                        list.Add(text);
                    }
                }
                while (FindNextStreamW(intPtr, out findStreamData));
                return list;
            }
            finally
            {
                FindClose(intPtr);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr FindFirstStreamW(string lpFileName, int infoLevel, out Win32FindStreamData findStreamData, uint dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern bool FindNextStreamW(IntPtr hFindStream, out Win32FindStreamData findStreamData);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool FindClose(IntPtr hFindFile);

        public static AdsScanResult Scan(string path)
        {
            var adsScanResult = new AdsScanResult
            {
                _path = path
            };
            try
            {
                var intPtr = FindFirstStreamW(path, 0, out var findStreamData, 0u);
                if (intPtr == _invalidHandle)
                {
                    adsScanResult._error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    return adsScanResult;
                }
                try
                {
                    AddStream(adsScanResult, findStreamData);
                    while (FindNextStreamW(intPtr, out findStreamData))
                    {
                        AddStream(adsScanResult, findStreamData);
                    }
                }
                finally
                {
                    FindClose(intPtr);
                }
                adsScanResult._ok = true;
                foreach (var stream in adsScanResult._streams)
                {
                    if (!stream.IsMainData && string.Equals(stream.Name, "Zone.Identifier", StringComparison.OrdinalIgnoreCase))
                    {
                        adsScanResult._hasZone = true;
                        adsScanResult._zoneLength = stream.Length;
                        adsScanResult._zoneContent = ReadZoneContentSafe(path);
                        adsScanResult._zoneId = ParseZoneId(adsScanResult._zoneContent);
                        adsScanResult._zoneHasExtraData = HasZoneExtraData(adsScanResult._zoneContent);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                adsScanResult._ok = false;
                adsScanResult._error = ex.Message;
            }
            return adsScanResult;
        }

        private static void AddStream(AdsScanResult r, Win32FindStreamData d)
        {
            var name = NormalizeStreamName(d.StreamName, out var isMain);
            r._streams.Add(new AdsStreamInfo
            {
                Name = name,
                Length = d.StreamSize,
                IsMainData = isMain
            });
        }

        private static string NormalizeStreamName(string raw, out bool isMain)
        {
            isMain = false;
            if (string.IsNullOrEmpty(raw))
            {
                isMain = true;
                return string.Empty;
            }
            var text = raw.TrimStart(':');
            if (string.Equals(text, "$DATA", StringComparison.OrdinalIgnoreCase))
            {
                isMain = true;
                return string.Empty;
            }
            if (text.EndsWith(":$DATA", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(0, text.Length - 6);
            }
            return text.TrimEnd(':');
        }

        public static string ReadZoneContentSafe(string path)
        {
            return ReadNamedStreamContentSafe(path, "Zone.Identifier");
        }

        public static string ReadNamedStreamContentSafe(string path, string streamName)
        {
            var intPtr = CreateFileW(path + ":" + streamName, 2147483648u, 7u, IntPtr.Zero, 3u, 128u, IntPtr.Zero);
            if (intPtr == new IntPtr(-1))
            {
                return null;
            }
            try
            {
                var array = new byte[65536];
                if (!ReadFile(intPtr, array, (uint)array.Length, out var lpNumberOfBytesRead, IntPtr.Zero))
                {
                    return null;
                }
                if (lpNumberOfBytesRead == 0)
                {
                    return string.Empty;
                }
                return DecodeText(array, (int)lpNumberOfBytesRead);
            }
            finally
            {
                CloseHandle(intPtr);
            }
        }

        private static string DecodeText(byte[] buf, int count)
        {
            if (count >= 2 && buf[0] == byte.MaxValue && buf[1] == 254)
            {
                return Encoding.Unicode.GetString(buf, 2, count - 2);
            }
            if (count >= 2 && buf[0] == 254 && buf[1] == byte.MaxValue)
            {
                return Encoding.BigEndianUnicode.GetString(buf, 2, count - 2);
            }
            if (count >= 3 && buf[0] == 239 && buf[1] == 187 && buf[2] == 191)
            {
                return Encoding.UTF8.GetString(buf, 3, count - 3);
            }
            if (LooksLikeUtf16Le(buf, count))
            {
                return Encoding.Unicode.GetString(buf, 0, count);
            }
            return Encoding.UTF8.GetString(buf, 0, count);
        }

        private static bool LooksLikeUtf16Le(byte[] buf, int count)
        {
            var num = Math.Min(count, 512);
            if (num < 8)
            {
                return false;
            }
            var num2 = 0;
            var num3 = 0;
            for (var i = 0; i < num; i++)
            {
                if (buf[i] == 0)
                {
                    if ((i & 1) == 1)
                    {
                        num2++;
                    }
                    else
                    {
                        num3++;
                    }
                }
            }
            if (num2 >= 8)
            {
                return num2 > num3 * 2;
            }
            return false;
        }

        private static int ParseZoneId(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return -1;
            }
            var num = content.IndexOf("ZoneId=", StringComparison.OrdinalIgnoreCase);
            if (num < 0)
            {
                return -1;
            }
            var num2 = num + "ZoneId=".Length;
            int i;
            for (i = num2; i < content.Length && (char.IsDigit(content[i]) || content[i] == '-'); i++)
            {
            }
            if (i <= num2)
            {
                return -1;
            }
            if (!int.TryParse(content.Substring(num2, i - num2), out var result))
            {
                return -1;
            }
            return result;
        }

        private static bool HasZoneExtraData(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }
            var array = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var t in array)
            {
                var text = t.Trim();
                if (text.Length != 0 && text[0] != '[' && !text.StartsWith("ZoneId=", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static List<string> ExpandPath(string root, bool recursive, ref int inaccessible)
        {
            var list = new List<string>();
            try
            {
                if (Directory.Exists(root))
                {
                    Walk(root, recursive, list, ref inaccessible);
                }
                else if (File.Exists(root))
                {
                    list.Add(root);
                }
            }
            catch (Exception ex)
            {
                if (!IsIoDenied(ex))
                {
                    throw;
                }
                inaccessible++;
            }
            return list;
        }

        private static void Walk(string dir, bool recursive, List<string> files, ref int inaccessible)
        {
            try
            {
                foreach (var item in Directory.EnumerateFiles(dir))
                {
                    files.Add(item);
                }
            }
            catch (Exception ex)
            {
                if (IsIoDenied(ex))
                {
                    inaccessible++;
                    return;
                }
                throw;
            }
            if (!recursive)
            {
                return;
            }
            IEnumerable<string> enumerable;
            try
            {
                enumerable = Directory.EnumerateDirectories(dir);
            }
            catch (Exception ex2)
            {
                if (IsIoDenied(ex2))
                {
                    inaccessible++;
                    return;
                }
                throw;
            }
            foreach (var item2 in enumerable)
            {
                try
                {
                    if ((File.GetAttributes(item2) & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                }
                catch (Exception ex3)
                {
                    if (IsIoDenied(ex3))
                    {
                        inaccessible++;
                        continue;
                    }
                    throw;
                }
                Walk(item2, recursive: true, files, ref inaccessible);
            }
        }

        private static bool IsIoDenied(Exception ex)
        {
            if (!(ex is UnauthorizedAccessException) && !(ex is IOException) && !(ex is SecurityException))
            {
                return ex is NotSupportedException;
            }
            return true;
        }

        public static CleanStats ProcessFiles(IList<string> files, Action<FileReport> onCompleted, CancellationToken ct)
        {
            var removed = 0;
            var clean = 0;
            var failed = 0;
            var gate = new object();
            var maxDegreeOfParallelism = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };
            Parallel.ForEach(files, parallelOptions, file =>
            {
                AdsRemoveResult adsRemoveResult;
                string error;
                if (!File.Exists(file))
                {
                    adsRemoveResult = AdsRemoveResult.Failed;
                    error = Localization.T("file.missing");
                }
                else
                {
                    adsRemoveResult = RemoveZoneIdentifier(file, out error);
                }
                lock (gate)
                {
                    switch (adsRemoveResult)
                    {
                        case AdsRemoveResult.Removed:
                            removed++;
                            break;
                        case AdsRemoveResult.NoStream:
                            clean++;
                            break;
                        default:
                            failed++;
                            break;
                    }
                }
                var obj = new FileReport
                {
                    _path = file,
                    _result = adsRemoveResult,
                    _error = error
                };
                if (onCompleted != null)
                {
                    onCompleted(obj);
                }
            });
            return new CleanStats
            {
                Total = files.Count,
                Removed = removed,
                Clean = clean,
                Failed = failed
            };
        }
    }
}
