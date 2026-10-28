using FileChecker.Data;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace FileChecker.Utilities
{
    /// <summary>
    /// 文件校验码（MD5 / SHA256）计算工具。
    /// <para>采用增量方式（TransformBlock）分块读取大文件，MD5 与 SHA256 在
    /// 同一次读取中同步计算，避免重复读盘；支持取消令牌与进度回调。
    /// 该工具类对所有调用方公开，供图形界面与命令行共用。</para>
    /// </summary>
    public static class FileHasher
    {
        /// <summary>
        /// 计算指定文件的 MD5 与 SHA256 校验码。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        /// <param name="ct">取消令牌；计算过程中调用方可请求取消。</param>
        /// <param name="reportProgress">进度回调，参数为 0.0~1.0 的完成比例（可空）。</param>
        /// <returns>
        /// 计算结果对象；<see cref="FileHashResult.Ok"/> 为 true 时
        /// <see cref="FileHashResult.MD5"/> 与 <see cref="FileHashResult.SHA256"/> 有效。
        /// </returns>
        public static FileHashResult Compute(string path, CancellationToken ct, Action<double> reportProgress = null)
        {
            var fileHashResult = new FileHashResult();
            try
            {
                // 文件不存在：直接返回本地化的错误信息
                if (!File.Exists(path))
                {
                    fileHashResult.Error = Localization.T("file.missing");
                    return fileHashResult;
                }
                // 以 1 MB 缓冲、顺序读模式打开文件，允许其他进程共享读/删除
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1048576, FileOptions.SequentialScan))
                {
                    using (var mD = MD5.Create())
                    {
                        using (var sHa = SHA256.Create())
                        {
                            var array = new byte[1048576];
                            var length = fileStream.Length;
                            var num = 0L;
                            int num2;
                            // 分块读取直到文件末尾；每次读取后同步喂给两个哈希算法
                            while ((num2 = fileStream.Read(array, 0, array.Length)) > 0)
                            {
                                // 每块之间检查取消请求，避免无法及时中止
                                ct.ThrowIfCancellationRequested();
                                mD.TransformBlock(array, 0, num2, null, 0);
                                sHa.TransformBlock(array, 0, num2, null, 0);
                                num += num2;
                                // 上报已读取字节数对应的完成比例
                                ReportProgress(reportProgress, length, num);
                            }
                            // 结束两个算法的输入流并取出最终摘要
                            mD.TransformFinalBlock(array, 0, 0);
                            sHa.TransformFinalBlock(array, 0, 0);
                            fileHashResult.MD5 = ToHex(mD.Hash);
                            fileHashResult.SHA256 = ToHex(sHa.Hash);
                            fileHashResult.Ok = true;
                        }
                    }
                }
                // 全部读取完成后补发一次 100% 进度（空文件等边界情况也需要收尾）
                if (reportProgress != null)
                {
                    ReportProgress(reportProgress, 1L, 1L);
                }
                return fileHashResult;
            }
            catch (OperationCanceledException)
            {
                // 用户取消：标记 Cancelled，不算错误
                fileHashResult.Cancelled = true;
                return fileHashResult;
            }
            catch (Exception ex2)
            {
                // 其他异常（IO 错误、权限不足等）：记录错误信息
                fileHashResult.Error = ex2.Message;
                return fileHashResult;
            }
        }

        /// <summary>
        /// 计算并上报完成比例（避免除零，并夹取到 0.0~1.0）。
        /// </summary>
        private static void ReportProgress(Action<double> report, long total, long read)
        {
            if (report == null)
            {
                return;
            }
            // 文件长度为 0 时按 1.0 处理；否则按已读/总量计算并限制不超过 1.0
            var obj = total <= 0 ? 1.0 : Math.Min(1.0, read / (double)total);
            try
            {
                report(obj);
            }
            catch
            {
                // 回调内部异常不向上传播，避免中断哈希计算
                // ignored
            }
        }

        /// <summary>
        /// 将哈希摘要字节数组转换为小写十六进制字符串。
        /// </summary>
        /// <param name="data">摘要原始字节。</param>
        /// <returns>如 "e3b0c44298fc1c149afbf4c8996fb924..." 形式的十六进制文本。</returns>
        private static string ToHex(byte[] data)
        {
            var stringBuilder = new StringBuilder(data.Length * 2);
            foreach (var b in data)
            {
                stringBuilder.Append(b.ToString("x2"));
            }
            return stringBuilder.ToString();
        }
    }
}
