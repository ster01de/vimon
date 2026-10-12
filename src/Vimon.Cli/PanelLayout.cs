using Spectre.Console;
using Spectre.Console.Rendering;

namespace Vimon.Cli;

/// <summary>A titled panel before it is drawn, so a pair can grow the shorter one to the height of the other.</summary>
internal sealed record Section(string Title, IRenderable Body, Style BorderStyle)
{
    private static readonly Text BlankLine = new(" ");

    /// <param name="blankLines">Empty lines added below the body.</param>
    public Panel ToPanel(int blankLines = 0)
    {
        IRenderable body = blankLines <= 0
            ? Body
            : new Rows(new[] { Body }.Concat(Enumerable.Repeat<IRenderable>(BlankLine, blankLines)));

        return new Panel(body)
        {
            Header = new PanelHeader($" {Title} "),
            Border = BoxBorder.Rounded,
            BorderStyle = BorderStyle,
            Expand = true
        };
    }
}

/// <summary>
/// Panels in pairs that split the window evenly. Both panels of a pair end on the same line: the
/// shorter one is measured at the width it gets and filled up with blank lines, so the layout below
/// keeps its place however many rows either side has.
/// </summary>
internal static class PanelLayout
{
    public const int SideBySideMinWidth = 100;
    private const int PairGap = 1;

    /// <summary>Sections in pairs from left to right; an odd last section takes the full width.</summary>
    public static IRenderable PairUp(IReadOnlyList<Section> sections, int width) =>
        new Rows(sections.Chunk(2).Select(pair => pair.Length == 2 ? Pair(pair[0], pair[1], width) : pair[0].ToPanel()));

    /// <summary>Two sections side by side, or stacked below <see cref="SideBySideMinWidth"/>.</summary>
    public static IRenderable Pair(Section left, Section right, int width) =>
        width < SideBySideMinWidth
            ? new Rows(left.ToPanel(), right.ToPanel())
            : new EqualHeightPair(left, right);

    private sealed class EqualHeightPair(Section left, Section right) : IRenderable
    {
        public Measurement Measure(RenderOptions options, int maxWidth) =>
            Grid(left.ToPanel(), right.ToPanel(), maxWidth).Measure(options, maxWidth);

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            var (leftWidth, rightWidth) = Widths(maxWidth);
            int leftLines = LineCount(left.ToPanel(), options, leftWidth);
            int rightLines = LineCount(right.ToPanel(), options, rightWidth);
            int lines = Math.Max(leftLines, rightLines);

            return Grid(left.ToPanel(lines - leftLines), right.ToPanel(lines - rightLines), maxWidth).Render(options, maxWidth);
        }
    }

    // A panel ends every line, its bottom border included, with a line break.
    private static int LineCount(IRenderable panel, RenderOptions options, int width) =>
        panel.Render(options, width).Count(segment => segment.IsLineBreak);

    private static (int Left, int Right) Widths(int width)
    {
        int left = (width - PairGap) / 2;
        return (left, width - PairGap - left);
    }

    private static IRenderable Grid(Panel left, Panel right, int width)
    {
        var (leftWidth, rightWidth) = Widths(width);
        var grid = new Grid();
        grid.AddColumn(new GridColumn { Width = leftWidth, Padding = new Padding(0, 0, PairGap, 0) });
        grid.AddColumn(new GridColumn { Width = rightWidth, Padding = new Padding(0) });
        grid.AddRow(left, right);
        return grid;
    }
}
