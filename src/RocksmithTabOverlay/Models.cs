using System.Text.Json.Serialization;

namespace RocksmithTabOverlay;

public enum SnifferState
{
    None = 0,
    InMenus = 1,
    SongSelected = 2,
    SongStarting = 3,
    SongPlaying = 4,
    SongEnding = 5,
}

public sealed class SnifferResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("currentState")] public SnifferState CurrentState { get; set; }
    [JsonPropertyName("memoryReadout")] public MemoryReadout? MemoryReadout { get; set; }
    [JsonPropertyName("songDetails")] public SongDetails? SongDetails { get; set; }
}

// Lightweight shape for the high-frequency /tab poll. System.Text.Json skips the
// large, unchanging arrangements subtree because it is not represented here.
public sealed class SnifferClockResponse
{
    [JsonPropertyName("currentState")] public SnifferState CurrentState { get; set; }
    [JsonPropertyName("memoryReadout")] public MemoryReadout? MemoryReadout { get; set; }
    [JsonPropertyName("songDetails")] public SongSummary? SongDetails { get; set; }
    [JsonPropertyName("sampleSequence")] public long SampleSequence { get; set; }
    [JsonPropertyName("sampleTimestamp")] public long SampleTimestamp { get; set; }
    [JsonPropertyName("sampleTimestampFrequency")] public long SampleTimestampFrequency { get; set; }
}

public sealed class SongSummary
{
    [JsonPropertyName("songID")] public string SongId { get; set; } = "";
}

public sealed class MemoryReadout
{
    [JsonPropertyName("songTimer")] public double SongTimer { get; set; }
    [JsonPropertyName("songID")] public string SongId { get; set; } = "";
    [JsonPropertyName("arrangementID")] public string ArrangementId { get; set; } = "";
    [JsonPropertyName("gameStage")] public string GameStage { get; set; } = "";
    [JsonPropertyName("noteData")] public NoteData? NoteData { get; set; }
}

public sealed class NoteData
{
    [JsonPropertyName("Accuracy")] public double Accuracy { get; set; }
    [JsonPropertyName("TotalNotes")] public int TotalNotes { get; set; }
    [JsonPropertyName("TotalNotesHit")] public int TotalNotesHit { get; set; }
    [JsonPropertyName("TotalNotesMissed")] public int TotalNotesMissed { get; set; }
    [JsonPropertyName("CurrentHitStreak")] public int CurrentHitStreak { get; set; }
    [JsonPropertyName("HighestHitStreak")] public int HighestHitStreak { get; set; }
}

public sealed class SongDetails
{
    [JsonPropertyName("songID")] public string SongId { get; set; } = "";
    [JsonPropertyName("songName")] public string SongName { get; set; } = "";
    [JsonPropertyName("artistName")] public string ArtistName { get; set; } = "";
    [JsonPropertyName("songLength")] public double SongLength { get; set; }
    [JsonPropertyName("arrangements")] public List<ArrangementDetails> Arrangements { get; set; } = [];
}

public sealed class ArrangementDetails
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("arrangementID")] public string ArrangementId { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("data")] public ArrangementData? Data { get; set; }
}

public sealed class ArrangementData
{
    [JsonPropertyName("Measures")] public List<Measure> Measures { get; set; } = [];
    [JsonPropertyName("TabEvents")] public List<TabEvent> TabEvents { get; set; } = [];
}

public sealed class Measure
{
    [JsonPropertyName("Time")] public double Time { get; set; }
    [JsonPropertyName("Number")] public int Number { get; set; }
}

public sealed class TabEvent
{
    [JsonPropertyName("Time")] public double Time { get; set; }
    [JsonPropertyName("Sustain")] public double Sustain { get; set; }
    [JsonPropertyName("Frets")] public int[] Frets { get; set; } = [-1, -1, -1, -1, -1, -1];
    [JsonPropertyName("ChordName")] public string ChordName { get; set; } = "";
    [JsonPropertyName("Technique")] public string Technique { get; set; } = "";
    [JsonPropertyName("HammerOn")] public bool HammerOn { get; set; }
    [JsonPropertyName("PullOff")] public bool PullOff { get; set; }
    [JsonPropertyName("Tremolo")] public bool Tremolo { get; set; }
    [JsonPropertyName("Bend")] public double Bend { get; set; }
    [JsonPropertyName("SlideTo")] public int SlideTo { get; set; } = -1;
    [JsonPropertyName("SlideUnpitchTo")] public int SlideUnpitchTo { get; set; } = -1;
}
