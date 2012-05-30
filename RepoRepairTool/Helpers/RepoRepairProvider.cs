using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GitHub;
using GitHub.Helpers;
using LibGit2Sharp;
using ReactiveUI;
using RepoRepairTool.ViewModels;

namespace RepoRepairTool.Helpers
{
    [Flags]
    public enum RepoRepairOptions {
        IgnoreRemoteBranches = 0x1 << 0,
        IgnoreLocalBranches = 0x1 << 1,
        IgnoreWorkingDirectory = 0x1 << 2,
        IgnoreAllBranches = IgnoreLocalBranches | IgnoreRemoteBranches,
    }

    public interface IRepoRepairProvider : IEnableLogger
    {
        IObservable<Unit> RepairRepo(Repository repo, RepoAnalysisResult analysis, RepoRepairOptions options);
    }

    public class RepoRepairProvider : IRepoRepairProvider
    {
        public IObservable<Unit> RepairRepo(Repository repo, RepoAnalysisResult analysis, RepoRepairOptions options)
        {
            // NB: Try to grab a bunch of locks beforehand, so that if some dumb
            // program has files locked, we won't fail halfway through
            IObservable<Unit> repairWd = Observable.Empty<Unit>();

            if (!options.HasFlag(RepoRepairOptions.IgnoreWorkingDirectory)) {
                IEnumerable<Tuple<string, Stream>> allWdFiles;

                try {
                    allWdFiles = repo.OpenAllFilesInWorkingDirectory(FileAccess.ReadWrite);
                } catch (Exception ex) {
                    return Observable.Throw<Unit>(ex);
                }

                var branchInfo = analysis.BranchAnalysisResults[Constants.WorkingDirectory];
                repairWd = Observable.Defer(() =>
                    repairWorkingDir(allWdFiles, repo.Info.WorkingDirectory, branchInfo));
            }

            return repo.ProcessAllBranchesAsync(branch => repairBranch(repo, branch, analysis.BranchAnalysisResults[branch.Name], options))
                .Select(_ => Unit.Default)
                .Merge(repairWd)
                .Aggregate(Unit.Default, (acc, _) => acc);
        }

        IObservable<Unit> repairBranch(Repository repo, Branch branch, HeuristicTreeInformation branchAnalysis, RepoRepairOptions options)
        {
            if (options.HasFlag(RepoRepairOptions.IgnoreRemoteBranches) && branch.IsRemote) {
                return Observable.Return(Unit.Default);
            }

            if (options.HasFlag(RepoRepairOptions.IgnoreLocalBranches) && !branch.IsRemote) {
                return Observable.Return(Unit.Default);
            }

            return Observable.Start(() => {
                if (branch.IsRemote) {
                    // TODO: Create a remote tracking branch
                    return;
                }

                var oldTree = branch.Tip.Tree;
                var newTree = TreeDefinition.From(oldTree);

                var di = new DirectoryInfo(repo.Info.WorkingDirectory);
                var tempDir = di.CreateSubdirectory("__TEMP__" + branch.Name.GetHashCode());

                // NB: The temp files are only read after we make the commit,
                // if we delete them after creating the TreeEntry, we'll be
                // kicking the file out from under libgit2
                branchAnalysis.BadEncodingFiles.Keys.ForEach(relativePath =>
                    processTreeEntry(branch.Name, oldTree, newTree, relativePath, repo.Info.WorkingDirectory, tempDir.FullName));

                branchAnalysis.BadLineEndingFiles.Keys.ForEach(relativePath =>
                    processTreeEntry(branch.Name, oldTree, newTree, relativePath, repo.Info.WorkingDirectory, tempDir.FullName,
                        s => LineEndingInfo.FixLineEndingsForString(s, branchAnalysis.LineEndingType)));

                var committer = new Signature(
                    repo.Config.Get("user.name", "Unknown User"),
                    repo.Config.Get("user.email", "unknown@localhost"),
                    DateTimeOffset.Now);

                // TODO: This commit message is stupid
                var commit = repo.ObjectDatabase.CreateCommit("Repaired branch",
                    new Signature("RepoRepairTool", "support@github.com", DateTimeOffset.Now),
                    committer,
                    repo.ObjectDatabase.CreateTree(newTree),
                    new[] { branch.Tip });

                repo.Refs.UpdateTarget(branch.CanonicalName, commit.Sha);
                CoreUtility.DeleteDirectory(di.FullName);
            }, RxApp.TaskpoolScheduler);
        }

        string processTreeEntry(string branchName, Tree originalTree, TreeDefinition newTree, string relativePath, string repoRootDir, string tempDir, Func<string, string> processor = null)
        {
            if (inNuGetPackagesDir(relativePath)) {
                return null;
            }

            var mode = originalTree[relativePath].Mode;
            var bytes = ((Blob)originalTree[relativePath].Target).Content;
            var text = CoreUtility.GuessEncodingForBytes(bytes).GetString(bytes);  // TODO: Null check

            var path = Path.Combine(tempDir, Path.GetRandomFileName());
            File.WriteAllText(path, processor != null ? processor(text) : text, Encoding.UTF8);

            this.Log().Info("Repaired {0}/{1}", branchName, relativePath);

            // XXX: Files added this way must be relative to the wd for no 
            // particular reason
            var relativeTempPath = path.Replace(repoRootDir, "");
            newTree.Remove(relativePath);
            newTree.Add(relativePath, relativeTempPath, mode);
            return path;
        }

        IObservable<Unit> repairWorkingDir(IEnumerable<Tuple<string, Stream>> filesInWd, string workingDir, HeuristicTreeInformation dirAnalysis)
        {
            return Observable.Start(() => {
                var lockList = filesInWd.ToDictionary(k => k.Item1, v => v.Item2);

                dirAnalysis.BadEncodingFiles.Keys.ForEach(relativePath => 
                    processWorkingDirectoryFile(workingDir, relativePath, lockList));

                dirAnalysis.BadLineEndingFiles.Keys.ForEach(relativePath =>
                    processWorkingDirectoryFile(workingDir, relativePath, lockList,
                        s => LineEndingInfo.FixLineEndingsForString(s, dirAnalysis.LineEndingType))
                );
            }, RxApp.TaskpoolScheduler);
        }

        void processWorkingDirectoryFile(string repoRoot, string relativePath, Dictionary<string, Stream> lockList, Func<string, string> processor = null)
        {
            var path = Path.Combine(repoRoot, relativePath);

            if (inNuGetPackagesDir(relativePath)) {
                return;
            }

            Stream inputFile;
            if (lockList.ContainsKey(path)) {
                inputFile = lockList[path];
            } else {
                inputFile = File.OpenRead(path);
            }

            var ms = new MemoryStream();
            inputFile.CopyTo(ms);

            var bytes = ms.ToArray();
            var text = CoreUtility.GuessEncodingForBytes(bytes).GetString(bytes);  // TODO: Null check
            var source = Path.GetTempFileName();

            File.WriteAllText(source, processor != null ? processor(text) : text, Encoding.UTF8);
            this.Log().Info("Repaired WorkingDir/{0}", relativePath);

            inputFile.Dispose();
            if (lockList.ContainsKey(path)) lockList.Remove(path);
            File.Copy(source, path, true);           
        }

        bool inNuGetPackagesDir(string relativePath)
        {
            return Regex.IsMatch(relativePath, @"^packages[\\/]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }
    }
}