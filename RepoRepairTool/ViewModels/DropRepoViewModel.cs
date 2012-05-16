using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using GitHub;
using GitHub.Extensions;
using GitHub.Helpers;
using LibGit2Sharp;
using Ninject;
using ReactiveUI;
using ReactiveUI.Routing;
using ReactiveUI.Xaml;
using GitHub.Helpers;
using RepoRepairTool.Helpers;

namespace RepoRepairTool.ViewModels
{
    public interface IDropRepoViewModel : IRoutableViewModel
    {
        string CurrentRepoPath { get; }

        ReactiveAsyncCommand AnalyzeRepo { get; }
        ReactiveCollection<IBranchInformationViewModel> BranchInformation { get; }
            
        Visibility RepairButtonVisibility { get; }
        ReactiveCommand RepairButton { get; }
    }

    public interface IBranchInformationViewModel : IReactiveNotifyPropertyChanged
    {
        string BranchName { get; }
        HeuristicTreeInformation Model { get; }
        Visibility NeedsRepair { get; }

        string BadEncodingInfoHeader { get; }
        string BadEndingsInfoHeader { get; }
    }

    public interface IRepoAnalysisProvider
    {
        IObservable<Tuple<string, Dictionary<string, HeuristicTreeInformation>>> AnalyzeRepo(string repo);
    }

    public class DropRepoViewModel : ReactiveObject, IDropRepoViewModel
    {
        public ReactiveAsyncCommand AnalyzeRepo { get; protected set; }

        ObservableAsPropertyHelper<string> _CurrentRepoPath;
        public string CurrentRepoPath { get { return _CurrentRepoPath.Value; } }

        ObservableAsPropertyHelper<ReactiveCollection<IBranchInformationViewModel>> _BranchInformation;
        public ReactiveCollection<IBranchInformationViewModel> BranchInformation { get { return _BranchInformation.Value; } }

        ObservableAsPropertyHelper<Visibility> _RepairButtonVisibility;
        public Visibility RepairButtonVisibility { get { return _RepairButtonVisibility.Value; } }

        public ReactiveCommand RepairButton { get; protected set; }

        public string UrlPathSegment {
            get { return "drop"; }
        }

        public IScreen HostScreen { get; protected set; }

