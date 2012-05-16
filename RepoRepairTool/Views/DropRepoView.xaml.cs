using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ReactiveUI;
using ReactiveUI.Routing;
using RepoRepairTool.ViewModels;

namespace RepoRepairTool.Views
{
    /// <summary>
    /// Interaction logic for DropRepoView.xaml
    /// </summary>
    public partial class DropRepoView : UserControl, IViewForViewModel<DropRepoViewModel>
    {
        public DropRepoViewModel ViewModel {
            get { return (DropRepoViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(DropRepoViewModel), typeof(DropRepoView), new UIPropertyMetadata(null));

        object IViewForViewModel.ViewModel { 
            get { return ViewModel; }
            set { ViewModel = (DropRepoViewModel)value; } 
        }

        public DropRepoView()
        {
            this.InitializeComponent();
            MessageBus.Current.Listen<string>("DropRepoViewState")
                .Subscribe(x => VisualStateManager.GoToElementState(LayoutRoot, x, true));
        }
    }
}
