using System;
using ReactiveUI;
using ReactiveUI.Routing;

namespace RepoRepairTool.ViewModels
{
    public interface IRepairViewModel : IRoutableViewModel
    {
    }

    public class RepairViewModel : ReactiveObject, IRepairViewModel
    {
        public string UrlPathSegment { get { return "repair"; } }
        public IScreen HostScreen { get; protected set; }

        public RepairViewModel(IScreen hostScreen)
        {
            HostScreen = hostScreen;
        }
    }
}