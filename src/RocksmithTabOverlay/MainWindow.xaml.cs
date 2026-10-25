using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace RocksmithTabOverlay;

public partial class MainWindow : Window
{
    private const double FreezeAfterSeconds = 0.22;
    private const double JumpThresholdSeconds = 0.60;
    private const double LargeDriftSeconds = 0.10;
    private const double SmallDriftSeconds = 0.025;
    private const double MaxDriftRate = 0.03;
    private const double MaxLargeDriftRate = 0.20;
    private const double MinPlaybackRate = 0.05;
    private const double MaxPlaybackRate = 1.50;
    private const double JudgementLookAheadSeconds = 0.10;
    private const double JudgementStaleSeconds = 0.65;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMilliseconds(600) };
    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromMilliseconds(70) };
    private readonly DispatcherTimer _positionTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly UserSettings _settings = UserSettings.Load();
    private bool _requestActive;
    private bool _userVisible = true;
    private double _sampledTime;
    private double _displayTime;
    private double _lastMemoryTime;
    private double _sampleReceivedAt;
    private double _lastAdvanceAt;
    private double _lastFrameAt;
    private double _playbackRate = 1.0;
    private double _lastRateSongTime;
    private long _lastRateSampleTimestamp;
    private long _lastSampleSequence = -1;
    private bool _hasRateSample;
    private bool _hasMeasuredPlaybackRate;
    private bool _hasTimer;
    private bool _timerAdvancing;
    private string _lastParsedSong = "";
    private string _lastParsedArrangement = "";
    private readonly Queue<int> _pendingPerformanceEvents = new();
    private readonly HashSet<int> _missedEvents = new();
    private string _performanceKey = "";
    private int _performanceCursor;
    private int _lastTotalHits;
    private int _lastTotalMisses;
    private double _lastPerformanceTime;
    private bool _hasPerformanceBaseline;
    private HwndSource? _source;

    private string SnifferUrl
    {
        get
        {
            if (Uri.TryCreate(_settings.SnifferUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                return uri.ToString();

            return "http://127.0.0.1:9938/tab";
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        Tab.ApplySettings(_settings);
        Tab.MissedEvents = _missedEvents;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64();
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle,
            new IntPtr(style | NativeMethods.WsExTransparent | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow));

        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
        NativeMethods.RegisterHotKey(hwnd, NativeMethods.HotkeyIdToggle, 0, 0x77); // F8
        NativeMethods.RegisterHotKey(hwnd, NativeMethods.HotkeyIdMode, 0, 0x76); // F7
        NativeMethods.RegisterHotKey(hwnd, NativeMethods.HotkeyIdBars, 0, 0x73); // F4
        NativeMethods.RegisterHotKey(hwnd, NativeMethods.HotkeyIdScaleDown, 0, 0x74); // F5
        NativeMethods.RegisterHotKey(hwnd, NativeMethods.HotkeyIdScaleUp, 0, 0x75); // F6

        _pollTimer.Tick += OnPollTick;
        _positionTimer.Tick += (_, _) => AttachToRocksmith();
        _lastFrameAt = _clock.Elapsed.TotalSeconds;
        CompositionTarget.Rendering += OnRendering;
        _pollTimer.Start();
        _positionTimer.Start();
        AttachToRocksmith();
        _ = PollAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _pollTimer.Stop();
        _positionTimer.Stop();
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.UnregisterHotKey(hwnd, NativeMethods.HotkeyIdToggle);
        NativeMethods.UnregisterHotKey(hwnd, NativeMethods.HotkeyIdMode);
        NativeMethods.UnregisterHotKey(hwnd, NativeMethods.HotkeyIdBars);
        NativeMethods.UnregisterHotKey(hwnd, NativeMethods.HotkeyIdScaleDown);
        NativeMethods.UnregisterHotKey(hwnd, NativeMethods.HotkeyIdScaleUp);
        _source?.RemoveHook(WndProc);
        _http.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == NativeMethods.HotkeyIdToggle)
        {
            _userVisible = !_userVisible;
            AttachToRocksmith();
            handled = true;
        }
        else if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == NativeMethods.HotkeyIdMode)
        {
            Tab.StaticPageMode = !Tab.StaticPageMode;
            Tab.ShowStatus(Tab.StaticPageMode ? "TWO ROW TAB" : "CONTINUOUS SCROLL");
            SaveSettings();
            Tab.InvalidateVisual();
            handled = true;
        }
        else if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == NativeMethods.HotkeyIdBars)
        {
            Tab.CycleBarsVisible();
            Tab.ShowStatus($"{Tab.BarsVisible} BARS VISIBLE");
            SaveSettings();
            Tab.InvalidateVisual();
            handled = true;
        }
        else if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == NativeMethods.HotkeyIdScaleDown)
        {
            Tab.ChangeNotationScale(-0.1);
            Tab.ShowStatus($"TAB SIZE {Tab.NotationScale:P0}");
            SaveSettings();
            Tab.InvalidateVisual();
            handled = true;
        }
        else if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == NativeMethods.HotkeyIdScaleUp)
        {
            Tab.ChangeNotationScale(0.1);
            Tab.ShowStatus($"TAB SIZE {Tab.NotationScale:P0}");
            SaveSettings();
            Tab.InvalidateVisual();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void SaveSettings()
    {
        _settings.StaticPageMode = Tab.StaticPageMode;
        _settings.NotationScale = Tab.NotationScale;
        _settings.BarsVisible = Tab.BarsVisible;
        _settings.Save();
    }

    private async void OnPollTick(object? sender, EventArgs e)
    {
        if (!_requestActive)
            await PollAsync();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var elapsed = Math.Clamp(now - _lastFrameAt, 0, 0.25);
        _lastFrameAt = now;
        AdvanceClock(now, elapsed);
        Tab.SongTime = _displayTime;

        if (!IsVisible) return;

        // This callback is already vsync-aligned. Redrawing once per composition
        // frame follows the monitor's refresh rate in both modes. In particular,
        // the two-row playhead must not be throttled to the RockSniffer polling
        // rate: every frame uses the interpolated monotonic clock, while fresh
        // samples only correct drift.
        Tab.InvalidateVisual();
    }

    private void AdvanceClock(double now, double elapsed)
    {
        if (!_hasTimer) return;

        // RockSniffer samples the game's clock about ten times per second. Between
        // samples we run on a monotonic wall clock, which gives every frame exactly
        // the same speed. The sample only nudges that clock when it has genuinely
        // drifted; it must never reset the velocity on every poll.
        if (_timerAdvancing && now - _lastAdvanceAt <= FreezeAfterSeconds)
        {
            _displayTime += elapsed * _playbackRate;

            var predicted = _sampledTime
                + Math.Clamp(now - _sampleReceivedAt, 0, 0.25) * _playbackRate;
            var error = predicted - _displayTime;
            if (Math.Abs(error) > SmallDriftSeconds)
            {
                // A changed Riff Repeater speed can leave a little accumulated error
                // while the new rate is being measured. Correct it continuously;
                // never snap the playhead backward to a fresh 100 ms clock sample.
                var limit = Math.Abs(error) > LargeDriftSeconds
                    ? MaxLargeDriftRate
                    : MaxDriftRate;
                var rate = Math.Clamp(error * 1.5, -limit, limit);
                _displayTime += elapsed * rate;
            }
        }
        else
        {
            // No new clock sample means Rocksmith is paused or the sniffer stopped.
            // Freeze on the last measured clock value instead of drifting away.
            _timerAdvancing = false;
            _displayTime = _sampledTime;
        }
    }

    private async Task PollAsync()
    {
        _requestActive = true;
        try
        {
            var json = await _http.GetStringAsync(SnifferUrl);

            // The clock response changes every 100 ms, but the arrangement payload is
            // static for the whole song. Parsing that 90 KB graph on every poll caused
            // UI-thread GC pauses, so first read only the clock/song identity. A full
            // parse happens only when the song or selected arrangement actually changes.
            var clock = await Task.Run(() =>
                JsonSerializer.Deserialize<SnifferClockResponse>(json, JsonOptions));
            if (clock?.MemoryReadout == null) return;

            UpdateClock(
                clock.MemoryReadout.SongTimer,
                clock.SampleSequence,
                clock.SampleTimestamp,
                clock.SampleTimestampFrequency);
            Tab.Stats = clock.MemoryReadout.NoteData;

            var songId = clock.SongDetails?.SongId ?? "";
            var arrangementId = clock.MemoryReadout.ArrangementId;
            if (string.IsNullOrEmpty(songId)) return;
            if (songId == _lastParsedSong && arrangementId == _lastParsedArrangement)
            {
                UpdatePerformance(songId, arrangementId, clock.MemoryReadout.SongTimer,
                    clock.MemoryReadout.NoteData);
                return;
            }

            var full = await Task.Run(() =>
                JsonSerializer.Deserialize<SnifferResponse>(json, JsonOptions));
            if (full?.SongDetails == null) return;

            _lastParsedSong = songId;
            _lastParsedArrangement = arrangementId;

            var arrangement = full.SongDetails.Arrangements.FirstOrDefault(a =>
                string.Equals(a.ArrangementId, arrangementId, StringComparison.OrdinalIgnoreCase));

            // While browsing, Rocksmith can expose a new song while the arrangement ID still
            // belongs to the previous song. Never guess Lead during that gap: only display an
            // exact live path match so Rhythm or Bass cannot silently receive Lead TAB.
            if (arrangement == null)
            {
                Tab.Song = full.SongDetails;
                Tab.Arrangement = null;
                ResetPerformance("", 0, null);
                return;
            }

            Tab.Song = full.SongDetails;
            Tab.Arrangement = arrangement;
            ResetPerformance(songId + "|" + arrangementId, clock.MemoryReadout.SongTimer,
                clock.MemoryReadout.NoteData);
        }
        catch (HttpRequestException)
        {
            // RockSniffer is not running yet; the launcher starts it and polling will reconnect.
        }
        catch (TaskCanceledException)
        {
        }
        catch (JsonException)
        {
        }
        finally
        {
            _requestActive = false;
        }
    }

    private void UpdatePerformance(string songId, string arrangementId, double songTime, NoteData? stats)
    {
        var key = songId + "|" + arrangementId;
        var events = Tab.Arrangement?.Data?.TabEvents;
        if (events == null || !string.Equals(key, _performanceKey, StringComparison.Ordinal))
        {
            ResetPerformance(key, songTime, stats);
            return;
        }

        if (!_hasPerformanceBaseline || stats == null)
        {
            ResetPerformance(key, songTime, stats);
            return;
        }

        // Restarting a song/Riff Repeater phrase can rewind the clock without the
        // counters resetting on the exact same sample. Treat either signal as a new
        // pass so results from the previous run never stain the new one.
        if (songTime < _lastPerformanceTime - 0.25 ||
            stats.TotalNotesHit < _lastTotalHits || stats.TotalNotesMissed < _lastTotalMisses)
        {
            ResetPerformance(key, songTime, stats);
            return;
        }

        while (_performanceCursor < events.Count &&
               events[_performanceCursor].Time <= songTime + JudgementLookAheadSeconds)
        {
            _pendingPerformanceEvents.Enqueue(_performanceCursor);
            _performanceCursor++;
        }

        var hitDelta = Math.Max(0, stats.TotalNotesHit - _lastTotalHits);
        var missDelta = Math.Max(0, stats.TotalNotesMissed - _lastTotalMisses);
        var resultCount = hitDelta + missDelta;
        var assignCount = Math.Min(resultCount, _pendingPerformanceEvents.Count);

        // Rocksmith exposes aggregate counters rather than a per-note result. Polling
        // at 70 ms normally yields one result at a time. If several land together,
        // consume them chronologically and put misses at the end of that tiny batch;
        // this avoids inventing misses when the mapping is ambiguous.
        var missesToAssign = Math.Min(missDelta, assignCount);
        var firstMiss = assignCount - missesToAssign;
        for (var i = 0; i < assignCount; i++)
        {
            var eventIndex = _pendingPerformanceEvents.Dequeue();
            if (i >= firstMiss)
                _missedEvents.Add(eventIndex);
        }

        // At reduced dynamic difficulty the full TAB can contain authored notes that
        // Rocksmith never judges. Let those expire instead of shifting later results.
        while (_pendingPerformanceEvents.Count > 0 &&
               events[_pendingPerformanceEvents.Peek()].Time < songTime - JudgementStaleSeconds)
            _pendingPerformanceEvents.Dequeue();

        _lastTotalHits = stats.TotalNotesHit;
        _lastTotalMisses = stats.TotalNotesMissed;
        _lastPerformanceTime = songTime;
    }

    private void ResetPerformance(string key, double songTime, NoteData? stats)
    {
        _performanceKey = key;
        _performanceCursor = 0;
        _pendingPerformanceEvents.Clear();
        _missedEvents.Clear();
        _lastPerformanceTime = songTime;
        _lastTotalHits = stats?.TotalNotesHit ?? 0;
        _lastTotalMisses = stats?.TotalNotesMissed ?? 0;
        _hasPerformanceBaseline = stats != null;

        var events = Tab.Arrangement?.Data?.TabEvents;
        if (events == null) return;
        while (_performanceCursor < events.Count &&
               events[_performanceCursor].Time <= songTime + JudgementLookAheadSeconds)
            _performanceCursor++;
    }

    private void UpdateClock(double incomingTime, long sampleSequence, long sampleTimestamp,
        long sampleTimestampFrequency)
    {
        var now = _clock.Elapsed.TotalSeconds;
        if (!_hasTimer)
        {
            _hasTimer = true;
            _displayTime = incomingTime;
            _lastMemoryTime = incomingTime;
            _lastAdvanceAt = now;
            _sampledTime = incomingTime;
            _sampleReceivedAt = now;
            _lastSampleSequence = sampleSequence;
            ResetRateSample(incomingTime, sampleTimestamp);
            return;
        }

        var advance = incomingTime - _lastMemoryTime;
        var isNewSample = sampleSequence > 0
            ? sampleSequence != _lastSampleSequence
            : Math.Abs(advance) > 0.0001;
        var jumped = advance < -0.02
            || advance > 0.75
            || Math.Abs(incomingTime - _displayTime) > JumpThresholdSeconds;

        if (isNewSample)
        {
            if (jumped)
                ResetRateSample(incomingTime, sampleTimestamp);
            else
                ObservePlaybackRate(incomingTime, sampleTimestamp, sampleTimestampFrequency, now);
            _lastSampleSequence = sampleSequence;
        }

        if (jumped)
        {
            // Scrubbing, restarting, Riff Repeater jumps, and pausing for a while
            // should land immediately rather than making the scroll race to catch up.
            _displayTime = incomingTime;
            _timerAdvancing = advance > 0.001;
            if (_timerAdvancing) _lastAdvanceAt = now;
        }
        else if (advance > 0.001)
        {
            if (!_timerAdvancing)
            {
                // First advancing sample after a pause: align to it once, then run
                // continuously from the wall clock again.
                _displayTime = incomingTime;
            }
            _timerAdvancing = true;
            _lastAdvanceAt = now;
        }

        if (advance > 0.001 || jumped)
        {
            // Only a genuinely new clock reading may reset the interpolation anchor.
            // Repeated polls of the same value must leave the anchor alone.
            _sampledTime = incomingTime;
            _sampleReceivedAt = now;
        }
        else
        {
            _sampledTime = incomingTime;
        }

        _lastMemoryTime = incomingTime;
    }

    private void ObservePlaybackRate(double songTime, long sampleTimestamp, long sampleTimestampFrequency,
        double receivedAt)
    {
        // New RockSniffer builds attach their monotonic memory-read timestamp. It
        // removes HTTP polling jitter from this measurement. The receive clock is
        // retained as a backward-compatible fallback for an older backend.
        var timestamp = sampleTimestamp > 0 ? sampleTimestamp : (long)(receivedAt * Stopwatch.Frequency);
        var frequency = sampleTimestamp > 0 && sampleTimestampFrequency > 0
            ? sampleTimestampFrequency
            : Stopwatch.Frequency;

        if (_hasRateSample && timestamp > _lastRateSampleTimestamp)
        {
            var elapsed = (timestamp - _lastRateSampleTimestamp) / (double)frequency;
            var songAdvance = songTime - _lastRateSongTime;
            if (elapsed is >= 0.03 and <= 0.75 && songAdvance > 0.001)
            {
                var observedRate = songAdvance / elapsed;
                if (observedRate is >= MinPlaybackRate and <= MaxPlaybackRate)
                {
                    if (!_hasMeasuredPlaybackRate)
                    {
                        _playbackRate = observedRate;
                        _hasMeasuredPlaybackRate = true;
                    }
                    else
                    {
                        // React quickly to a deliberate speed change, then use a
                        // gentler filter for float/timer noise at a steady speed.
                        var difference = Math.Abs(observedRate - _playbackRate);
                        var blend = difference > 0.08 ? 0.60 : 0.22;
                        _playbackRate += (observedRate - _playbackRate) * blend;
                    }
                }
            }
        }

        _lastRateSongTime = songTime;
        _lastRateSampleTimestamp = timestamp;
        _hasRateSample = true;
    }

    private void ResetRateSample(double songTime, long sampleTimestamp)
    {
        _lastRateSongTime = songTime;
        _lastRateSampleTimestamp = sampleTimestamp;
        _hasRateSample = sampleTimestamp > 0;
        _hasMeasuredPlaybackRate = false;
    }

    private void AttachToRocksmith()
    {
        var process = Process.GetProcessesByName("Rocksmith2014").FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
        if (process == null)
        {
            Visibility = Visibility.Hidden;
            return;
        }
        var hwnd = process.MainWindowHandle;
        var foreground = NativeMethods.GetForegroundWindow();
        NativeMethods.GetWindowThreadProcessId(foreground, out var foregroundProcessId);
        var rocksmithIsActive = foregroundProcessId == process.Id && !NativeMethods.IsIconic(hwnd);
        var shouldShow = _userVisible && rocksmithIsActive;
        if (shouldShow && Visibility != Visibility.Visible)
            ResyncClock();
        Visibility = shouldShow ? Visibility.Visible : Visibility.Hidden;
        if (!rocksmithIsActive) return;
        if (!NativeMethods.GetClientRect(hwnd, out var rect)) return;
        var origin = new NativeMethods.Point();
        if (!NativeMethods.ClientToScreen(hwnd, ref origin)) return;

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;

        // Win32 returns physical pixels, while WPF window geometry and all drawing
        // inside TabSurface use device-independent units. Converting the whole
        // layout, including its height limits, keeps the two-row page usable on
        // 1080p, 1440p, 4K and every Windows scaling factor.
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget == null) return;
        var fromDevice = source.CompositionTarget.TransformFromDevice;
        var clientWidth = width * fromDevice.M11;
        var clientHeight = height * fromDevice.M22;
        var topOffset = 12.0;
        var overlayHeight = Math.Min(
            Math.Clamp(clientHeight * 0.47, 320.0, 680.0),
            Math.Max(200.0, clientHeight - topOffset));
        var overlayOrigin = new System.Windows.Point(origin.X * fromDevice.M11, origin.Y * fromDevice.M22 + topOffset);
        Left = overlayOrigin.X;
        Top = overlayOrigin.Y;
        Width = Math.Max(1, clientWidth);
        Height = Math.Max(1, overlayHeight);
    }

    private void ResyncClock()
    {
        var now = _clock.Elapsed.TotalSeconds;
        _lastFrameAt = now;
        if (!_hasTimer) return;

        // While hidden, composition callbacks may stop. Re-anchor to the newest sample
        // before the overlay becomes visible again instead of replaying the gap.
        if (!_timerAdvancing || now - _lastAdvanceAt > FreezeAfterSeconds)
        {
            _timerAdvancing = false;
            _displayTime = _sampledTime;
            return;
        }

        var age = Math.Clamp(now - _sampleReceivedAt, 0, 0.25);
        _displayTime = _sampledTime + age * _playbackRate;
    }
}
