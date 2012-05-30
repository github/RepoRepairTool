using System;
using System.Reactive.Linq;
using System.Windows;
using Ninject.MockingKernel.NSubstitute;
using ReactiveUI.Routing;
using ReactiveUI.Xaml;
using RepoRepairTool.ViewModels;
using Should;
using Xunit;
using Xunit.Extensions;

namespace RepoRepairTool.Tests.ViewModels
{
    public class RepoAnalysisProviderTests
    {
        [Theory]
        [InlineData("Z:\\____Derp")]                        // Not found
        [InlineData("C:\\Windows\\System32\\Notepad.exe")]  // It's a file
        [InlineData("C:\\System Volume Information")]       // Access Denied
        [InlineData(null)]
        public void AnalyzeRepoWithThingsThatArentReposShouldFail(string input)
        {
            var router = new RoutingState();
            var kernel = new NSubstituteMockingKernel();
            UserError error = null;

            var fixture = new RepoAnalysisProvider();

            Assert.Throws<Exception>(() => {
                fixture.AnalyzeRepo(input).First();
            });
        }
    }
}