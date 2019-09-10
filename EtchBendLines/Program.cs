using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EtchBendLines
{
    class Program
    {
		const double ETCH_LENGTH = 1.0;

		static Layer BendLayer = new Layer("BEND")
		{
			Color = AciColor.Yellow
		};

        static Regex bendNoteRegex = new Regex(@"(?<direction>UP|DOWN|DN)\s*(?<angle>\d*(\.\d*)?)°\s*R\s*(?<radius>\d*(\.\d*)?)");

        static void Main(string[] args)
        {
			var path = AppDomain.CurrentDomain.BaseDirectory;
            var files = Directory.GetFiles(path, "*.dxf", SearchOption.AllDirectories);

            if (files == null || files.Length == 0)
            {
                Console.WriteLine($"No DXF files founds. Place DXF files in \"{AppDomain.CurrentDomain.BaseDirectory}\" and run this program again.");
                PressAnyKeyToExit();
                return;
            }

            foreach (var file in files)
            {
                AddEtchLines(file);
                Console.WriteLine();
            }

            PressAnyKeyToExit();
        }

        static void PressAnyKeyToExit()
        {
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        static void AddEtchLines(string filePath)
        {
            Console.WriteLine(filePath);

            var dxf = LoadDoc(filePath);
            var bendLines = GetBendLines(dxf);
            var bendNotes = GetBendNotes(dxf);

            if (bendLines.Count == 0)
            {
                Console.WriteLine("No bend lines found.");
                return;
            }
            else
            {
                Console.WriteLine($"Found {bendLines.Count} bend lines.");
            }

            if (bendNotes.Count == 0)
            {
                Console.WriteLine("No bend notes found.");
                return;
            }
            else
            {
                Console.WriteLine($"Found {bendNotes.Count} bend notes.");
            }

            foreach (var bendLine in bendLines)
			{
				bendLine.Line.Layer = BendLayer;
				bendLine.Line.Color = AciColor.ByLayer;
			}

			foreach (var note in bendNotes)
			{
				note.Layer = BendLayer;
			}

			var upBends = bendLines.Where(b => b.Direction == BendDirection.Up);
            var upBendCount = upBends.Count();
            var downBendCount = bendLines.Count - upBendCount;

            Console.WriteLine($"{upBendCount} Up     {downBendCount} Down");

            var partType = GetPartType(bendLines);

            foreach (var bendline in upBends)
            {
				var etchLines = bendline.GetEtchLines(ETCH_LENGTH);

                foreach (var etchLine in etchLines)
                {
                    var existing = dxf.Lines
                        .Where(l => IsEtchLayer(l.Layer))
                        .FirstOrDefault(l => l.StartPoint.IsEqualTo(etchLine.StartPoint) && l.EndPoint.IsEqualTo(etchLine.EndPoint));

                    if (existing != null)
                    {
                        // ensure the layer is correct and skip adding the etch line since it already exists.
                        existing.Layer = etchLine.Layer;
                        continue;
                    }

                    dxf.AddEntity(etchLine);
                }
            }

            dxf.Save(filePath);
        }

        static bool IsEtchLayer(Layer layer)
        {
            if (layer.Name == "ETCH")
                return true;

            if (layer.Name == "SCRIBE")
                return true;

            if (layer.Name == "SCRIBE-TEXT")
                return true;

            return false;
        }

        static void AssignBendDirections(IEnumerable<Bend> bendlines, IEnumerable<MText> bendNotes)
        {
            foreach (var bendline in bendlines)
            {
                var bendNote = FindBendNote(bendline.Line, bendNotes);

                if (bendNote == null)
                    continue;

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

        static double MaxBendRadius
        {
            get { return double.Parse(ConfigurationManager.AppSettings["MaxBendRadius"]); }
        }

        //static MText FindBendNote(Line bendLine, IEnumerable<MText> bendNotes)
        //      {
        //          var startPoint = new Vector2(bendLine.StartPoint.X, bendLine.StartPoint.Y);
        //          var endPoint = new Vector2(bendLine.EndPoint.X, bendLine.EndPoint.Y);
        //          var angle = startPoint.AngleTo(endPoint);

        //          if (angle >= 180.0)
        //              angle -= 180.0;

        //          const double ANGLE_TOLERANCE = 0.001;

        //          var bendNotesWithSameAngle = bendNotes.Where(n => Math.Abs(n.Rotation - angle) < ANGLE_TOLERANCE).ToList();

        //          var midPoint = bendLine.MidPoint();

        //          MText closestNote = bendNotes.First();
        //          Vector2 closestPoint = closestNote.Position.ToVector2();



        //          foreach (var note in bendNotes)
        //          {
        //              var pt = note.Position.ToVector2();
        //              var dist = midPoint.DistanceTo(pt);

        //              if (dist < distance)
        //              {
        //                  closestNote = note;
        //                  distance = dist;
        //                  closestPoint = pt;
        //              }
        //          }

        //	var distToBendNote = closestNote.Position.ToVector2().DistanceTo(midPoint);

        //	if (distToBendNote > 18)
        //		return null;

        //          return closestNote;
        //      }

        static MText FindBendNote(Line bendLine, IEnumerable<MText> bendNotes)
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

        static DxfDocument LoadDoc(string file)
        {
            return DxfDocument.Load(file);
        }

        static List<Bend> GetBendLines(DxfDocument dxf)
        {
            var bends = new List<Bend>();
            var bendNotes = GetBendNotes(dxf);

            foreach (var line in dxf.Lines)
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

        static List<MText> GetBendNotes(DxfDocument dxf)
        {
            var bendNotes = new List<MText>();

            foreach (var text in dxf.MTexts)
            {
                var textAsUpper = text.Value.ToUpper();

                if (textAsUpper.Contains("UP") || textAsUpper.Contains("DOWN"))
                {
                    bendNotes.Add(text);
                }
            }

            return bendNotes;
        }

        static PartType GetPartType(List<Bend> bends)
        {
            if (bends.Count == 0)
                return PartType.Flat;

            var upBends = bends.Where(b => b.Direction == BendDirection.Up).ToList();
            var downBends = bends.Where(b => b.Direction == BendDirection.Down).ToList();

            if (upBends.Count == 0 || downBends.Count == 0)
            {
                // bends are going the same direction

                if (bends.Count == 2 && bends[0].IsParallelTo(bends[1]))
                    return PartType.Channel;

                if (bends.Count == 4)
                {
                    var groups = bends.GroupBy(b => b.Line.Slope()).ToList();

                    if (groups.Count == 2)
                    {
                        var bend1 = groups[0].First();
                        var bend2 = groups[1].First();

                        if (bend1.IsPerpendicularTo(bend2))
                        {
                            return PartType.Pan;
                        }
                    }
                }
            }

            if (bends.Count == 1)
                return PartType.Angle;

            if (bends.Count == 2)
            {
                var bend1 = bends[0];
                var bend2 = bends[1];

                if (bend1.IsParallelTo(bend2))
                    return PartType.ZAngle;
            }

            return PartType.Other;
        }
    }

    public enum PartType
    {
        Flat,
        Angle,
        Channel,
        Pan,
        ZAngle,
        Other
    }
}
