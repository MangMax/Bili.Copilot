// Copyright (c) Bili Copilot. All rights reserved.

using System.IO;
using System.Xml.Linq;

namespace BiliCopilot.UI.Tests;

/// <summary>
/// 读取项目资源文件并定位仓库根目录的测试辅助.
/// </summary>
/// <remarks>
/// 多个资源契约测试都要读同一份 <c>Resources.resw</c>，
/// 这里集中维护路径与仓库根目录的查找逻辑，避免各测试各写一份。
/// </remarks>
internal static class ResourcesFileHelper
{
    /// <summary>
    /// 读取资源文件里的全部条目.
    /// </summary>
    /// <returns>资源文件中的 <c>data</c> 节点.</returns>
    public static IReadOnlyList<XElement> LoadResources()
    {
        var reswPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Desktop",
            "BiliCopilot.UI",
            "Resources",
            "zh-Hans-CN",
            "Resources.resw");

        return XDocument.Load(reswPath).Root!.Elements("data").ToList();
    }

    /// <summary>
    /// 从测试程序所在目录向上查找仓库根目录.
    /// </summary>
    /// <returns>仓库根目录的绝对路径.</returns>
    public static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory, "src", "Desktop", "BiliCopilot.UI")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("未找到仓库根目录（含 src/Desktop/BiliCopilot.UI）。");
    }
}
