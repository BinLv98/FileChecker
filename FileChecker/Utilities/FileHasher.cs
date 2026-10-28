using FileChecker.Data;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace FileChecker.Utilities
{
    public static class FileHasher
    {
        public static FileHashResult Compute(string path, CancellationToken ct, Action<double> reportProgress = null)
        {
            var fileHashResult = new FileHashResult();
            try
            {
                if (!File.Exists(path))
                {
                    fileHashResult.Error = Localization.T("file.missing");
                    return fileHashResult;
                }
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
                            while ((num2 = fileStream.Read(array, 0, array.Length)) > 0)
                            {
                                ct.ThrowIfCancellationRequested();
                                mD.TransformBlock(array, 0, num2, null, 0);
                                sHa.TransformBlock(array, 0, num2, null, 0);
                                num += num2;
                                ReportProgress(reportProgress, length, num);
                            }
                            mD.TransformFinalBlock(array, 0, 0);
                            sHa.TransformFinalBlock(array, 0, 0);
                            fileHashResult.MD5 = ToHex(mD.Hash);
                            fileHashResult.SHA256 = ToHex(sHa.Hash);
                            fileHashResult.Ok = true;
                        }
                    }
                }
                if (reportProgress != null)
                {
                    ReportProgress(reportProgress, 1L, 1L);
                }
                return fileHashResult;
            }
            catch (OperationCanceledException)
            {
                fileHashResult.Cancelled = true;
                return fileHashResult;
            }
            catch (Exception ex2)
            {
                fileHashResult.Error = ex2.Message;
                return fileHashResult;
            }
        }

        private static void ReportProgress(Action<double> report, long total, long read)
        {
            if (report == null)
            {
                return;
            }
            var obj = total <= 0 ? 1.0 : Math.Min(1.0, read / (double)total);
            try
            {
                report(obj);
            }
            catch
            {
                // ignored
            }
        }

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
