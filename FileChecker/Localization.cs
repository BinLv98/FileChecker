using FileChecker.Definitions;
using System;
using System.Collections.Generic;

namespace FileChecker
{
    /// <summary>
    /// 界面本地化（国际化）支持。
    /// <para>以"资源键 → 文本"字典的方式维护中（<see cref="_zh"/>）、英（<see cref="_en"/>）
    /// 两套文案；所有界面文字都通过 <see cref="T"/> / <see cref="F"/> 间接获取，
    /// 切换语言后通过 <see cref="LanguageChanged"/> 事件通知各窗口即时刷新。</para>
    /// </summary>
    internal static class Localization
    {
        /// <summary>简体中文文案字典：资源键 → 中文文本。</summary>
        private static readonly Dictionary<string, string> _zh = new()
        {
            // —— 主窗口（MainWindow.xaml）——
            ["main.dropTitle"] = "将文件或文件夹拖放到这里",
            ["main.dropSub"] = "文件夹会被递归处理（可在下方取消勾选“包含子文件夹”）",
            ["main.addFiles"] = "添加文件…",
            ["main.addFolder"] = "添加文件夹…",
            ["main.removeSel"] = "移除选中",
            ["main.clear"] = "清空列表",
            ["main.rescan"] = "重新读取文件标记",
            ["main.removeMark"] = "去除网络标记",
            ["main.removeMarkTip"] = "移除列表中所有文件带有的下载来源标记（如“来自网络”），解除打开文件时的“安全警告”",
            ["main.hash"] = "计算 SHA256 / MD5",
            ["main.recursive"] = "包含子文件夹",
            ["main.threads"] = "计算线程数",
            ["main.threadsTip"] = "计算 SHA256 / MD5 时同时处理的文件数（1～10，默认为 5）",
            ["main.cancel"] = "取消",
            ["main.language"] = "界面语言",

            // —— 列表列标题 ——
            ["col.path"] = "文件路径",
            ["col.mark"] = "文件标记",
            ["col.progress"] = "进度",
            ["col.md5"] = "MD5",
            ["col.sha256"] = "SHA256",

            // —— 右键菜单项 ——
            ["menu.removeMark"] = "去除选中项的网络标记",
            ["menu.detail"] = "查看文件标记详情",
            ["menu.rescan"] = "重新读取文件标记",
            ["menu.openFolder"] = "打开所在目录",
            ["menu.copyPath"] = "复制完整路径",
            ["menu.copyMd5"] = "复制 MD5",
            ["menu.copySha"] = "复制 SHA256",
            ["menu.copyBoth"] = "复制 SHA256 和 MD5",
            ["menu.remove"] = "移除选中项",
            ["menu.clear"] = "清空列表",

            // —— 空列表提示 ——
            ["empty.title"] = "列表为空",
            ["empty.sub"] = "点击“添加文件 / 添加文件夹”，或将文件、文件夹拖放到上方区域",

            // —— 对话框 ——
            ["dlg.addFilesTitle"] = "选择要解除标记的文件（可多选）",
            ["dlg.filter"] = "所有文件|*.*",
            ["dlg.folderDesc"] = "选择文件夹（将递归处理其中所有文件）",

            // —— 主窗口状态/摘要消息（{0} 等为占位符）——
            ["msg.added"] = "已添加 {0} 个文件。",
            ["msg.noNew"] = "没有可添加的新文件。",
            ["msg.ignoreInvalid"] = "（忽略 {0} 个无效路径）",
            ["msg.ignoreDirs"] = "（{0} 个子目录无法访问，已跳过）",
            ["msg.noItems"] = "请先添加文件或文件夹。",
            ["msg.selFirst"] = "请先选中要去除网络标记的文件。",
            ["msg.removing"] = "正在去除 {0} 个文件的网络标记…",
            ["msg.cancellingRemove"] = "正在取消去除网络标记…请稍候",
            ["msg.cancellingHash"] = "正在取消校验码计算…请稍候",
            ["msg.fatal"] = "发生内部错误：{0}",
            ["msg.cancelSummary"] = "已取消：去除网络标记完成 {0} / {1} 项（已解除 {2}｜无标记 {3}｜失败 {4}），剩 {5} 项未处理。",
            ["msg.doneSummary"] = "去除网络标记完成：共 {0} 项 ｜ 已解除 {1} ｜ 无标记 {2} ｜ 失败 {3}",
            ["msg.rescanSel"] = "正在重新读取 {0} 个选中项的文件标记…",
            ["msg.rescanAll"] = "正在重新读取全部 {0} 项的文件标记…",
            ["msg.hashing"] = "正在计算 SHA256 / MD5…（并发 {0} 个文件，可点“取消”中止）",
            ["msg.hashCancelled"] = "校验码计算已取消。",
            ["msg.hashDoneFail"] = "校验码计算完成：成功 {0} ｜ 失败 {1}（失败项悬停可查看原因）",
            ["msg.hashDoneAll"] = "校验码计算完成：共 {0} 个文件",
            ["msg.noHashYet"] = "所选文件还没有可用的校验码，请先点击“计算 SHA256 / MD5”。",
            ["msg.closeRemove"] = "文件仍在去除网络标记，确定要退出吗？",
            ["msg.closeHash"] = "校验码仍在计算，确定要退出吗？",
            ["msg.idle"] = "就绪。",

            // —— 列表项状态文字 ——
            ["item.reading"] = "读取中…",
            ["item.readFailed"] = "读取失败",
            ["item.errPrefix"] = "无法读取文件标记：{0}",
            ["item.removed"] = "已移除网络标记",
            ["item.noMark"] = "未检测到网络标记",
            ["item.cannotRemove"] = "无法移除该标记",
            ["item.computing"] = "计算中…",
            ["item.cancelled"] = "已取消",
            ["item.unknown"] = "未知原因",
            ["item.md5Copy"] = "MD5: {0}{1}SHA256: {2}",
            ["item.hashFailedTip"] = "校验码读取失败：{0}",

            // —— 悬停提示 ——
            ["tip.mark"] = "文件标记：",
            ["tip.result"] = "处理结果：",
            ["tip.refTxt"] = "参照 txt：",
            ["tip.refTxtFail"] = "参照 txt 无法读取，未进行比对。",
            ["tip.md5Cmp"] = "MD5 比对：",
            ["tip.shaCmp"] = "SHA256 比对：",
            ["tip.match"] = "一致",
            ["tip.mismatchRef"] = "不一致（txt 中记录的 {0} 为 {1}）",
            ["tip.noRef"] = "txt 中未找到有效的 {0} 记录",

            // —— 分区编号（ZoneId）标签：0=本地 … 4=受限站点 ——
            ["zone.0"] = "本地",
            ["zone.1"] = "来自内网",
            ["zone.2"] = "受信站点",
            ["zone.3"] = "来自网络",
            ["zone.4"] = "受限站点",

            // —— 标记/信息说明文字 ——
            ["ads.other"] = "其他",
            ["ads.none"] = "无",
            ["info.parsed"] = "解析结果：{0}",
            ["info.extra"] = "（还带有附加信息，原始内容可在详情中查看）",
            ["info.unknownZone"] = "该文件带有标记数据但无法识别其含义，原始内容可在详情中查看。",
            ["info.otherAds"] = "该文件含其他附加数据（不含下载来源标记），详情中可查看。",
            ["info.clean"] = "该文件无网络标记。",

            // —— 体积单位 ——
            ["fmt.mb"] = " MB",
            ["fmt.kb"] = " KB",
            ["fmt.bytes"] = " 字节",

            // —— 命令行静默模式 ——
            ["cli.missing"] = "（{0}）",

            // —— 文件标记详情窗口（StreamDetailWindow）——
            ["detail.title"] = "文件标记详情",
            ["detail.clear"] = "清除全部",
            ["detail.clearTip"] = "删除该文件上的全部附加数据（含网络来源标记），不可恢复",
            ["detail.copy"] = "复制全部文本",
            ["detail.close"] = "关闭",
            ["detail.unreadable"] = "无法读取该文件的标记信息：{0}",
            ["detail.rescanHint"] = "请先“重新读取文件标记”。",
            ["detail.noAds"] = "该文件不包含任何附加数据。",
            ["detail.streamUnreadable"] = "（内容不可读取）",
            ["detail.streamEmpty"] = "（内容为空）",
            ["detail.clearAsk"] = "将删除该文件上的全部附加数据（含网络来源标记）。此操作不可撤销，是否继续？",
            ["detail.clearAskTitle"] = "清除全部附加数据",
            ["detail.clearDone"] = "已清除该文件的全部附加数据（共 {0} 项）。",
            ["detail.clearDoneTitle"] = "清除完成",
            ["detail.clearNone"] = "该文件本来就没有附加数据。",
            ["detail.clearPartial"] = "已清除 {0} 项，但仍有流无法清除：{1}",
            ["detail.clearFail"] = "清除失败：{0}",
            ["detail.clearFailTitle"] = "清除失败",

            // —— 命令行摘要与错误 ——
            ["cli.summary"] = "FileChecker 处理完成：共扫描 {0} 个文件，已解除 {1} 个，无标记 {2} 个，失败 {3} 个",
            ["cli.badArg"] = "无法识别的参数：{0}",
            ["cli.inaccessible"] = "另有 {0} 个子目录无法访问（已跳过）。",
            ["cli.cannot"] = "无法处理的项目：",
            ["cli.internalError"] = "内部错误：{0}",
            ["cli.logFail"] = "写入日志失败: {0}",
            ["app.badArgsTitle"] = "FileChecker - 参数错误",
            ["app.helpTitle"] = "FileChecker - 命令行帮助",
            ["file.missing"] = "文件不存在或已被移动。",
            ["cmd.usage"] = "FileChecker —— 文件校验与批量解除文件的\"安全警告\"\r\n\r\n用法：\r\n  FileChecker.exe                  启动图形界面（支持拖放、计算 SHA256 / MD5）\r\n  FileChecker.exe <路径> [...]     静默处理指定的文件或文件夹，完成后退出\r\n  FileChecker.exe --log=结果.txt <路径>   处理并把摘要写入日志\r\n\r\n选项：\r\n  -r, --recursive        递归处理子文件夹（默认开启）\r\n  --no-recursive         仅处理文件夹内的直接文件\r\n  -s, --silent           静默模式（传入路径时本就默认静默）\r\n  --log=<文件>            将结果摘要写入文本日志\r\n  -h, --help / /?        显示本帮助\r\n\r\n示例：\r\n  FileChecker.exe D:\\Downloads\\setup.exe\r\n  FileChecker.exe D:\\Downloads D:\\Tools\\app.exe --log=result.txt\r\n\r\n把文件或文件夹直接拖到本程序图标上，也会自动静默处理。\r\n退出代码：0 = 成功（全部处理）；1 = 存在失败项或不可访问项。"
        };

