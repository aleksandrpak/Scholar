using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

using HtmlAgilityPack;

namespace Scholar.Common.Tools
{
    public static class WebTool
    {
        public enum SearchEngine
        {
            GoogleScholar
        }

        private static string[] GetChildNodeLines(HtmlNode node)
        {
            var lines = new List<string>();

            if (node.ChildNodes.Count == 0 && !string.IsNullOrWhiteSpace(node.InnerText))
            {
                return new[] { node.InnerText.Trim() };
            }

            foreach (var childNode in node.ChildNodes)
            {
                if (childNode.OriginalName != "#comment" &&
                    childNode.ChildNodes.Count == 0 && !string.IsNullOrWhiteSpace(childNode.InnerText))
                {
                    lines.Add(childNode.InnerText.Trim());
                }
                else if (childNode.ChildNodes.Count > 0)
                {
                    foreach (var thisChildNode in childNode.ChildNodes)
                    {
                        lines.AddRange(GetChildNodeLines(thisChildNode));
                    }
                }
            }

            return lines.ToArray();
        }

        public static string[] GetTextLines(string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html);

            var htmlNode = document.DocumentNode.ChildNodes.FirstOrDefault(i => i.Name == "html");
            if (htmlNode != null)
            {
                var bodyNode = htmlNode.ChildNodes.FirstOrDefault(i => i.Name == "body");

                return GetChildNodeLines(bodyNode);
            }

            return new string[0];
        }

        public static string GetResponse(string url)
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create(url);

            httpRequest.CookieContainer = new CookieContainer();
            httpRequest.UserAgent =
                "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.4 (KHTML, like Gecko) Chrome/22.0.1229.94 Safari/537.4";

            var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
            using (var stream = httpResponse.GetResponseStream())
            {
                if (stream == null)
                    return null;

                stream.ReadTimeout = 30000;
                var charSet = string.IsNullOrWhiteSpace(httpResponse.CharacterSet)
                                  ? "UTF-8"
                                  : httpResponse.CharacterSet;

                using (var streamReader = new StreamReader(stream, Encoding.GetEncoding(charSet)))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }

        public static string GetText(string html, SearchEngine searchEngine)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html);

            var systemTags = new[] { "script", "style" };
            var newLineTags = new[] { "div", "p", "h1", "h2", "h3", "h4", "h5" };
            var queueTags = new[] { "#document", "html", "body", "form", "div", "table", "#text", "h1", "h2", "h3", "h4", "h5" };

            var nodes = new Stack<HtmlNode>();
            nodes.Push(document.DocumentNode);

            var builder = new StringBuilder();
            while (nodes.Count > 0)
            {
                var node = nodes.Pop();
                var nodeName = node.Name.ToLower();

                if (systemTags.Contains(nodeName))
                    continue;

                if (queueTags.Contains(nodeName))
                {
                    for (var i = node.ChildNodes.Count - 1; i >= 0; i--)
                    {
                        nodes.Push(node.ChildNodes[i]);
                    }
                }

                if (newLineTags.Contains(nodeName))
                {
                    builder.AppendLine();
                }

                if (node.InnerHtml == node.InnerText)
                {
                    var text = node.InnerText
                        .Replace("&nbsp;", string.Empty)
                        .Replace("&hellip;", string.Empty)
                        .Replace("&copy;", string.Empty)
                        .Replace("&raquo;", string.Empty)
                        .Replace("</form>", string.Empty)
                        .Replace("&amp;", "&")
                        .Replace("&quot;", "\"")
                        .Trim();

                    if (string.IsNullOrWhiteSpace(text) ||
                        (text.StartsWith("<") && text.EndsWith(">")))
                        continue;

                    builder.AppendFormat(" {0}", text);
                }
            }

            var modified = builder.ToString();
            while (true)
            {
                var old = modified;

                modified = modified.Replace("\r\n\r\n\r\n", "\r\n\r\n");

                if (modified.StartsWith("\r\n"))
                    modified = modified.Remove(0, 2);

                if (modified.EndsWith("\r\n"))
                    modified = modified.Remove(modified.Length - 2);

                modified = modified
                    .Replace(" ,", ",")
                    .Replace("\" ", "\"")
                    .Replace(",\"", "\"");

                if (searchEngine == SearchEngine.GoogleScholar)
                {
                    modified = modified
                        .Replace("Account Options", string.Empty)
                        .Replace("Мои цитаты", string.Empty)
                        .Replace("My Citations", string.Empty);
                }

                var asciiRegex = new Regex("\\&\\#[0-9]+\\;");
                var matches = new List<string>();
                foreach (var match in asciiRegex.Matches(modified))
                {
                    var stringMatch = match.ToString();
                    if (matches.Contains(stringMatch))
                        continue;

                    var charCode = Convert.ToInt32(stringMatch.Remove(stringMatch.Length - 1).Remove(0, 2));
                    var charValue = char.ConvertFromUtf32(charCode);

                    modified = modified.Replace(stringMatch, charValue.ToString(CultureInfo.InvariantCulture));

                    matches.Add(stringMatch);
                }

                modified = modified.Trim();

                if (old == modified)
                    break;
            }

            while (true)
            {
                var old = modified;

                var lines = modified.Split(new[] { "\r\n" }, StringSplitOptions.None);

                var textBuilder = new StringBuilder();
                var removeNextNewLine = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var modifiedLine = line.Trim();

                    if (removeNextNewLine &&
                        string.IsNullOrEmpty(modifiedLine))
                    {
                        removeNextNewLine = false;
                        continue;
                    }

                    removeNextNewLine = false;

                    while (true)
                    {
                        var oldLine = modifiedLine;

                        if (modifiedLine.StartsWith(","))
                            modifiedLine = modifiedLine.Remove(0, 1);

                        if (modifiedLine.StartsWith("..."))
                            modifiedLine = modifiedLine.Remove(0, 3);

                        modifiedLine = modifiedLine.Trim();

                        if (oldLine == modifiedLine)
                            break;
                    }

                    if (searchEngine == SearchEngine.GoogleScholar)
                    {
                        if (modifiedLine.StartsWith("[DOC]") ||
                            modifiedLine.StartsWith("[PDF]"))
                        {
                            removeNextNewLine = true;
                        }

                        if ((i > 0 && i < (lines.Length - 1)) &&
                            !string.IsNullOrWhiteSpace(line) &&
                            string.IsNullOrWhiteSpace(lines[i - 1]) &&
                            string.IsNullOrWhiteSpace(lines[i + 1]))
                        {
                            removeNextNewLine = true;
                        }
                    }

                    textBuilder.AppendLine(modifiedLine);
                }

                modified = textBuilder.ToString().Trim();

                if (old == modified)
                    break;
            }

            return modified;
        }
    }
}
