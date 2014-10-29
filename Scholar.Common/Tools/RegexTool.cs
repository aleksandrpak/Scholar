using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using Scholar.Common.Database;
using Scholar.Common.Extensions;

namespace Scholar.Common.Tools
{
    public static class RegexTool
    {
        public static readonly Dictionary<string, string> RegexList;

        private const string NameGroup = "Name";
        private const string PublisherGroup = "Publisher";
        private const string EditionGroup = "Edition";
        private const string ArticleGroup = "Article";
        private const string YearGroup = "Year";
        private const string CitedGroup = "Cited";
        private const string UrlGroup = "Url";

        static RegexTool()
        {
            RegexList = new Dictionary<string, string>
            {
                { NameGroup, string.Format("(?<{0}>.+)", NameGroup) },
                { PublisherGroup, string.Format("(?<{0}>.+)", PublisherGroup) },
                { EditionGroup, string.Format("(?<{0}>.+)", EditionGroup) },
                { ArticleGroup, string.Format("(?<{0}>.+)", ArticleGroup) },
                { YearGroup, string.Format("(?<{0}>", YearGroup) + "\\d{2,4})" },
                { CitedGroup, string.Format("(?<{0}>\\d+)", CitedGroup) }
            };
        }

        private static void SaveName(ScholarDatabaseEntities context, int articleId, int requestId, string name)
        {
            var splittedName = TextTool.GetName(name);
            if (splittedName == null)
                return;

            var nameId = context.InsertName(splittedName[0], splittedName[1]).First();
            if (nameId != null)
                context.InsertResult(nameId.Value, articleId, requestId);
        }

        public static void PlanUrls(Requests request, string text, string regex)
        {
            var matches = Regex.Matches(text, regex, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
            if (matches.Count == 0)
                return;

            using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
            {
                var baseRequest = entities.Requests.First(i => i.SessionId == request.SessionId);
                var language = baseRequest.Language;
                var baseUrl = baseRequest.Request.Substring(0,
                    (baseRequest.Request.StartsWith("http://") ?
                        baseRequest.Request.IndexOf("/", 7, StringComparison.Ordinal) : baseRequest.Request.IndexOf("/", StringComparison.Ordinal)));

                foreach (Match match in matches)
                {
                    var urlGroup = match.Groups[UrlGroup];

                    if (!urlGroup.Success)
                    {
                        continue;
                    }

                    var url = Uri.EscapeUriString(Uri.UnescapeDataString(urlGroup.Captures[0].Value)
                        .Replace("&amp;", "&")
                        .Replace("&oe=CP1251", string.Empty)
                        .Replace("&as_sdt=0,5", string.Empty)
                        .Replace("&as_sdt=0", string.Empty));

                    if (url.StartsWith("/"))
                        url = baseUrl + url;

                    if (entities.Requests.Count(i => i.SessionId == request.SessionId && i.Request == url) > 0)
                    {
                        continue;
                    }

                    entities.Requests.AddObject(new Requests
                    {
                        Language = language,
                        Request = url,
                        SessionId = request.SessionId,
                        Response = null,
                        IsMatched = false,
                        IsParsed = false,
                        Search = request.Search,
                        PageLimit = request.PageLimit
                    });
                }

                entities.SaveChanges();
            }
        }

        public static bool FindMoreAuthorsAndEdition(Requests request, string html, string text)
        {
            var values = request.Response.Split(';');
            if (values.Length != 5)
            {
                return false;
            }

            var lastName = values[0];
            var edition = values[1];
            
            var isMatch = false;
            var oldEdition = edition;
            var articleId = Convert.ToInt32(values[2]);
            var publisherId = Convert.ToInt32(values[3]);
            var requestId = Convert.ToInt32(values[4]);
            var document = new HtmlDocument();
            document.LoadHtml(html);

            using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
            {
                var article = entities.Articles.First(i => i.ArticleId == articleId);
                article.Abstract = document.GetPlainText().ToLower();

                entities.SaveChanges();

                // Editions
                string editionAttributeValue;

                try
                {
                    editionAttributeValue = document.GetValueContaining(edition).FirstOrDefault();
                }
                catch
                {
                    return false;
                }

                if (editionAttributeValue != null)
                {
                    editionAttributeValue = HttpUtility.HtmlDecode(editionAttributeValue);
                    var index = editionAttributeValue.ToLower().IndexOf(edition.ToLower(), StringComparison.Ordinal);
                    if (index >= 0)
                    {
                        var endIndex = index + edition.Length;
                        while (endIndex < editionAttributeValue.Length && TextTool.IsEditionChar(editionAttributeValue[endIndex]))
                        {
                            endIndex++;
                        }

                        if (endIndex == index + edition.Length)
                        {
                            endIndex++;
                        }

                        if (endIndex > editionAttributeValue.Length)
                            endIndex = editionAttributeValue.Length;

                        edition = editionAttributeValue.Substring(index, endIndex - index).Trim();
                    }
                }

                if (edition.ToLower() != edition && edition != oldEdition)
                {
                    var editionId = entities.InsertEdition(edition, publisherId).FirstOrDefault();
                    if (editionId != null)
                    {
                        article.EditionId = editionId.Value;
                        entities.SaveChanges();
                    }

                    isMatch = true;
                }

                // Names
                var nameAttributeValues = document.GetValueContaining(lastName);

                if (nameAttributeValues != null)
                {
                    var firstName = nameAttributeValues.FirstOrDefault();
                    IEnumerable<string> names;
                    if (nameAttributeValues.Count() == 1 && firstName != null && firstName.Contains(","))
                    {
                        firstName = HttpUtility.HtmlDecode(firstName);
                        names = TextTool.GetNames(lastName, firstName);
                    }
                    else
                    {
                        names = nameAttributeValues;
                    }

                    foreach (var name in names)
                    {
                        SaveName(entities, articleId, requestId, name);
                        isMatch = true;
                    }
                }

                // Keywords
                var lines = WebTool.GetTextLines(html);

                var keywordsIndex = -1;
                var keywords = new List<string>();
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].ToLower().StartsWith("keyword"))
                    {
                        keywordsIndex = i + 1;
                        break;
                    }
                }

