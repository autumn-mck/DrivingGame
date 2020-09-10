using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Windows.Media;

namespace RacingTest
{
	/// <summary>
	/// Used to return the information required to create a new tyre track
	/// </summary>
	public class UpdateTracksReturnData
	{
		public Vector2D Position { get; set; }
		public Vector2D LastLinePos { get; set; }
		public Vector2D WheelDirection { get; set; }
		public double WheelMovementAngle { get; set; }
		public string LnName { get; set; }
		public Brush TrailBrush { get; set; }
		public Line2D Line { get; set; }

		public UpdateTracksReturnData(Vector2D position, Vector2D lastLinePos, Vector2D wheelDirection, double wheelMovementAngle, string lnName, Brush trailBrush, Line2D ln)
		{
			Position = position;
			LastLinePos = lastLinePos;
			WheelDirection = wheelDirection;
			WheelMovementAngle = wheelMovementAngle;
			LnName = lnName;
			TrailBrush = trailBrush;
			Line = ln;
		}
	}
}
