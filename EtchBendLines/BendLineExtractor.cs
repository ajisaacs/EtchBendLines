using netDxf;
using netDxf.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EtchBendLines
{
    class BendLineExtractor
    {
        public BendLineExtractor(string dxfFile)
        {
            DxfDocument = DxfDocument.Load(dxfFile);
        }

        public BendLineExtractor(DxfDocument dxfDocument)
        {
            DxfDocument = dxfDocument;
        }

        /// <summary>
        /// Maximum bend radius to be considered. Anything beyond this number
        /// is a center line for rolling.
        /// </summary>
        public double MaxBendRadius { get; set; } = 4;

        /// <summary>
        /// The regular expression pattern the bend note must match
        /// </summary>
        static readonly Regex bendNoteRegex = new Regex(@"(?<direction>UP|DOWN|DN)\s*(?<angle>\d*(\.\d*)?)°\s*R\s*(?<radius>\d*(\.\d*)?)");

        public DxfDocument DxfDocument { get; private set; }

        public List<Bend> GetBendLines()
        {
            var bends = new List<Bend>();
            var bendNotes = GetBendNotes();

            foreach (var line in DxfDocument.Lines)
            {
                if (line.Linetype.Name != "CENTERX2" && line.Layer.Name != "BEND")
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

        private List<MText> GetBendNotes()
        {
            var bendNotes = new List<MText>();

            foreach (var text in DxfDocument.MTexts)
            {
                var textAsUpper = text.Value.ToUpper();

                if (textAsUpper.Contains("UP") || textAsUpper.Contains("DOWN"))
                {
                    bendNotes.Add(text);
                }
            }

            return bendNotes;
        }

        private static void AssignBendDirections(IEnumerable<Bend> bendlines, IEnumerable<MText> bendNotes)
        {
            foreach (var bendline in bendlines)
            {
                var bendNote = FindBendNote(bendline.Line, bendNotes);

                if (bendNote == null)
                    continue;

                bendline.BendNote = bendNote;

                var note = bendNote.Value.ToUpper();

                if (note.Contains("UP"))
                    bendline.Direction = BendDirection.Up;

                else if (note.Contains("DOWN") || note.Contains("DN"))
                    bendline.Direction = BendDirection.Down;

                var match = bendNoteRegex.Match(note);

                if (match.Success)
                {
                    bendline.Radius = double.Parse(match.Groups["radius"].Value);
                    bendline.Angle = double.Parse(match.Groups["angle"].Value);
                }
            }
        }

        private static MText FindBendNote(Line bendLine, IEnumerable<MText> bendNotes)
        {
            var bendNotesList = bendNotes.ToList();

            for (int i = bendNotesList.Count - 1; i >= 0; i--)
            {
                var note = bendNotesList[i];
                var notePos = note.Position.ToVector2();
                var perpendicularPoint = bendLine.PointPerpendicularTo(notePos);
                var dist = notePos.DistanceTo(perpendicularPoint);
                var maxAcceptableDist = note.Height * 2.0;

                if (dist > maxAcceptableDist)
                    bendNotesList.RemoveAt(i);
            }

            if (bendNotesList.Count == 0)
                return null;

            var closestNote = bendNotesList.First();
            var p1 = closestNote.Position.ToVector2();
            var p2 = bendLine.ClosestPointOnLineTo(p1);
            var dist2 = p1.DistanceTo(p2);

            for (int i = 1; i < bendNotesList.Count; i++)
            {
                var note = bendNotesList[i];
                var p3 = note.Position.ToVector2();
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
