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
    /// <summary>
    /// Windows 文件附加数据流（ADS，Alternate Data Streams）的扫描、读取与清理工具。
    /// <para>核心用途：定位并移除 NTFS 文件上的 Zone.Identifier 流（下载来源标记，
    /// 打开文件时触发"安全警告"的元凶），同时支持扫描全部命名流供详情窗口展示。</para>
    /// <para>全部操作通过 kernel32 的 FindFirstStreamW / FindNextStreamW / DeleteFileW 等
    /// 原生 API 完成，属于 P/Invoke 层，务必保持参数签名与 Win32 约定一致。</para>
    /// </summary>
    internal static class AdsCleaner
    {
        /// <summary>下载来源标记流的固定名称（Zone.Identifier）。</summary>
        public const string ZONE_STREAM_NAME = "Zone.Identifier";

        // —— Win32 常量（CreateFileW 等调用使用）——
        private const uint GENERIC_READ = 0x80000000u;          // 只读访问
        private const uint FILE_SHARE_RW_DELETE = 7u;          // 共享读/写/删除
        private const uint OPEN_EXISTING = 3u;                 // 仅打开已存在的文件
        private const uint FILE_FLAG_SEQUENTIAL_SCAN = 0x80u;  // 顺序扫描提示

        /// <summary>FindFirstStreamW 失败时返回的无效句柄（值为 -1）。</summary>
        private static readonly IntPtr _invalidHandle = new(-1);

        /// <summary>
        /// FindFirstStreamW / FindNextStreamW 返回的流描述结构。
        /// <para>布局必须与 WIN32_FIND_STREAM_DATA 完全一致：先 8 字节的流大小，
        /// 再 296 字符宽度的流名称（MAX_PATH + 36）。</para>
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Win32FindStreamData
        {
            /// <summary>数据流的字节大小。</summary>
            public long StreamSize;

            /// <summary>数据流名称（形如 ":Zone.Identifier:$DATA"，固定宽度 296 字符）。</summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
            public string StreamName;
        }

        /// <summary>
        /// 删除指定文件（P/Invoke 包装）。
        /// <para>当文件名形如 "C:\path\file.txt:Zone.Identifier" 时，DeleteFileW 会删除
        /// 该文件上的指定附加数据流而非整个文件——这正是本工具移除标记的原理。</para>
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFileW(string lpFileName);

        /// <summary>
        /// 打开文件（P/Invoke 包装），用于读取附加数据流的内容。
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        /// <summary>从文件句柄读取字节（P/Invoke 包装）。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        /// <summary>关闭句柄（P/Invoke 包装）。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        /// <summary>开始枚举文件附加数据流（P/Invoke 包装）。</summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr FindFirstStreamW(string lpFileName, int infoLevel, out Win32FindStreamData findStreamData, uint dwFlags);

        /// <summary>继续枚举文件附加数据流（P/Invoke 包装）。</summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern bool FindNextStreamW(IntPtr hFindStream, out Win32FindStreamData findStreamData);

        /// <summary>关闭枚举句柄（P/Invoke 包装）。</summary>
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool FindClose(IntPtr hFindFile);

        /// <summary>
        /// 移除单个文件的 Zone.Identifier 下载来源标记流。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        /// <param name="error">失败时的错误信息（成功时为 null）。</param>
        /// <returns>移除结果状态。</returns>
        public static AdsRemoveResult RemoveZoneIdentifier(string path, out string error)
        {
            error = null;
            // 借助"文件名:流名"语法删除附加数据流；成功说明标记确实存在并被移除
            if (DeleteFileW(path + ":" + ZONE_STREAM_NAME))
            {
                return AdsRemoveResult.Removed;
            }
            var win32Error = Marshal.GetLastWin32Error();
            // ERROR_FILE_NOT_FOUND(2) / ERROR_PATH_NOT_FOUND(3)：文件上根本没有该流
            if (win32Error is 2 or 3)
            {
                return AdsRemoveResult.NoStream;
            }
            // 其他错误：转换为可读信息返回
            error = new Win32Exception(win32Error).Message;
            return AdsRemoveResult.Failed;
        }

        /// <summary>
        /// 移除指定文件上的全部附加数据流（含网络来源标记及其他命名流）。
        /// <para>用于"文件标记详情"窗口中的"清除全部"操作，是 Zone.Identifier 之外
        /// 其他附加数据（如"附件信息"等）的清理入口。</para>
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        /// <param name="removedCount">实际成功移除的流数量。</param>
        /// <param name="error">失败时的错误信息（成功时为 null）。</param>
        /// <returns>移除结果状态。</returns>
        public static AdsRemoveResult RemoveAllStreams(string path, out int removedCount, out string error)
        {
            removedCount = 0;
            error = null;
            List<string> streamNames;
            try
            {
                // 先枚举出全部命名（非主）数据流的名称
                streamNames = CollectNamedStreamNames(path);
            }
            catch (Exception ex)
            {
                // 枚举过程自身抛出异常（如路径不合法）
                error = ex.Message;
                return AdsRemoveResult.Failed;
            }
            // 返回 null 表示 Win32 枚举失败（最后一次错误的系统错误码可查）
            if (streamNames == null)
            {
                error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return AdsRemoveResult.Failed;
            }
            // 没有任何命名流：无需清理
            if (streamNames.Count == 0)
            {
                return AdsRemoveResult.NoStream;
            }
            // 逐一流删除
            foreach (var streamName in streamNames)
            {
                if (DeleteFileW(path + ":" + streamName))
                {
                    removedCount++;
                    continue;
                }
                var win32Error = Marshal.GetLastWin32Error();
                // 流在枚举与删除之间消失（2/3）也视为成功删除
                if (win32Error is 2 or 3)
                {
                    removedCount++;
                    continue;
                }
                // 记录第一个不可恢复的错误并中止
                error ??= new Win32Exception(win32Error).Message;
                return AdsRemoveResult.Failed;
            }
            return AdsRemoveResult.Removed;
        }

        /// <summary>
        /// 枚举指定文件的全部命名（非主）数据流的名称。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        /// <returns>命名流名称列表；Win32 枚举失败时返回 null（此时系统错误码可查）。</returns>
        private static List<string> CollectNamedStreamNames(string path)
        {
            var streamNames = new List<string>();
            var handle = FindFirstStreamW(path, 0, out var streamData, 0u);
            // 句柄无效：枚举根本无法开始
            if (handle == _invalidHandle)
            {
                return null;
            }
            try
            {
                // 首条流已在 FindFirstStreamW 输出中，随后用 FindNextStreamW 迭代
                do
                {
                    var name = NormalizeStreamName(streamData.StreamName, out var isMain);
                    // 只收集命名（非主）流
                    if (!isMain && name.Length > 0)
                    {
                        streamNames.Add(name);
                    }
                }
                while (FindNextStreamW(handle, out streamData));
                return streamNames;
            }
            finally
            {
                // 无论成功与否都必须关闭查找句柄
                FindClose(handle);
            }
        }

        /// <summary>
        /// 扫描指定文件的全部附加数据流，并解析其中的 Zone.Identifier 下载来源标记。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        /// <returns>扫描结果（含流列表与 Zone 解析信息）。</returns>
        public static AdsScanResult Scan(string path)
        {
            var result = new AdsScanResult { Path = path };
            try
            {
                var handle = FindFirstStreamW(path, 0, out var streamData, 0u);
                // 无法枚举（文件不存在、非 NTFS、无权限等）：记录系统错误
                if (handle == _invalidHandle)
                {
                    result.Error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    return result;
                }
                try
                {
                    AddStream(result, streamData);
                    while (FindNextStreamW(handle, out streamData))
                    {
                        AddStream(result, streamData);
                    }
                }
                finally
                {
                    FindClose(handle);
                }
                result.Ok = true;
                // 在所有命名流中查找 Zone.Identifier，命中后读取并解析其内容
                foreach (var stream in result.Streams)
                {
                    if (!stream.IsMainData && string.Equals(stream.Name, ZONE_STREAM_NAME, StringComparison.OrdinalIgnoreCase))
                    {
                        result.HasZone = true;
                        result.ZoneLength = stream.Length;
                        result.ZoneContent = ReadZoneContentSafe(path);
                        result.ZoneId = ParseZoneId(result.ZoneContent);
                        result.ZoneHasExtraData = HasZoneExtraData(result.ZoneContent);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // 枚举中途的意外异常：标记失败并记录原因
                result.Ok = false;
                result.Error = ex.Message;
            }
            return result;
        }

        /// <summary>把 Win32 枚举到的单条流信息规整后加入扫描结果。</summary>
        private static void AddStream(AdsScanResult result, Win32FindStreamData data)
        {
            var name = NormalizeStreamName(data.StreamName, out var isMain);
            result.Streams.Add(new AdsStreamInfo
            {
                Name = name,
                Length = data.StreamSize,
                IsMainData = isMain
            });
        }

        /// <summary>
        /// 规整 Win32 返回的原始流名称。
        /// <para>原始名称形如 ":Zone.Identifier:$DATA" 或 ":$DATA"（主数据流），
        /// 本方法去掉前导/尾随冒号与 "$DATA" 后缀，输出简洁的流名。</para>
        /// </summary>
        /// <param name="raw">Win32 返回的原始流名称。</param>
        /// <param name="isMain">是否为文件主数据流。</param>
        /// <returns>规整后的流名称（主数据流返回空字符串）。</returns>
        private static string NormalizeStreamName(string raw, out bool isMain)
        {
            isMain = false;
            if (string.IsNullOrEmpty(raw))
            {
                // 空名称视为主数据流
                isMain = true;
                return string.Empty;
            }
            var name = raw.TrimStart(':');
            // 恰好是 "$DATA"：主数据流
            if (string.Equals(name, "$DATA", StringComparison.OrdinalIgnoreCase))
            {
                isMain = true;
                return string.Empty;
            }
            // 以 ":$DATA" 结尾：剥掉该后缀得到真正的流名
            if (name.EndsWith(":$DATA", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 6);
            }
            return name.TrimEnd(':');
        }

        /// <summary>安全读取 Zone.Identifier 流的文本内容（读取失败返回 null）。</summary>
        public static string ReadZoneContentSafe(string path)
        {
            return ReadNamedStreamContentSafe(path, ZONE_STREAM_NAME);
        }

        /// <summary>
        /// 安全读取指定命名流的内容文本。
        /// <para>以只读、共享读写方式直接打开 "路径:流名"，最多读取 64 KB，
        /// 并按 BOM / 内容特征猜测文本编码。任何失败均返回 null 而不是抛异常。</para>
        /// </summary>
        /// <param name="path">文件路径。</param>
        /// <param name="streamName">命名流名称。</param>
        /// <returns>流内容文本；读取失败返回 null，内容为空返回空字符串。</returns>
        public static string ReadNamedStreamContentSafe(string path, string streamName)
        {
            var handle = CreateFileW(path + ":" + streamName, GENERIC_READ, FILE_SHARE_RW_DELETE,
                IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_SEQUENTIAL_SCAN, IntPtr.Zero);
            if (handle == new IntPtr(-1))
            {
                return null;
            }
            try
            {
                var buffer = new byte[65536];
                // 附加数据流通常很小，一次读取 64 KB 已足够
                if (!ReadFile(handle, buffer, (uint)buffer.Length, out var bytesRead, IntPtr.Zero))
                {
                    return null;
                }
                return bytesRead == 0 ? string.Empty : DecodeText(buffer, (int)bytesRead);
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        /// <summary>
        /// 根据字节前缀（BOM）或内容特征选择正确的编码来解码文本。
        /// <para>依次识别：UTF-16 LE（FF FE）、UTF-16 BE（FE FF）、UTF-8（EF BB BF），
        /// 无 BOM 时再通过零字节分布判断是否为 UTF-16 LE，最后回退到 UTF-8。</para>
        /// </summary>
        private static string DecodeText(byte[] buf, int count)
        {
            // UTF-16 LE BOM
            if (count >= 2 && buf[0] == byte.MaxValue && buf[1] == 254)
            {
                return Encoding.Unicode.GetString(buf, 2, count - 2);
            }
            // UTF-16 BE BOM
            if (count >= 2 && buf[0] == 254 && buf[1] == byte.MaxValue)
            {
                return Encoding.BigEndianUnicode.GetString(buf, 2, count - 2);
            }
            // UTF-8 BOM
            if (count >= 3 && buf[0] == 239 && buf[1] == 187 && buf[2] == 191)
            {
                return Encoding.UTF8.GetString(buf, 3, count - 3);
            }
            // 无 BOM：若内容呈现 UTF-16 LE 特征则按 UTF-16 LE 解码
            if (LooksLikeUtf16Le(buf, count))
            {
                return Encoding.Unicode.GetString(buf, 0, count);
            }
            // 默认按 UTF-8 解码
            return Encoding.UTF8.GetString(buf, 0, count);
        }

        /// <summary>
        /// 粗略判断字节流是否为 UTF-16 LE 编码。
        /// <para>原理：ASCII 字符在 UTF-16 LE 中偶数下标位置为 0，若前 512 字节中
        /// 奇数位 0 字节数足够多且显著多于偶数位 0 字节，则判定为 UTF-16 LE。</para>
        /// </summary>
        private static bool LooksLikeUtf16Le(byte[] buf, int count)
        {
            var sampleLen = Math.Min(count, 512);
            // 样本过少无法可靠判断
            if (sampleLen < 8)
            {
                return false;
            }
            // oddZeroes = 奇数下标出现 0 的次数；evenZeroes = 偶数下标出现 0 的次数
            var oddZeroes = 0;
            var evenZeroes = 0;
            for (var i = 0; i < sampleLen; i++)
            {
                if (buf[i] == 0)
                {
                    if ((i & 1) == 1)
                    {
                        oddZeroes++;
                    }
                    else
                    {
                        evenZeroes++;
                    }
                }
            }
            // 奇数位 0 至少 8 个且是偶数位 0 的两倍以上，才判定为 UTF-16 LE
            return oddZeroes >= 8 && oddZeroes > evenZeroes * 2;
        }

        /// <summary>
        /// 从 Zone.Identifier 内容中解析分区编号 ZoneId。
        /// </summary>
        /// <param name="content">Zone.Identifier 流的文本内容。</param>
        /// <returns>解析到的 ZoneId（0~4）；未找到或解析失败返回 -1。</returns>
        private static int ParseZoneId(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return -1;
            }
            // 定位 "ZoneId=" 关键字（大小写不敏感）
            var keywordIndex = content.IndexOf("ZoneId=", StringComparison.OrdinalIgnoreCase);
            if (keywordIndex < 0)
            {
                return -1;
            }
            var valueStart = keywordIndex + "ZoneId=".Length;
            // 向后扫描数字（允许负号），取连续的数字串
            int i;
            for (i = valueStart; i < content.Length && (char.IsDigit(content[i]) || content[i] == '-'); i++)
            {
            }
            // 关键字后面没有数字：解析失败
            if (i <= valueStart)
            {
                return -1;
            }
            return int.TryParse(content.Substring(valueStart, i - valueStart), out var zoneId) ? zoneId : -1;
        }

        /// <summary>
        /// 判断 Zone.Identifier 内容是否含有除分区信息之外的额外数据。
        /// <para>规则：按行切分后，存在既不是空行、又不是节标题（"[" 开头）、
        /// 也不是 "ZoneId=" 的行，即认为带有额外数据。</para>
        /// </summary>
        private static bool HasZoneExtraData(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }
            // 统一换行符后按行拆分
            var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                // 非空、非节标题、非 ZoneId 行的内容 → 视为额外数据
                if (line.Length != 0 && line[0] != '[' && !line.StartsWith("ZoneId=", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 将用户给定的根路径展开为待处理的文件列表。
        /// <para>目录会被递归展开（除非 recursive 为 false）；文件直接入列；
        /// 不存在的路径被忽略；无权限访问的子目录通过 inaccessible 计数统计。
        /// 注意：展开时会跳过符号链接/重解析点目录，避免循环递归。</para>
        /// </summary>
        /// <param name="root">用户给定的文件或文件夹路径。</param>
        /// <param name="recursive">是否递归处理子目录。</param>
        /// <param name="inaccessible">累计统计：因权限等原因无法访问的子目录数量。</param>
        /// <returns>展开后的文件完整路径列表。</returns>
        public static List<string> ExpandPath(string root, bool recursive, ref int inaccessible)
        {
            var files = new List<string>();
            try
            {
                if (Directory.Exists(root))
                {
                    Walk(root, recursive, files, ref inaccessible);
                }
                else if (File.Exists(root))
                {
                    files.Add(root);
                }
            }
            catch (Exception ex)
            {
                // 根路径本身无权限访问：计入不可访问数；其他异常照常抛出
                if (!IsIoDenied(ex))
                {
                    throw;
                }
                inaccessible++;
            }
            return files;
        }

        /// <summary>
        /// 递归遍历目录，收集其中的全部文件。
        /// <para>某个子目录无权限访问时跳过该目录并计数，不中断整体遍历；
        /// 重解析点（符号链接/目录联接）一律跳过，防止环路。</para>
        /// </summary>
        private static void Walk(string dir, bool recursive, List<string> files, ref int inaccessible)
        {
            // 收集当前目录的直接文件
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    files.Add(file);
                }
            }
            catch (Exception ex)
            {
                // 当前目录本身不可访问：计数并返回，不再下探
                if (IsIoDenied(ex))
                {
                    inaccessible++;
                    return;
                }
                throw;
            }
            // 不要求递归时直接结束
            if (!recursive)
            {
                return;
            }
            // 收集直接子目录
            IEnumerable<string> subDirs;
            try
            {
                subDirs = Directory.EnumerateDirectories(dir);
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
            foreach (var subDir in subDirs)
            {
                // 跳过符号链接/重解析点，避免目录环路造成死循环
                try
                {
                    if ((File.GetAttributes(subDir) & FileAttributes.ReparsePoint) != 0)
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
                // 递归下探子目录（递归深度下始终开启递归，即使顶层 recursive=false 也已提前返回）
                Walk(subDir, recursive: true, files, ref inaccessible);
            }
        }

        /// <summary>
        /// 判断异常是否属于"无权限访问"一类（可安全跳过并计数）。
        /// </summary>
        private static bool IsIoDenied(Exception ex)
        {
            // 权限、IO 抢占、安全限制、不支持的操作都归为可跳过类别
            return ex is UnauthorizedAccessException or IOException or SecurityException or NotSupportedException;
        }

        /// <summary>
        /// 并行批量移除文件的网络来源标记。
        /// <para>以 CPU 核心数为并发度上限（最多 8 线程）并行调用
        /// <see cref="RemoveZoneIdentifier"/>；每处理完一个文件即通过
        /// onCompleted 回调投递一条进度报告，并返回汇总统计。</para>
        /// </summary>
        /// <param name="files">待处理的文件路径列表。</param>
        /// <param name="onCompleted">每个文件处理完成后的回调（可空；用于图形界面的实时刷新）。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>本次任务的汇总统计。</returns>
        public static CleanStats ProcessFiles(IList<string> files, Action<FileReport> onCompleted, CancellationToken ct)
        {
            var removed = 0;
            var clean = 0;
            var failed = 0;
            // 统计计数器由多个并行线程共享写入，用锁保护
            var gate = new object();
            // 并发度：CPU 核心数，限制在 1~8 之间
            var maxDegreeOfParallelism = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };
            Parallel.ForEach(files, parallelOptions, file =>
            {
                AdsRemoveResult removeResult;
                string error;
                // 文件在队列期间被移动/删除：按失败处理并给出明确原因
                if (!File.Exists(file))
                {
                    removeResult = AdsRemoveResult.Failed;
                    error = Localization.T("file.missing");
                }
                else
                {
                    removeResult = RemoveZoneIdentifier(file, out error);
                }
                // 原子地更新汇总计数
                lock (gate)
                {
                    switch (removeResult)
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
                // 生成进度报告并回调（GUI 模式下由回调把结果投递给 UI 线程）
                var report = new FileReport
                {
                    Path = file,
                    Result = removeResult,
                    Error = error
                };
                if (onCompleted != null)
                {
                    onCompleted(report);
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
