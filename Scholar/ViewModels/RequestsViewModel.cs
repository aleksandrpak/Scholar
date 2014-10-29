using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Scholar.Common.Database;
using Scholar.Common.Tools;
using Scholar.Rows;

namespace Scholar.ViewModels
{
    internal class RequestsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        #region Private Members

        private ICommand _stopCommand;
        private ICommand _deleteCommand;

        private ObservableCollection<RequestRow> _requests;

        #endregion

        #region Public Properties

        public ICommand StopCommand
        {
            get { return _stopCommand ?? (_stopCommand = new RelayCommand(Stop, CanStop)); }
        }

        public ICommand DeleteCommand
        {
            get { return _deleteCommand ?? (_deleteCommand = new RelayCommand(Delete)); }
        }

        public ObservableCollection<RequestRow> Requests
        {
            get { return _requests ?? new ObservableCollection<RequestRow>(); }
            private set
            {
                _requests = value;
                OnPropertyChanged("Requests");
            }
        }

        public ObservableCollection<RequestRow> SelectedRequests { get; set; }

        #endregion

        #region Public Constructors

        public RequestsViewModel()
        {

            RefreshSource();

            SelectedRequests = new ObservableCollection<RequestRow>();
        }

        #endregion

        #region Private Methods

        private bool CanStop(object parameter)
        {
            if (parameter == null || parameter.GetType() != typeof(Guid))
                return false;

            var request = Requests.SingleOrDefault(i => i.SessionId == (Guid)parameter);
            return request != null && request.CanStop;
        }

        private void Stop(object parameter)
        {
            if (!CanStop(parameter))
                return;

            var sessionId = (Guid)parameter;

            using (var context = new ScholarDatabaseEntities { CommandTimeout = 600 })
            {
                foreach (var request in context.Requests
                        .Where(i => i.SessionId == sessionId && i.Response == null)
                        .OrderBy(i => i.RequestId)
                        .ThenBy(i => i.IsAdvanced)
                        .Skip(1))
                {
                    context.DeleteObject(request);
                }

                context.SaveChanges();
            }

            RefreshSource();
        }

        private void Delete(object parameter)
        {
            Delete(parameter, true);
        }

        private void Delete(object parameter, bool refreshGrid)
        {
            if (parameter == null || parameter.GetType() != typeof(Guid))
                return;

            var sessionId = (Guid)parameter;
            using (var context = new ScholarDatabaseEntities { CommandTimeout = 600 })
            {
                var isFirstSkipped = false;
                foreach (var databaseRequest in context.Requests
                        .Where(i => i.SessionId == sessionId)
                        .OrderBy(i => i.RequestId)
                        .ThenBy(i => i.IsAdvanced))
                {
                    databaseRequest.IsHidden = true;

                    if (databaseRequest.Response == null)
                    {
                        if (isFirstSkipped)
                            context.DeleteObject(databaseRequest);

                        isFirstSkipped = true;
                    }
                }

                context.SaveChanges();
            }

            if (refreshGrid)
                RefreshSource();
        }

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, e);
            }
        }

        #endregion

        #region Public Methods

        public void RefreshSource()
        {
            using (var context = new ScholarDatabaseEntities { CommandTimeout = 600 })
            {
                Requests = new ObservableCollection<RequestRow>(context.Requests
                    .Where(i => !i.IsHidden)
                    .Select(i => new
                    {
                        i.SessionId,
                        i.Search,
                        i.Response,
                        Results = i.Results.Count(),
                        i.StartTime,
                        i.IsAdvanced
                    })

                    .GroupBy(i => new { i.SessionId, i.Search })
                    .ToList()
                    .Select(i => new RequestRow
                    {
                        SessionId = i.Key.SessionId,
                        Search = i.Key.Search,
                        ProcessedPercent =
                            ((double)i.Count(j => (j.Response != null && !j.IsAdvanced) || ((j.Response == null || j.Response == "skipped") && j.IsAdvanced)) /
                            i.Count()).ToString("P2"),
                        ProcessedPages = i.Count(j => (j.Response != null && !j.IsAdvanced) || ((j.Response == null || j.Response == "skipped") && j.IsAdvanced)),
                        PageLimit = i.Count(j => !j.IsAdvanced) * 100 + i.Count(j => j.IsAdvanced), // ToDo: change to limit when references will be generated
                        Results = i.Sum(j => j.Results),
                        StartTime = i.Min(j => j.StartTime).ToString("yyyy-MM-dd HH:mm:ss")
                    })
                    .ToList());
            }
        }

        public void DeleteSelected()
        {
            foreach (var request in SelectedRequests)
            {
                Delete(request.SessionId, false);
            }

            RefreshSource();
        }

        #endregion
    }
}
