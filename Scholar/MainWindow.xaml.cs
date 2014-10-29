using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.EntityClient;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Printing;
using DevExpress.XtraGrid;
using Microsoft.Win32;

using Scholar.Common.Database;
using Scholar.Common.Tools;
using Scholar.Properties;
using Scholar.ViewModels;
using Scholar.Views;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using System.Windows.Documents;
using System.Windows.Data;
using System.Diagnostics;
using System.Globalization;

namespace Scholar
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly List<Thread> _threads = new List<Thread>();

        private string[] _lines;

        private const string FilteredItemsString = "Отфильтровано результатов: {0}";

        public MainWindow()
        {
            InitializeComponent();
        }

        private TableView View { get { return (dgData == null ? null : (TableView)dgData.View); } }

        #region Classes

        // ReSharper disable UnusedAutoPropertyAccessor.Local

        private struct FieldItem
        {
            public bool IsSelected { get; set; }
            public string Field { get; set; }
        }

        private struct SearchItem
        {
            public Guid SessionId { get; set; }
            public bool IsSelected { get; set; }
            public string Search { get; set; }
        }

        private struct BaseRow
        {
            public int NameId { get; set; }
            public int ArticleId { get; set; }
            public int RequestId { get; set; }
            public string Initials { get; set; }
            public string LastName { get; set; }
            public string Article { get; set; }
            public int Cited { get; set; }
            public int Year { get; set; }
            public string Edition { get; set; }
            public string Publisher { get; set; }
            public string Field { get; set; }
            public string Url { get; set; }
        }

        private struct DefaultRow
        {
            [Display(AutoGenerateField = false)]
            public Guid Id { get; set; }
            public List<int[]> ResultIds { get; set; }

            [Display(ShortName = @"Инициалы")]
            public string Initials { get; set; }

            [Display(ShortName = @"Фамилия")]
            public string LastName { get; set; }

            [Display(AutoGenerateField = false)]
            public string KirLastName { get; set; }

            [Display(AutoGenerateField = false)]
            public string LatLastName { get; set; }

            [Display(ShortName = @"Статья")]
            public string Article { get; set; }

            [Display(ShortName = @"Цитирования")]
            public int Cited { get; set; }

            [Display(ShortName = @"Год")]
            public int Year { get; set; }

            [Display(ShortName = @"Журнал")]
            public string Edition { get; set; }

            [Display(ShortName = @"Издательство")]
            public string Publisher { get; set; }

            [Display(ShortName = @"Ссылка")]
            public string Url { get; set; }
        }

        private struct NameRow
        {
            [Display(AutoGenerateField = false)]
            public Guid Id { get; set; }
            public List<int[]> ResultIds { get; set; }

            [Display(ShortName = @"Инициалы")]
            public string Initials { get; set; }

            [Display(ShortName = @"Фамилия")]
            public string LastName { get; set; }

            [Display(AutoGenerateField = false)]
            public string KirLastName { get; set; }

            [Display(AutoGenerateField = false)]
            public string LatLastName { get; set; }

            [Display(ShortName = @"Индекс Хирша")]
            public int HIndex { get; set; }

            [Display(ShortName = @"Всего статей")]
            public int Articles { get; set; }

            [Display(ShortName = @"Всего цитирований")]
            public int TotalCited { get; set; }

            [Display(ShortName = @"Год последней статьи")]
            public int LastYear { get; set; }

            [Display(ShortName = @"Область знаний")]
            public string Field { get; set; }
        }

        private struct ArticleRow
        {
            [Display(AutoGenerateField = false)]
            public Guid Id { get; set; }
            public List<int[]> ResultIds { get; set; }

            [Display(ShortName = @"Соавторов")]
            public int Authors { get; set; }

            [Display(ShortName = @"Статья")]
            public string Article { get; set; }

            [Display(ShortName = @"Цитирования")]
            public int Cited { get; set; }

            [Display(ShortName = @"Год")]
            public int Year { get; set; }

            [Display(ShortName = @"Журнал")]
            public string Edition { get; set; }

            [Display(ShortName = @"Издательство")]
            public string Publisher { get; set; }

            [Display(ShortName = @"Ссылка")]
            public string Url { get; set; }
        }

        // ReSharper restore UnusedAutoPropertyAccessor.Local

        #endregion

        #region Private Methods

        private static void HandleException(Exception exception)
        {
            Log.Error(exception, DateTime.Now);
            MessageBox.Show(exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static bool CompareObjects(object a, object b)
        {
            if (a == null || b == null)
                return false;

            var aType = a.GetType();
            var bType = b.GetType();

            var aProperties = aType.GetProperties();
            var aInitialsProperty = aProperties.SingleOrDefault(i => i.Name == "Initials");
            var aLastNameProperty = aProperties.SingleOrDefault(i => i.Name == "LastName");
            var aArticleProperty = aProperties.SingleOrDefault(i => i.Name == "Article");
            var aYearProperty = aProperties.SingleOrDefault(i => i.Name == "Year");
            var aEditionProperty = aProperties.SingleOrDefault(i => i.Name == "Edition");

            var bProperties = bType.GetProperties();
            var bInitialsProperty = bProperties.SingleOrDefault(i => i.Name == "Initials");
            var bLastNameProperty = bProperties.SingleOrDefault(i => i.Name == "LastName");
            var bArticleProperty = bProperties.SingleOrDefault(i => i.Name == "Article");
            var bYearProperty = bProperties.SingleOrDefault(i => i.Name == "Year");
            var bEditionProperty = bProperties.SingleOrDefault(i => i.Name == "Edition");

            bool? isEqualAuthor = null;
            if (aInitialsProperty != null && aLastNameProperty != null &&
                bInitialsProperty != null && bLastNameProperty != null)
            {
                var aInitials = (string)aInitialsProperty.GetValue(a, null);
                var aLastName = (string)aLastNameProperty.GetValue(a, null);

                var bInitials = (string)bInitialsProperty.GetValue(b, null);
                var bLastName = (string)bLastNameProperty.GetValue(b, null);

                isEqualAuthor = (aInitials == bInitials && aLastName == bLastName);
            }

            if (aArticleProperty != null && bArticleProperty != null &&
                aYearProperty != null && bYearProperty != null &&
                aEditionProperty != null && bEditionProperty != null)
            {
                var aArticle = (string)aArticleProperty.GetValue(a, null);
                var bArticle = (string)bArticleProperty.GetValue(b, null);

                var aYear = (int)aYearProperty.GetValue(a, null);
                var bYear = (int)bYearProperty.GetValue(b, null);

                var aEdition = (string)aEditionProperty.GetValue(a, null);
                var bEdition = (string)bEditionProperty.GetValue(b, null);

                if (aArticle == bArticle && aYear == bYear && aEdition == bEdition)
                {
                    if (isEqualAuthor.HasValue)
                        return isEqualAuthor.Value;

                    return true;
                }

                if (isEqualAuthor.HasValue)
                    return false;
            }

            return isEqualAuthor.HasValue && isEqualAuthor.Value;
        }

        private void Search()
        {
            try
            {
                var search = TextBoxSearch.Text.Trim();

                if (string.IsNullOrWhiteSpace(search))
                {
                    return;
                }

                int pageLimit;
                if (!int.TryParse(TextBoxPageLimit.Text, out pageLimit) ||
                    pageLimit < 1)
                {
                    MessageBox.Show("Введите целое число большее нуля", "Ограничение поиска");
                    return;
                }

                using (var context = new ScholarDatabaseEntities { CommandTimeout = 600 })
                {
                    string language;

                    var request = GetRequest(search, out language);
                    var sessionId = Guid.NewGuid();

                    // Because of years
                    var multiplier = (DateTime.Now.Year - 2007);
                    pageLimit *= multiplier;

                    // ToDo: Remove after normal regular expressions are ready
                    for (var yearHigh = DateTime.Now.Year; yearHigh >= 2008; yearHigh--)
                    {
                        var yearLow = (yearHigh == 2008 ? 1985 : yearHigh);

                        for (var i = 0; i < pageLimit / multiplier; i++)
                        {
                            context.Requests.AddObject(new Requests
                            {
                                Request = request + string.Format("&start={0}&as_ylo={1}&as_yhi={2}&num=100", i * 100, yearLow, yearHigh),
                                Language = language,
                                SessionId = sessionId,
                                Search = search,
                                PageLimit = pageLimit,
                                StartTime = DateTime.Now
                            });
                        }
                    }

                    context.SaveChanges();
                    TextBoxSearch.Text = string.Empty;
                }

                var list = new List<String>();
                var assemblies = new Queue<System.Reflection.Assembly>();
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    assemblies.Enqueue(a);
                }

                while (assemblies.Count > 0)
                {
                    var assembly = assemblies.Dequeue();

                    if (!list.Contains(assembly.FullName) && assembly.FullName.Contains("DevExpress"))
                        list.Add(assembly.FullName);

                    foreach (var refAssembly in assembly.GetReferencedAssemblies())
                    {
                        if (!list.Contains(refAssembly.FullName) && refAssembly.FullName.Contains("DevExpress"))
                            list.Add(refAssembly.FullName);
                    }
                }

                RefreshStatus();
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        private void LoadData()
        {
            try
            {
                if (cbGrouping == null || cbFields == null)
                    return;

                var tag = ((ComboBoxItem)cbGrouping.SelectedItem).Tag;
                if (tag == null)
                    return;

                var layoutName = tag.ToString();
                var layoutFilename = string.Format("{0}_layout.xml", layoutName);

                object[] selectedRows = null;
                GridSortInfo[] selectedSorting = null;
                object[] previousHeaders = null;

                if (dgData != null)
                {
                    if (View.SelectedRows.Count > 0)
                    {
                        selectedRows = new object[View.SelectedRows.Count];
                        View.SelectedRows.CopyTo(selectedRows, 0);
                    }

                    if (dgData.SortInfo.Count > 0)
                    {
                        selectedSorting = new GridSortInfo[dgData.SortInfo.Count];
                        dgData.SortInfo.CopyTo(selectedSorting, 0);
                        previousHeaders = dgData.Columns.Select(i => i.Header).ToArray();
                    }

                    if (dgData.Columns.Count > 0)
                        dgData.SaveLayoutToXml(layoutFilename);
                }

                using (var context = new ScholarDatabaseEntities { CommandTimeout = 600 })
                {
                    var source = GetSource(context);

                    switch (tag.ToString())
                    {
                        case "Default":
                            ShowDefaultView(source);
                            break;

                        case "Name":
                            ShowByNames(source);
                            break;

                        case "Article":
                            ShowByArticles(source);
                            break;

                        default:
                            throw new ArgumentException();
                    }

                    if (dgData != null)
                    {
                        dgData.Columns.Clear();
                        dgData.PopulateColumns();

                        foreach (var column in dgData.Columns)
                        {
                            column.ReadOnly = true;
                            column.Name = column.FieldName;
                        }

                        if (selectedSorting != null && selectedSorting.Length > 0 &&
                            previousHeaders.SequenceEqual(dgData.Columns.Select(i => i.Header).ToArray()))
                        {
                            foreach (var sortInfo in selectedSorting)
                                dgData.SortInfo.Add(sortInfo);
                        }

                        if (selectedRows != null && selectedRows.Any())
                        {
                            View.BeginSelection();
                            View.ClearSelection();

                            for (int i = 0; i < dgData.VisibleRowCount; i++)
                            {
                                var rowHandle = dgData.GetRowHandleByVisibleIndex(i);
                                var item = dgData.GetRow(rowHandle);

                                if (selectedRows.Any(j => CompareObjects(j, item)))
                                {
                                    View.SelectRow(rowHandle);
                                    View.ScrollIntoView(rowHandle);
                                }
                            }

                            View.EndSelection();
                        }

                        lbFilteredItems.Content = string.Format(FilteredItemsString, ((IList)dgData.ItemsSource).Count);

                        if (!File.Exists(layoutFilename))
                            layoutFilename = null;

                        if (!string.IsNullOrWhiteSpace(layoutFilename))
                        {
                            try
                            {
                                dgData.RestoreLayoutFromXml(layoutFilename);
                            }
                            catch (Exception exception)
                            {
                                Log.Current.Error(exception);
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        private static string[][] GetAuthors(IEnumerable<string> names)
        {
            var authors = new List<string[]>();

            var lastName = string.Empty;
            var initials = new List<string>();
            foreach (var name in names.Select(i => i.ToLower()))
            {
                if (name.Length <= 2)
                {
                    initials.Add(name);
                }
                else
                {
                    if (initials.Count > 0)
                    {
                        var author = new List<string> { string.IsNullOrWhiteSpace(lastName) ? name : lastName };
                        author.AddRange(initials);

                        authors.Add(author.ToArray());
                        initials.Clear();

                        if (!string.IsNullOrWhiteSpace(lastName))
                        {
                            lastName = name;
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(lastName))
                        {
                            authors.Add(new[] { lastName });
                        }

                        lastName = name;
                    }
                }
            }

            if (authors.Count > 0 && authors.Last().First() != lastName)
            {
                if (initials.Count > 0)
                {
                    var author = new List<string> { lastName };
                    author.AddRange(initials);

                    authors.Add(author.ToArray());
                }
                else if (!string.IsNullOrEmpty(lastName))
                    authors.Add(new[] { lastName });
            }
            else if (initials.Count > 0)
            {
                var author = new List<string>();
                if (!string.IsNullOrEmpty(lastName))
                    author.Add(lastName);

                author.AddRange(initials.ToArray());

                authors.Add(author.ToArray());
            }
            else if (authors.Count == 0 && !string.IsNullOrWhiteSpace(lastName))
            {
                authors.Add(new[] { lastName });
            }

            return authors.ToArray();
        }

        private List<BaseRow> GetSource(ScholarDatabaseEntities context)
        {
            var language = ((ComboBoxItem)cbLanguage.SelectedItem).Tag.ToString();
            var isOriginal = (language == "Original");

            var names = tbAuthor.Text
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(i => i.Trim())
                .ToArray();

            var keywords = tbKeywords.Text
                .ToLower()
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(i => i.Trim()
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(j => j.Trim())
                    .ToArray())
                .ToArray();

            var authors = GetAuthors(names);
            if (authors.Length == 1 && !authors[0].Any(i => i.Length > 2))
                return new List<BaseRow>();

            if (!isOriginal)
            {
                names = tbAuthor.Text
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(i => GetTransformedText(i.Trim(), language))
                    .ToArray();

                authors = authors.Union(GetAuthors(names)).ToArray();
            }

            int startYear;
            if (!int.TryParse(tbStartYear.Text.Trim(), out startYear) ||
                startYear < 0)
            {
                startYear = 0;
                tbStartYear.Text = string.Empty;
            }

            int endYear;
            if (!int.TryParse(tbEndYear.Text.Trim(), out endYear) ||
                endYear < 0)
            {
                endYear = 0;
                tbEndYear.Text = string.Empty;
            }

            if (endYear < startYear)
            {
                endYear = startYear;
                tbEndYear.Text = tbStartYear.Text;
            }

            var nameIds = new List<int>();
            foreach (var author in authors)
            {
                var authorTemp = author;
                var firstAuthor = authorTemp.FirstOrDefault();

                nameIds.AddRange(context.Names
                    .Where(i =>
                        ((!authorTemp.OrderBy(j => j).Skip(1).Any()) || authorTemp.OrderByDescending(j => j.Length).Skip(1)
                            .Count(j => (j.Length == i.Initials.Length ?
                                i.Initials.ToLower() == j : (!(j.Length > i.Initials.Length) && i.Initials.ToLower().Substring(0, 1) == j.Substring(0, 1)))) > 0) &&
                        (firstAuthor != null && (firstAuthor.Length < 3 || i.LastName.ToLower().Contains(firstAuthor))))
                    .Select(i => i.NameId)
                    .ToArray());
            }

            for (var h = 0; h < keywords.Length; h++)
            {
                keywords[h] = TextTool.GetModifiedArray(keywords[h]);
            }

            var articleIds = new List<int>();
            var articlesList = new List<int[]>();
            foreach (var keywordLine in keywords)
            {
                var currentArticles = new List<int>();
                foreach (var keyword in keywordLine)
                {
                    var currentKeyword = keyword.ToLower();

                    currentArticles.AddRange(context.Articles
                        .Where(i =>
                            i.Article.ToLower().Contains(currentKeyword) ||
                            i.Abstract.Contains(currentKeyword) ||
                            i.Keywords.Any(j => j.Keyword.ToLower().Contains(currentKeyword)))
                        .Select(i => i.ArticleId)
                        .ToList());
                }

                articlesList.Add(currentArticles.Distinct().ToArray());
            }

            if (articlesList.Count > 0)
            {
                articleIds.AddRange(articlesList[0]);
                for (var i = 1; i < articlesList.Count; i++)
                {
                    articleIds = articleIds.Intersect(articlesList[i]).ToList();
                }
            }

            var isArticleIdsEmpty = (keywords.Length == 0);
            var isAuthorsEmpty = (authors.Length == 0);
            var isStartYearEmpty = (startYear == 0);
            var isEndYearEmpty = (endYear == 0);
            var isArticleEmpty = (string.IsNullOrWhiteSpace(tbArticle.Text));

            var articleWords = new string[0];
            if (!isArticleEmpty)
            {
                articleWords = tbArticle.Text
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(i => i.Trim().ToLower())
                    .ToArray();
            }

            articleWords = TextTool.GetModifiedArray(articleWords);

            var selectedFields = (cbFields.Items.Cast<object>()
                                          .Where(item => ((FieldItem) item).IsSelected)
                                          .Select(item => ((FieldItem) item).Field)).ToArray();

            var selectedSearches = (cbSessions.Items.Cast<object>()
                                              .Where(item => ((SearchItem) item).IsSelected)
                                              .Select(item => ((SearchItem) item).SessionId)).ToArray();

            var isFieldsEmpty = selectedFields.Length == 0;
            var isSessionsEmpty = selectedSearches.Length == 0;

            var tag = ((ComboBoxItem)cbLanguage.SelectedItem).Tag;
            var isRussian = (tag != null && tag.ToString() == "English");
            var isReferal = ((ComboBoxItem)cbEditions.SelectedItem).Tag.ToString() == "Refered";
            var isList = ((ComboBoxItem)cbEditions.SelectedItem).Tag.ToString() == "List";
            var russianLastNames = TextTool.RussianLastNames();

            var baseSource = new List<BaseRow>();

            if (isAuthorsEmpty)
            {
                baseSource = context.Results
                        .Where(j =>
                                (isSessionsEmpty || selectedSearches.Contains(j.Requests.SessionId)) &&
                                (!isReferal || j.Articles.Editions.IsReferal) &&
                                (!isList || j.Articles.Editions.IsList) &&
                                (isFieldsEmpty || selectedFields.Contains(j.Articles.Editions.Field)) &&
                                (isArticleIdsEmpty || articleIds.Contains(j.ArticleId)) &&
                                (isStartYearEmpty || j.Articles.Year >= startYear) &&
                                (isEndYearEmpty || j.Articles.Year <= endYear) &&
                                (isArticleEmpty || articleWords.Count(k => j.Articles.Article.ToLower().Contains(k)) > 0))
                        .Select(i => new
                        {
                            Name = i.Names,
                            Article = i.Articles,
                            Edition = i.Articles.Editions,
                            i.Articles.Editions.Publishers.Publisher,
                            Request = i.Requests
                        })
                        .ToArray()
                        .Where(i => (!isRussian ||
                            i.Name.LastName.All(k => (k >= 'А' && k <= 'я') || k == 'Ё' || k == 'ё') ||
                            russianLastNames.Any(k => i.Name.LastName.EndsWith(k))))
                        .Select(j => new BaseRow
                        {
                            NameId = j.Name.NameId,
                            ArticleId = j.Article.ArticleId,
                            RequestId = j.Request.RequestId,
                            Initials = j.Name.Initials,
                            LastName = j.Name.LastName,
                            Article = j.Article.Article,
                            Cited = j.Article.Cited,
                            Year = j.Article.Year,
                            Edition = j.Edition.Edition,
                            Publisher = j.Publisher,
                            Field = j.Edition.Field,
                            Url = j.Article.Url
                        }).ToList();
            }
            else
            {
                var source = context.Names.Where(i => nameIds.Contains(i.NameId));

                foreach (var result in source)
                {
                    baseSource.AddRange(result.Results
                        .Where(j =>
                                (isSessionsEmpty || selectedSearches.Contains(j.Requests.SessionId)) &&
                                (!isReferal || j.Articles.Editions.IsReferal) &&
                                (!isList || j.Articles.Editions.IsList) &&
                                (isFieldsEmpty || selectedFields.Contains(j.Articles.Editions.Field)) &&
                                (isArticleIdsEmpty || articleIds.Contains(j.ArticleId)) &&
                                (isStartYearEmpty || j.Articles.Year >= startYear) &&
                                (isEndYearEmpty || j.Articles.Year <= endYear) &&
                                (isArticleEmpty || articleWords.Count(k => j.Articles.Article.ToLower().Contains(k)) > 0) &&
                                (!isRussian ||
                                    result.LastName.All(k => (k >= 'А' && k <= 'я') || k == 'Ё' || k == 'ё') ||
                                    russianLastNames.Any(k => result.LastName.EndsWith(k))))
                        .Select(j => new BaseRow
                        {
                            NameId = j.NameId,
                            ArticleId = j.ArticleId,
                            RequestId = j.RequestId,
                            Initials = result.Initials,
                            LastName = result.LastName,
                            Article = j.Articles.Article,
                            Cited = j.Articles.Cited,
                            Year = j.Articles.Year,
                            Edition = j.Articles.Editions.Edition,
                            Publisher = j.Articles.Editions.Publishers.Publisher,
                            Field = j.Articles.Editions.Field,
                            Url = j.Articles.Url
                        }).ToList());
                }
            }

            return baseSource;
        }

        private void ShowDefaultView(IEnumerable<BaseRow> source)
        {
            dgData.ItemsSource = new ObservableCollection<DefaultRow>(source
                .Select(i => new DefaultRow
                {
                    Id = Guid.NewGuid(),
                    ResultIds = new List<int[]> { new[] { i.NameId, i.ArticleId, i.RequestId } },
                    Initials = i.Initials,
                    LastName = i.LastName,
                    KirLastName = TextTool.TransformToKir(i.LastName),
                    LatLastName = TextTool.TransformToLat(i.LastName),
                    Article = i.Article,
                    Cited = i.Cited,
                    Year = i.Year,
                    Edition = i.Edition,
                    Publisher = i.Publisher,
                    Url = i.Url
                }).ToList());
        }

        private void ShowByNames(List<BaseRow> source)
        {
            using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
            {
                var nameIds = new DataTable();
                nameIds.Columns.Add("Item");

                foreach (var nameId in source.Select(i => i.NameId).Distinct())
                {
                    nameIds.Rows.Add(nameId);
                }

                var nameIdsParameter = new SqlParameter("@NameIds", SqlDbType.Structured)
                {
                    Value = nameIds,
                    TypeName = "[dbo].[IntArray]"
                };

                var connection = ((EntityConnection)entities.Connection).StoreConnection;
                if (connection.State != ConnectionState.Open)
                    connection.Open();

                var command = connection.CreateCommand();
                command.CommandTimeout = 600;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "GetNamesResults";
                command.Parameters.Add(nameIdsParameter);

                source = new List<BaseRow>();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        source.Add(new BaseRow
                        {
                            NameId = reader.GetInt32(reader.GetOrdinal("NameId")),
                            ArticleId = reader.GetInt32(reader.GetOrdinal("ArticleId")),
                            RequestId = reader.GetInt32(reader.GetOrdinal("RequestId")),
                            Initials = reader.GetString(reader.GetOrdinal("Initials")),
                            LastName = reader.GetString(reader.GetOrdinal("LastName")),
                            Cited = reader.GetInt32(reader.GetOrdinal("Cited")),
                            Year = reader.GetInt32(reader.GetOrdinal("Year")),
                            Field = reader.GetString(reader.GetOrdinal("Field"))
                        });
                    }
                }

                var results = source
                    .GroupBy(i => new { i.Initials, i.LastName, i.Field })
                    .ToArray()
                    .Select(i => new
                    {
                        ResultIds = i.Select(j => new[] { j.NameId, j.ArticleId, j.RequestId }).ToList(),
                        Id = Guid.NewGuid(),
                        i.Key.Initials,
                        i.Key.LastName,
                        KirLastName = TextTool.TransformToKir(i.Key.LastName),
                        LatLastName = TextTool.TransformToLat(i.Key.LastName),
                        Articles = i.Select(j => new { j.ArticleId, j.Cited, j.Year }).ToList(),
                        i.Key.Field
                    })
                    .ToArray();

                var hIndexes = new Dictionary<Guid, int>();

                foreach (var result in results)
                {
                    var articles = result.Articles.OrderByDescending(i => i.Cited).ToArray();
                    var hIndex = Math.Min(articles.Length, articles.Min(i => i.Cited));

                    foreach (var article in articles)
                    {
                        if (hIndex < article.Cited &&
                            articles.Count(i => i.Cited >= article.Cited) >= article.Cited)
                        {
                            hIndex = article.Cited;
                        }
                    }

                    hIndexes.Add(result.Id, hIndex);
                }

                dgData.ItemsSource = new ObservableCollection<NameRow>(results
                    .Select(i => new NameRow
                    {
                        Id = Guid.NewGuid(),
                        ResultIds = i.ResultIds,
                        Initials = i.Initials,
                        LastName = i.LastName,
                        KirLastName = i.KirLastName,
                        LatLastName = i.LatLastName,
                        HIndex = hIndexes[i.Id],
                        Articles = i.Articles.Count(),
                        TotalCited = i.Articles.Sum(j => j.Cited),
                        LastYear = i.Articles.Max(j => j.Year),
                        Field = i.Field
                    })
                    .ToList());
            }
        }

        private void ShowByArticles(IEnumerable<BaseRow> source)
        {
            using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
            {
                var initialSource = source.Select(i => i.ArticleId).Distinct().ToArray();

                dgData.ItemsSource = new ObservableCollection<ArticleRow>(entities.Results
                    .Where(i => initialSource.Contains(i.ArticleId))
                    .Select(i => new
                    {
                        i.NameId,
                        i.ArticleId,
                        i.RequestId,
                        i.Articles.Article,
                        i.Articles.Cited,
                        i.Articles.Year,
                        i.Articles.Editions.Edition,
                        i.Articles.Editions.Publishers.Publisher,
                        i.Articles.Editions.Field,
                        i.Articles.Url
                    })
                    .ToArray()
                    .GroupBy(i => new
                    {
                        i.Article,
                        i.Cited,
                        i.Year,
                        i.Edition,
                        i.Publisher,
                        i.Url
                    })
                    .Select(i => new ArticleRow
                    {
                        Id = Guid.NewGuid(),
                        ResultIds = i.Select(j => new[] { j.NameId, j.ArticleId, j.RequestId }).ToList(),
                        Authors = i.Count(),
                        Article = i.Key.Article,
                        Cited = i.Key.Cited,
                        Year = i.Key.Year,
                        Edition = i.Key.Edition,
                        Publisher = i.Key.Publisher,
                        Url = i.Key.Url
                    }).ToList());
            }
        }

        private void RefreshStatus()
        {
            try
            {
                const string status = "Запросов в обработке: {0}";
                const string totalItems = "Всего результатов: {0}";

                int sessionsCount;
                using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
                {
                    sessionsCount = entities.Requests
                        .Where(i => (i.Response == null && !i.IsAdvanced) || (i.Response != null && i.Response != "skipped" && i.IsAdvanced))
                        .Join(entities.Requests, i => i.SessionId, j => j.SessionId, (i, j) => new
                            {
                                j.SessionId,
                                j.RequestId,
                                IsProcessed =
                                    (j.Response != null && !i.IsAdvanced) || ((i.Response == null || i.Response == "skipped") && i.IsAdvanced)
                            })
                        .Distinct()
                        .GroupBy(i => new { i.SessionId })
                        .Select(i => new
                            {
                                i.Key.SessionId,
                                ProcessedCount = i.Count(j => j.IsProcessed),
                                PageLimit = i.Count()
                            }).Count(i => i.ProcessedCount < i.PageLimit);

                    lbTotalItems.Content = string.Format(totalItems, entities.Results.Count());
                }

                LabelStatus.Content = string.Format(status, sessionsCount);
                if (sessionsCount > 0)
                {
                    _threads.RemoveAll(i => !i.IsAlive);
                    var thread = new Thread(Crawler.LaunchCrawler);
                    _threads.Add(thread);
                    thread.Start();
                }

                LoadFields();
                LoadSessions();
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        private void LoadFields()
        {
            try
            {
                cbFields.Items.Clear();

                using (var context = new ScholarDatabaseEntities { CommandTimeout = 600 })
                {
                    foreach (var field in context.Editions.Select(i => i.Field).Distinct())
                    {
                        cbFields.Items.Add(new FieldItem { IsSelected = false, Field = field });
                    }
                }
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        private void LoadSessions()
        {
            cbSessions.Items.Clear();

            using (var context = new ScholarDatabaseEntities { CommandTimeout = 600 })
            {
                foreach (var session in context.Requests.Where(i => i.Results.Count > 0).GroupBy(i => new { i.SessionId, i.Search }))
                {
                    cbSessions.Items.Add(new SearchItem
                    {
                        IsSelected = false,
                        SessionId = session.Key.SessionId,
                        Search = session.Key.Search
                    });
                }
            }
        }

        private string GetRequest(string search, out string language)
        {
            var searchString = Uri.EscapeDataString(search);

            switch (((ComboBoxItem)ComboBoxSearchEngine.SelectedItem).Tag.ToString())
            {
                case "GSRU":
                    language = "ru";
                    return string.Format(@"http://scholar.google.ru/scholar?q={0}&hl=ru", searchString);

                case "GSEN":
                    language = "en";
                    return string.Format(@"http://scholar.google.com/scholar?q={0}&hl=en", searchString);

                default:
                    throw new ArgumentException();
            }
        }

        private void LoadEditions()
        {
            try
            {
                const string regex = "\"(?<Edition>[^\"]+)\",\"(?<Field>[^\"]+)\",(?<IsReferal>\\d),(?<ISSN>\\d{0,8}),(?<Rating>[\\.\\d]+),\"(?<Country>[^\"]+)\"";

                using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
                {
                    var connection = ((EntityConnection)entities.Connection).StoreConnection;
                    if (connection.State != ConnectionState.Open)
                        connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandTimeout = 600;

                    command.CommandText = "UPDATE [dbo].[Editions] SET [Field] = N'Неизвестная область' WHERE [IsList] = 1";
                    command.ExecuteNonQuery();

                    command.CommandText = "UPDATE [dbo].[Editions] SET [IsReferal] = 0, [IsList] = 0";
                    command.ExecuteNonQuery();

                    var unknownPublisherId = entities.InsertPublisher("Неизвестное издательство").First();

                    foreach (var line in _lines)
                    {
                        var match = Regex.Match(line, regex, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);

                        var editionName = match.Groups["Edition"].Value;
                        if (string.IsNullOrWhiteSpace(editionName))
                            continue;

                        var edition = entities.Editions.FirstOrDefault(i => i.Edition.ToLower() == editionName.ToLower());
                        var country = match.Groups["Country"].Value;

                        var publisherId = (string.IsNullOrWhiteSpace(country) ?
                            unknownPublisherId :
                            entities.InsertPublisher(string.Format("Неизвестное издательство {0}", country)).First());

                        if (edition == null)
                        {
                            var editionId = entities.InsertEdition(editionName, publisherId).First();
                            edition = entities.Editions.FirstOrDefault(i => i.EditionId == editionId);
                        }

                        if (edition == null)
                            continue;

                        edition.Field = string.IsNullOrWhiteSpace(match.Groups["Field"].Value) ? "Неизвестная область" : match.Groups["Field"].Value;
                        edition.IsReferal = match.Groups["IsReferal"].Value == "1";
                        edition.IsList = true;

                        if (!string.IsNullOrWhiteSpace(match.Groups["ISSN"].Value))
                            edition.ISSN = Convert.ToInt64(match.Groups["ISSN"].Value);

                        if (!string.IsNullOrWhiteSpace(match.Groups["Rating"].Value))
                            edition.Rating = Convert.ToDecimal(match.Groups["Rating"].Value, new NumberFormatInfo { NumberDecimalSeparator = "." });

                        if (!string.IsNullOrWhiteSpace(country) && country != edition.Publishers.Country)
                            edition.Publishers.Country = country;

                        entities.SaveChanges();
                    }
                }
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        private static string GetTransformedText(string text, string destinationLanguage)
        {
            switch (destinationLanguage)
            {
                case "Russian":
                    return TextTool.TransformToKir(text);

                case "English":
                    return TextTool.TransformToLat(text);

                default:
                    throw new ArgumentException();
            }
        }

        #endregion

        #region Private Handlers

        private void WindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (TextBoxSearch.IsFocused ||
                TextBoxPageLimit.IsFocused ||
                ComboBoxSearchEngine.IsFocused)
            {
                Search();
            }
            else if (!tbKeywords.IsFocused)
            {
                LoadData();
            }
        }

        private void BtnLoadEditionsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog { DefaultExt = ".txt", Filter = "Text documents (.txt)|*.txt" };

                var result = openFileDialog.ShowDialog();
                if (!result.Value)
                {
                    return;
                }

                btnLoadEditions.IsEnabled = false;
                _lines = File.ReadAllLines(openFileDialog.FileName);

                var worker = new BackgroundWorker();
                worker.DoWork += Worker_DoWork;
                worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
                worker.RunWorkerAsync();
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnLoadEditions.IsEnabled = true;
            LoadFields();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            LoadEditions();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            if (!DatabaseUpdate.Update())
            {
                Close();
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(Settings.Default.State))
                {
                    Height = Settings.Default.Size.Height;
                    Width = Settings.Default.Size.Width;

                    Top = Settings.Default.Location.X;
                    Left = Settings.Default.Location.Y;

                    if (Settings.Default.State == WindowState.Maximized.ToString())
                    {
                        WindowState = WindowState.Maximized;
                    }
                }
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }

            //Settings.Default.ClearOld = true;
            //Settings.Default.Save();

            //if (Settings.Default.ClearOld)
            //{
            //    using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
            //    {
            //        // ToDo: Update logic
            //        foreach (var request in entities.Requests
            //            .Where(i =>
            //                (i.Response == null && i.SessionId != Guid.Empty) ||
            //                (i.Response != null && i.SessionId == Guid.Empty)))
            //        {
            //            entities.Requests.DeleteObject(request);
            //        }

            //        entities.SaveChanges();
            //    }

            //    Settings.Default.ClearOld = false;
            //    Settings.Default.Save();
            //}

            RefreshStatus();
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            Settings.Default.Size = new Size((int)Width, (int)Height);
            Settings.Default.Location = new Point((int)Top, (int)Left);
            Settings.Default.State = WindowState.ToString();
            Settings.Default.Save();

            if (dgData.Columns.Count > 0)
            {
                foreach (var column in dgData.Columns)
                {
                    dgData.ClearColumnFilter(column);
                }

                dgData.SaveLayoutToXml(string.Format("{0}_layout.xml", ((ComboBoxItem)cbGrouping.SelectedItem).Tag));
            }

            foreach (var thread in _threads)
            {
                thread.Abort();
            }
        }

        private void BtnCleanClick(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы действительно хотите очистить фильтр?", "Очистка фильтра",
                MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                return;
            }

            cbLanguage.SelectedIndex = 0;
            cbFields.SelectedIndex = 0;
            cbEditions.SelectedIndex = 0;

            tbAuthor.Text = string.Empty;
            tbStartYear.Text = string.Empty;
            tbEndYear.Text = string.Empty;
            tbArticle.Text = string.Empty;

            LoadData();
        }

        private void BtnClearDatabaseClick(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы действительно хотите очистить базу?", "Очистка базы",
                MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
                {
                    var connection = ((EntityConnection)entities.Connection).StoreConnection;
                    if (connection.State != ConnectionState.Open)
                        connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandTimeout = 600;

                    command.CommandText = "DELETE FROM [dbo].[Results]";
                    command.ExecuteNonQuery();

                    command.CommandText = "DELETE FROM [dbo].[Requests]";
                    command.ExecuteNonQuery();

                    command.CommandText = "DELETE FROM [dbo].[Articles]";
                    command.ExecuteNonQuery();
                }

                RefreshStatus();
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        private void BtnRefreshStatusClick(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
        }

        private void BtnSearchClick(object sender, RoutedEventArgs e)
        {
            Search();
        }

        private void CbEditionsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadData();
        }

        private void CbLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadData();
        }

        private void CbGroupingSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.RemovedItems.Count > 0 && dgData.Columns.Count > 0)
                {
                    foreach (var column in dgData.Columns)
                    {
                        dgData.ClearColumnFilter(column);
                    }

                    var layoutName = ((ComboBoxItem)e.RemovedItems[0]).Tag.ToString();
                    dgData.SaveLayoutToXml(string.Format("{0}_layout.xml", layoutName));
                }
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }

            LoadData();
        }

        private void BtnLoadClick(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void DgDataPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete)
                return;

            var dialogResult = MessageBox.Show("Удалить также из базы?", "Удаление", MessageBoxButton.YesNoCancel);
            if (dialogResult == MessageBoxResult.Cancel)
            {
                return;
            }

            var isDeleteDatabase = (dialogResult == MessageBoxResult.Yes);

            try
            {
                if (isDeleteDatabase)
                {
                    using (var context = new ScholarDatabaseEntities { CommandTimeout = 600 })
                    {
                        foreach (var item in View.SelectedRows)
                        {
                            dynamic result = item;

                            var resultIds = (List<int[]>)result.ResultIds;
                            foreach (var resultId in resultIds)
                            {
                                var nameId = resultId[0];
                                var articleId = resultId[1];
                                var requestId = resultId[2];

                                context.Results.DeleteObject(context.Results.Single(i =>
                                                                                    i.NameId == nameId &&
                                                                                    i.ArticleId == articleId &&
                                                                                    i.RequestId == requestId));
                            }

                            context.SaveChanges();

                            foreach (var resultId in resultIds)
                            {
                                var nameId = resultId[0];
                                var articleId = resultId[1];

                                if (context.Results.Count(i => i.NameId == nameId) == 0)
                                {
                                    context.Names.DeleteObject(context.Names.Single(i => i.NameId == nameId));
                                }

                                if (context.Results.Count(i => i.ArticleId == articleId) == 0)
                                {
                                    var article = context.Articles.Single(i => i.ArticleId == articleId);
                                    var editionId = article.EditionId;
                                    context.Articles.DeleteObject(article);

                                    var articles = context.Articles.Where(i => i.EditionId == editionId);
                                    if (!articles.Any())
                                    {
                                        var edition = context.Editions.Single(i => i.EditionId == editionId);
                                        var publisherId = edition.PublisherId;
                                        context.Editions.DeleteObject(edition);

                                        var editions = context.Editions.Where(i => i.PublisherId == publisherId);
                                        if (!editions.Any())
                                        {
                                            context.Publishers.DeleteObject(context.Publishers.Single(i => i.PublisherId == publisherId));
                                        }
                                    }
                                }
                            }

                            context.SaveChanges();
                        }
                    }
                }

                dynamic source = dgData.ItemsSource;
                while (View.SelectedRows.Count > 0)
                {
                    dynamic item = View.SelectedRows[0];
                    foreach (var sourceItem in source)
                    {
                        if (sourceItem.Id == item.Id)
                        {
                            source.Remove(sourceItem);
                            break;
                        }
                    }
                }

                ((GridControl)sender).RefreshData();
                lbFilteredItems.Content = string.Format(FilteredItemsString, ((IList)dgData.ItemsSource).Count);
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        private void DgDataColumnsPopulated(object sender, RoutedEventArgs e)
        {
            if (dgData.Columns.Count <= 0)
                return;

            try
            {
                var lastNameColumn = dgData.Columns.FirstOrDefault(i => i.FieldName == "LastName");
                if (lastNameColumn != null)
                {
                    lastNameColumn.SortMode = ColumnSortMode.Custom;
                }

                var urlColumn = dgData.Columns.FirstOrDefault(i => i.FieldName == "Url");
                if (urlColumn != null)
                {
                    var urlBinding = new Binding(string.Format("Data.{0}", urlColumn.FieldName));
                    var dataTemplate = new DataTemplate();

                    var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
                    var hyperLinkFactory = new FrameworkElementFactory(typeof(Hyperlink));

                    hyperLinkFactory.SetValue(Hyperlink.NavigateUriProperty, urlBinding);
                    hyperLinkFactory.SetValue(Hyperlink.TargetNameProperty, "_blank");

                    var linkText = new FrameworkElementFactory(typeof(TextBlock));
                    linkText.SetValue(TextBlock.TextProperty, urlBinding);

                    hyperLinkFactory.AppendChild(linkText);

                    textBlockFactory.AppendChild(hyperLinkFactory);

                    dataTemplate.VisualTree = textBlockFactory;
                    urlColumn.CellTemplate = dataTemplate;
                }
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        private void DgDataCustomColumnSort(object sender, CustomColumnSortEventArgs e)
        {
            if ((string)e.Column.Header != "Фамилия" || cbLanguage.SelectedIndex <= 0)
                return;

            try
            {
                var list = ((IList)dgData.ItemsSource);
                if (list.Count <= 0)
                    return;

                var type = list[0].GetType();

                var propertyName = ((ComboBoxItem)cbLanguage.SelectedItem).Tag.ToString() == "Russian" ? "KirLastName" : "LatLastName";
                var property = type.GetProperty(propertyName);
                var firstValue = (string)property.GetValue(list[e.ListSourceRowIndex1], null);
                var secondValue = (string)property.GetValue(list[e.ListSourceRowIndex2], null);

                e.Result = Comparer<string>.Default.Compare(firstValue, secondValue);
                e.Handled = true;
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        private void DgDataPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var cell = View.GetCellElementByMouseEventArgs(e);
                if (cell != null)
                {
                    var columnData = (GridColumnData)cell.DataContext;
                    if (columnData.Column.Name == "Url")
                    {
                        dynamic dynamicCell = cell;
                        Process.Start(dynamicCell.DataContext.RowData.Row.Url);
                    }
                }
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        private void BtnViewStatusClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var requestsViewModel = new RequestsViewModel();
                var requestsView = new RequestsView { DataContext = requestsViewModel };

                requestsView.ShowDialog();
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }

            // ToDo: Refresh status in background
            //RefreshStatus();
        }

        private void BtnSaveToFileClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    FileName = "*",
                    DefaultExt = ".xlsx",
                    Filter = "Excel documents (.xlsx)|*.xlsx"
                };

                var result = dialog.ShowDialog();
                if (result == true)
                {
                    //View.ExportToText(dialog.FileName);

                    var link = new PrintableControlLink(View);
                    link.ExportToXlsx(dialog.FileName);
                }
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }
        }

        #endregion
    }
}
