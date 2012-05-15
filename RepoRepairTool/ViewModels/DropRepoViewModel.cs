using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using GitHub;
using GitHub.Helpers;
using LibGit2Sharp;
using ReactiveUI;
using ReactiveUI.Routing;
using ReactiveUI.Xaml;
using GitHub.Helpers;

namespace RepoRepairTool.ViewModels
{
    public interface IDropRepoViewModel : IRoutableViewModel
    {
        string CurrentRepoPath { get; }

        ReactiveAsyncCommand AnalyzeRepo { get; }
        Dictionary<string, HeuristicTreeInformation> RepoAnalysis { get; } 
            
        Visibility RepairButtonVisibility { get; }
        ReactiveCommand RepairButton { get; }
    }

    public class DropRepoViewModel : ReactiveObject, IDropRepoViewModel
    {
        public ReactiveAsyncCommand AnalyzeRepo { get; protected set; }

        ObservableAsPropertyHelper<string> _CurrentRepoPath;
        public string CurrentRepoPath { get { return _CurrentRepoPath.Value; } }

        ObservableAsPropertyHelper<Dictionary<string, HeuristicTreeInformation>> _RepoAnalysis;
        public Dictionary<string, HeuristicTreeInformation> RepoAnalysis { get { return _RepoAnalysis.Value; } }

        ObservableAsPropertyHelper<Visibility> _RepairButtonVisibility;
        public Visibility RepairButtonVisibility { get { return _RepairButtonVisibility.Value; } }

        public ReactiveCommand RepairButton { get; protected set; }

        public string UrlPathSegment {
            get { return "drop"; }
        }

        public IScreen HostScreen { get; protected set; }

        public DropRepoViewModel(IScreen hostScreen)
        {
            HostScreen = hostScreen;

            AnalyzeRepo = new ReactiveAsyncCommand();
            var scanResult = AnalyzeRepo.RegisterAsyncObservable(pathObj => {
                Repository repo;

                CoreUtility.ExtractLibGit2();

                try {
                    repo = new Repository((string)pathObj);
                } catch (Exception ex) {
                    UserError.Throw("This doesn't appear to be a Git repository", ex);
                    return Observable.Empty<Tuple<string, Dictionary<string, HeuristicTreeInformation>>>();
                }

                string path = (string) pathObj;

                var scanAllBranches = repo.Branches.Select(branch =>
                    Observable.Defer(() =>
                        Observable.Start(() =>
                            branch.Tip.Tree.AnalyzeRepository(true))
                        .Select(x => new { Branch = branch.Name, Result = x })))
                    .Merge(2);

                var scanWorkingDirectory = Observable.Defer(() => Observable.Start(() =>
                    new { Branch = "Working Directory", Result = TreeWalkerMixin.AnalyzeRepository(
                        allFilesInDirectory(path).Select(x => Tuple.Create(x, (Stream)File.OpenRead(x))), true) }));

                return scanAllBranches.Merge(scanWorkingDirectory)
                    .Aggregate(new Dictionary<string, HeuristicTreeInformation>(),
                        (acc, x) => { acc[x.Branch] = x.Result; return acc; })
                    .Select(x => Tuple.Create(path, x));
            });

            scanResult.Select(x => x != null ? x.Item1 : null).ToProperty(this, x => x.CurrentRepoPath);
            scanResult.Select(x => x != null ? x.Item2 : null).ToProperty(this, x => x.RepoAnalysis);
        }

        IEnumerable<string> allFilesInDirectory(string rootPath)
        {
            var di = new DirectoryInfo(rootPath);
            return di.GetDirectories().Where(x => x.Name != ".git")
                .SelectMany(x => allFilesInDirectory(x.FullName))
                .Concat(di.GetFiles().Select(x => x.FullName));
        }
    }
}