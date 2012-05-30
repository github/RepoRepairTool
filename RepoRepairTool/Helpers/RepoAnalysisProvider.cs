using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using GitHub.Extensions;
using GitHub.Helpers;
using LibGit2Sharp;
using ReactiveUI;
using ReactiveUI.Xaml;

namespace RepoRepairTool.ViewModels
{
    public static class Constants
    {
        public const string WorkingDirectory = "Working Directory";
    }

    public interface IRepoAnalysisProvider
    {
        IObservable<RepoAnalysisResult> AnalyzeRepo(string repo);
    }

    public static class RepositoryMixin
    {
        public static IEnumerable<string> GetAllFilesInWorkingDirectory(this Repository repo)
        {
            var path = repo.Info.WorkingDirectory;
            return Enumerable.Concat(
                    repo.Index.RetrieveStatus().Select(x => Path.Combine(path, x.FilePath)),
                    repo.Index.Select(x => Path.Combine(path, x.Path)))
                .Distinct();
        }

        public static IEnumerable<Tuple<string, Stream>> OpenAllFilesInWorkingDirectory(this Repository repo, FileAccess access)
        {
            return GetAllFilesInWorkingDirectory(repo)
                .Where(File.Exists)
                .Select(x => Tuple.Create(x, safeOpenFile(x, access)))
                .Where(x => x.Item2 != null)
                .ToArray();
        }

        public static IObservable<Tuple<string, TResult>> ProcessAllBranchesAsync<TResult>(this Repository repo, Func<Branch, IObservable<TResult>> selector, int parallelism = 2)
        {
            return repo.Branches.ToObservable()
                .Select(branch => Observable.Defer(() =>
                    selector(branch)).Select(result => Tuple.Create(branch.Name, result)))
                .Merge(parallelism);
        }

        static Stream safeOpenFile(string fileName, FileAccess access)
        {
            var fi = new FileInfo(fileName);
            if (fi.Length == 0) {
                return null;
            }

            return new Func<Stream>(() => File.Open(fileName, FileMode.Open, access)).Retry(3);
        }
    }

    public class RepoAnalysisProvider : IRepoAnalysisProvider, IEnableLogger
    {
        public IObservable<RepoAnalysisResult> AnalyzeRepo(string path)
        {
            Repository repo;

            try {
                repo = new Repository(path);
            } catch (Exception ex) {
                return Observable.Throw<RepoAnalysisResult>(new Exception("This doesn't appear to be a Git repository", ex));
            }

            var scanAllBranches = repo.ProcessAllBranchesAsync(branch => 
                Observable.Start(() => branch.Tip.Tree.AnalyzeRepository(false), RxApp.TaskpoolScheduler));

            var scanWorkingDirectory = Observable.Defer(() => Observable.Start(() => {
                var allFiles = repo.OpenAllFilesInWorkingDirectory(FileAccess.Read);

                var ret = Tuple.Create(Constants.WorkingDirectory, TreeWalkerMixin.AnalyzeRepository(allFiles, false));

                allFiles.ForEach(x => x.Item2.Dispose());
                return ret;
            }));

            return scanAllBranches.Merge(scanWorkingDirectory)
                .Aggregate(new Dictionary<string, HeuristicTreeInformation>(),
                    (acc, x) => { acc[x.Item1] = x.Item2; return acc; })
                .Finally(() => repo.Dispose())
                .Select(x => new RepoAnalysisResult() { RepositoryPath = path, BranchAnalysisResults = x });
        }
    }

    public class RepoAnalysisResult
    {
        public string RepositoryPath { get; set; }
        public Dictionary<string, HeuristicTreeInformation> BranchAnalysisResults { get; set; }
    }
}