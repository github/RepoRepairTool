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
using System.Windows.Navigation;
using System.Windows.Shapes;
using ReactiveUI.Routing;
using RepoRepairTool.ViewModels;

namespace RepoRepairTool.Views
{
    /// <summary>
    /// Interaction logic for BranchInformationView.xaml
    /// </summary>
    public partial class BranchInformationView : UserControl, IViewForViewModel<BranchInformationViewModel>
    {
        public BranchInformationViewModel ViewModel {
            get { return (BranchInformationViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(BranchInformationViewModel), typeof(BranchInformationView), new UIPropertyMetadata(null));

        object IViewForViewModel.ViewModel { 
            get { return ViewModel; }
            set { ViewModel = (BranchInformationViewModel)value; } 
        }

        public BranchInformationView()
        {
            this.InitializeComponent();
        }
    }
}
