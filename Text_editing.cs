using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;


namespace FirstCell 
{
    // ==========================
    // File: SyntaxHighlighter.cs
    // ==========================

    public class SyntaxHighlighter
    {
        private readonly Regex _htmlTagRegex = new(@"<\s*(\w+)");
        private readonly Regex _htmlClosingTagRegex = new(@"</\s*(\w+)");
        private readonly Regex _htmlAttrRegex = new(@"\b\w+(?=\s*=\s*)");
        private readonly Regex _htmlStringRegex = new("\"[^\"]*\"|\'[^\']*\'");
        private readonly Regex _htmlCommentRegex = new("<!--.*?-->", RegexOptions.Singleline);

        private readonly Regex _cssSelectorRegex = new(@"([^\{\}]+)(?=\s*\{)");
        private readonly Regex _cssPropertyRegex = new(@"\b[a-zA-Z-]+(?=:)\b");
        private readonly Regex _cssValueRegex = new(@"(?<=:\s?)[^;]+(?=;?)");
        private readonly Regex _cssCommentRegex = new(@"/\*.*?\*/", RegexOptions.Singleline);

        private readonly Regex _jsKeywordRegex = new(@"\b(function|var|let|const|if|else|return|for|while|class|new|this)\b");
        private readonly Regex _jsVarRegex = new(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b");
        private readonly Regex _jsNumberRegex = new(@"\b\d+(\.\d+)?\b");
        private readonly Regex _jsCommentRegex = new(@"//.*?$|/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Multiline);
        private readonly Regex _jsFunctionCallRegex = new(@"\b[a-zA-Z_][a-zA-Z0-9_]*\s*(?=\()\b");

        public void FullHighlight(RichTextBox editor, string extension)
        {
            foreach (var block in editor.Document.Blocks)
            {
                if (block is Paragraph paragraph)
                    HighlightParagraph(paragraph, extension);
            }
        }

        private void HighlightChangedParagraph(RichTextBox editor)
        {
            editor.TextChanged -= Editor_TextChanged;
            var caret = editor.CaretPosition;
            var paragraph = caret.Paragraph;
            if (paragraph != null)
            {
                string ext = GetFileExtensionFromTab(editor);
                HighlightParagraph(paragraph, ext);
            }
            editor.TextChanged += Editor_TextChanged;
        }
        public void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            HighlightChangedParagraph(sender as RichTextBox);
        }
        private string GetFileExtensionFromTab(RichTextBox editor)
        {
            if (editor.Parent is TabItem tab && tab.Tag is File file)
                return file.Extension;
            return string.Empty;
        }

        private void HighlightParagraph(Paragraph paragraph, string extension)
        {
            string text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
            var range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
            range.ClearAllProperties();

            if (extension == ".html")
            {
                foreach (Match match in _htmlTagRegex.Matches(text))
                    ApplyColor(paragraph, match.Index + 1, match.Length-1, Brushes.Blue);

                foreach (Match match in _htmlClosingTagRegex.Matches(text))
                    ApplyColor(paragraph, match.Index + 1, match.Length - 1, Brushes.Blue);

                foreach (Match match in _htmlAttrRegex.Matches(text))
                    ApplyColor(paragraph, match.Index, match.Length, Brushes.DeepSkyBlue);

                foreach (Match match in _htmlStringRegex.Matches(text))
                    ApplyColor(paragraph, match.Index, match.Length, Brushes.Orange);

                foreach (Match match in _htmlCommentRegex.Matches(text))
                    ApplyColor(paragraph, match.Index, match.Length+1, Brushes.Green);
            }
            else if (extension == ".css")
            {
                foreach (Match match in _cssSelectorRegex.Matches(text))
                    ApplyColor(paragraph, match.Index, match.Length, Brushes.BlueViolet);

                foreach (Match match in _cssPropertyRegex.Matches(text))
                    ApplyColor(paragraph, match.Index, match.Length, Brushes.DarkCyan);

                foreach (Match match in _cssValueRegex.Matches(text))
                    ApplyColor(paragraph, match.Index, match.Length, Brushes.OrangeRed);

                foreach (Match match in _cssCommentRegex.Matches(text))
                    ApplyColor(paragraph, match.Index, match.Length, Brushes.Green);
                
            }
            else if (extension == ".js")
            {
                foreach (Match match in _jsCommentRegex.Matches(text))
                    ApplyColor(paragraph, match.Index, match.Length, Brushes.Green);

                foreach (Match match in _jsNumberRegex.Matches(text))
                    ApplyColor(paragraph, match.Index, match.Length, Brushes.DarkOrange);

                foreach (Match match in _jsFunctionCallRegex.Matches(text))
                    ApplyColor(paragraph, match.Index, match.Length, Brushes.MediumPurple);

                foreach (Match match in _jsKeywordRegex.Matches(text))
                    ApplyColor(paragraph, match.Index, match.Length, Brushes.Blue);
            }
        }

        private void ApplyColorv1(Paragraph paragraph, int index, int length, SolidColorBrush color)
        {
            var start = paragraph.ContentStart;
            var navigator = start;
            int count = 0;

            while (navigator != null && count < index && navigator.CompareTo(paragraph.ContentEnd) < 0)
            {
                if (navigator.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string runText = navigator.GetTextInRun(LogicalDirection.Forward);
                    int remaining = index - count;
                    if (runText.Length >= remaining)
                    {
                        navigator = navigator.GetPositionAtOffset(remaining);
                        break;
                    }
                    count += runText.Length;
                }
                navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
            }

            var highlightStart = navigator;
            var highlightEnd = highlightStart?.GetPositionAtOffset(length);

            if (highlightStart != null && highlightEnd != null)
            {
                var highlightRange = new TextRange(highlightStart, highlightEnd);
                highlightRange.ApplyPropertyValue(TextElement.ForegroundProperty, color);
            }
        }

        private void ApplyColor(Paragraph paragraph, int index, int length, SolidColorBrush color)
        {
            var start = GetTextPointerAtOffset(paragraph, index);
            var end = GetTextPointerAtOffset(paragraph, index + length);

            if (start != null && end != null)
            {
                var range = new TextRange(start, end);
                var currentColor = range.GetPropertyValue(TextElement.ForegroundProperty);
                range.ApplyPropertyValue(TextElement.ForegroundProperty, color);
            }
        }

        private TextPointer? GetTextPointerAtOffset(Paragraph paragraph, int offset)
        {
            var navigator = paragraph.ContentStart;
            int count = 0;

            while (navigator != null && navigator.CompareTo(paragraph.ContentEnd) < 0)
            {
                if (navigator.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string textRun = navigator.GetTextInRun(LogicalDirection.Forward);
                    if (count + textRun.Length >= offset)
                    {
                        return navigator.GetPositionAtOffset(offset - count);
                    }
                    count += textRun.Length;
                }
                navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
            }
            return null;
        }
    }

