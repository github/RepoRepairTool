using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using GitHub.Helpers;
using LibGit2Sharp;
using ReactiveUI;
using RepoRepairTool.ViewModels;

namespace RepoRepairTool.Helpers
{
    [Flags]
    public enum RepoRepairOptions {
        IgnoreRemoteBranches = 0x1 << 0,
        IgnoreWorkingDirectory = 0x1 << 1,
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

                repairWd = Observable.Defer(() =>
                    repairWorkingDir(allWdFiles, analysis.BranchAnalysisResults[Constants.WorkingDirectory]));
            }

            return repo.ProcessAllBranchesAsync(branch => repairBranch(branch, analysis.BranchAnalysisResults[branch.Name], options))
                .Select(_ => Unit.Default)
                .Merge(repairWd)
                .Aggregate(Unit.Default, (acc, _) => acc);
        }

        IObservable<Unit> repairBranch(Branch branch, HeuristicTreeInformation branchAnalysis, RepoRepairOptions options)
        {
            if (options.HasFlag(RepoRepairOptions.IgnoreRemoteBranches) && branch.IsRemote && !branch.IsTracking) {
                return Observable.Return(Unit.Default);
            }

            return Observable.Throw<Unit>(new NotImplementedException());
        }

        IObservable<Unit> repairWorkingDir(IEnumerable<Tuple<string, Stream>> filesInWd, HeuristicTreeInformation dirAnalysis)
        {
            return Observable.Throw<Unit>(new NotImplementedException());
        }
    }
}