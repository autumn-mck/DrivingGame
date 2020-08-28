using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Input;
using System.Windows.Media;

namespace RacingTest
{
	public abstract class Car
	{
		public string Name { get; set; }

		public Vector2 Velocity { get; set; }
		public Vector2 Position { get; set; }
		public Vector2 FacingDirection { get; set; } // A vector with length 1 representing the direction the player is facing
		public Vector2[] CarCorners { get; set; } // The positions of the 4 corners of the car; used for collision detection

		public Vector2[] LastLinePos { get; set; } // Contains 2 coordinates, representing the last locations a line was created for skid marks

		private float TurnRate { get; set; } // The rate at which the car turns
		public float MaxTurnAngle { get; set; } // The maximum turning angle a car can have

		public float Mass { get; set; }
		public float EngineForce { get; set; } // A simplification of how cars work - the engine acts as a constant force on the car
		public Vector2 ExternalForces { get; set; } // External forces acting on the car

		protected int maxSkidLines = 1500; // The number of skid lines a car has behind it is maxSkidLines / lineEveryNFrames
		public int FrameCount { get; set; } // A count up to maxSkidLines
		protected int lineEveryNFrames = 7; // Allows a skid line to be created every Nth time UpdateTracks() is called
		public Brush TrailBrush { get; set; } // The brush used by the skid lines

		public double AngleBetweenWheelsAndVelocity { get; set; }

		private double turningAngle; // The angle that the car is turning at
		public double TurningAngle
		{
			get { return turningAngle; }
			set
			{
				if (value > MaxTurnAngle)
					turningAngle = MaxTurnAngle;
				else if (value < -MaxTurnAngle)
					turningAngle = -MaxTurnAngle;
				else turningAngle = value;
			}
		}

		private double rotation;
		public double Rotation
		{
			get { return rotation; }
			set
			{
				rotation = value;

				if (rotation > 360) rotation -= 360;
				else if (rotation < 0) rotation += 360;

				FacingDirection = new Vector2((float)Math.Cos(rotation / 180f * Math.PI), (float)Math.Sin(rotation / 180f * Math.PI));

			}
		}

		public Car(Vector2 _position, Vector2 _velocity, float _maxTurnAngle, float _mass, string _name)
		{
			CarCorners = new Vector2[4];
			Position = _position;
			Velocity = _velocity;
			MaxTurnAngle = _maxTurnAngle;
			Name = _name;
			TurnRate = 90;
			TurningAngle = 0;
			FacingDirection = new Vector2(0, 1);
			Rotation = 0;

			Mass = _mass;
			EngineForce = 14000; // Could possibly use some tweaking

			LastLinePos = new Vector2[2];
			LastLinePos[0] = Position;
			LastLinePos[1] = Position;
		}

		protected float GetTurnMult()
		{
			float speedMaxTurnRate = 20f; // A larger value decreases the maximum turn speed
			return (float)(Math.Clamp(Velocity.Length(), 0, speedMaxTurnRate) / speedMaxTurnRate);
		}

		protected abstract bool IsAccelerating();

		protected abstract bool IsBraking();

		protected abstract bool IsTurningLeft();

		protected abstract bool IsTurningRight();

		/// <summary>
		/// Updates and returns the variables used for creating tyre tracks
		/// </summary>
		/// <returns>All relevant data required to create the relevant new Line object</returns>
		public UpdateTracksReturnData UpdateTracks()
		{
			UpdateTracksReturnData toReturn = null;
			FrameCount++;
			if (FrameCount == maxSkidLines) FrameCount = 0;

			if (FrameCount % lineEveryNFrames == 0)
			{
				Vector2 position;
				if (FrameCount % 2 == 0)
					position = CarCorners[0];
				else
					position = CarCorners[1];

				Vector2 wheelDirection = new Vector2((float)Math.Cos((Rotation + TurningAngle) / 180f * Math.PI), (float)Math.Sin((Rotation + TurningAngle) / 180f * Math.PI));

				toReturn = new UpdateTracksReturnData(position, LastLinePos[FrameCount % 2], wheelDirection, (float)(AngleBetweenWheelsAndVelocity * AngleBetweenWheelsAndVelocity / 1), "ln" + FrameCount + Name, TrailBrush);
				
				LastLinePos[FrameCount % 2] = position;
			}
			return toReturn;
		}

		public void UpdateRotation(double timePassed)
		{
			float turnMult = GetTurnMult();
			float rotMult = (float)Math.Sin(Math.Clamp(Math.Abs(TurningAngle), 0, 10) / 10 * Math.PI / 2);

			if (IsTurningLeft())
			{
				TurningAngle -= timePassed * TurnRate;
			}
			if (IsTurningRight())
			{
				TurningAngle += timePassed * TurnRate;
			}

			if (TurningAngle != 0 && Velocity.LengthSquared() != 0)
			{
				if (TurningAngle > 0)
				{
					Rotation += Math.Clamp(200 * timePassed * turnMult * rotMult, 0, TurningAngle);
					TurningAngle -= Math.Clamp(200 * timePassed * turnMult * rotMult, 0, TurningAngle);
				}
				else
				{
					Rotation -= Math.Clamp(200 * timePassed * turnMult * rotMult, 0, -TurningAngle);
					TurningAngle += Math.Clamp(200 * timePassed * turnMult * rotMult, 0, -TurningAngle);
				}
			}
		}

