using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GitHub.Extensions;
using ReactiveUI;

namespace RepoRepairTool.Helpers
{
    public static class FilesystemInfoExtensions
    {
        public static IEnumerable<FileInfo> SafeGetFiles(this DirectoryInfo di)
        {
            try {
                return new Func<IEnumerable<FileInfo>>(di.EnumerateFiles).Retry(3);
            } catch (Exception ex) {
                LogHost.Default.WarnException("Really couldn't read files: " + di.FullName, ex);
                return Enumerable.Empty<FileInfo>();
            }
        }

        public static IEnumerable<DirectoryInfo> SafeGetDirectories(this DirectoryInfo di)
        {
            try {
                return new Func<IEnumerable<DirectoryInfo>>(di.EnumerateDirectories).Retry(3);
            } catch (Exception ex) {
                LogHost.Default.WarnException("Really couldn't read dirs: " + di.FullName, ex);
                return Enumerable.Empty<DirectoryInfo>();
            }
        }
    }
}
