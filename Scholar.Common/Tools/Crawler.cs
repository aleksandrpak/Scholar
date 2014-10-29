using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

using Scholar.Common.Database;

namespace Scholar.Common.Tools
{
    public static class Crawler
    {
        private const int RequestTimeoutSeconds = 30;

        private static bool _isStarted;
        private static readonly object Lock = new object();

        private static void SkipRequest(Requests request)
        {
            if (request == null)
                return;

            using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
            {
                var databaseRequest = entities.Requests.SingleOrDefault(i => i.RequestId == request.RequestId);
                if (databaseRequest == null)
                    return;

                databaseRequest.Response = "skipped";
                databaseRequest.IsParsed = false;
                databaseRequest.IsMatched = false;

                entities.SaveChanges();
            }
        }

        private static void StartCrawler()
        {
            var lastRequest = DateTime.Now.AddSeconds(-RequestTimeoutSeconds);
            var regexes = new List<Regexes>();

            using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
            {
                // Getting all regex
                if (regexes.Count == 0)
                {
                    regexes = entities.Regex.ToList();
                }
            }

            while (true)
            {
                var isRequest = (DateTime.Now - lastRequest).TotalSeconds >= RequestTimeoutSeconds;
                Requests requestObject;

                using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
                {
                    requestObject =
                        entities.Requests.OrderBy(i => i.RequestId).FirstOrDefault(i =>
                            (isRequest && !i.IsAdvanced && i.Response == null) ||
                            (!isRequest && i.IsAdvanced && i.Response != null && i.Response != "skipped"));

                    if (requestObject == null)
                    {
                        // No need to check additionalQueue because of loop condition
                        requestObject =
                            entities.Requests.OrderBy(i => i.RequestId).FirstOrDefault(i =>
                                (!isRequest && !i.IsAdvanced && i.Response == null) ||
                                (isRequest && i.IsAdvanced && i.Response != null && i.Response != "skipped"));

                        if (!isRequest && requestObject != null)
                        {
                            var millisecondsWait = (int)(RequestTimeoutSeconds - (DateTime.Now - lastRequest).TotalSeconds) * 1000;
                            if (millisecondsWait > 0)
                            {
                                Thread.Sleep(millisecondsWait);
                            }
                        }
                    }
                }

                if (requestObject == null)
                {
                    return;
                }

                var request = requestObject.Request;
                if (string.IsNullOrWhiteSpace(request))
                {
                    SkipRequest(requestObject);
                    continue;
                }

                string htmlText;
                var usedRegex = regexes
                    .Where(i => Regex.IsMatch(request, i.UrlPart) && !i.IsCrawler)
                    .Select(i => i.Regex);

                try
                {
                    if (request.Contains("scholar.google"))
                        lastRequest = DateTime.Now;

                    htmlText = WebTool.GetResponse(request);
                    if (string.IsNullOrWhiteSpace(htmlText))
                    {
                        SkipRequest(requestObject);
                        continue;
                    }
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    SkipRequest(requestObject);
                    Log.Current.Error(exception);
                    continue;
                }

                htmlText = htmlText.Replace("<b>", string.Empty).Replace("</b>", string.Empty);
                var plainText = WebTool.GetText(htmlText, WebTool.SearchEngine.GoogleScholar);

                if (request.Contains("scholar.google") && request.Contains("&start=") && request.Contains("&as_ylo=") && request.Contains("&as_yhi=") &&
                    plainText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Length < 50) // ToDo: Get real number
                {
                    using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
                    {
                        try
                        {
                            SkipRequest(requestObject);

                            var requestString = request.Substring(0,
                                                                  request.LastIndexOf("&start=",
                                                                                      StringComparison.Ordinal));
                            var yearLow = request.Substring(request.LastIndexOf("&as_ylo=", StringComparison.Ordinal),
                                                            12);
                            var yearHigh = request.Substring(request.LastIndexOf("&as_yhi=", StringComparison.Ordinal),
                                                             12);

                            var invalidRequests = entities.Requests
                                                          .Where(i =>
                                                                 i.SessionId == requestObject.SessionId &&
                                                                 i.Request.StartsWith(requestString) &&
                                                                 i.Request.Contains(yearHigh) &&
                                                                 i.Request.Contains(yearLow) &&
                                                                 i.RequestId >= requestObject.RequestId)
                                                          .ToList();

                            foreach (var invalidRequest in invalidRequests)
                            {
                                entities.DeleteObject(invalidRequest);
                            }

                            entities.SaveChanges();
                        }
                        catch (Exception exception)
                        {
                            Log.Current.Error(exception);
                        }
                    }

                    continue;
                }

                var isMatch = false;

                try
                {
                    if (requestObject.IsAdvanced)
                    {
                        isMatch = RegexTool.FindMoreAuthorsAndEdition(requestObject, htmlText, plainText);
                    }
                    else
                    {
                        foreach (var currentRegex in usedRegex)
                        {
                            isMatch = RegexTool.FindFields(
                                requestObject,
                                plainText,
                                htmlText,
                                requestObject.Language,
                                currentRegex);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SkipRequest(requestObject);
                    Log.Current.Error(exception);
                    continue;
                }

                requestObject.IsMatched = isMatch;
                requestObject.IsParsed = true;

                using (var entities = new ScholarDatabaseEntities { CommandTimeout = 600 })
                {
                    var requestTemp = requestObject;
                    var oldRequest = entities.Requests.Single(i => i.RequestId == requestTemp.RequestId);

                    // ToDo: Save response, when system for reparsing is ready
                    oldRequest.Response = (requestObject.IsAdvanced ? null : "parsed"); //requestObject.Response;
                    oldRequest.IsMatched = requestObject.IsMatched;
                    oldRequest.IsParsed = requestObject.IsParsed;

                    entities.SaveChanges();
                }
            }
        }

        public static void LaunchCrawler()
        {
            if (_isStarted)
                return;

            lock (Lock)
            {
                _isStarted = true;
            }

            while (true)
            {
                try
                {
                    StartCrawler();

                    lock (Lock)
                    {
                        _isStarted = false;
                    }

                    break;
                }
                catch (ThreadAbortException)
                {
                    _isStarted = false;
                    break;
                }
                catch (Exception exception)
                {
                    MessageBox.Show(null, exception.ToString(), "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Log.Current.Error(exception);
                }
            }
        }
    }
}
