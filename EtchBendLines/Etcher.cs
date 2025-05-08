using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.IO;
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

        private const double DefaultEtchLength = 1.0;

        /// <summary>
        /// Maximum bend radius to be considered. Anything beyond this number will be rolled.
        /// </summary>
        public double MaxBendRadius { get; set; } = 4.0;

        private DxfDocument LoadDocument(string path)
        {
            try
            {
                return DxfDocument.Load(path)
                    ?? throw new InvalidOperationException("DXF load returned null");
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Failed to load DXF '{path}'", ex);
            }
        }

        private IEnumerable<Bend> ExtractUpBends(DxfDocument doc)
        {
            // your existing BendLineExtractor logic
            var extractor = new BendLineExtractor(doc);
            return extractor.GetBendLines()
                            .Where(b => b.Direction == BendDirection.Up);
        }

        private HashSet<string> BuildExistingKeySet(DxfDocument doc)
            => new HashSet<string>(
                doc.Lines
                   .Where(l => IsEtchLayer(l.Layer))
                   .Select(l => KeyFor(l.StartPoint, l.EndPoint))
            );

        private void InsertEtchLines(DxfDocument doc, IEnumerable<Bend> bends, HashSet<string> existingKeys, double etchLength)
        {
            foreach (var bend in bends)
            {
                foreach (var etch in GetEtchLines(bend.Line, etchLength))
                {
                    var key = KeyFor(etch.StartPoint, etch.EndPoint);
                    if (existingKeys.Contains(key))
                    {
                        // ensure correct layer
                        var existing = doc.Lines.First(l => KeyFor(l) == key);
                        existing.Layer = EtchLayer;
                    }
                    else
                    {
                        etch.Layer = EtchLayer;
                        doc.AddEntity(etch);
                        existingKeys.Add(key);
                    }
                }
            }
        }

        private void SaveDocument(DxfDocument doc, string path)
        {
            doc.Save(path);
            Console.WriteLine($"→ Saved with etch lines: {path}");
        }

        private static string KeyFor(Line l) => KeyFor(l.StartPoint, l.EndPoint);

        private static string KeyFor(Vector3 a, Vector3 b) => $"{a.X:F3},{a.Y:F3}|{b.X:F3},{b.Y:F3}";

        public void AddEtchLines(string filePath, double etchLength = DefaultEtchLength)
        {
            Console.WriteLine(filePath);

            var doc = LoadDocument(filePath);
            var upBends = ExtractUpBends(doc);
            var existing = BuildExistingKeySet(doc);

            InsertEtchLines(doc, upBends, existing, etchLength);
            SaveDocument(doc, filePath);
        }

        private bool IsEtchLayer(Layer layer)
        {
            if (layer == null)
                return false;

            if (layer.Name.Equals(EtchLayer.Name, StringComparison.OrdinalIgnoreCase))
                return true;

            switch (layer.Name)
            {
                case "ETCH":
                case "SCRIBE":
                case "SCRIBE-TEXT":
                    return true;
                default:
                    return false;
            }
        }

        private IEnumerable<Line> GetEtchLines(Line bendLine, double etchLength)
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

            yield break;
        }
    }
}
