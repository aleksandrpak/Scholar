using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Scholar.Common.Tools;
using Scholar.ViewModels;

namespace Scholar.Views
{
    /// <summary>
    /// Interaction logic for ViewRequests.xaml
    /// </summary>
    public partial class RequestsView 
    {
        private RequestsViewModel ViewModel
        {
            get { return (RequestsViewModel)DataContext; }
        }

        public RequestsView()
        {
            InitializeComponent();
        }

        #region Private Handlers

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                dgRequests.RestoreLayoutFromXml("RequestsView_layout.xml");
            }
            catch (Exception exception)
            {
                Log.Current.Error(exception);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                dgRequests.SaveLayoutToXml("RequestsView_layout.xml");
            }
            catch (Exception exception)
            {
                Log.Current.Error(exception);
            }
        }

        private void WindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                ViewModel.RefreshSource();
            }
            else if (e.Key == Key.Delete)
            {
                var dialogResult = MessageBox.Show("Вы действительно хотите удалить запросы?", "Удаление запросов", MessageBoxButton.YesNo);
                if (dialogResult == MessageBoxResult.No)
                {
                    return;
                }

                ViewModel.DeleteSelected();
            }
        }

        #endregion
    }
}
