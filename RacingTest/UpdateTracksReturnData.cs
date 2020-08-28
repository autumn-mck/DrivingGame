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
		public Vector2 Position { get; set; }
		public Vector2 LastLinePos { get; set; }
		public Vector2 WheelDirection { get; set; }
		public float WheelMovementAngle { get; set; }
		public string LnName { get; set; }
		public Brush TrailBrush { get; set; }

		public UpdateTracksReturnData(Vector2 position, Vector2 lastLinePos, Vector2 wheelDirection, float wheelMovementAngle, string lnName, Brush trailBrush)
		{
			Position = position;
			LastLinePos = lastLinePos;
			WheelDirection = wheelDirection;
			WheelMovementAngle = wheelMovementAngle;
			LnName = lnName;
			TrailBrush = trailBrush;
		}
	}
}