		public void UpdateVelocity(double timePassed, Material material)
		{
			float forceX = 0f;
			float forceY = 0f;
			if (IsAccelerating())
			{
				forceX += FacingDirection.X * EngineForce;
				forceY += FacingDirection.Y * EngineForce;
			}

			Vector2 wheelDirection = new Vector2((float)Math.Cos((Rotation + TurningAngle) / 180f * Math.PI), (float)Math.Sin((Rotation + TurningAngle) / 180f * Math.PI));
			Vector2 friction = Friction(new Vector2(forceX, forceY), wheelDirection, Velocity.Length(), material);

			forceX += friction.X;
			forceY += friction.Y;

			forceX += ExternalForces.X;
			forceY += ExternalForces.Y;

			ExternalForces = Vector2.Zero;

			Vector2 acc = new Vector2(forceX / Mass, forceY / Mass);

			double newX = Velocity.X + acc.X * timePassed;
			double newY = Velocity.Y + acc.Y * timePassed;

			// A car cannot turn while it is stopped
			if (Velocity.LengthSquared() != 0)
			{
				double angleBetweenWheelsAndVelocity = Math.Acos(Vector2.Dot(wheelDirection, Velocity) / (wheelDirection.Length() * Velocity.Length()));
				angleBetweenWheelsAndVelocity *= TurningAngle / Math.Abs(TurningAngle);
				AngleBetweenWheelsAndVelocity = angleBetweenWheelsAndVelocity;
				if (Math.Abs(angleBetweenWheelsAndVelocity) > 0.01 && angleBetweenWheelsAndVelocity != float.NaN)
				{
					float[] rotationMatrix = new float[4] { (float)Math.Cos(angleBetweenWheelsAndVelocity), -(float)Math.Sin(angleBetweenWheelsAndVelocity), (float)Math.Sin(angleBetweenWheelsAndVelocity), (float)Math.Cos(angleBetweenWheelsAndVelocity) };

					double rotMult = 0.7 / timePassed;

					newX = ((newX * rotationMatrix[0] + newY * rotationMatrix[1]) + newX * rotMult) / (rotMult + 1);
					newY = ((newX * rotationMatrix[2] + newY * rotationMatrix[3]) + newY * rotMult) / (rotMult + 1);
				}
			}

			if ((newX < 0 && Velocity.X > 0 && !IsAccelerating()) || (newX > 0 && Velocity.X < 0 && !IsAccelerating()))
			{
				newX = 0;
			}
			if ((newY < 0 && Velocity.Y > 0 && !IsAccelerating()) || (newY > 0 && Velocity.Y < 0 && !IsAccelerating()))
			{
				newY = 0;
			}

			Velocity = new Vector2((float)newX, (float)newY);
		}

		/// <summary>
		/// Calculates friction from rolling resistance, braking, air resistance and other friction
		/// </summary>
		private Vector2 Friction(Vector2 forces, Vector2 wheelDirection, float speed, Material material)
		{
			if (Velocity.LengthSquared() == 0) return Vector2.Zero;

			// Rolling Resistance
			Vector2 direction = Vector2.Normalize(Velocity);

			float fMag = material.RollingResistance * Mass * Constants.g;
			float fX = -direction.X * fMag;
			float fY = -direction.Y * fMag;

			if (IsBraking())
			{
				fX = -40000 * direction.X;
				fY = -40000 * direction.Y;
			}


			if (fX > forces.X && Velocity.X == 0) fX = forces.X;
			if (fY > forces.Y && Velocity.Y == 0) fX = forces.X;


			// Air resistance
			float airRes = 0.5f * 1.225f * 0.6f * speed * speed * 20 * material.AirResMult;
			fX -= airRes * direction.X;
			fY -= airRes * direction.Y;

			// Standard Friction
			float wheelMovementAngle = 1 - (Vector2.Dot(direction, wheelDirection) / (direction.Length() * wheelDirection.Length()));
			if (wheelMovementAngle != 0)
			{
				fX -= (float)(material.GroundResistance * Mass * Constants.g * Math.Abs(wheelMovementAngle) * direction.X);
				fY -= (float)(material.GroundResistance * Mass * Constants.g * Math.Abs(wheelMovementAngle) * direction.Y);
			}

			Vector2 force = new Vector2(fX, fY);

			return force;
		}

