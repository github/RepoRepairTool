using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using GitHub;
using Ionic.Zip;
using LibGit2Sharp;
using RepoRepairTool.Helpers;
using Should;

namespace RepoRepairTool.Tests
{
    public static class IntegrationTestHelper
    {
        public static string GetPath(params string[] paths)
        {
            var ret = GetIntegrationTestRootDirectory();
            return (new FileInfo(paths.Aggregate(ret, Path.Combine))).FullName;
        }

        public static string GetIntegrationTestRootDirectory()
        {
            // XXX: This is an evil hack, but it's okay for a unit test
            // We can't use Assembly.Location because unit test runners love
            // to move stuff to temp directories
            var st = new StackFrame(true);
            var di = new DirectoryInfo(Path.GetDirectoryName(st.GetFileName()));

            return di.FullName;
        }

        public static DisposableContainer<string> GetEmptyDirectory()
        {
            var di = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            di.Create();

            return DisposableContainer.Create(di.FullName, CoreUtility.DeleteDirectory);
        }

        public static DisposableContainer<Repository> GetRepositoryFromFixture(string fixtureName)
        {
            var zipPath = GetPath("fixture", fixtureName + ".zip");
            var target = GetEmptyDirectory();

            File.Exists(zipPath).ShouldBeTrue();

            using (var zf = new ZipFile(zipPath)) {
                zf.ExtractAll(target.Value, ExtractExistingFileAction.Throw);
            }

            CoreUtility.ExtractLibGit2();
            var ret = new Repository(Path.Combine(target.Value, fixtureName));
            return DisposableContainer.Create(ret, new CompositeDisposable(ret, target));
        }

        public static bool SkipTestOnXPAndVista()
        {
            int osVersion = Environment.OSVersion.Version.Major * 100 + Environment.OSVersion.Version.Minor;
            return (osVersion < 601);
        }
    }
}