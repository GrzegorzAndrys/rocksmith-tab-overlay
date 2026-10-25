using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace RocksmithTabOverlay;

public sealed class TabSurface : FrameworkElement
{
    private enum NoteVisualState
    {
        Upcoming,
        Played,
        Missed
    }

    private static readonly string[] StringNames = ["E", "B", "G", "D", "A", "E"];
    private static readonly string[] FretLabels = BuildFretLabels();
    private static readonly Brush[] StringBrushes =
    [
        BrushFrom("#D24CF0"), BrushFrom("#56D835"), BrushFrom("#FF9E2A"),
        BrushFrom("#32B5FF"), BrushFrom("#F0D52E"), BrushFrom("#FF5555")
    ];
    private static readonly Brush LeadPathBrush = BrushFrom("#48D6FF");
    private static readonly Brush RhythmPathBrush = BrushFrom("#FFAD4A");
    private static readonly Brush BassPathBrush = BrushFrom("#F0D52E");
    private static readonly Brush BadgeFill = BrushFrom(210, 16, 19, 25);
    private static readonly Pen LeadBadgePen = PenFrom(LeadPathBrush, 1.2);
    private static readonly Pen RhythmBadgePen = PenFrom(RhythmPathBrush, 1.2);
    private static readonly Pen BassBadgePen = PenFrom(BassPathBrush, 1.2);
    private static readonly Brush HitBrush = BrushFrom("#66E38D");
    private static readonly Brush MissBrush = BrushFrom("#FF7777");
    private static readonly Brush ControlsBrush = BrushFrom(130, 220, 225, 235);
    private static readonly Brush StatusFill = BrushFrom(235, 20, 23, 30);
    private static readonly Pen StatusPen = PenFrom(BrushFrom(180, 72, 214, 255), 1.5);

    private static readonly Brush PanelBrush = BrushFrom(215, 8, 10, 14);
    private static readonly Brush ActiveSystemFill = BrushFrom(34, 72, 214, 255);
    private static readonly Brush InactiveSystemFill = BrushFrom(22, 255, 255, 255);
    private static readonly Pen ActiveSystemPen = PenFrom(BrushFrom(105, 72, 214, 255), 1);
    private static readonly Pen InactiveSystemPen = PenFrom(BrushFrom(45, 255, 255, 255), 1);
    private static readonly Brush CurrentMeasureFill = BrushFrom(25, 100, 230, 255);
    private static readonly Pen StringLinePen = PenFrom(BrushFrom(175, 190, 195, 205), 1.25);
    private static readonly Pen MeasureLinePen = PenFrom(BrushFrom(72, 255, 255, 255), 1);
    private static readonly Brush NoteFill = BrushFrom(235, 12, 14, 18);
    private static readonly Brush PlayedNoteFill = BrushFrom(175, 12, 14, 18);
    private static readonly Brush PastNoteFill = BrushFrom(120, 12, 14, 18);
    private static readonly Brush PastLabelBrush = BrushFrom(135, 175, 180, 188);
    private static readonly Brush[] PlayedStringBrushes = CreateStringBrushes(145);
    private static readonly Pen[] ActiveNotePens = CreateStringPens(255, 2);
    private static readonly Pen[] PlayedNotePens = CreateStringPens(145, 1.4);
    private static readonly Pen PastNotePen = PenFrom(PastLabelBrush, 1);
    private static readonly Pen[] ActiveSustainPens = CreateStringPens(170, 2);
    private static readonly Pen[] PlayedSustainPens = CreateStringPens(105, 1.6);
    private static readonly Pen PastSustainPen = PenFrom(BrushFrom(70, 175, 180, 188), 2);
    private static readonly Pen PlayheadPen = PenFrom(BrushFrom(235, 255, 255, 255), 2);
    private static readonly Pen ProgressTrackPen = PenFrom(BrushFrom(90, 255, 255, 255), 2);
    private static readonly Pen ProgressFillPen = PenFrom(BrushFrom(255, 72, 214, 255), 3);
    private static readonly Brush LegatoBrush = BrushFrom("#7FE7FF");
    private static readonly Brush BendBrush = BrushFrom("#FFBF5A");
    private static readonly Brush TechniqueBadgeFill = BrushFrom(235, 8, 10, 14);
    private static readonly Pen LegatoPen = PenFrom(LegatoBrush, 1.8);
    private static readonly Pen BendPen = PenFrom(BendBrush, 1.8);
    private static readonly Pen PastTechniquePen = PenFrom(PastLabelBrush, 1.2);

