using netDxf;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EtchBendLines
{
    public class Etcher
    {
        public Layer BendLayer = new Layer("BEND")
        {
            Color = AciColor.Yellow
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
            }

            var upBends = bendLines.Where(b => b.Direction == BendDirection.Up);
            var upBendCount = upBends.Count();
            var downBendCount = bendLines.Count - upBendCount;

            Console.WriteLine($"{upBendCount} Up     {downBendCount} Down");

            foreach (var bendline in upBends)
            {
                var etchLines = bendline.GetEtchLines(EtchLength);

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
    }
}
