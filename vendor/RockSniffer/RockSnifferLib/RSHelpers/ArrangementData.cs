using Rocksmith2014PsarcLib.Psarc.Asset;
using Rocksmith2014PsarcLib.Psarc.Models.Sng;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RockSnifferLib.RSHelpers
{
    [Serializable]
    public class ArrangementData
    {
        private const uint NoteMaskHammerOn = 0x0200;
        private const uint NoteMaskPullOff = 0x0400;

        [Serializable]
        public class TabEvent
        {
            public float Time { get; set; }
            public float Sustain { get; set; }
            public int[] Frets { get; set; } = new[] { -1, -1, -1, -1, -1, -1 };
            public string ChordName { get; set; } = "";
            public string Technique { get; set; } = "";
            public bool HammerOn { get; set; }
            public bool PullOff { get; set; }
            public float Bend { get; set; }
        }

        public class Measure
        {
            public float Time { get; set; }
            public int Number { get; set; }

            public Measure() { }

            public Measure(Bpm bpm)
            {
                Time = bpm.Time;
                Number = bpm.Measure;
            }
        }
        public List<Measure> Measures { get; set; }
        public List<TabEvent> TabEvents { get; set; }

        public ArrangementData() { }

        public ArrangementData(SngAsset sng)
        {
            Measures = new List<Measure>();
            TabEvents = new List<TabEvent>();

            if (sng.BPMs.Length > 0)
            {
                Measures.Add(new Measure(sng.BPMs[0]));

                for (int i = 0; i < sng.BPMs.Length; ++i)
                {
                    var bpm = sng.BPMs[i];

                    if (bpm.Measure != Measures.Last().Number)
                    {
                        Measures.Add(new Measure(bpm));
                    }
                }
            }

            // Dynamic difficulty is authored per phrase, not globally. A song's highest global
            // level may therefore contain only the handful of phrases whose MaxDifficulty reaches
            // that level. Build the complete chart by selecting the highest authored level for
            // each phrase iteration, then merge those notes in time order.
            if (sng.Arrangements != null && sng.Arrangements.Length > 0)
            {
                var selectedIterations = new HashSet<int>();
                var selectedNotes = new List<Note>();

                foreach (var level in sng.Arrangements.OrderByDescending(a => a.Difficulty))
                {
                    foreach (var iteration in level.Notes.GroupBy(n =>
                        n.PhraseIterationId >= 0 ? n.PhraseIterationId : 1000000 + n.PhraseId))
                    {
                        if (selectedIterations.Contains(iteration.Key)) continue;

                        var representative = iteration.First();
                        var phraseId = representative.PhraseId;
                        if (representative.PhraseIterationId >= 0 && sng.PhraseIterations != null &&
                            representative.PhraseIterationId < sng.PhraseIterations.Length)
                            phraseId = sng.PhraseIterations[representative.PhraseIterationId].PhraseId;

                        var maxDifficulty = int.MaxValue;
                        if (phraseId >= 0 && sng.Phrases != null && phraseId < sng.Phrases.Length)
                            maxDifficulty = sng.Phrases[phraseId].MaxDifficulty;

                        if (level.Difficulty > maxDifficulty) continue;
                        selectedIterations.Add(iteration.Key);
                        selectedNotes.AddRange(iteration);
                    }
                }

                foreach (var note in selectedNotes.OrderBy(n => n.Time))
                {
                    var tab = new TabEvent
                    {
                        Time = note.Time,
                        Sustain = note.Sustain,
                        Frets = new[] { -1, -1, -1, -1, -1, -1 },
                        Technique = DescribeTechnique(note),
                        HammerOn = (note.NoteMask & NoteMaskHammerOn) != 0,
                        PullOff = (note.NoteMask & NoteMaskPullOff) != 0,
                        Bend = note.MaxBend
                    };

                    if (note.ChordId >= 0 && note.ChordId < sng.Chords.Length)
                    {
                        var chord = sng.Chords[note.ChordId];
                        tab.ChordName = chord.Name ?? "";
                        for (var i = 0; i < Math.Min(6, chord.Frets.Length); i++)
                        {
                            tab.Frets[i] = DecodeFret(chord.Frets[i]);
                        }
                    }
                    else if (note.StringIndex < 6)
                    {
                        tab.Frets[note.StringIndex] = DecodeFret(note.FretId);
                    }

                    TabEvents.Add(tab);
                }
            }
        }

        private static int DecodeFret(byte fret)
        {
            if (fret == 255) return -1;
            if (fret == 254) return -2;
            return fret;
        }

        private static string DescribeTechnique(Note note)
        {
            var parts = new List<string>();
            if (note.SlideTo != 255 && note.SlideTo > 0) parts.Add("/" + note.SlideTo);
            if (note.SlideUnpitchTo != 255 && note.SlideUnpitchTo > 0) parts.Add("\\" + note.SlideUnpitchTo);
            if (note.Vibrato > 0) parts.Add("~");
            // These byte fields use 255 as their "not authored" sentinel.
            if (note.Tap != 255 && note.Tap > 0) parts.Add("T");
            if (note.Slap != 255 && note.Slap > 0) parts.Add("S");
            if (note.Pluck != 255 && note.Pluck > 0) parts.Add("P");
            return string.Join(" ", parts);
        }
    }
}
