using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using GitHub;
using GitHub.Helpers;
using NSubstitute;
using Newtonsoft.Json;
using Ninject;
using Ninject.MockingKernel.NSubstitute;
using ReactiveUI;
using ReactiveUI.Routing;
using ReactiveUI.Xaml;
using RepoRepairTool.ViewModels;
using Should;
using Xunit;
using Xunit.Extensions;

namespace RepoRepairTool.Tests.ViewModels
{
    public class DropRepoViewModelTests : IEnableLogger
    {
        [Theory]
        [InlineData("Derp")]
        [InlineData("C:\\Windows\\System32\\Notepad.exe")]
        [InlineData(null)]
        public void AnalyzeRepoWithThingsThatArentReposShouldFail(string input)
        {
            UserError error = null;

            var fixture = new DropRepoViewModel(null, null, null);
            using(UserError.OverrideHandlersForTesting(x => { error = x; return RecoveryOptionResult.CancelOperation; })) {
                fixture.AnalyzeRepo.Execute(input);
            }

            error.ShouldNotBeNull();
            fixture.RepairButtonVisibility.ShouldNotEqual(Visibility.Visible);
        }

        [Fact]
        public void ScanningOurselvesShouldReturnResults()
        {
            var repoRootPath = CoreUtility.FindRepositoryRoot(IntegrationTestHelper.GetIntegrationTestRootDirectory());

            var fixture = new DropRepoViewModel(null, null, null);
            fixture.AnalyzeRepo.Execute(repoRootPath);
            fixture.AnalyzeRepo.ItemsInflight.Where(x => x == 0).First();

            fixture.BranchInformation.ShouldNotBeNull();
            fixture.CurrentRepoPath.ShouldEqual(repoRootPath);

            this.Log().Info(JsonConvert.SerializeObject(fixture.BranchInformation, Formatting.Indented));

            // We should have both the WD and the branches
            fixture.BranchInformation.Any(x => x.BranchName.ToLowerInvariant().Contains("working")).ShouldBeTrue();
            fixture.BranchInformation.Any(x => x.BranchName.ToLowerInvariant().Contains("master")).ShouldBeTrue();

            // We should have examined some files
            fixture.BranchInformation.All(x => x.BadEncodingInfoHeader != null).ShouldBeTrue();
            fixture.BranchInformation.All(x => x.BadEndingsInfoHeader != null).ShouldBeTrue();
            fixture.BranchInformation.All(x => x.Model.TotalFilesExamined > 0).ShouldBeTrue();

            // .gitignored files shouldn't show up
            var working = fixture.BranchInformation.First(x => x.BranchName == Constants.WorkingDirectory);
            working.Model.BadLineEndingFiles.Any(x => x.Key.ToLowerInvariant().Contains("_resharper")).ShouldBeFalse();
        }
        
        [Fact]
        public void AppStateShouldBeSetOnRepairClicked()
        {
            var router = new RoutingState();
            var kernel = new NSubstituteMockingKernel();

            RxApp.ConfigureServiceLocator((t,s) => kernel.Get(t,s), (t,s) => kernel.GetAll(t,s));

            var branchInfo = new Dictionary<string, HeuristicTreeInformation>() {
                { "Working Directory", new HeuristicTreeInformation("derp", true)}
            };

            kernel.Bind<IDropRepoViewModel>().To<DropRepoViewModel>();
            kernel.Get<IRepoAnalysisProvider>().AnalyzeRepo(null)
                .ReturnsForAnyArgs(Observable.Return(Tuple.Create("foo", branchInfo)));
            kernel.Get<IScreen>().Router.Returns(router);

            IRoutableViewModel latestVm = null;
            router.ViewModelObservable().Subscribe(x => latestVm = x);

            var fixture = kernel.Get<IDropRepoViewModel>();
            fixture.AnalyzeRepo.Execute("foo");

            fixture.RepairButtonVisibility.ShouldEqual(Visibility.Visible);
            fixture.RepairButton.Execute(null);

            var result = kernel.Get<IAppState>();

            (latestVm is IRepairViewModel).ShouldBeTrue();
            result.CurrentRepo.ShouldEqual("foo");
            result.BranchInformation.ShouldNotBeNull();
        }
    }
}