                if (keywordsIndex == -1)
                    return isMatch;

                char? separator = null;
                var hasSeparator = lines[keywordsIndex + 1].Length == 1 && !char.IsLetterOrDigit(lines[keywordsIndex + 1][0]);
                if (hasSeparator)
                {
                    separator = lines[keywordsIndex + 1][0];
                }
                else if (!char.IsLetterOrDigit(lines[keywordsIndex][lines[keywordsIndex].Length - 1]))
                {
                    separator = lines[keywordsIndex][lines[keywordsIndex].Length - 1];
                }

                for (var i = keywordsIndex; i < lines.Length; i++)
                {
                    if (!char.IsLetterOrDigit(lines[i][0]))
                        break;

                    var line = lines[i];
                    keywords.Add(line.EndsWith(separator.ToString()) ? line.Substring(0, line.Length - 1) : line);

                    if (hasSeparator)
                    {
                        if (lines[i + 1][0] == separator)
                            i++;
                        else
                            break;
                    }
                    else if (separator != null && lines[i][lines[i].Length - 1] != separator)
                    {
                        break;
                    }
                }

                foreach (var keyword in keywords)
                {
                    // ToDo: Remove constant substring
                    var keywordObject = entities.InsertKeyword(keyword.Substring(0, keyword.Length > 128 ? 128 : keyword.Length)).First();
                    if (keywordObject != null)
                    {
                        var keywordId = keywordObject.Value;
                        entities.InsertArticleKeyword(articleId, keywordId);
                    }
                }

                if (!isMatch)
                    isMatch = keywords.Count > 0;
            }

