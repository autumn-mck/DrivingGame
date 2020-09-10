using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace RacingTest
{
	public class Line2D
	{
		public Point P1 { get; set; }
		public Point P2 { get; set; }
		public double Opacity { get; set; }

		public Line2D(Point _P1, Point _P2, double _opacity)
		{
			P1 = _P1;
			P2 = _P2;
			Opacity = _opacity;
		}

		public Line2D(Vector2D _P1, Vector2D _P2, double _opacity)
		{
			P1 = new Point(_P1.X, _P1.Y);
			P2 = new Point(_P2.X, _P2.Y);
			Opacity = _opacity;
		}
	}
}
