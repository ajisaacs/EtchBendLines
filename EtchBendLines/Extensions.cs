using netDxf;
using netDxf.Entities;
using System;

namespace EtchBendLines
{
    public static class Extensions
    {
        const double TwoPI = Math.PI * 2.0;

        public static Vector2 ToVector2(this Vector3 pt)
        {
            return new Vector2(pt.X, pt.Y);
        }

        public static bool IsEqualTo(this Vector3 pt, Vector3 pt1)
        {
            return pt.X.IsEqualTo(pt1.X) && pt.Y.IsEqualTo(pt1.Y) && pt.Z.IsEqualTo(pt1.Z);
        }

        public static bool IsVertical(this Line line)
        {
            return line.StartPoint.X == line.EndPoint.X;
        }

        public static bool IsHorizontal(this Line line)
        {
            return line.StartPoint.Y == line.EndPoint.Y;
        }

		public static double Slope(this Line line)
		{
			if (line.IsVertical())
				return double.NaN;

			var p1 = line.StartPoint;
			var p2 = line.EndPoint;

			return Math.Round((p2.Y - p1.Y) / (p2.X - p1.X), 4);
		}

		public static double YIntercept(this Line line)
		{
			var p1 = line.StartPoint;
			var p2 = line.EndPoint;
			var slope = line.Slope();

			// y = mx + b

			return Math.Round(p1.Y - slope * p1.X, 4);
		}

		public static Vector2 PointPerpendicularTo(this Line line, Vector2 pt)
		{
			var startPoint = line.StartPoint.ToVector2();
			var endPoint = line.EndPoint.ToVector2();

			var d1 = pt - startPoint;
			var d2 = endPoint - startPoint;
			var dotProduct = d1.X * d2.X + d1.Y * d2.Y;
			var lengthSquared = d2.X * d2.X + d2.Y * d2.Y;
			var param = dotProduct / lengthSquared;

			if (param < 0)
				return startPoint;
			else if (param > 1)
				return endPoint;
			else
			{
				return new Vector2(
					startPoint.X + param * d2.X,
					startPoint.Y + param * d2.Y);
			}
		}

		public static Vector2 MidPoint(this Line line)
		{
			var x = (line.StartPoint.X + line.EndPoint.X) * 0.5;
			var y = (line.StartPoint.Y + line.EndPoint.Y) * 0.5;

			return new Vector2(x, y);
		}

        public static double DistanceTo(this Vector2 startPoint, Vector2 endPoint)
        {
            var x = endPoint.X - startPoint.X;
            var y = endPoint.Y - startPoint.Y;

            return Math.Sqrt(x * x + y * y);
        }

        public static double AngleTo(this Vector2 startPoint, Vector2 endPoint)
        {
            var x = endPoint.X - startPoint.X;
            var y = endPoint.Y - startPoint.Y;

            return NormalizeRad(Math.Atan2(y, x));
        }

        static double NormalizeRad(double angle)
        {
            double r = angle % TwoPI;
            return r < 0 ? TwoPI + r : r;
        }

        public static bool IsPerpendicularTo(this Line line1, Line line2)
        {
            bool line1Vertical = line1.IsVertical();
            bool line2Vertical = line2.IsVertical();

            if (line1Vertical)
                return line2.IsHorizontal();
            else if (line2.IsVertical())
                return line1.IsHorizontal();

            return line1.Slope().IsEqualTo(-1 / line2.Slope());
        }

        public static bool IsParallelTo(this Line line1, Line line2)
        {
            if (line1.IsVertical())
            {
                if (line2.IsVertical())
                    return true;

                return false;
            }
            else if (line2.IsVertical())
            {
                return false;
            }

            return line2.Slope().IsEqualTo(line1.Slope());
        }

        public const double Epsilon = 0.00001;

        public static bool IsEqualTo(this double a, double b, double tolerance = Epsilon)
        {
            return Math.Abs(b - a) <= tolerance;
        }

        public static Vector2 ClosestPointOnLineTo(this Line line, Vector2 pt)
        {
            var startPoint = line.StartPoint.ToVector2();
            var endPoint = line.EndPoint.ToVector2();

            var diff1 = pt - startPoint;
            var diff2 = endPoint - startPoint;
            var dotProduct = diff1.X * diff2.X + diff1.Y * diff2.Y;
            var lengthSquared = diff2.X * diff2.X + diff2.Y * diff2.Y;
            var param = dotProduct / lengthSquared;

            if (param < 0)
                return startPoint;
            else if (param > 1)
                return endPoint;
            else
            {
                return new Vector2(
                    startPoint.X + param * diff2.X,
                    startPoint.Y + param * diff2.Y);
            }
        }
    }
}