    // ==========================
    // File: AutoCompleter.cs
    // ==========================


    public class AutoCompleter
    {
        private readonly Regex _tagStartRegex = new(@"<([a-zA-Z][a-zA-Z0-9]*)[^<>]*?>");
        private readonly HashSet<string> _selfClosingTags = new() { "br", "img", "hr", "meta", "link", "input" };

        public void HandleInput(RichTextBox editor, string input)
        {
            
            var caret = editor.CaretPosition;
            if (caret == null) return;

            switch (input)
            {
                case "\"":
                case "'":
                    InsertText(editor, caret, input, offset: 0);
                    break;
                case "(":
                    InsertText(editor, caret, ")", offset: 0);
                    break;
                case "[":
                    InsertText(editor, caret, "]", offset: 0);
                    break;
                case "{":
                    InsertText(editor, caret, "}", offset: 0);
                    break;
                case ">":
                    HandleHtmlTagAutoClose(editor);
                    break;
            }
        }

        private void InsertText(RichTextBox editor, TextPointer caret, string text, int offset)
        {
            caret.InsertTextInRun(text);
            editor.CaretPosition = caret.GetPositionAtOffset(offset, LogicalDirection.Backward);
        }

        private void HandleHtmlTagAutoClose(RichTextBox editor)
        {
            var caret = editor.CaretPosition;
            var lineStart = caret.GetLineStartPosition(0);
            if (lineStart == null) return;

            string currentLine = new TextRange(lineStart, caret).Text + ">";

            var matches = _tagStartRegex.Matches(currentLine);
            if (matches.Count > 0)
            {
                var lastMatch = matches[matches.Count - 1];
                string tag = lastMatch.Groups[1].Value;
                if (!_selfClosingTags.Contains(tag.ToLower()))
                {
                    InsertText(editor, caret, $"</{tag}>", offset: 0);
                }
            }
        }
    }
}