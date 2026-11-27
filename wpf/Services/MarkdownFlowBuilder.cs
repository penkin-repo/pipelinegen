using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PipelineGenWpf.Services
{
    public static class MarkdownFlowBuilder
    {
        private static readonly Brush TextBrush = CreateFrozenBrush("#1B1B1B");
        private static readonly Brush MutedBrush = CreateFrozenBrush("#666666");
        private static readonly Brush AccentBrush = CreateFrozenBrush("#0067C0");
        private static readonly Brush SurfaceBrush = CreateFrozenBrush("#FFFFFF");
        private static readonly Brush Surface2Brush = CreateFrozenBrush("#F7F7F7");
        private static readonly Brush BorderSoftBrush = CreateFrozenBrush("#DCDCDC");
        private static readonly Brush AmberSoftBrush = CreateFrozenBrush("#F3F3F3");
        private static readonly Brush AmberBorderBrush = CreateFrozenBrush("#C8C8C8");
        private static readonly Brush AmberTextBrush = CreateFrozenBrush("#1B1B1B");
        private static readonly Brush AmberBrush = CreateFrozenBrush("#0067C0");
        private static readonly Brush GreenBrush = CreateFrozenBrush("#107C10");
        private static readonly Brush CodeBgBrush = CreateFrozenBrush("#F3F3F3");

        private static readonly FontFamily UiFont = new FontFamily("Segoe UI");
        private static readonly FontFamily MonoFont = new FontFamily("Consolas, Courier New");

        private static readonly Regex CopyTagRegex = new Regex(@"<copy>([\s\S]*?)</copy>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex InlineTokenRegex = new Regex(
            @"<copy>([\s\S]*?)</copy>|`([^`]+)`|\*\*([^*]+)\*\*|__([^_]+)__|\*([^*\r\n]+)\*|\[([^\]]+)\]\((https?://[^)\s]+)\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex OrderedListRegex = new Regex(@"^\s*(\d+)\.\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex UnorderedListRegex = new Regex(@"^\s*[-*+]\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex HeadingRegex = new Regex(@"^(#{1,4})\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex HrRegex = new Regex(@"^\s*([-*_])\1{2,}\s*$", RegexOptions.Compiled);
        private static readonly Regex TableSepRegex = new Regex(@"^\s*\|?(\s*:?-+:?\s*\|)+\s*:?-+:?\s*\|?\s*$", RegexOptions.Compiled);

        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            SolidColorBrush b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        public static List<string> ExtractCopyItems(string content)
        {
            List<string> items = new List<string>();
            if (string.IsNullOrEmpty(content)) return items;

            MatchCollection matches = CopyTagRegex.Matches(content);
            foreach (Match m in matches)
            {
                string val = m.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(val) && !items.Contains(val))
                {
                    items.Add(val);
                }
            }
            return items;
        }

        public static FlowDocument BuildDocument(string markdown)
        {
            FlowDocument doc = new FlowDocument();
            doc.FontFamily = UiFont;
            doc.FontSize = 12.5;
            doc.Foreground = TextBrush;
            doc.PagePadding = new Thickness(0);
            doc.TextAlignment = TextAlignment.Left;

            if (string.IsNullOrEmpty(markdown))
            {
                return doc;
            }

            string normalized = markdown.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = normalized.Split('\n');

            int i = 0;
            while (i < lines.Length)
            {
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    i++;
                    continue;
                }

                // Fenced code block
                if (line.TrimStart().StartsWith("```"))
                {
                    string lang = line.TrimStart().Substring(3).Trim();
                    StringBuilder codeSb = new StringBuilder();
                    i++;
                    while (i < lines.Length && !lines[i].TrimStart().StartsWith("```"))
                    {
                        if (codeSb.Length > 0) codeSb.Append("\n");
                        codeSb.Append(lines[i]);
                        i++;
                    }
                    if (i < lines.Length) i++; // skip closing ```
                    doc.Blocks.Add(BuildCodeBlock(codeSb.ToString(), lang));
                    continue;
                }

                // Horizontal rule
                if (HrRegex.IsMatch(line))
                {
                    BlockUIContainer hr = new BlockUIContainer(new Border
                    {
                        Height = 1,
                        Background = BorderSoftBrush,
                        Margin = new Thickness(0, 8, 0, 8)
                    });
                    hr.Margin = new Thickness(0);
                    doc.Blocks.Add(hr);
                    i++;
                    continue;
                }

                // Heading
                Match hm = HeadingRegex.Match(line);
                if (hm.Success)
                {
                    int level = hm.Groups[1].Value.Length;
                    Paragraph hp = new Paragraph();
                    hp.FontWeight = FontWeights.Bold;
                    hp.Foreground = TextBrush;
                    if (level == 1) { hp.FontSize = 19; hp.Margin = new Thickness(0, 12, 0, 6); }
                    else if (level == 2) { hp.FontSize = 16.5; hp.Margin = new Thickness(0, 11, 0, 5); }
                    else if (level == 3) { hp.FontSize = 14.5; hp.Margin = new Thickness(0, 9, 0, 4); }
                    else { hp.FontSize = 13.5; hp.Margin = new Thickness(0, 8, 0, 3); }

                    AppendInlines(hp.Inlines, hm.Groups[2].Value.Trim());
                    doc.Blocks.Add(hp);
                    i++;
                    continue;
                }

                // Table (current line has | and next line is separator)
                if (line.Contains("|") && i + 1 < lines.Length && TableSepRegex.IsMatch(lines[i + 1]))
                {
                    List<string[]> rows = new List<string[]>();
                    rows.Add(SplitTableRow(line));
                    i += 2; // skip header & separator
                    while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && lines[i].Contains("|"))
                    {
                        rows.Add(SplitTableRow(lines[i]));
                        i++;
                    }
                    doc.Blocks.Add(BuildTable(rows));
                    continue;
                }

                // Blockquote
                if (line.TrimStart().StartsWith(">"))
                {
                    StringBuilder quoteSb = new StringBuilder();
                    while (i < lines.Length && lines[i].TrimStart().StartsWith(">"))
                    {
                        string qLine = lines[i].TrimStart().Substring(1);
                        if (qLine.StartsWith(" ")) qLine = qLine.Substring(1);
                        if (quoteSb.Length > 0) quoteSb.Append("\n");
                        quoteSb.Append(qLine);
                        i++;
                    }
                    Paragraph qp = new Paragraph();
                    qp.Margin = new Thickness(0, 6, 0, 6);
                    qp.Padding = new Thickness(12, 6, 10, 6);
                    qp.BorderBrush = AccentBrush;
                    qp.BorderThickness = new Thickness(3, 0, 0, 0);
                    qp.Background = SurfaceBrush;
                    qp.Foreground = MutedBrush;
                    AppendInlines(qp.Inlines, quoteSb.ToString());
                    doc.Blocks.Add(qp);
                    continue;
                }

                // Unordered list
                if (UnorderedListRegex.IsMatch(line))
                {
                    List ul = new List();
                    ul.MarkerStyle = TextMarkerStyle.Disc;
                    ul.Margin = new Thickness(0, 4, 0, 6);
                    ul.Padding = new Thickness(22, 0, 0, 0);
                    while (i < lines.Length)
                    {
                        Match um = UnorderedListRegex.Match(lines[i]);
                        if (!um.Success) break;
                        Paragraph itemP = new Paragraph();
                        itemP.Margin = new Thickness(0, 2, 0, 2);
                        itemP.LineHeight = 21;
                        AppendInlines(itemP.Inlines, um.Groups[1].Value);
                        ul.ListItems.Add(new ListItem(itemP));
                        i++;
                    }
                    doc.Blocks.Add(ul);
                    continue;
                }

                // Ordered list
                if (OrderedListRegex.IsMatch(line))
                {
                    List ol = new List();
                    ol.MarkerStyle = TextMarkerStyle.Decimal;
                    ol.Margin = new Thickness(0, 4, 0, 6);
                    ol.Padding = new Thickness(24, 0, 0, 0);
                    while (i < lines.Length)
                    {
                        Match om = OrderedListRegex.Match(lines[i]);
                        if (!om.Success) break;
                        Paragraph itemP = new Paragraph();
                        itemP.Margin = new Thickness(0, 2, 0, 2);
                        itemP.LineHeight = 21;
                        AppendInlines(itemP.Inlines, om.Groups[2].Value);
                        ol.ListItems.Add(new ListItem(itemP));
                        i++;
                    }
                    doc.Blocks.Add(ol);
                    continue;
                }

                // Regular paragraph (group consecutive non-special lines)
                Paragraph p = new Paragraph();
                p.Margin = new Thickness(0, 4, 0, 6);
                p.LineHeight = 21.5;
                bool firstLine = true;
                while (i < lines.Length &&
                       !string.IsNullOrWhiteSpace(lines[i]) &&
                       !lines[i].TrimStart().StartsWith("```") &&
                       !HeadingRegex.IsMatch(lines[i]) &&
                       !HrRegex.IsMatch(lines[i]) &&
                       !lines[i].TrimStart().StartsWith(">") &&
                       !UnorderedListRegex.IsMatch(lines[i]) &&
                       !OrderedListRegex.IsMatch(lines[i]) &&
                       !(lines[i].Contains("|") && i + 1 < lines.Length && TableSepRegex.IsMatch(lines[i + 1])))
                {
                    if (!firstLine)
                    {
                        p.Inlines.Add(new LineBreak());
                    }
                    AppendInlines(p.Inlines, lines[i]);
                    firstLine = false;
                    i++;
                }
                doc.Blocks.Add(p);
            }

            return doc;
        }

        private static string[] SplitTableRow(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("|")) trimmed = trimmed.Substring(1);
            if (trimmed.EndsWith("|")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
            string[] parts = trimmed.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
            }
            return parts;
        }

        private static Table BuildTable(List<string[]> rows)
        {
            Table table = new Table();
            table.CellSpacing = 0;
            table.Margin = new Thickness(0, 8, 0, 10);
            table.BorderBrush = BorderSoftBrush;
            table.BorderThickness = new Thickness(1);
            table.Background = SurfaceBrush;

            int colCount = 0;
            foreach (string[] r in rows)
            {
                if (r.Length > colCount) colCount = r.Length;
            }
            for (int c = 0; c < colCount; c++)
            {
                table.Columns.Add(new TableColumn());
            }

            TableRowGroup group = new TableRowGroup();
            for (int rIdx = 0; rIdx < rows.Count; rIdx++)
            {
                TableRow tr = new TableRow();
                if (rIdx == 0)
                {
                    tr.Background = Surface2Brush;
                    tr.FontWeight = FontWeights.Bold;
                }

                string[] cells = rows[rIdx];
                for (int c = 0; c < colCount; c++)
                {
                    string cellText = c < cells.Length ? cells[c] : "";
                    Paragraph cp = new Paragraph();
                    cp.Margin = new Thickness(0);
                    cp.FontSize = 12.5;
                    AppendInlines(cp.Inlines, cellText);

                    TableCell cell = new TableCell(cp);
                    cell.Padding = new Thickness(8, 5, 8, 5);
                    cell.BorderBrush = BorderSoftBrush;
                    cell.BorderThickness = new Thickness(0, 0, c < colCount - 1 ? 1 : 0, rIdx < rows.Count - 1 ? 1 : 0);
                    tr.Cells.Add(cell);
                }
                group.Rows.Add(tr);
            }

            table.RowGroups.Add(group);
            return table;
        }

        private static BlockUIContainer BuildCodeBlock(string code, string lang)
        {
            Border container = new Border();
            container.Background = SurfaceBrush;
            container.BorderBrush = BorderSoftBrush;
            container.BorderThickness = new Thickness(1);
            container.CornerRadius = new CornerRadius(8);
            container.Margin = new Thickness(0, 6, 0, 8);

            DockPanel root = new DockPanel();

            // Header bar with language & copy button
            Border head = new Border();
            head.Background = Surface2Brush;
            head.BorderBrush = BorderSoftBrush;
            head.BorderThickness = new Thickness(0, 0, 0, 1);
            head.CornerRadius = new CornerRadius(7, 7, 0, 0);
            head.Padding = new Thickness(10, 4, 8, 4);
            DockPanel.SetDock(head, Dock.Top);

            DockPanel headPanel = new DockPanel();
            TextBlock langLabel = new TextBlock();
            langLabel.Text = string.IsNullOrEmpty(lang) ? "code" : lang;
            langLabel.FontFamily = MonoFont;
            langLabel.FontSize = 11;
            langLabel.Foreground = MutedBrush;
            langLabel.VerticalAlignment = VerticalAlignment.Center;
            headPanel.Children.Add(langLabel);

            Button copyBtn = new Button();
            copyBtn.Content = "Копировать";
            copyBtn.FontSize = 11;
            copyBtn.Padding = new Thickness(8, 2, 8, 2);
            copyBtn.Cursor = Cursors.Hand;
            copyBtn.Background = SurfaceBrush;
            copyBtn.Foreground = TextBrush;
            copyBtn.BorderBrush = BorderSoftBrush;
            copyBtn.BorderThickness = new Thickness(1);
            copyBtn.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(copyBtn, Dock.Right);

            string capturedCode = code;
            copyBtn.Click += delegate
            {
                try { Clipboard.SetText(capturedCode); } catch { }
                copyBtn.Content = "✓ Скопировано";
                copyBtn.Foreground = GreenBrush;
                DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
                t.Tick += delegate
                {
                    t.Stop();
                    copyBtn.Content = "Копировать";
                    copyBtn.Foreground = TextBrush;
                };
                t.Start();
            };
            headPanel.Children.Add(copyBtn);
            head.Child = headPanel;
            root.Children.Add(head);

            TextBox codeBox = new TextBox();
            codeBox.Text = code;
            codeBox.IsReadOnly = true;
            codeBox.BorderThickness = new Thickness(0);
            codeBox.Background = Brushes.Transparent;
            codeBox.FontFamily = MonoFont;
            codeBox.FontSize = 12;
            codeBox.Foreground = TextBrush;
            codeBox.Padding = new Thickness(10, 8, 10, 8);
            codeBox.TextWrapping = TextWrapping.Wrap;
            root.Children.Add(codeBox);

            container.Child = root;
            BlockUIContainer block = new BlockUIContainer(container);
            block.Margin = new Thickness(0);
            return block;
        }

        private static void AppendInlines(InlineCollection inlines, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            int lastIdx = 0;
            MatchCollection matches = InlineTokenRegex.Matches(text);
            foreach (Match m in matches)
            {
                if (m.Index > lastIdx)
                {
                    inlines.Add(new Run(text.Substring(lastIdx, m.Index - lastIdx)));
                }

                if (m.Groups[1].Success)
                {
                    // <copy>...</copy> interactive button
                    string copyText = m.Groups[1].Value.Trim();
                    inlines.Add(new InlineUIContainer(CreateInlineCopyChip(copyText))
                    {
                        BaselineAlignment = BaselineAlignment.Center
                    });
                }
                else if (m.Groups[2].Success)
                {
                    // `inline code`
                    Run codeRun = new Run(" " + m.Groups[2].Value + " ");
                    codeRun.FontFamily = MonoFont;
                    codeRun.FontSize = 12;
                    codeRun.Background = CodeBgBrush;
                    codeRun.Foreground = TextBrush;
                    inlines.Add(codeRun);
                }
                else if (m.Groups[3].Success || m.Groups[4].Success)
                {
                    // **bold** or __bold__
                    string boldText = m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value;
                    Bold bold = new Bold();
                    AppendInlines(bold.Inlines, boldText);
                    inlines.Add(bold);
                }
                else if (m.Groups[5].Success)
                {
                    // *italic*
                    Italic italic = new Italic();
                    AppendInlines(italic.Inlines, m.Groups[5].Value);
                    inlines.Add(italic);
                }
                else if (m.Groups[6].Success && m.Groups[7].Success)
                {
                    // [title](url)
                    string linkLabel = m.Groups[6].Value;
                    string url = m.Groups[7].Value;
                    Hyperlink link = new Hyperlink(new Run(linkLabel));
                    link.Foreground = AccentBrush;
                    string capturedUrl = url;
                    link.Click += delegate
                    {
                        try { Process.Start(new ProcessStartInfo(capturedUrl) { UseShellExecute = true }); } catch { }
                    };
                    inlines.Add(link);
                }

                lastIdx = m.Index + m.Length;
            }

            if (lastIdx < text.Length)
            {
                inlines.Add(new Run(text.Substring(lastIdx)));
            }
        }

        private static UIElement CreateInlineCopyChip(string textToCopy)
        {
            Border chip = new Border();
            chip.Background = AmberSoftBrush;
            chip.BorderBrush = AmberBorderBrush;
            chip.BorderThickness = new Thickness(1);
            chip.CornerRadius = new CornerRadius(6);
            chip.Padding = new Thickness(7, 1.5, 7, 1.5);
            chip.Margin = new Thickness(2, 1, 2, 1);
            chip.Cursor = Cursors.Hand;
            chip.ToolTip = "Нажмите, чтобы скопировать: " + textToCopy;

            TextBlock label = new TextBlock();
            label.Text = textToCopy + " ❐";
            label.FontFamily = MonoFont;
            label.FontSize = 12;
            label.FontWeight = FontWeights.Medium;
            label.Foreground = AmberTextBrush;
            chip.Child = label;

            bool isCopied = false;

            chip.MouseEnter += delegate
            {
                if (!isCopied)
                {
                    chip.Background = AmberBrush;
                    label.Foreground = Brushes.White;
                }
            };
            chip.MouseLeave += delegate
            {
                if (!isCopied)
                {
                    chip.Background = AmberSoftBrush;
                    label.Foreground = AmberTextBrush;
                }
            };
            chip.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                try { Clipboard.SetText(textToCopy); } catch { }
                isCopied = true;
                chip.Background = GreenBrush;
                chip.BorderBrush = GreenBrush;
                label.Foreground = Brushes.White;
                label.Text = "✓ Скопировано!";

                DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                timer.Tick += delegate
                {
                    timer.Stop();
                    isCopied = false;
                    chip.Background = AmberSoftBrush;
                    chip.BorderBrush = AmberBorderBrush;
                    label.Foreground = AmberTextBrush;
                    label.Text = textToCopy + " ❐";
                };
                timer.Start();
            };

            return chip;
        }
    }
}