            return isMatch;
        }

        public static bool FindFields(Requests requestObject, string text, string html, string language, string regex)
        {
            if (requestObject.IsAdvanced)
            {
                return FindMoreAuthorsAndEdition(requestObject, html, text);
            }

            var matches = Regex.Matches(text, regex, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
            if (matches.Count == 0)
                return false;

            using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
            {
                if (entities.Connection.State != ConnectionState.Open)
                    entities.Connection.Open();

                foreach (Match match in matches)
                {
                    var isAlive = true;
                    var transaction = entities.Connection.BeginTransaction();

                    var nameGroup = match.Groups[NameGroup];
                    var publisherGroup = match.Groups[PublisherGroup];
                    var editionGroup = match.Groups[EditionGroup];
                    var articleGroup = match.Groups[ArticleGroup];
                    var yearGroup = match.Groups[YearGroup];
                    var citedGroup = match.Groups[CitedGroup];

                    if (!nameGroup.Success ||
                        !publisherGroup.Success ||
                        !articleGroup.Success ||
                        !yearGroup.Success ||
                        !citedGroup.Success)
                    {
                        transaction.Rollback();
                        continue;
                    }

                    var article = articleGroup.Captures[0].Value.Replace("\r", string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(article) ||
                        article.StartsWith("["))
                    {
                        transaction.Rollback();
                        continue;
                    }

                    var edition = (editionGroup.Success ? editionGroup.Captures[0].Value : "Неизвестное издание").Replace("\r", string.Empty);
                    var publisher = publisherGroup.Captures[0].Value.Replace("\r", string.Empty);
                    var year = Convert.ToInt32(yearGroup.Captures[0].Value.Replace("\r", string.Empty));
                    var cited = Convert.ToInt32(citedGroup.Captures[0].Value.Replace("\r", string.Empty));

                    if (cited <= 0)
                    {
                        transaction.Rollback();
                        continue;
                    }

                    if (entities.Articles.Any(
                            i => i.Article.ToLower() == article.ToLower() && i.Year == year && i.Cited == cited))
                    {
                        transaction.Rollback();
                        continue;
                    }

                    var publisherId = entities.InsertPublisher(publisher).First();
                    var editionId = entities.InsertEdition(edition, publisherId).First();
                    var articleId = entities.InsertArticle(article, editionId, year, cited).First();

                    var names = nameGroup.Captures[0].Value
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(i => i.Trim());

                    var isPlanned = false;
                    foreach (var values in names.Select(name => name
                        .Replace(".", string.Empty)
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        .Where(values => values.Length >= 2))
                    {
                        string initial;
                        string lastName;

                        if (values[0].Length == 1 || values[0].Length == 2)
                        {
                            initial = values[0];
                            lastName = values[1];
                        }
                        else if (values[1].Length == 1 || values[1].Length == 2)
                        {
                            initial = values[1];
                            lastName = values[0];
                        }
                        else
                        {
                            continue;
                        }

                        var isValid = (initial + lastName).All(char.IsLetter);

                        if (!isValid)
                            continue;

                        var nameId = entities.InsertName(initial.ToUpper(), lastName).First();
                        entities.InsertResult(nameId, articleId, requestObject.RequestId);

                        if (isPlanned)
                            continue;

                        isPlanned = true;

                        var articleIndex = html.IndexOf(article, StringComparison.Ordinal);
                        if (articleIndex < 0)
                            continue;

                        var hrefIndex = html.Substring(0, articleIndex).LastIndexOf("href=\"", StringComparison.Ordinal) + 6;
                        var request = html.Substring(hrefIndex, html.Substring(hrefIndex).IndexOf("\"", StringComparison.Ordinal));
                        if (!request.StartsWith("http://"))
                        {
                            transaction.Rollback();
                            isAlive = false;
                            break;
                        }

                        var articleObject = entities.Articles.First(i => i.ArticleId == articleId);
                        articleObject.Url = request;

                        entities.Requests.AddObject(new Requests
                        {
                            SessionId = requestObject.SessionId,
                            Request = request,
                            Response = string.Format("{0};{1};{2};{3};{4}",
                                                        lastName, edition, articleId, publisherId, requestObject.RequestId),
                            Language = language,
                            PageLimit = 0,
                            Search = requestObject.Search,
                            IsMatched = false,
                            IsParsed = false,
                            StartTime = DateTime.Now,
                            IsAdvanced = true
                        });

                        entities.SaveChanges();
                    }

                    if (isAlive)
                        transaction.Commit();
                }
            }

            return true;
        }
    }
}
