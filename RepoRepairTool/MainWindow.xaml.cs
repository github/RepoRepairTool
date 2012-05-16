using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using ReactiveUI;
using ReactiveUI.Xaml;
using RepoRepairTool.ViewModels;

namespace RepoRepairTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IEnableLogger
    {
        public AppBootstrapper ViewModel { get; protected set; }

        public MainWindow()
        {
            ViewModel = new AppBootstrapper();
            InitializeComponent();
            AllowDrop = true;
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            DropTargetHelper.DragEnter(this, e.Data, e.GetPosition(this), e.Effects);
            e.Handled = true;
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            DropTargetHelper.DragLeave();
            e.Handled = true;
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            DropTargetHelper.DragOver(e.GetPosition(this), e.Effects);
            e.Handled = true;
        }

        protected override void OnDrop(DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            DropTargetHelper.Drop(e.Data, e.GetPosition(this), e.Effects);
            e.Handled = true;

            string[] files = null;
            try {
                files = (string[])e.Data.GetDataEx("FileNameW");
            } catch (Exception ex) {
                this.Log().InfoException("Something weird happened on drop", ex);
            }

            if (files == null || files.Length != 1 || !Directory.Exists(files[0])) {
                UserError.Throw("Drop a directory onto the window");
                return;
            }

            MessageBus.Current.SendMessage(files[0], "DropFolder");
        }
    }
}