    private readonly Dictionary<(string Text, double Size, Color Color, FontWeight Weight), FormattedText> _textCache = new();
    private readonly Dictionary<(string Text, double Size, Color Color, FontWeight Weight), FormattedText> _liveTextCache = new();
    private readonly Dictionary<int, string> _measureNumberText = new();
    private double _pixelsPerDip = 1.0;
    private double _dpiScaleX = 1.0;
    private double _dpiScaleY = 1.0;

    public SongDetails? Song { get; set; }
    public ArrangementDetails? Arrangement { get; set; }
    public NoteData? Stats { get; set; }
    public IReadOnlySet<int> MissedEvents { get; set; } = new HashSet<int>();
    public double SongTime { get; set; }
    public double PixelsPerSecond { get; set; } = 145;
    public bool StaticPageMode { get; set; } = true;
    public double NotationScale { get; private set; } = 1.0;
    public int BarsVisible { get; private set; } = 4;
    private string _statusMessage = "";
    private DateTime _statusUntilUtc;

    internal void ApplySettings(UserSettings settings)
    {
        StaticPageMode = settings.StaticPageMode;
        NotationScale = Math.Round(Math.Clamp(settings.NotationScale, 0.7, 1.5), 1);
        BarsVisible = settings.BarsVisible is 2 or 4 or 6 or 8 ? settings.BarsVisible : 4;
        _textCache.Clear();
        _liveTextCache.Clear();
    }

    public void ShowStatus(string message)
    {
        _statusMessage = message;
        _statusUntilUtc = DateTime.UtcNow.AddSeconds(1.6);
    }

    public void ChangeNotationScale(double amount)
    {
        NotationScale = Math.Round(Math.Clamp(NotationScale + amount, 0.7, 1.5), 1);
        _textCache.Clear();
        _liveTextCache.Clear();
    }

