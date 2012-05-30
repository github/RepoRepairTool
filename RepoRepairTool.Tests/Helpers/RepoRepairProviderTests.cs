using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using LibGit2Sharp;
using Newtonsoft.Json;
using Ninject;
using Ninject.MockingKernel.NSubstitute;
using ReactiveUI;
using RepoRepairTool.Helpers;
using RepoRepairTool.ViewModels;
using Should;
using Xunit;
using Xunit.Extensions;

namespace RepoRepairTool.Tests.Helpers
{
    public class RepoRepairProviderTests : IEnableLogger
    {
        [Fact]
        public void BrokenRemoteBranchesNeedTrackingBranches()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void CleanRepositoryShouldntBeChanged()
        {
            throw new NotImplementedException();
        }

        [Theory]
        [InlineData("callisto-heuer")]
        [InlineData("dapper-dot-net")]
        [InlineData("FluentValidation")]
        [InlineData("JabbR")]
        public void IntegrationFixturesShouldHaveACleanAnalysisReport(string repoName)
        {
            var kernel = new NSubstituteMockingKernel();

            using (var repo = IntegrationTestHelper.GetRepositoryFromFixture(repoName)) {
                kernel.Bind<IRepoAnalysisProvider>().To<RepoAnalysisProvider>();
                kernel.Bind<IRepoRepairProvider>().To<RepoRepairProvider>();

                var analysis = kernel.Get<IRepoAnalysisProvider>().AnalyzeRepo(repo.Value.Info.WorkingDirectory).First();

                analysis.BranchAnalysisResults
                    .All(x => (x.Value.BadLineEndingFiles.Any() || x.Value.BadEncodingFiles.Any()) && x.Value.TotalFilesExamined > 0)
                    .ShouldBeTrue();

                var fixture = kernel.Get<IRepoRepairProvider>();
                fixture.RepairRepo(repo, analysis, RepoRepairOptions.IgnoreRemoteBranches).First();

                var finalAnalysis = kernel.Get<IRepoAnalysisProvider>().AnalyzeRepo(repo.Value.Info.WorkingDirectory).First();
                this.Log().Info("Final analysis for repaired: {0} {1}",
                    repo.Value.Info.WorkingDirectory, JsonConvert.SerializeObject(finalAnalysis, Formatting.Indented));

                finalAnalysis.BranchAnalysisResults
                    .All(x => !x.Value.BadLineEndingFiles.Any() && !x.Value.BadEncodingFiles.Any() && x.Value.TotalFilesExamined > 0)
                    .ShouldBeTrue();
            }
        }

        [Fact]
        public void IgnoreNuGetPackagesDirectory()
        {
            throw new NotImplementedException();
        }
    }

    [Flags]
    public enum RepoRepairOptions {
        IgnoreRemoteBranches = 0x1,
    }

    public interface IRepoRepairProvider : IEnableLogger
    {
        IObservable<Unit> RepairRepo(DisposableContainer<Repository> repo, RepoAnalysisResult analysis, RepoRepairOptions ignoreRemoteBranches);
    }

    public class RepoRepairProvider : IRepoRepairProvider
    {
        public IObservable<Unit> RepairRepo(DisposableContainer<Repository> repo, RepoAnalysisResult analysis, RepoRepairOptions ignoreRemoteBranches)
        {
            throw new NotImplementedException();
        }
    }
}