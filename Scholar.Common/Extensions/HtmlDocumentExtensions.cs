using System.Collections.Generic;
using System.Linq;

using HtmlAgilityPack;
using System.Text;

namespace Scholar.Common.Extensions
{
    public static class HtmlDocumentExtensions
    {
        private static readonly string[] ValueAttributes = { "content", "value" };

        private static HtmlNode GetNodeWithValue(IEnumerable<HtmlNode> nodes, string valueAttributeName, string value)
        {
            foreach (var node in nodes)
            {
                var valueAttribute = node.Attributes[valueAttributeName];
                if (valueAttribute != null && valueAttribute.Value != null &&
                    valueAttribute.Value.ToLower().Contains(value.ToLower()))
                {
                    return node;
                }

                var childNodeResult = GetNodeWithValue(node.ChildNodes, valueAttributeName, value);
                if (childNodeResult != null)
                {
                    return childNodeResult;
                }
            }

            return null;
        }

        private static string GetText(IEnumerable<HtmlNode> nodes)
        {
            var builder = new StringBuilder();

            foreach (var node in nodes)
            {
                if (!string.IsNullOrWhiteSpace(node.InnerText) && node.InnerHtml != node.InnerText)
                {
                    var text = node.InnerText
                        .Replace("\r", " ")
                        .Replace("\n", " ")
                        .Replace("\t", " ");

                    while (true)
                    {
                        var length = text.Length;
                        text = text.Replace("  ", " ");

                        if (length == text.Length)
                            break;
                    }

                    builder.AppendFormat("{0} ", text);
                }

                if (node.ChildNodes.Count > 0)
                {
                    var text = GetText(node.ChildNodes);
                    if (!string.IsNullOrWhiteSpace(text))
                        builder.AppendFormat("{0} ", text);
                }
            }

            if (builder.Length > 0)
                builder.Remove(builder.Length - 1, 1);

            return builder.ToString();
        }

        public static string[] GetValueContaining(this HtmlDocument document, string value)
        {
            var values = new List<string>();

            foreach (var valueAttributeName in ValueAttributes)
            {
                var node = GetNodeWithValue(document.DocumentNode.ChildNodes, valueAttributeName, value);
                if (node == null)
                    continue;

                var nameAttribute = node.Attributes["name"];
                if (node.Name == "meta" && nameAttribute != null)
                {
                    var name = valueAttributeName;
                    values.AddRange(node.ParentNode.ChildNodes
                                        .Where(i => i.Attributes["name"] != null && i.Attributes["name"].Value == nameAttribute.Value)
                                        .Select(i => i.Attributes[name].Value));
                }
                else
                {
                    values.Add(node.Attributes[valueAttributeName].Value);
                }

                return values.ToArray();
            }

            return null;
        }

        public static string GetPlainText(this HtmlDocument document)
        {
            return GetText(document.DocumentNode.ChildNodes);
        }
    }
}