    public void CycleBarsVisible() => BarsVisible = BarsVisible switch
    {
        2 => 4,
        4 => 6,
        6 => 8,
        _ => 2
    };

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        UpdateDpiCache();
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 200 || h < 140) return;

        dc.DrawRoundedRectangle(PanelBrush, null, new Rect(0, 0, w, h), 14, 14);

        if (Song == null || Arrangement?.Data == null)
        {
            DrawHeader(dc, w);
            var message = Song == null ? "Start a song in Learn a Song" : "Waiting for the selected instrument path…";
            DrawText(dc, message, 24, Brushes.White, new Point(34, h / 2 - 15));
            DrawText(dc, "TAB appears when Rocksmith confirms Lead, Rhythm, or Bass", 14, Brushes.Gray,
                new Point(36, h / 2 + 20));
            return;
        }

        var left = 62.0;
        var right = w - 18.0;
        if (StaticPageMode)
        {
            var page = GetTwoRowPage();
            var contentTop = 66.0;
            var contentBottom = h - 16.0;
            var gap = 24.0;
            var systemHeight = (contentBottom - contentTop - gap) / 2.0;
            DrawTabSystem(dc, left, right, contentTop, contentTop + systemHeight,
                page.Row1Start, page.Row1End, true, false);
            DrawTabSystem(dc, left, right, contentTop + systemHeight + gap, contentBottom,
                page.Row2Start, page.Row2End, false, false);
        }
        else
        {
            var playhead = Math.Max(left + 100, w * 0.31);
            var rangeStart = SongTime - (playhead - left) / PixelsPerSecond;
            var rangeEnd = SongTime + (right - playhead) / PixelsPerSecond;
            DrawTabSystem(dc, left, right, 70, h - 22, rangeStart, rangeEnd, true, true);
        }

        DrawHeader(dc, w);
        DrawStatus(dc, w);
    }

    private void DrawTabSystem(DrawingContext dc, double left, double right, double regionTop, double regionBottom,
        double rangeStart, double rangeEnd, bool active, bool scrolling)
    {
        if (Arrangement?.Data == null) return;
        var stringTop = regionTop + 24;
        var stringBottom = regionBottom - 18;
        var spacing = (stringBottom - stringTop) / 5.0;
        var pixelsPerSecond = scrolling
            ? PixelsPerSecond
            : (right - left) / Math.Max(0.1, rangeEnd - rangeStart);
        var playhead = scrolling
            ? Math.Max(left + 100, ActualWidth * 0.31)
            : Math.Clamp(left + (SongTime - rangeStart) * pixelsPerSecond, left, right);

        dc.DrawRoundedRectangle(
            active ? ActiveSystemFill : InactiveSystemFill,
            active ? ActiveSystemPen : InactiveSystemPen,
            new Rect(left - 8, regionTop, right - left + 16, regionBottom - regionTop), 7, 7);

        if (active && !scrolling)
        {
            var currentStart = Arrangement.Data.Measures.LastOrDefault(m => m.Time <= SongTime)?.Time ?? rangeStart;
            var currentEnd = Arrangement.Data.Measures.FirstOrDefault(m => m.Time > SongTime)?.Time ?? rangeEnd;
            var currentLeft = Math.Clamp(left + (currentStart - rangeStart) * pixelsPerSecond, left, right);
            var currentRight = Math.Clamp(left + (currentEnd - rangeStart) * pixelsPerSecond, left, right);
            dc.DrawRectangle(CurrentMeasureFill, null,
                new Rect(currentLeft, regionTop + 1, Math.Max(0, currentRight - currentLeft), regionBottom - regionTop - 2));
        }

        for (var row = 0; row < 6; row++)
        {
            var y = stringTop + row * spacing;
            dc.DrawLine(StringLinePen, new Point(left, y), new Point(right, y));
            DrawText(dc, StringNames[row], 16 * NotationScale, StringBrushes[row],
                new Point(27, y - 11 * NotationScale), FontWeights.Bold);
        }

        foreach (var measure in Arrangement.Data.Measures)
        {
            if (measure.Time < rangeStart || measure.Time > rangeEnd) continue;
            var x = scrolling
                ? playhead + (measure.Time - SongTime) * pixelsPerSecond
                : playhead + (measure.Time - SongTime) * pixelsPerSecond;
            if (!scrolling)
                x = left + (measure.Time - rangeStart) * pixelsPerSecond;
            x = SnapX(x);
            dc.DrawLine(MeasureLinePen,
                new Point(x, stringTop - 10), new Point(x, stringBottom + 8));
            DrawText(dc, MeasureNumberText(measure.Number), 11 * NotationScale, Brushes.Gray,
                new Point(x + 3, regionTop + 3));
        }

        var previousNoteX = Enumerable.Repeat(double.NaN, 6).ToArray();
        var previousNoteRadius = new double[6];

        for (var noteIndex = 0; noteIndex < Arrangement.Data.TabEvents.Count; noteIndex++)
        {
            var note = Arrangement.Data.TabEvents[noteIndex];
            // In scrolling mode a little tolerance lets notes slide in and out at
            // the edges. In the two-row page the next row starts exactly at
            // rangeEnd, so including that boundary note would draw a clipped
            // duplicate on the right edge of this row.
            if (scrolling)
            {
                if (note.Time < rangeStart - 0.1 || note.Time > rangeEnd + 0.1) continue;
            }
            else if (note.Time < rangeStart || note.Time >= rangeEnd)
            {
                continue;
            }

            var x = scrolling
                ? playhead + (note.Time - SongTime) * pixelsPerSecond
                : left + (note.Time - rangeStart) * pixelsPerSecond;
            x = SnapX(x);
            var noteEndTime = note.Time + Math.Max(0, note.Sustain);
            var completed = note.Sustain > 0.08
                ? noteEndTime < SongTime - 0.04
                : note.Time < SongTime - 0.04;
            var missed = note.Time < SongTime - 0.04 && MissedEvents.Contains(noteIndex);
            var state = missed
                ? NoteVisualState.Missed
                : completed ? NoteVisualState.Played : NoteVisualState.Upcoming;
            var chordTop = double.MaxValue;

            for (var stringIndex = 0; stringIndex < Math.Min(6, note.Frets.Length); stringIndex++)
            {
                var fret = note.Frets[stringIndex];
                if (fret < -1) fret = -2;
                if (fret == -1) continue;
                var row = 5 - stringIndex;
                var y = SnapY(stringTop + row * spacing);
                chordTop = Math.Min(chordTop, y);
                var label = fret == -2 ? FretLabels[25]
                    : fret is >= 0 and < 25 ? FretLabels[fret]
                    : fret.ToString(CultureInfo.InvariantCulture);
                var radius = (label.Length > 1 ? 21.5 : 18.5) * NotationScale;

                var legato = note.HammerOn && note.PullOff ? "H/P"
                    : note.HammerOn ? "H"
                    : note.PullOff ? "P"
                    : "";
                if (!string.IsNullOrEmpty(legato))
                {
                    if (!double.IsNaN(previousNoteX[stringIndex]))
                        DrawLegatoConnector(dc, previousNoteX[stringIndex], previousNoteRadius[stringIndex],
                            x, radius, y, regionTop, legato, state != NoteVisualState.Upcoming);
                    else
                        DrawTechniqueBadge(dc, legato, x, Math.Max(regionTop + 4, y - 35 * NotationScale),
                            LegatoBrush, state != NoteVisualState.Upcoming);
                }

                var noteFill = state switch
                {
                    NoteVisualState.Played => PlayedNoteFill,
                    NoteVisualState.Missed => PastNoteFill,
                    _ => NoteFill
                };
                var notePen = state switch
                {
                    NoteVisualState.Played => PlayedNotePens[row],
                    NoteVisualState.Missed => PastNotePen,
                    _ => ActiveNotePens[row]
                };
                var labelBrush = state switch
                {
                    NoteVisualState.Played => PlayedStringBrushes[row],
                    NoteVisualState.Missed => PastLabelBrush,
                    _ => StringBrushes[row]
                };

                dc.DrawEllipse(noteFill, notePen,
                    new Point(x, y), radius, 18.5 * NotationScale);
                var ft = MakeText(label, 23 * NotationScale,
                    labelBrush, FontWeights.Bold);
                dc.DrawText(ft, Snap(new Point(x - ft.Width / 2, y - ft.Height / 2)));

                if (note.Sustain > 0.08)
                {
                    var endX = Math.Min(right, x + note.Sustain * pixelsPerSecond);
                    var sustainPen = state switch
                    {
                        NoteVisualState.Played => PlayedSustainPens[row],
                        NoteVisualState.Missed => PastSustainPen,
                        _ => ActiveSustainPens[row]
                    };
                    var slideTarget = note.SlideTo >= 0 ? note.SlideTo : note.SlideUnpitchTo;
                    if (slideTarget >= 0)
                    {
                        DrawSlide(dc, x, radius, y, endX, right, fret, slideTarget,
                            note.SlideUnpitchTo >= 0, sustainPen, noteFill, notePen, labelBrush);
                    }
                    else if (note.Tremolo)
                    {
                        DrawTremolo(dc, x + radius, endX, y, sustainPen);
                    }
                    else
                    {
                        dc.DrawLine(sustainPen, new Point(x + radius, y), new Point(endX, y));
                    }
                }

                if (note.Bend > 0.001)
                    DrawBend(dc, x, y, radius, regionTop, right, note.Bend,
                        state != NoteVisualState.Upcoming);

                previousNoteX[stringIndex] = x;
                previousNoteRadius[stringIndex] = radius;
            }

            if (chordTop < double.MaxValue && !string.IsNullOrWhiteSpace(note.ChordName))
                DrawText(dc, note.ChordName, 13 * NotationScale,
                    state == NoteVisualState.Upcoming ? Brushes.White : Brushes.Gray,
                    new Point(x - 12, Math.Max(regionTop + 1, chordTop - 34)), FontWeights.SemiBold);
            if (chordTop < double.MaxValue && !string.IsNullOrWhiteSpace(note.Technique))
                DrawText(dc, note.Technique, 11 * NotationScale, Brushes.LightGray,
                    new Point(x + 13, Math.Min(regionBottom - 4, chordTop + 16)));
        }

        if (active)
        {
            dc.DrawLine(PlayheadPen,
                new Point(playhead, stringTop - 16), new Point(playhead, stringBottom + 10));
            var marker = new StreamGeometry();
            using (var markerContext = marker.Open())
            {
                markerContext.BeginFigure(new Point(playhead - 6, stringTop - 16), true, true);
                markerContext.LineTo(new Point(playhead + 6, stringTop - 16), true, false);
                markerContext.LineTo(new Point(playhead, stringTop - 6), true, false);
            }
            marker.Freeze();
            dc.DrawGeometry(Brushes.White, null, marker);

            var progressY = regionBottom - 5;
            dc.DrawLine(ProgressTrackPen,
                new Point(left, progressY), new Point(right, progressY));
            dc.DrawLine(ProgressFillPen,
                new Point(left, progressY), new Point(playhead, progressY));
        }
    }

    private void DrawTremolo(DrawingContext dc, double startX, double endX, double y, Pen pen)
    {
        if (endX <= startX + 1) return;

        var step = Math.Max(5.0, 7.0 * NotationScale);
        var amplitude = Math.Max(2.0, 3.2 * NotationScale);
        var x = startX;
        var previous = new Point(x, y);
        var direction = -1.0;
        while (x < endX)
        {
            x = Math.Min(endX, x + step);
            var next = new Point(x, x >= endX ? y : y + direction * amplitude);
            dc.DrawLine(pen, previous, next);
            previous = next;
            direction = -direction;
        }
    }

    private void DrawSlide(DrawingContext dc, double x, double radius, double y,
        double endX, double right, int sourceFret, int targetFret, bool unpitched,
        Pen pen, Brush noteFill, Pen notePen, Brush labelBrush)
    {
        var startX = x + radius;
        if (endX <= startX + 2) return;

        var targetLabel = targetFret is >= 0 and < 25
            ? FretLabels[targetFret]
            : targetFret.ToString(CultureInfo.InvariantCulture);
        var targetRadius = (targetLabel.Length > 1 ? 20.0 : 17.0) * NotationScale;
        var targetVisible = endX + targetRadius <= right + 1;
        var lineEndX = targetVisible ? endX - targetRadius : endX;
        if (lineEndX <= startX + 2)
        {
            DrawText(dc, (targetFret >= sourceFret ? "/" : "\\") + targetLabel,
                12 * NotationScale, labelBrush, new Point(x + radius + 2, y - 22 * NotationScale),
                FontWeights.Bold);
            return;
        }

        // Both fret numbers stay on their string, as in conventional TAB. The
        // connecting stroke itself slopes up or down to make the slide direction
        // readable at a glance. Unpitched slides use a slightly steeper stroke.
        var direction = targetFret >= sourceFret ? -1.0 : 1.0;
        var rise = (unpitched ? 6.0 : 4.0) * NotationScale;
        dc.DrawLine(pen,
            new Point(startX, y - direction * rise),
            new Point(lineEndX, y + direction * rise));

        if (!targetVisible) return;
        dc.DrawEllipse(noteFill, notePen, new Point(endX, y), targetRadius, 17.0 * NotationScale);
        var targetText = MakeText(targetLabel, 21 * NotationScale, labelBrush, FontWeights.Bold);
        dc.DrawText(targetText, Snap(new Point(endX - targetText.Width / 2, y - targetText.Height / 2)));
    }

    private void DrawLegatoConnector(DrawingContext dc, double fromX, double fromRadius,
        double toX, double toRadius, double y, double regionTop, string label, bool past)
    {
        var startX = fromX + Math.Min(13 * NotationScale, fromRadius * 0.7);
        var endX = toX - Math.Min(13 * NotationScale, toRadius * 0.7);
        if (endX - startX < 14 * NotationScale)
        {
            DrawTechniqueBadge(dc, label, toX,
                Math.Max(regionTop + 4, y - 35 * NotationScale), LegatoBrush, past);
            return;
        }

        var baseY = y - 14 * NotationScale;
        var archY = Math.Max(regionTop + 7, baseY - 13 * NotationScale);
        var pen = past ? PastTechniquePen : LegatoPen;
        var shoulder = Math.Min((endX - startX) * 0.18, 9 * NotationScale);
        dc.DrawLine(pen, new Point(startX, baseY), new Point(startX + shoulder, archY));
        dc.DrawLine(pen, new Point(startX + shoulder, archY), new Point(endX - shoulder, archY));
        dc.DrawLine(pen, new Point(endX - shoulder, archY), new Point(endX, baseY));
        DrawTechniqueBadge(dc, label, (startX + endX) / 2,
            archY - 2 * NotationScale, LegatoBrush, past);
    }

    private void DrawBend(DrawingContext dc, double x, double y, double radius,
        double regionTop, double right, double amount, bool past)
    {
        var direction = x > right - 70 ? -1.0 : 1.0;
        var start = new Point(x + direction * radius * 0.45, y - 12 * NotationScale);
        var end = new Point(start.X + direction * 10 * NotationScale,
            Math.Max(regionTop + 8, y - 37 * NotationScale));
        var pen = past ? PastTechniquePen : BendPen;
        dc.DrawLine(pen, start, end);
        dc.DrawLine(pen, end, new Point(end.X - direction * 5, end.Y + 7));
        dc.DrawLine(pen, end, new Point(end.X + direction * 3, end.Y + 6));

        var amountText = amount.ToString("0.#", CultureInfo.InvariantCulture);
        DrawTechniqueBadge(dc, "B" + amountText,
            end.X + direction * 18 * NotationScale, end.Y + 1, BendBrush, past);
    }

    private void DrawTechniqueBadge(DrawingContext dc, string label, double centerX,
        double centerY, Brush color, bool past)
    {
        var text = MakeText(label, Math.Max(12, 14 * NotationScale),
            past ? PastLabelBrush : color, FontWeights.Bold);
        var rect = new Rect(
            centerX - text.Width / 2 - 5,
            centerY - text.Height / 2 - 3,
            text.Width + 10,
            text.Height + 6);
        var activePen = ReferenceEquals(color, BendBrush) ? BendPen : LegatoPen;
        dc.DrawRoundedRectangle(past ? PastNoteFill : TechniqueBadgeFill,
            past ? PastTechniquePen : activePen, rect, 4, 4);
        dc.DrawText(text, Snap(new Point(centerX - text.Width / 2, centerY - text.Height / 2)));
    }

    private (double Row1Start, double Row1End, double Row2Start, double Row2End) GetTwoRowPage()
    {
        var measures = Arrangement?.Data?.Measures;
        if (measures == null || measures.Count < 2)
        {
            var rowDuration = BarsVisible * 2.0;
            var rowStart = Math.Max(0, Math.Floor(SongTime / rowDuration) * rowDuration);
            return (rowStart, rowStart + rowDuration, rowStart + rowDuration, rowStart + rowDuration * 2);
        }

        var active = 0;
        for (var i = 1; i < measures.Count && measures[i].Time <= SongTime; i++)
            active = i;

        // The current system always occupies the upper row. At its final bar the
        // lower preview is promoted to the top and a new preview is placed below,
        // matching the rolling two-system layout used by conventional TAB readers.
        var first = active / BarsVisible * BarsVisible;
        var row1Start = first == 0 && SongTime < measures[0].Time ? 0 : measures[first].Time;
        var averageBar = measures.Count > 1 ? Math.Max(0.5, measures[1].Time - measures[0].Time) : 2.0;
        var songEnd = Song?.SongLength ?? 0;
        double Boundary(int index, double fallbackStart) => index < measures.Count
            ? measures[index].Time
            : songEnd > fallbackStart ? songEnd : fallbackStart + BarsVisible * averageBar;

        var row1End = Boundary(first + BarsVisible, row1Start);
        var row2Start = row1End;
        var row2End = Boundary(first + BarsVisible * 2, row2Start);
        if (row2End <= row2Start)
            row2End = row2Start + BarsVisible * averageBar;
        return (row1Start, row1End, row2Start, row2End);
    }

    private void DrawHeader(DrawingContext dc, double width)
    {
        var title = Song == null ? "ROCKSMITH TAB" : $"{Song.ArtistName} — {Song.SongName}";
        var titleText = MakeText(title, 16, Brushes.White, FontWeights.SemiBold);
        dc.DrawText(titleText, Snap(new Point(28, 15)));
        if (Arrangement != null)
        {
            var pathName = string.IsNullOrWhiteSpace(Arrangement.Type) ? Arrangement.Name : Arrangement.Type;
            var (pathColor, pathPen) = pathName.ToLowerInvariant() switch
            {
                "rhythm" => (RhythmPathBrush, RhythmBadgePen),
                "bass" => (BassPathBrush, BassBadgePen),
                _ => (LeadPathBrush, LeadBadgePen)
            };
            var badge = MakeText(pathName.ToUpperInvariant(), 12, pathColor, FontWeights.Bold);
            var badgeX = Math.Min(28 + titleText.Width + 22, Math.Max(400, width - 760));
            dc.DrawRoundedRectangle(BadgeFill,
                pathPen, new Rect(badgeX - 8, 12, badge.Width + 16, badge.Height + 8), 6, 6);
            dc.DrawText(badge, Snap(new Point(badgeX, 16)));
        }
        var mode = StaticPageMode ? "TWO ROW TAB" : "SCROLL";
        var zoom = Math.Round(NotationScale * 100);
        var arrangementName = Arrangement == null ? "" :
            string.Equals(Arrangement.Type, Arrangement.Name, StringComparison.OrdinalIgnoreCase)
                ? Arrangement.Name
                : $"{Arrangement.Type} / {Arrangement.Name}";
        var detail = Arrangement == null ? "waiting for Rocksmith" :
            $"{arrangementName}  •  {mode}  •  {BarsVisible} BARS/ROW  •  {zoom}%";
        DrawText(dc, detail, 11, Brushes.LightGray, new Point(30, 40));
        if (Stats != null && Stats.TotalNotes > 0)
        {
            var scoreX = Math.Max(560, width - 570);
            var hit = MakeLiveText($"HIT {Stats.TotalNotesHit}", 13, HitBrush, FontWeights.SemiBold);
            dc.DrawText(hit, Snap(new Point(scoreX, 18)));
            var miss = MakeLiveText($"MISS {Stats.TotalNotesMissed}", 13, MissBrush, FontWeights.SemiBold);
            dc.DrawText(miss, Snap(new Point(scoreX + hit.Width + 24, 18)));
            var streak = MakeLiveText($"STREAK {Stats.CurrentHitStreak}/{Stats.HighestHitStreak}", 13,
                Brushes.LightCyan, FontWeights.SemiBold);
            dc.DrawText(streak, Snap(new Point(scoreX + hit.Width + miss.Width + 48, 18)));
        }
        const string controls = "F4 bars/row   F5/F6 size   F7 mode   F8 hide";
        var controlsText = MakeText(controls, 10, ControlsBrush);
        dc.DrawText(controlsText, Snap(new Point(width - controlsText.Width - 28, 41)));
        var timeText = MakeLiveText(FormatTime(SongTime), 13, Brushes.White, FontWeights.SemiBold);
        dc.DrawText(timeText, Snap(new Point(width - 78, 18)));
    }

    private void DrawStatus(DrawingContext dc, double width)
    {
        if (DateTime.UtcNow >= _statusUntilUtc || string.IsNullOrWhiteSpace(_statusMessage)) return;
        var text = MakeLiveText(_statusMessage, 18, Brushes.White, FontWeights.Bold);
        var x = (width - text.Width) / 2;
        dc.DrawRoundedRectangle(StatusFill, StatusPen,
            new Rect(x - 18, 10, text.Width + 36, text.Height + 14), 9, 9);
        dc.DrawText(text, Snap(new Point(x, 17)));
    }

    private static string[] BuildFretLabels()
    {
        var labels = new string[26];
        for (var fret = 0; fret < 25; fret++)
            labels[fret] = fret.ToString(CultureInfo.InvariantCulture);
        labels[25] = "x";
        return labels;
    }

    private string MeasureNumberText(int number)
    {
        if (_measureNumberText.TryGetValue(number, out var text))
            return text;

        text = number.ToString(CultureInfo.InvariantCulture);
        _measureNumberText[number] = text;
        return text;
    }

    private static string FormatTime(double seconds) =>
        $"{Math.Max(0, (int)seconds) / 60}:{Math.Max(0, (int)seconds) % 60:00}";

    private static SolidColorBrush BrushFrom(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush BrushFrom(byte alpha, byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static Pen PenFrom(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    private static Pen[] CreateStringPens(byte alpha, double thickness) =>
        StringBrushes.Select(brush =>
        {
            var color = ((SolidColorBrush)brush).Color;
            return PenFrom(BrushFrom(alpha, color.R, color.G, color.B), thickness);
        }).ToArray();

    private static Brush[] CreateStringBrushes(byte alpha) =>
        StringBrushes.Select(brush =>
        {
            var color = ((SolidColorBrush)brush).Color;
            return (Brush)BrushFrom(alpha, color.R, color.G, color.B);
        }).ToArray();

    private void UpdateDpiCache()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        if (Math.Abs(dpi.PixelsPerDip - _pixelsPerDip) < 0.0001 &&
            Math.Abs(dpi.DpiScaleX - _dpiScaleX) < 0.0001 &&
            Math.Abs(dpi.DpiScaleY - _dpiScaleY) < 0.0001)
            return;

        _pixelsPerDip = dpi.PixelsPerDip;
        _dpiScaleX = dpi.DpiScaleX;
        _dpiScaleY = dpi.DpiScaleY;
        _textCache.Clear();
        _liveTextCache.Clear();
    }

    // Snap text and note origins to physical pixels, not WPF DIPs. At 150% scaling
    // an integer DIP is 1.5 device pixels, so the old Math.Round(x) still left many
    // glyphs on half-pixels and made moving numbers look soft.
    private double SnapX(double value) => Math.Round(value * _dpiScaleX) / _dpiScaleX;
    private double SnapY(double value) => Math.Round(value * _dpiScaleY) / _dpiScaleY;
    private Point Snap(Point point) => new(SnapX(point.X), SnapY(point.Y));

    private FormattedText MakeText(string text, double size, Brush brush, FontWeight? weight = null) =>
        MakeTextCore(text, size, brush, weight, _textCache, 4096);

    private FormattedText MakeLiveText(string text, double size, Brush brush, FontWeight? weight = null) =>
        MakeTextCore(text, size, brush, weight, _liveTextCache, 64);

    private FormattedText MakeTextCore(string text, double size, Brush brush, FontWeight? weight,
        Dictionary<(string Text, double Size, Color Color, FontWeight Weight), FormattedText> cache, int limit)
    {
        var fontWeight = weight ?? FontWeights.Normal;
        var color = (brush as SolidColorBrush)?.Color ?? Colors.White;
        var key = (text, size, color, fontWeight);
        if (cache.TryGetValue(key, out var cached))
            return cached;

        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Consolas"), FontStyles.Normal, fontWeight, FontStretches.Normal),
            size, brush, _pixelsPerDip);
        if (cache.Count >= limit) cache.Clear();
        cache[key] = formatted;
        return formatted;
    }

    private void DrawText(DrawingContext dc, string text, double size, Brush brush, Point point, FontWeight? weight = null) =>
        dc.DrawText(MakeText(text, size, brush, weight), Snap(point));
}
