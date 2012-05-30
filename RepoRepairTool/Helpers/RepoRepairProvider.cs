using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
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
                branchAnalysis.BadEncodingFiles.Keys.ForEach(relativePath => processTreeEntry(oldTree, newTree, relativePath));
                branchAnalysis.BadLineEndingFiles.Keys.ForEach(relativePath => 
                    processTreeEntry(oldTree, newTree, relativePath, s => LineEndingInfo.FixLineEndingsForString(s, branchAnalysis.LineEndingType)));

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
            }, RxApp.TaskpoolScheduler);
        }

        void processTreeEntry(Tree originalTree, TreeDefinition newTree, string relativePath, Func<string, string> processor = null)
        {
            var mode = originalTree[relativePath].Mode;
            var bytes = ((Blob)originalTree[relativePath].Target).Content;
            var text = CoreUtility.GuessEncodingForBytes(bytes).GetString(bytes);  // TODO: Null check

            var path = Path.GetTempFileName();
            File.WriteAllText(path, processor != null ? processor(text) : text, Encoding.UTF8);

            newTree.Remove(relativePath);
            newTree.Add(relativePath, path, mode);
            File.Delete(path);           
        }

        IObservable<Unit> repairWorkingDir(IEnumerable<Tuple<string, Stream>> filesInWd, string workingDir, HeuristicTreeInformation dirAnalysis)
        {
            return Observable.Start(() => {
                var lockList = filesInWd.ToDictionary(k => k.Item1, v => v.Item2);

                dirAnalysis.BadEncodingFiles.Keys.ForEach(relativePath => 
                    processWorkingDirectoryFile(Path.Combine(workingDir, relativePath), lockList));

                dirAnalysis.BadLineEndingFiles.Keys.ForEach(relativePath =>
                    processWorkingDirectoryFile(Path.Combine(workingDir, relativePath), lockList,
                        s => LineEndingInfo.FixLineEndingsForString(s, dirAnalysis.LineEndingType))
                );
            }, RxApp.TaskpoolScheduler);
        }

        void processWorkingDirectoryFile(string path, Dictionary<string, Stream> lockList, Func<string, string> processor = null)
        {
            Stream inputFile;
            if (lockList.ContainsKey(path)) {
                inputFile = lockList[path];
            } else {
                this.Log().Warn("File wasn't in lock list: {0}", path);
                inputFile = File.OpenRead(path);
            }

            var ms = new MemoryStream();
            lockList[path].CopyTo(ms);

            var bytes = ms.ToArray();
            var text = CoreUtility.GuessEncodingForBytes(bytes).GetString(bytes);  // TODO: Null check
            var source = Path.GetTempFileName();

            File.WriteAllText(source, processor != null ? processor(text) : text, Encoding.UTF8);

            inputFile.Dispose();
            File.Copy(source, path, true);           
        }
    }
}