using netDxf;
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

        static double MaxBendRadius
        {
            get { return double.Parse(ConfigurationManager.AppSettings["MaxBendRadius"]); }
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
