using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using ACadSharp.IO;
using CSMath;
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
            Color = Color.Yellow
        };

        static readonly Layer EtchLayer = new Layer("ETCH")
        {
            Color = Color.Green,
        };

        private const double DefaultEtchLength = 1.0;

        /// <summary>
        /// Maximum bend radius to be considered. Anything beyond this number will be rolled.
        /// </summary>
        public double MaxBendRadius { get; set; } = 4.0;

        private CadDocument LoadDocument(string path)
        {
            try
            {
                using var reader = new DxfReader(path);
                reader.Configuration.CreateDefaults = true;
                return reader.Read()
                    ?? throw new InvalidOperationException("DXF load returned null");
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new ApplicationException($"Failed to load DXF '{path}'", ex);
            }
        }

        private List<Bend> ExtractBends(CadDocument doc)
        {
            var extractor = new BendLineExtractor(doc);
            return extractor.GetBendLines();
        }

        private HashSet<string> BuildExistingKeySet(CadDocument doc)
            => new HashSet<string>(
                doc.Entities.OfType<Line>()
                   .Where(l => IsEtchLayer(l.Layer))
                   .Select(l => KeyFor(l.StartPoint, l.EndPoint))
            );

        private void InsertEtchLines(CadDocument doc, IEnumerable<Bend> bends, HashSet<string> existingKeys, double etchLength)
        {
            foreach (var bend in bends)
            {
                foreach (var etch in GetEtchLines(bend.Line, etchLength))
                {
                    var key = KeyFor(etch.StartPoint, etch.EndPoint);
                    if (existingKeys.Contains(key))
                    {
                        // ensure correct layer
                        var existing = doc.Entities.OfType<Line>().First(l => KeyFor(l) == key);
                        existing.Layer = EtchLayer;
                    }
                    else
                    {
                        etch.Layer = EtchLayer;
                        doc.Entities.Add(etch);
                        existingKeys.Add(key);
                    }
                }
            }
        }

        private void SaveDocument(CadDocument doc, string path)
        {
            using (var writer = new DxfWriter(path, doc, false))
            {
                writer.Write();
            }
            Console.WriteLine($"→ Saved with etch lines: {path}");
        }

        private static string KeyFor(Line l) => KeyFor(l.StartPoint, l.EndPoint);

        private static string KeyFor(XYZ a, XYZ b) => $"{a.X:F3},{a.Y:F3}|{b.X:F3},{b.Y:F3}";

        public void AddEtchLines(string filePath, double etchLength = DefaultEtchLength)
        {
            Console.WriteLine(filePath);

            var doc = LoadDocument(filePath);
            var bends = ExtractBends(doc);

            // Ensure all bend lines are on the BEND layer with ByLayer color
            foreach (var bend in bends)
            {
                bend.Line.Layer = BendLayer;
                bend.Line.Color = Color.ByLayer;
            }

            var upBends = bends.Where(b => b.Direction == BendDirection.Up);
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

        private List<Line> GetEtchLines(Line bendLine, double etchLength)
        {
            var lines = new List<Line>();

            var startPoint = new XY(bendLine.StartPoint.X, bendLine.StartPoint.Y);
            var endPoint = new XY(bendLine.EndPoint.X, bendLine.EndPoint.Y);
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

                    var p1 = new XYZ(x, bottomY1, 0);
                    var p2 = new XYZ(x, bottomY2, 0);
                    var p3 = new XYZ(x, topY1, 0);
                    var p4 = new XYZ(x, topY2, 0);

                    lines.Add(new Line(p1, p2));
                    lines.Add(new Line(p3, p4));
                }
                else
                {
                    var start = bendLine.StartPoint.ToXY();
                    var end = bendLine.EndPoint.ToXY();

                    var dx = Math.Cos(angle) * etchLength;
                    var dy = Math.Sin(angle) * etchLength;

                    var p1 = new XYZ(start.X, start.Y, 0);
                    var p2 = new XYZ(start.X + dx, start.Y + dy, 0);
                    var p3 = new XYZ(end.X, end.Y, 0);
                    var p4 = new XYZ(end.X - dx, end.Y - dy, 0);

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
