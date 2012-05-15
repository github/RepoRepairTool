using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using GitHub;
using Newtonsoft.Json;
using ReactiveUI;
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

            using(UserError.OverrideHandlersForTesting(x => { error = x; return RecoveryOptionResult.CancelOperation; })) {
                var fixture = new DropRepoViewModel(null);
                fixture.AnalyzeRepo.Execute(input);
            }

            error.ShouldNotBeNull();
        }

        [Fact]
        public void ScanningOurselvesShouldReturnResults()
        {
            var repoRootPath = CoreUtility.FindRepositoryRoot(IntegrationTestHelper.GetIntegrationTestRootDirectory());

            var fixture = new DropRepoViewModel(null);
            fixture.AnalyzeRepo.Execute(repoRootPath);
            fixture.AnalyzeRepo.ItemsInflight.Where(x => x == 0).First();

            fixture.RepoAnalysis.ShouldNotBeNull();
            fixture.CurrentRepoPath.ShouldEqual(repoRootPath);

            this.Log().Info(JsonConvert.SerializeObject(fixture.RepoAnalysis, Formatting.Indented));
            fixture.RepoAnalysis.Keys.Any(x => x.ToLowerInvariant().Contains("working")).ShouldBeTrue();
            fixture.RepoAnalysis.Keys.Any(x => x.ToLowerInvariant().Contains("master")).ShouldBeTrue();
        }
    }
}
