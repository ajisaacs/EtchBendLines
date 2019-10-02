using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using System;
using System.Collections.Generic;

namespace EtchBendLines
{
    public class Bend
    {
        public Line Line { get; set; }

        public MText BendNote { get; set; }

        public double YIntercept
        {
            get { return Line.YIntercept(); }
        }

        public double Slope
        {
            get { return Line.Slope(); }
        }

        public bool IsVertical
        {
            get { return Line.IsVertical(); }
        }

        public bool IsHorizontal
        {
            get { return Line.IsHorizontal(); }
        }

        public bool IsParallelTo(Bend bend)
        {
            return Line.IsParallelTo(bend.Line);
        }

        public bool IsPerpendicularTo(Bend bend)
        {
            return Line.IsPerpendicularTo(bend.Line);
        }

        public bool IsCollinearTo(Bend bend)
        {
            if (bend.IsVertical || this.IsVertical)
                return (bend.IsVertical && this.IsVertical && bend.YIntercept == this.YIntercept);

            if (bend.YIntercept != this.YIntercept)
                return false;

            return bend.Slope == this.Slope;
        }

        public List<Line> GetEtchLines(double etchLength)
        {
            var lines = new List<Line>();

            var startPoint = new Vector2(Line.StartPoint.X, Line.StartPoint.Y);
            var endPoint = new Vector2(Line.EndPoint.X, Line.EndPoint.Y);
            var bendLength = startPoint.DistanceTo(endPoint);

            if (bendLength < (etchLength * 3.0))
            {
                lines.Add(new Line(Line.StartPoint, Line.EndPoint));
            }
            else
            {
                var angle = startPoint.AngleTo(endPoint);

                if (Line.IsVertical())
                {
                    var x = Line.StartPoint.X;

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
                    var start = Line.StartPoint.ToVector2();
                    var end = Line.EndPoint.ToVector2();

                    var x1 = Math.Cos(angle);
                    var y1 = Math.Sin(angle);

                    var p1 = new Vector2(start.X, start.Y);
                    var p2 = new Vector2(start.X + x1, start.Y + y1);
                    var p3 = new Vector2(end.X, end.Y);
                    var p4 = new Vector2(end.X - x1, end.Y - y1);

                    lines.Add(new Line(p1, p2));
                    lines.Add(new Line(p3, p4));
                }
            }

            var etchLayer = new Layer("ETCH")
            {
                Color = AciColor.Green,
            };

            foreach (var line in lines)
            {
                line.Layer = etchLayer;
            }

            return lines;
        }

        public BendDirection Direction { get; set; }

        public double Length
        {
            get
            {
                var x = Line.EndPoint.X - Line.StartPoint.X;
                var y = Line.EndPoint.Y - Line.StartPoint.Y;
                return Math.Sqrt(x * x + y * y);
            }
        }

        public double? Radius { get; set; }

        public double? Angle { get; set; }

        public override string ToString()
        {
            return $"{Direction.ToString()} {Angle}° R{Radius}";
        }
    }
}
