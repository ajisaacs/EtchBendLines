using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EtchBendLines
{
    class Program
    {
		const double ETCH_LENGTH = 1.0;

		static Layer BendLayer = new Layer("BEND")
		{
			Color = AciColor.Yellow
		};

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
                Console.WriteLine("----------------------------------------------------------------");

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
            var name = Path.GetFileNameWithoutExtension(filePath);
            Console.WriteLine($"Adding etch lines to file \"{name}\"");

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

			AssignBendDirections(bendLines, bendNotes);

			var upBends = bendLines.Where(b => b.Direction == BendDirection.Up);

            Console.WriteLine($"{upBends.Count()} up bends, {bendLines.Count - upBends.Count()} down bends.");

            foreach (var bendline in upBends)
            {
				var etchLines = bendline.GetEtchLines(ETCH_LENGTH);
				dxf.AddEntity(etchLines);
            }

            dxf.Save(filePath);
        }

		static void AssignBendDirections(IEnumerable<Bend> bendlines, IEnumerable<MText> bendNotes)
		{
			foreach (var bendline in bendlines)
			{
				var bendNote = FindBendNote(bendline.Line, bendNotes);

				if (bendNote == null)
					continue;

				bendNote.Layer = BendLayer;

				var note = bendNote.Value.ToUpper();

				if (note.Contains("UP"))
					bendline.Direction = BendDirection.Up;

				else if (note.Contains("DOWN") || note.Contains("DN"))
					bendline.Direction = BendDirection.Down;
			}
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

		static MText FindBendNote(Line bendLIne, IEnumerable<MText> bendNotes)
		{
			var list = bendNotes.ToList();
			var shortestDist = double.MaxValue;
			var shortestDistIndex = -1;

			for (int i = 0; i < list.Count; i++)
			{
				var note = list[i];
				var notePos = note.Position.ToVector2();
				var perpendicularPoint = bendLIne.PointPerpendicularTo(notePos);
				var dist = notePos.DistanceTo(perpendicularPoint);

				if (dist < shortestDist)
				{
					shortestDistIndex = i;
					shortestDist = dist;
				}
			}

			if (shortestDistIndex == -1)
				return null;

			var bendNote = list[shortestDistIndex];
			var maxAcceptableDist = bendNote.Height * 2.0;

			if (shortestDist > maxAcceptableDist)
				return null;

			return bendNote;
		}

        static Vector2? FindClosestPoint(Vector2 originPoint, List<Vector2> pts)
        {
            if (pts == null || pts.Any() == false)
                return null;

            var closest = pts[0];
            var distance = originPoint.DistanceTo(closest);

            for (int i = 1; i < pts.Count; i++)
            {
                var pt = pts[i];
                var dist = originPoint.DistanceTo(pt);

                if (dist < distance)
                {
                    distance = dist;
                    closest = pts[i];
                }
            }

            return closest;
        }

        static DxfDocument LoadDoc(string file)
        {
            return DxfDocument.Load(file);
        }

        static List<Bend> GetBendLines(DxfDocument dxf)
        {
			var bends = new List<Bend>();

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

            return bends;
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
    }
}