        /// <summary>英文文案字典：资源键 → 英文文本（与 _zh 的键一一对应）。</summary>
        private static readonly Dictionary<string, string> _en = new()
        {
            // —— Main window ——
            ["main.dropTitle"] = "Drag files or folders here",
            ["main.dropSub"] = "Folders are processed recursively (uncheck \"Include subfolders\" below if not needed)",
            ["main.addFiles"] = "Add Files…",
            ["main.addFolder"] = "Add Folder…",
            ["main.removeSel"] = "Remove Selected",
            ["main.clear"] = "Clear List",
            ["main.rescan"] = "Re-read File Markers",
            ["main.removeMark"] = "Remove Web Markers",
            ["main.removeMarkTip"] = "Remove the download-source marker (e.g. \"From the Internet\") from every file in the list to dismiss the \"Security Warning\" shown when opening files",
            ["main.hash"] = "Calculate SHA256 / MD5",
            ["main.recursive"] = "Include subfolders",
            ["main.threads"] = "Hash threads",
            ["main.threadsTip"] = "Number of files hashed at the same time for SHA256 / MD5 (1-10, default 5)",
            ["main.cancel"] = "Cancel",
            ["main.language"] = "Language",

            // —— Column headers ——
            ["col.path"] = "File Path",
            ["col.mark"] = "Marker",
            ["col.progress"] = "Progress",
            ["col.md5"] = "MD5",
            ["col.sha256"] = "SHA256",

            // —— Context menu ——
            ["menu.removeMark"] = "Remove Web Marker from Selected",
            ["menu.detail"] = "View File Marker Details",
            ["menu.rescan"] = "Re-read File Markers",
            ["menu.openFolder"] = "Open Containing Folder",
            ["menu.copyPath"] = "Copy Full Path",
            ["menu.copyMd5"] = "Copy MD5",
            ["menu.copySha"] = "Copy SHA256",
            ["menu.copyBoth"] = "Copy SHA256 and MD5",
            ["menu.remove"] = "Remove Selected",
            ["menu.clear"] = "Clear List",

            // —— Empty list hint ——
            ["empty.title"] = "The list is empty",
            ["empty.sub"] = "Click \"Add Files / Add Folder\", or drag files or folders to the area above",

            // —— Dialogs ——
            ["dlg.addFilesTitle"] = "Select files to unmark (multiple selection)",
            ["dlg.filter"] = "All files|*.*",
            ["dlg.folderDesc"] = "Select a folder (all files inside will be processed recursively)",

            // —— Status / summary messages ——
            ["msg.added"] = "{0} file(s) added.",
            ["msg.noNew"] = "No new files to add.",
            ["msg.ignoreInvalid"] = " ({0} invalid path(s) ignored)",
            ["msg.ignoreDirs"] = " ({0} inaccessible subfolder(s) skipped)",
            ["msg.noItems"] = "Please add files or folders first.",
            ["msg.selFirst"] = "Please select files whose web markers you want to remove first.",
            ["msg.removing"] = "Removing web markers from {0} file(s)…",
            ["msg.cancellingRemove"] = "Cancelling web-marker removal… please wait",
            ["msg.cancellingHash"] = "Cancelling hash calculation… please wait",
            ["msg.fatal"] = "Internal error: {0}",
            ["msg.cancelSummary"] = "Cancelled: web-marker removal finished for {0} of {1} item(s) (removed {2} | no marker {3} | failed {4}); {5} item(s) left unprocessed.",
            ["msg.doneSummary"] = "Web-marker removal finished: {0} total | removed {1} | no marker {2} | failed {3}",
            ["msg.rescanSel"] = "Re-reading markers of {0} selected item(s)…",
            ["msg.rescanAll"] = "Re-reading markers of all {0} item(s)…",
            ["msg.hashing"] = "Calculating SHA256 / MD5… (hashing {0} file(s) concurrently; click Cancel to abort)",
            ["msg.hashCancelled"] = "Hash calculation cancelled.",
            ["msg.hashDoneFail"] = "Hash calculation finished: {0} succeeded | {1} failed (hover a failed item for the reason)",
            ["msg.hashDoneAll"] = "Hash calculation finished: {0} file(s)",
            ["msg.noHashYet"] = "The selected file has no checksum yet. Click \"Calculate SHA256 / MD5\" first.",
            ["msg.closeRemove"] = "Web-marker removal is still running. Exit anyway?",
            ["msg.closeHash"] = "Hash calculation is still running. Exit anyway?",
            ["msg.idle"] = "Ready.",

            // —— Item state text ——
            ["item.reading"] = "Reading…",
            ["item.readFailed"] = "Read failed",
            ["item.errPrefix"] = "Cannot read file marker: {0}",
            ["item.removed"] = "Web marker removed",
            ["item.noMark"] = "No web marker detected",
            ["item.cannotRemove"] = "Cannot remove this marker",
            ["item.computing"] = "Calculating…",
            ["item.cancelled"] = "Cancelled",
            ["item.unknown"] = "Unknown reason",
            ["item.md5Copy"] = "MD5: {0}{1}SHA256: {2}",
            ["item.hashFailedTip"] = "Hash read failed: {0}",

            // —— Tooltips ——
            ["tip.mark"] = "Marker: ",
            ["tip.result"] = "Result: ",
            ["tip.refTxt"] = "Reference txt: ",
            ["tip.refTxtFail"] = "Reference txt could not be read; skipped comparison.",
            ["tip.md5Cmp"] = "MD5 comparison: ",
            ["tip.shaCmp"] = "SHA256 comparison: ",
            ["tip.match"] = "Match",
            ["tip.mismatchRef"] = "Mismatch (txt records {0} as {1})",
            ["tip.noRef"] = "No valid {0} found in the txt",

            // —— Zone labels ——
            ["zone.0"] = "Local",
            ["zone.1"] = "Intranet",
            ["zone.2"] = "Trusted site",
            ["zone.3"] = "Internet",
            ["zone.4"] = "Restricted site",

            // —— Marker / info text ——
            ["ads.other"] = "Other",
            ["ads.none"] = "None",
            ["info.parsed"] = "Parsed result: {0}",
            ["info.extra"] = " (also contains extra data; raw content is shown in Details)",
            ["info.unknownZone"] = "The file has marker data whose meaning cannot be determined; see Details for the raw content.",
            ["info.otherAds"] = "The file has other extra data (no web marker); see Details.",
            ["info.clean"] = "This file has no web marker.",

            // —— Size units ——
            ["fmt.mb"] = " MB",
            ["fmt.kb"] = " KB",
            ["fmt.bytes"] = " bytes",

            // —— CLI silent mode ——
            ["cli.missing"] = " ({0})",

            // —— Detail window ——
            ["detail.title"] = "File Marker Details",
            ["detail.clear"] = "Clear All",
            ["detail.clearTip"] = "Delete all extra data on this file (including web-source markers). This cannot be undone.",
            ["detail.copy"] = "Copy All Text",
            ["detail.close"] = "Close",
            ["detail.unreadable"] = "Cannot read the marker info of this file: {0}",
            ["detail.rescanHint"] = "Run \"Re-read File Markers\" first.",
            ["detail.noAds"] = "This file has no extra data.",
            ["detail.streamUnreadable"] = "(content unreadable)",
            ["detail.streamEmpty"] = "(content empty)",
            ["detail.clearAsk"] = "Delete all extra data on this file (including web-source markers)? This cannot be undone. Continue?",
            ["detail.clearAskTitle"] = "Clear All Extra Data",
            ["detail.clearDone"] = "Cleared all extra data on this file ({0} item(s)).",
            ["detail.clearDoneTitle"] = "Clear Complete",
            ["detail.clearNone"] = "This file had no extra data.",
            ["detail.clearPartial"] = "Cleared {0} item(s), but some streams could not be cleared: {1}",
            ["detail.clearFail"] = "Clear failed: {0}",
            ["detail.clearFailTitle"] = "Clear Failed",

            // —— CLI summary & errors ——
            ["cli.summary"] = "FileChecker finished: scanned {0} file(s), unmarked {1}, no marker {2}, failed {3}",
            ["cli.badArg"] = "Unrecognized argument: {0}",
            ["cli.inaccessible"] = "{0} subfolder(s) were inaccessible and skipped.",
            ["cli.cannot"] = "Items that could not be processed:",
            ["cli.internalError"] = "Internal error: {0}",
            ["cli.logFail"] = "Failed to write log: {0}",
            ["app.badArgsTitle"] = "FileChecker - Invalid Arguments",
            ["app.helpTitle"] = "FileChecker - Command Line Help",
            ["file.missing"] = "The file does not exist or has been moved.",
            ["cmd.usage"] = "FileChecker - file verification and batch removal of the \"Security Warning\" (Web Markers)\r\n\r\nUsage:\r\n  FileChecker.exe                  Launch the GUI (drag & drop, SHA256 / MD5)\r\n  FileChecker.exe <path> [...]     Process the given files/folders silently, then exit\r\n  FileChecker.exe --log=out.txt <path>   Process and write a summary to a log file\r\n\r\nOptions:\r\n  -r, --recursive         Recurse into subfolders (enabled by default)\r\n  --no-recursive          Process only the files directly inside the folder\r\n  -s, --silent            Silent mode (already the default when paths are given)\r\n  --log=<file>             Write the result summary to a text file\r\n  -h, --help / /?         Show this help\r\n\r\nExamples:\r\n  FileChecker.exe D:\\Downloads\\setup.exe\r\n  FileChecker.exe D:\\Downloads D:\\Tools\\app.exe --log=result.txt\r\n\r\nDragging files or folders onto this program's icon also starts silent processing.\r\nExit code: 0 = success (all processed); 1 = failed or inaccessible items."
        };

