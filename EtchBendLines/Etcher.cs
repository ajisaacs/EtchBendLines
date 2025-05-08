using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EtchBendLines
{
    public class Etcher
    {
        public readonly Layer BendLayer = new Layer("BEND")
        {
            Color = AciColor.Yellow
        };

        static readonly Layer EtchLayer = new Layer("ETCH")
        {
            Color = AciColor.Green,
        };

        public double EtchLength { get; set; } = 1.0;

        public double MaxBendRadius { get; set; } = 4.0;

        public void AddEtchLines(string filePath)
        {
            Console.WriteLine(filePath);

            var bendLineExtractor = new BendLineExtractor(filePath);
            bendLineExtractor.MaxBendRadius = MaxBendRadius;

            var bendLines = bendLineExtractor.GetBendLines();

            if (bendLines.Count == 0)
            {
                Console.WriteLine("No bend lines found.");
                return;
            }
            else
            {
                Console.WriteLine($"Found {bendLines.Count} bend lines.");
            }

            foreach (var bendLine in bendLines)
            {
                bendLine.Line.Layer = BendLayer;
                bendLine.Line.Color = AciColor.ByLayer;

                bendLine.BendNote.Layer = BendLayer;
                bendLine.BendNote.Color = AciColor.ByLayer;
            }

            var upBends = bendLines.Where(b => b.Direction == BendDirection.Up);
            var upBendCount = upBends.Count();
            var downBendCount = bendLines.Count - upBendCount;

            Console.WriteLine($"{upBendCount} Up     {downBendCount} Down");

            foreach (var bendline in upBends)
            {
                var etchLines = GetEtchLines(bendline.Line, EtchLength);

                foreach (var etchLine in etchLines)
                {
                    var existing = bendLineExtractor.DxfDocument.Lines
                        .Where(l => IsEtchLayer(l.Layer))
                        .FirstOrDefault(l => l.StartPoint.IsEqualTo(etchLine.StartPoint) && l.EndPoint.IsEqualTo(etchLine.EndPoint));

                    if (existing != null)
                    {
                        // ensure the layer is correct and skip adding the etch line since it already exists.
                        existing.Layer = etchLine.Layer;
                        continue;
                    }

                    bendLineExtractor.DxfDocument.AddEntity(etchLine);
                }
            }

            bendLineExtractor.DxfDocument.Save(filePath);
        }

        private bool IsEtchLayer(Layer layer)
        {
            if (layer.Name == "ETCH")
                return true;

            if (layer.Name == "SCRIBE")
                return true;

            if (layer.Name == "SCRIBE-TEXT")
                return true;

            return false;
        }

        public List<Line> GetEtchLines(Line bendLine, double etchLength)
        {
            var lines = new List<Line>();

            var startPoint = new Vector2(bendLine.StartPoint.X, bendLine.StartPoint.Y);
            var endPoint = new Vector2(bendLine.EndPoint.X, bendLine.EndPoint.Y);
            var bendLength = startPoint.DistanceTo(endPoint);

            if (bendLength < (etchLength * 3.0))
            {
                lines.Add(new Line(bendLine.StartPoint, bendLine.EndPoint));
            }
            else
            {
                var angle = startPoint.AngleTo(endPoint);

                if (bendLine.IsVertical())
                {
                    var x = bendLine.StartPoint.X;

                    var bottomY1 = Math.Min(startPoint.Y, endPoint.Y);
                    var bottomY2 = bottomY1 + etchLength;

                    var topY1 = Math.Max(startPoint.Y, endPoint.Y);
                    var topY2 = topY1 - etchLength;

                    var p1 = new Vector2(x, bottomY1);
                    var p2 = new Vector2(x, bottomY2);
                    var p3 = new Vector2(x, topY1);
                    var p4 = new Vector2(x, topY2);

                    lines.Add(new Line(p1, p2));
                    lines.Add(new Line(p3, p4));
                }
                else
                {
                    var start = bendLine.StartPoint.ToVector2();
                    var end = bendLine.EndPoint.ToVector2();

                    var dx = Math.Cos(angle) * etchLength;
                    var dy = Math.Sin(angle) * etchLength;

                    var p1 = new Vector2(start.X, start.Y);
                    var p2 = new Vector2(start.X + dx, start.Y + dy);
                    var p3 = new Vector2(end.X, end.Y);
                    var p4 = new Vector2(end.X - dx, end.Y - dy);

                    lines.Add(new Line(p1, p2));
                    lines.Add(new Line(p3, p4));
                }
            }

            foreach (var line in lines)
            {
                line.Layer = EtchLayer;
            }

            return lines;
        }
    }
}
