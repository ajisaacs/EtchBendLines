using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace EtchBendLines
{
    public class BendLineExtractor
    {
        public BendLineExtractor(string dxfFile)
        {
            using var reader = new DxfReader(dxfFile);
            Document = reader.Read();
        }

        public BendLineExtractor(CadDocument document)
        {
            Document = document;
        }

        /// <summary>
        /// Maximum bend radius to be considered. Anything beyond this number
        /// is a center line for rolling.
        /// </summary>
        public double MaxBendRadius { get; set; } = 4;

        public double SharpRadius { get; set; } = 0.001;

        public bool ReplaceSharpRadius { get; set; } = true;

        /// <summary>
        /// The regular expression pattern the bend note must match
        /// </summary>
        static readonly Regex bendNoteRegex = new Regex(
            @"\b(?<direction>UP|DOWN|DN)\s+(?<angle>\d+(\.\d+)?)°?\s*R\s*(?<radius>\d+(\.\d+)?)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        public CadDocument Document { get; private set; }

        public List<Bend> GetBendLines()
        {
            var bends = new List<Bend>();
            var bendNotes = GetBendNotes();

            if (ReplaceSharpRadius)
                FixSharpBends();

            foreach (var line in Document.Entities.OfType<Line>())
            {
                if (!IsBendLine(line))
                    continue;

                var bend = new Bend
                {
                    Line = line,
                    Direction = BendDirection.Unknown
                };

                bends.Add(bend);
            }

            AssignBendDirections(bends, bendNotes);

            return bends.Where(b => b.Radius <= MaxBendRadius).ToList();
        }

        private bool IsBendLine(Line line)
        {
            if (line.LineType.Name != "CENTERX2")
                return false;

            switch (line.Layer.Name.ToUpperInvariant())
            {
                case "BEND":
                case "BEND LINES":
                case "BENDLINES":
                    return true;
                default:
                    return false;
            }
        }

        private List<MText> GetBendNotes()
        {
            return Document.Entities.OfType<MText>()
                .Where(t => GetBendDirection(t) != BendDirection.Unknown)
                .ToList();
        }

        private void FixSharpBends()
        {
            var bendNotes = GetBendNotes();

            foreach (var bendNote in bendNotes)
            {
                var text = bendNote.Value?.ToUpper();

                if (text == null)
                    continue;

                var index = text.IndexOf("SHARP");

                if (index == -1)
                    continue;

                bendNote.Value = bendNote.Value
                    .Remove(index, 5)
                    .Insert(index, $"R{SharpRadius}");
            }
        }

        private static BendDirection GetBendDirection(MText mText)
        {
            if (mText == null || mText.Value == null)
                return BendDirection.Unknown;

            var text = mText.Value.ToUpper();

            if (text.Contains("UP"))
                return BendDirection.Up;

            if (text.Contains("DOWN") || text.Contains("DN"))
                return BendDirection.Down;

            return BendDirection.Unknown;
        }

        private static void AssignBendDirections(IEnumerable<Bend> bendlines, IEnumerable<MText> bendNotes)
        {
            foreach (var bendline in bendlines)
            {
                var bendNote = FindBendNote(bendline.Line, bendNotes);

                if (bendNote == null)
                    continue;

                bendline.BendNote = bendNote;
                bendline.Direction = GetBendDirection(bendNote);

                var note = bendNote.Value.ToUpper().Replace("SHARP", "R0");
                var match = bendNoteRegex.Match(note);

                if (match.Success)
                {
                    var radius = match.Groups["radius"].Value;
                    var angle = match.Groups["angle"].Value;
                    bendline.Radius = double.Parse(radius, CultureInfo.InvariantCulture);
                    bendline.Angle = double.Parse(angle, CultureInfo.InvariantCulture);
                }
            }
        }

        private static MText FindBendNote(Line bendLine, IEnumerable<MText> bendNotes)
        {
            var bendNotesList = bendNotes.ToList();

            for (int i = bendNotesList.Count - 1; i >= 0; i--)
            {
                var note = bendNotesList[i];
                var notePos = note.InsertPoint.ToXY();
                var perpendicularPoint = bendLine.PointPerpendicularTo(notePos);
                var dist = notePos.DistanceTo(perpendicularPoint);
                var maxAcceptableDist = note.Height * 2.0;

                if (dist > maxAcceptableDist)
                    bendNotesList.RemoveAt(i);
            }

            if (bendNotesList.Count == 0)
                return null;

            var closestNote = bendNotesList.First();
            var p1 = closestNote.InsertPoint.ToXY();
            var p2 = bendLine.ClosestPointOnLineTo(p1);
            var dist2 = p1.DistanceTo(p2);

            for (int i = 1; i < bendNotesList.Count; i++)
            {
                var note = bendNotesList[i];
                var p3 = note.InsertPoint.ToXY();
                var p4 = bendLine.ClosestPointOnLineTo(p3);
                var dist = p3.DistanceTo(p4);

                if (dist < dist2)
                {
                    dist2 = dist;
                    closestNote = note;
                }
            }

            return closestNote;
        }
    }
}