        /// <summary>当前界面语言（默认简体中文）。</summary>
        internal static AppLanguage CurrentLanguage { get; set; } = AppLanguage.Chinese;

        /// <summary>语言切换事件：切换后所有已打开窗口据此刷新文案。</summary>
        internal static event EventHandler LanguageChanged;

        /// <summary>
        /// 获取指定资源键在当前语言下的文本。
        /// </summary>
        /// <param name="key">资源键（如 "main.dropTitle"）。</param>
        /// <returns>当前语言的文案；键不存在时原样返回键名。</returns>
        internal static string T(string key)
        {
            // 英文模式下优先查英文字典；查不到再回退中文
            if (CurrentLanguage == AppLanguage.English && _en.TryGetValue(key, out var value))
            {
                return value;
            }

            if (_zh.TryGetValue(key, out value))
            {
                return value;
            }

            return key;
        }

        /// <summary>
        /// 获取带格式化占位符的文案（等价于 string.Format(T(key), args)）。
        /// </summary>
        /// <param name="key">资源键。</param>
        /// <param name="args">格式化参数。</param>
        /// <returns>格式化后的文案。</returns>
        internal static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        /// <summary>
        /// 切换界面语言；语言变化时触发 <see cref="LanguageChanged"/> 事件。
        /// </summary>
        /// <param name="lang">目标语言。</param>
        internal static void Set(AppLanguage lang)
        {
            if (CurrentLanguage != lang)
            {
                CurrentLanguage = lang;
                LanguageChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
