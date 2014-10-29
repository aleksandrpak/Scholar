using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Input;

using Scholar.Common.Tools;

namespace RegexCreator
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView
    {
        #region Private Members

        private Dictionary<string, List<Tuple<TextPointer, TextPointer>>> _selectedItems;
        private Dictionary<string, Color> _selectionColor;

        private string _text;
        private string _html;

        #endregion

        public MainView()
        {
            InitializeComponent();
        }

        #region Private Methods

        private void Mark(int offset, int length, string key)
        {
            Mark(GetPoint(offset), GetPoint(offset + length), key);
        }

        private void Mark(TextPointer start, TextPointer end, string key)
        {
            if (key != "Clean")
            {
                _selectedItems[key]
                    .Add(new Tuple<TextPointer, TextPointer>(start, end));
            }

            EditConflicts(start, end);

            var textRange = tbText.Selection;
            textRange.Select(start, end);

            textRange.ApplyPropertyValue(TextElement.BackgroundProperty,
                new SolidColorBrush(_selectionColor[key]));
        }

        private void EditConflicts(TextPointer start, TextPointer end)
        {
            var addItems = new Dictionary<string, List<Tuple<TextPointer, TextPointer>>>();
            var deleteItems = new Dictionary<string, List<Tuple<TextPointer, TextPointer>>>();

            foreach (var key in _selectedItems.Keys)
            {
                deleteItems.Add(key, new List<Tuple<TextPointer, TextPointer>>());
                addItems.Add(key, new List<Tuple<TextPointer, TextPointer>>());


                foreach (var value in _selectedItems[key])
                {
                    if (start.CompareTo(value.Item1) == -1 && end.CompareTo(value.Item2) == 1)
                    {
                        deleteItems[key].Add(new Tuple<TextPointer, TextPointer>(value.Item1, value.Item2));
                    }
                    else if (start.CompareTo(value.Item1) == 1 && end.CompareTo(value.Item2) == -1)
                    {
                        deleteItems[key].Add(new Tuple<TextPointer, TextPointer>(value.Item1, value.Item2));
                        addItems[key].Add(new Tuple<TextPointer, TextPointer>(value.Item1, start));
                        addItems[key].Add(new Tuple<TextPointer, TextPointer>(end, value.Item2));
                    }
                    else if (start.CompareTo(value.Item2) == -1 && end.CompareTo(value.Item2) == 1)
                    {
                        deleteItems[key].Add(new Tuple<TextPointer, TextPointer>(value.Item1, value.Item2));
                        addItems[key].Add(new Tuple<TextPointer, TextPointer>(value.Item1, start));
                    }
                    else if (start.CompareTo(value.Item1) == -1 && end.CompareTo(value.Item1) == 1)
                    {
                        deleteItems[key].Add(new Tuple<TextPointer, TextPointer>(value.Item1, value.Item2));
                        addItems[key].Add(new Tuple<TextPointer, TextPointer>(end, value.Item2));
                    }
                }
            }

            foreach (var key in addItems.Keys)
            {
                foreach (var value in addItems[key])
                {
                    _selectedItems[key].Add(value);
                }
            }

            foreach (var key in deleteItems.Keys)
            {
                foreach (var value in deleteItems[key])
                {
                    _selectedItems[key].Remove(value);
                }
            }
        }

        private TextPointer GetPoint(int offset)
        {
            var result = tbText.Document.ContentStart;
            var i = 0;
            while (i < offset && result != null)
            {
                if (result.GetPointerContext(LogicalDirection.Backward) == TextPointerContext.Text ||
                    result.GetPointerContext(LogicalDirection.Backward) == TextPointerContext.None)
                {
                    i++;
                }

                if (result.GetPositionAtOffset(1, LogicalDirection.Forward) == null)
                {
                    return result;
                }

                result = result.GetPositionAtOffset(1, LogicalDirection.Forward);
            }

            return result;
        }

        private void ChangeView()
        {
            var document = new FlowDocument();
            document.Blocks.Add(new Paragraph(new Run((cbHtml.IsChecked != null && cbHtml.IsChecked.Value ? _html : _text))));
            tbText.Document = document;
        }

        private int GetTextIndex(TextPointer pointer)
        {
            var index = Math.Abs(tbText.Document.ContentStart.GetOffsetToPosition(pointer));

            // Start of document
            index -= 4;
            if (index < 0)
                return 0;

            var currentText = (cbHtml.IsChecked != null && cbHtml.IsChecked.Value ? _html : _text);
            var lines = currentText
                .Substring(0, index)
                .Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Count();

            index -= ((2 * lines) + (lines > 1 ? 2 : 0));

            return index;
        }

        private string CreateRegex(Dictionary<string, List<Tuple<TextPointer, TextPointer>>> selection)
        {
            var plainSelection = selection.
                SelectMany(i => i.Value
                    .Select(j => new
                    {
	                    i.Key,
                        Start = j.Item1,
                        End = j.Item2
                    })
                ).OrderBy(i => GetTextIndex(i.Start))
                .ToArray();

            var builder = new StringBuilder();
            var currentText = (cbHtml.IsChecked != null && cbHtml.IsChecked.Value ? _html : _text);

	        for (int i = 0; i < plainSelection.Count() - 1; i++)
            {
                builder.Append(RegexTool.RegexList[plainSelection[i].Key]);
                
                var offset = GetTextIndex(plainSelection[i].End);
                var length = Math.Abs(GetTextIndex(plainSelection[i + 1].Start) - offset);

                var splitter = currentText.Substring(offset, length);

                splitter = splitter
                    .Replace(" ", "\\s")
                    .Replace(".", "\\.")
                    .Replace("(", "\\(")
                    .Replace(")", "\\)")
                    .Replace("?", "\\?")
                    .Replace("-", "\\-")
                    .Replace("_", "\\_")
                    .Replace("+", "\\+")
                    .Replace("[", "\\[")
                    .Replace("]", "\\]")
                    .Replace(":", "\\:")
                    .Replace("\r\n", "\\r\\n");

                if (string.IsNullOrWhiteSpace(splitter))
                    splitter = "\\r\\n";

                builder.AppendFormat("?(?:{0})", splitter);
            }

            builder.Append(RegexTool.RegexList[plainSelection[plainSelection.Count() - 1].Key]);

            return builder.ToString();
        }

        private void ClearAll()
        {
            Mark(tbText.Document.ContentStart, tbText.Document.ContentEnd, "Clean");

            tbText.Selection.Select(tbText.Document.ContentStart, tbText.Document.ContentStart);
        }

        #endregion

        #region Private Handlers

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _selectedItems = new Dictionary<string, List<Tuple<TextPointer, TextPointer>>>();
            _selectionColor = new Dictionary<string, Color>();

            foreach (ComboBoxItem item in cbFields.Items)
            {
                _selectedItems.Add(item.Tag.ToString(), new List<Tuple<TextPointer, TextPointer>>());
            }

            _selectionColor.Add("Clean", Colors.White);
            _selectionColor.Add("Name", Colors.BurlyWood);
            _selectionColor.Add("Publisher", Colors.SpringGreen);
            _selectionColor.Add("Edition", Colors.OrangeRed);
            _selectionColor.Add("Article", Colors.DarkGoldenrod);
            _selectionColor.Add("Year", Colors.SkyBlue);
            _selectionColor.Add("Cited", Colors.Violet);
        }

        private void btnUrl_Click(object sender, RoutedEventArgs e)
        {
            if (!System.IO.File.Exists("text.txt"))
                System.IO.File.WriteAllText("text.txt", string.Empty);

            var text = System.IO.File.ReadAllText("text.txt");
            if (string.IsNullOrWhiteSpace(text))
                text = WebTool.GetResponse(tbUrl.Text);

            System.IO.File.WriteAllText("text.txt", text);

            _html = text;
            _html = WebTool.GetResponse(tbUrl.Text);
            _text = WebTool.GetText(_html, WebTool.SearchEngine.GoogleScholar);

            ChangeView();
        }

        private void btnMark_Click(object sender, RoutedEventArgs e)
        {
            Mark(tbText.Selection.Start, tbText.Selection.End, ((ComboBoxItem)cbFields.SelectedItem).Tag.ToString());
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            ClearAll();
        }

        private void cbHtml_CheckedChanged(object sender, RoutedEventArgs e)
        {
            ChangeView();
        }

        private void tbText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                var textRange = tbText.Selection;
                var start = textRange.Start;
                var end = textRange.End;

                switch (e.Key)
                {
	                case Key.D1:
                        Mark(start, end, "Article");
                        break;

                    case Key.D2:
                        Mark(start, end, "Name");
                        break;

                    case Key.D3:
                        Mark(start, end, "Edition");
                        break;

                    case Key.D4:
                        Mark(start, end, "Year");
                        break;

                    case Key.D5:
                        Mark(start, end, "Publisher");
                        break;

                    case Key.D6:
                        Mark(start, end, "Cited");
                        break;
                }
            }
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            var regex = CreateRegex(_selectedItems);

            var currentText = (cbHtml.IsChecked != null && cbHtml.IsChecked.Value ? _html : _text);

            var results = Regex.Matches(currentText, regex,
                RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            ClearAll();

            foreach (Match result in results)
            {
                foreach (var key in RegexTool.RegexList.Keys)
                {
                    var group = result.Groups[key];

                    if (!group.Success)
                        continue;

                    foreach (Capture capture in group.Captures)
                    {
                        Mark(capture.Index, capture.Length, key);
                    }
                }
            }
        }

        #endregion
    }
}
