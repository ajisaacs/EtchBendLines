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
            => $"{Direction} {(Angle?.ToString("0.##") ?? "?")}° R{(Radius?.ToString("0.##") ?? "?")}";
    }
}