        public DropRepoViewModel(IScreen hostScreen, IAppState appState, [Optional] IRepoAnalysisProvider analyzeFunc)
        {
            HostScreen = hostScreen;

            AnalyzeRepo = new ReactiveAsyncCommand();

            CoreUtility.ExtractLibGit2();

            IObservable<Tuple<string, Dictionary<string, HeuristicTreeInformation>>> scanResult;
            if (analyzeFunc != null) {
                scanResult = AnalyzeRepo.RegisterAsyncObservable(x => analyzeFunc.AnalyzeRepo((string) x));
            } else {
                scanResult = AnalyzeRepo.RegisterAsyncObservable(pathObj => {
                    Repository repo;

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
                                branch.Tip.Tree.AnalyzeRepository(false), RxApp.TaskpoolScheduler)
                            .Select(x => new { Branch = branch.Name, Result = x })))
                        .Merge(2);

                    var scanWorkingDirectory = Observable.Defer(() => Observable.Start(() => {
                        var allFiles = allFilesInDirectory(new DirectoryInfo(path))
                            .Select(x => Tuple.Create(x, safeOpenFileRead(x)))
                            .Where(x => x.Item2 != null)
                            .ToArray();

                        var ret = new {
                            Branch = Constants.WorkingDirectory,
                            Result = TreeWalkerMixin.AnalyzeRepository(allFiles, false)
                        };

                        allFiles.ForEach(x => x.Item2.Dispose());
                        return ret;
                    }));

                    return scanAllBranches.Merge(scanWorkingDirectory)
                        .Aggregate(new Dictionary<string, HeuristicTreeInformation>(),
                            (acc, x) => { acc[x.Branch] = x.Result; return acc; })
                        .Select(x => Tuple.Create(path, x));
                });
            }

            scanResult.Select(x => x.Item1).ToProperty(this, x => x.CurrentRepoPath);
            scanResult
                .Select(x => x.Item2.Select(y => (IBranchInformationViewModel)new BranchInformationViewModel(y.Key, y.Value)))
                .Select(x => new ReactiveCollection<IBranchInformationViewModel>(x))
                .ToProperty(this, x => x.BranchInformation);

            this.WhenAny(x => x.BranchInformation, x => x.Value != null ? Visibility.Visible : Visibility.Hidden)
                .ToProperty(this, x => x.RepairButtonVisibility);

            RepairButton = new ReactiveCommand();
            RepairButton.Subscribe(_ => {
                appState.BranchInformation = BranchInformation.Where(x => x.BranchName != Constants.WorkingDirectory).ToArray();
                appState.WorkingDirectoryInformation = BranchInformation.First(x => x.BranchName == Constants.WorkingDirectory).Model;
                appState.CurrentRepo = CurrentRepoPath;

                HostScreen.Router.Navigate.Execute(RxApp.GetService<IRepairViewModel>());
            });

            this.WhenNavigatedTo(() =>
                MessageBus.Current.Listen<string>("DropFolder").Subscribe(path => AnalyzeRepo.Execute(path)));
        }

        IEnumerable<string> allFilesInDirectory(DirectoryInfo rootPath)
        {
            return rootPath.SafeGetDirectories().Where(x => x.Name != ".git")
                .SelectMany(allFilesInDirectory)
                .Concat(rootPath.SafeGetFiles().Select(x => x.FullName))
                .ToArray();
        }

        Stream safeOpenFileRead(string fileName)
        {
            try {
                return new Func<Stream>(() => File.OpenRead(fileName)).Retry(3);
            } catch (Exception ex) {
                return null;
            }
        }
    }

    public class BranchInformationViewModel : ReactiveObject, IBranchInformationViewModel
    {
        [DataMember]
        public string BranchName { get; protected set; }
        [DataMember]
        public HeuristicTreeInformation Model { get; protected set; }
        [DataMember]
        public Visibility NeedsRepair { get; protected set; }

        [DataMember]
        public string BadEncodingInfoHeader { get; protected set; }
        [DataMember]
        public string BadEndingsInfoHeader { get; protected set; }

        public BranchInformationViewModel(string branchName, HeuristicTreeInformation treeInformation)
        {
            BranchName = branchName;
            Model = treeInformation;

            if (Model.BadEncodingFiles == null || Model.BadLineEndingFiles == null) {
                NeedsRepair = Visibility.Collapsed;
                BadEncodingInfoHeader = "Unknown number of files incorrectly encoded";
                BadEndingsInfoHeader = "Unknown number of files with incorrect line endings";
                return;
            }

            if (Model.TotalFilesExamined == 0 || Model.LineEndingType == LineEndingType.Unsure) {
                NeedsRepair = Visibility.Collapsed;
                BadEncodingInfoHeader = "No text files found";
                BadEndingsInfoHeader = "No text files found";
                return;
            }

            // > 5% mixed line endings or any UTF-16 files => needs repair
            bool shouldRepair = Model.BadEncodingFiles.Count > 0 ||
                (double) Model.BadLineEndingFiles.Count / Model.TotalFilesExamined > 0.05;

            NeedsRepair = shouldRepair ? Visibility.Visible : Visibility.Collapsed;

            BadEncodingInfoHeader = Model.BadEncodingFiles.Count > 0 ?
                String.Format("{0:P2} of files are not in UTF-8 encoding", (double)Model.BadEncodingFiles.Count / Model.TotalFilesExamined) :
                "All of the files are correctly encoded";

            BadEndingsInfoHeader = Model.BadLineEndingFiles.Count > 0 ?
                String.Format("{0:P2} of files have a different line ending type than the repo", (double)Model.BadLineEndingFiles.Count / Model.TotalFilesExamined) :
                "All of the files have correct line endings";
        }
    }
}