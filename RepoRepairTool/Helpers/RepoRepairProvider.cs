using System;
using System.Reactive;
using LibGit2Sharp;
using ReactiveUI;
using RepoRepairTool.ViewModels;

namespace RepoRepairTool.Helpers
{
    [Flags]
    public enum RepoRepairOptions {
        IgnoreRemoteBranches = 0x1,
    }

    public interface IRepoRepairProvider : IEnableLogger
    {
        IObservable<Unit> RepairRepo(Repository repo, RepoAnalysisResult analysis, RepoRepairOptions ignoreRemoteBranches);
    }

    public class RepoRepairProvider : IRepoRepairProvider
    {
        public IObservable<Unit> RepairRepo(Repository repo, RepoAnalysisResult analysis, RepoRepairOptions ignoreRemoteBranches)
        {
            throw new NotImplementedException();
        }
    }
}