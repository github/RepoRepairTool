using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Akavache;
using GitHub.Helpers;
using Ninject;
using Ninject.Extensions.Conventions;
using Ninject.Extensions.Conventions.BindingGenerators;
using Ninject.Extensions.Logging.NLog2;
using Ninject.Syntax;
using ReactiveUI;
using ReactiveUI.Routing;
using RepoRepairTool.ViewModels;

namespace RepoRepairTool.ViewModels
{
    public interface IAppState : IReactiveNotifyPropertyChanged
    {
        string CurrentRepo { get; set; }
        Dictionary<string, HeuristicTreeInformation> BranchInformation { get; set; }
        HeuristicTreeInformation WorkingDirectoryInformation { get; set; }
    }

    public class AppBootstrapper : ReactiveObject, IScreen, IAppState
    {
        string _CurrentRepo;
        public string CurrentRepo {
            get { return _CurrentRepo; }
            set { this.RaiseAndSetIfChanged(x => x.CurrentRepo, value); }
        }

        Dictionary<string, HeuristicTreeInformation> _BranchInformation;
        public Dictionary<string, HeuristicTreeInformation> BranchInformation {
            get { return _BranchInformation; }
            set { this.RaiseAndSetIfChanged(x => x.BranchInformation, value); }
        }

        HeuristicTreeInformation _WorkingDirectoryInformation;
        public HeuristicTreeInformation WorkingDirectoryInformation {
            get { return _WorkingDirectoryInformation; }
            set { this.RaiseAndSetIfChanged(x => x.WorkingDirectoryInformation, value); }
        }

        public IRoutingState Router { get; protected set; }

        public AppBootstrapper(IKernel kernel = null, IRoutingState router = null)
        {
            kernel = kernel ?? createStandardKernel();
            Router = router ?? new RoutingState();

            RxApp.ConfigureServiceLocator(
                (t, s) => kernel.Get(t, s), (t, s) => kernel.GetAll(t, s));

            Router.Navigate.Execute(RxApp.GetService<IDropRepoViewModel>());
        }

        IKernel createStandardKernel()
        {
            var ret = new StandardKernel();

            ret.Bind<IScreen>().ToConstant(this);
            ret.Bind<IAppState>().ToConstant(this);

            bindAllViewModels(ret);

            return ret;
        }

        void bindAllViewModels(IKernel kernel)
        {
            Assembly.GetExecutingAssembly().GetTypes()
                .Where(x => x.Namespace.Contains("ViewModels") && x.IsInterface)
                .ForEach(x => {
                    if (!x.Name.Contains("ViewModel")) {
                        return;
                    }

                    // Foo.ViewModels.IBarViewModel => Foo.ViewModels.BarViewModel
                    var targetType = Type.GetType(x.FullName.Replace(x.Name, x.Name.Substring(1)));
                    this.Log().Info("Wiring {0} to {1}", x, targetType);
                    kernel.Bind(x).To(targetType);

                    // IViewForViewModel<Foo.ViewModels.BarViewModel> => Foo.Views.BarView
                    Type viewType;
                    try {
                        var viewTypeName = targetType.FullName
                            .Replace(".ViewModels.", ".Views.")
                            .Replace(targetType.Name, targetType.Name.Replace("Model", ""));
                        viewType = Type.GetType(viewTypeName);

                        if (viewType == null) throw new Exception("No View, tried to find " + viewTypeName);
                    } catch (Exception ex) {
                        this.Log().WarnException("Couldn't find view for " + targetType.FullName, ex);
                        return;
                    }

                    var viewInterface = (typeof(IViewForViewModel<>)).MakeGenericType(targetType);
                    this.Log().Info("Wiring {0} to {1}", viewInterface, viewType);
                    kernel.Bind(viewInterface).To(viewType);
                });
        }
    }
}