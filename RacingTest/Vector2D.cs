using System;
using System.Collections.Generic;
using System.Text;

namespace RacingTest
{
	public class Vector2D
	{
		public double X { get; set; }
		public double Y { get; set; }

		public Vector2D(double _X, double _Y)
		{
			X = _X;
			Y = _Y;
		}

		public double LengthSquared()
		{
			return X * X + Y * Y;
		}

		public double Length()
		{
			return Math.Sqrt(LengthSquared());
		}

		public Vector2D Normalised()
		{
			double length = Length();
			return new Vector2D(X / length, Y / length);
		}

		public static double Dot(Vector2D left, Vector2D right)
		{
			return left.X * right.X + left.Y * right.Y;
		}

		public static Vector2D Zero { get { return new Vector2D(0, 0); } }

		public static Vector2D operator +(Vector2D left, Vector2D right)
		{
			return new Vector2D(left.X + right.X, left.Y + right.Y);
		}
		public static Vector2D operator -(Vector2D value)
		{
			return new Vector2D(-value.X, -value.Y);
		}
		public static Vector2D operator -(Vector2D left, Vector2D right)
		{
			return new Vector2D(left.X - right.X, left.Y - right.Y);
		}
		public static Vector2D operator *(Vector2D left, double right)
		{
			return new Vector2D(left.X * right, left.Y * right);
		}
		public static Vector2D operator *(double left, Vector2D right)
		{
			return new Vector2D(right.X * left, right.Y * left);
		}
		public static Vector2D operator /(Vector2D left, double right)
		{
			return new Vector2D(left.X / right, left.Y / right);
		}
		public static bool operator ==(Vector2D left, Vector2D right)
		{
			if (ReferenceEquals(left, right))
			{
				return true;
			}
			else return false;
			//try
			//{
			//	if (left.X == right.X && left.Y == right.Y) return true;
			//	else return false;
			//}
			//catch
			//{
			//	return false;
			//}
		}
		public static bool operator !=(Vector2D left, Vector2D right)
		{
			return !(left == right);
		}
		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(X, Y);
		}
	}
}