		public virtual void UpdatePosition(double timePassed)
		{
			float newX = (float)(Position.X + Velocity.X * timePassed);
			float newY = (float)(Position.Y + Velocity.Y * timePassed);

			// Prevents the car from driving off the edge of the screen
			if (newX < 0)
			{
				newX = 0;
				Velocity = new Vector2(0, Velocity.Y);
			}
			if (newY < 0)
			{
				newY = 0;
				Velocity = new Vector2(Velocity.X, 0);
			}
			if (newX > Constants.courseWidth - 4)
			{
				newX = Constants.courseWidth - 4;
				Velocity = new Vector2(0, Velocity.Y);
			}
			if (newY > Constants.courseHeight - 4)
			{
				newY = Constants.courseHeight - 4;
				Velocity = new Vector2(Velocity.X, 0);
			}

			Position = new Vector2(newX, newY);

			// Updates the locations of the car's corners
			CarCorners[0] = new Vector2(Position.X + 2.25f - 2.25f * FacingDirection.X + 1.5f * FacingDirection.Y, Position.Y + 1.5f - 2.25f * FacingDirection.Y - 1.5f * FacingDirection.X);
			CarCorners[1] = new Vector2(Position.X + 2.25f - 2.25f * FacingDirection.X - 1.5f * FacingDirection.Y, Position.Y + 1.5f - 2.25f * FacingDirection.Y + 1.5f * FacingDirection.X);
			CarCorners[2] = new Vector2(Position.X + 2.25f + 2.25f * FacingDirection.X + 1.5f * FacingDirection.Y, Position.Y + 1.5f + 2.25f * FacingDirection.Y - 1.5f * FacingDirection.X);
			CarCorners[3] = new Vector2(Position.X + 2.25f + 2.25f * FacingDirection.X - 1.5f * FacingDirection.Y, Position.Y + 1.5f + 2.25f * FacingDirection.Y + 1.5f * FacingDirection.X);
		}
	}

	/// <summary>
	/// A car representing a user
	/// </summary>
	class PlayerCar : Car
	{
		public PlayerCar(Vector2 _position, Vector2 _velocity, float _maxTurnAngle, float _mass, string _name, Key accelerateKey, Key steerLeftKey, Key steerRightKey) : base(_position, _velocity, _maxTurnAngle, _mass, _name)
		{
			KeysDown = new List<Key>();
			AccelerateKey = accelerateKey;
			SteerLeftKey = steerLeftKey;
			SteerRightKey = steerRightKey;
		}

		// Each user can have separate controls
		public List<Key> KeysDown { get; set; }
		private Key AccelerateKey { get; set; }
		private Key SteerLeftKey { get; set; }
		private Key SteerRightKey { get; set; }

		protected override bool IsAccelerating()
		{
			if (KeysDown.Contains(AccelerateKey)) return true;
			else return false;
		}

		protected override bool IsBraking()
		{
			if (KeysDown.Contains(Key.Space)) return true;
			else return false;
		}

		protected override bool IsTurningLeft()
		{
			if (KeysDown.Contains(SteerLeftKey)) return true;
			else return false;
		}

		protected override bool IsTurningRight()
		{
			if (KeysDown.Contains(SteerRightKey)) return true;
			else return false;
		}
	}

	class ComputerCar : Car
	{
		public List<Vector2> AimList { get; set; }
		private int aimListCount = 0;
		public Vector2 CurrentAim { get; set; }
		private Vector2 NextAim { get; set; }

		public ComputerCar(Vector2 _position, Vector2 _velocity, float _maxTurnAngle, float _mass, string _name, List<Vector2> _aimList) : base(_position, _velocity, _maxTurnAngle, _mass, _name)
		{
			AimList = _aimList;
			CurrentAim = AimList[0];
			NextAim = AimList[NextAimCount()];
		}

		protected override bool IsAccelerating()
		{
			return true; // Should be adjusted if the angle between the computer, the current aim and the next aim is large enough
		}

		protected override bool IsBraking()
		{
			return false;
		}

		protected override bool IsTurningLeft()
		{
			Vector2 directionToGoal = new Vector2(CurrentAim.X - Position.X, CurrentAim.Y - Position.Y);
			float angle = (float)Math.Atan2(directionToGoal.X * FacingDirection.Y - directionToGoal.Y * FacingDirection.X, directionToGoal.X * FacingDirection.X + directionToGoal.Y * FacingDirection.Y);

			if (angle > 0.05) return true;
			else return false;
		}

		protected override bool IsTurningRight()
		{
			Vector2 directionToGoal = new Vector2(CurrentAim.X - Position.X, CurrentAim.Y - Position.Y);
			float angle = (float)Math.Atan2(directionToGoal.X * FacingDirection.Y - directionToGoal.Y * FacingDirection.X, directionToGoal.X * FacingDirection.X + directionToGoal.Y * FacingDirection.Y);

			if (angle < -0.05)
				return true;
			else return false;
		}

		public override void UpdatePosition(double timePassed)
		{
			base.UpdatePosition(timePassed);

			// If the computer is close enough to their current aim, they will aim for the next location
			if ((Position - CurrentAim).Length() < 10 || (Position - NextAim).Length() < 20)
			{
				aimListCount = NextAimCount();
				CurrentAim = NextAim;
				NextAim = AimList[NextAimCount()];
			}
		}

		private int NextAimCount()
		{
			int next = aimListCount + 1;
			if (next == AimList.Count) return 0;
			else return next;
		}
	}
}
