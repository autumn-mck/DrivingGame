using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RacingTest
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class GameWindow : Window
	{
		// A list of all cars/players
		List<Car> cars = new List<Car>();
		List<Car> carsToAddQueue = new List<Car>();

		// Used to let other threads know when they should quit as the application is closing
		bool toExit = false;

		// The thread used for calculating game physics
		Thread gameThread;
		// The thread used for updating what appears on-screen
		Thread screenUpdateThread;

		// Used to keep track of time elapsed since the previous physics calculation
		double timeElapsed = 0;
		// Used to keep track of the recent physics update rates
		double[] frameRates = new double[1000];
		// Used to keep track of the current position in frameRates[]
		int frameCount = 0;

		string trackDataLocation = Directory.GetCurrentDirectory() + "//track2//";

		// The image that shows what material a part of the track is made of
		BitmapImage trackBitmap;
		// The data from trackBitmap in byte[] form
		byte[] terrainPixelByteArray;

		List<Vector2D> baseAims = new List<Vector2D>();

		Random random;

		public GameWindow()
		{
			InitializeComponent();
			RenderOptions.SetBitmapScalingMode(imgWrBitmapCars, BitmapScalingMode.NearestNeighbor);

			// Reads the location aims for computers in from TrackData.txt to baseAims
			{
				FileStream fs = new FileStream(trackDataLocation + "TrackData.txt", FileMode.Open);
				StreamReader sr = new StreamReader(fs);
				string line;
				string lineSoFar;
				double vec1 = double.NaN;
				double vec2 = double.NaN;

				while ((line = sr.ReadLine()) != null)
				{
					lineSoFar = "";
					foreach (Char c in line)
					{
						if (c != ',')
							lineSoFar += c;
						else
						{
							vec1 = Convert.ToDouble(lineSoFar);
							lineSoFar = "";
						}
					}
					vec2 = Convert.ToDouble(lineSoFar);
					baseAims.Add(new Vector2D(vec1, vec2));
				}
				sr.Close();
			}

			// Add 7 cars - 1 player and 6 computers. Currently also requires you to add an element to GameWindow.xml to represent the car and ScreenUpdateLoop().
			cars.Add(new PlayerCar(new Vector2D(120, 95), Vector2D.Zero, 1500, "Car1", Brushes.Red, Key.W, Key.A, Key.D));
			cars.Add(new ComputerCar(new Vector2D(140, 90), Vector2D.Zero, 1500, "Car2", Brushes.Orange, baseAims));
			cars.Add(new ComputerCar(new Vector2D(130, 95), Vector2D.Zero, 1500, "Car3", Brushes.Yellow, baseAims));
			cars.Add(new ComputerCar(new Vector2D(120, 90), Vector2D.Zero, 1500, "Car4", Brushes.Green, baseAims));
			cars.Add(new ComputerCar(new Vector2D(100, 95), Vector2D.Zero, 1500, "Car5", Brushes.Blue, baseAims));
			cars.Add(new ComputerCar(new Vector2D(80, 90), Vector2D.Zero, 1500, "Car6", Brushes.Indigo, baseAims));
			cars.Add(new ComputerCar(new Vector2D(60, 95), Vector2D.Zero, 1500, "Car7", Brushes.Violet, baseAims));

			// For each computer, slightly randomize its aim locations by +-2. This is done to prevent all computers taking exactly the same path

			random = new Random();
			for (int i = 0; i < cars.Count; i++)
			{
				cars[i].FrameCount = i;
				cars[i].TrailBrush = Brushes.Black.Clone();
				grdTrack.Children.Add(cars[i].Rectangle);

				if (cars[i] is ComputerCar c)
				{
					List<Vector2D> randomisedAims = new List<Vector2D>();
					foreach (Vector2D vector in c.AimList)
					{
						randomisedAims.Add(RandomizeVector2InRange(vector, 2, random));
					}
					c.AimList = randomisedAims;
				}
			}


			// Add  trackBitmap as the window background so the user can see the terrain
			trackBitmap = new BitmapImage(new Uri(Directory.GetCurrentDirectory() + "//track2//terrain.png"));
			imgBG.Source = trackBitmap;
			// Copy the data from trackBitmap to terrainPixelByteArray
			int height = trackBitmap.PixelHeight;
			int width = trackBitmap.PixelWidth;
			int nStride = (trackBitmap.PixelWidth * trackBitmap.Format.BitsPerPixel + 7) / 8;
			terrainPixelByteArray = new byte[trackBitmap.PixelHeight * nStride];
			trackBitmap.CopyPixels(terrainPixelByteArray, nStride, 0);
			trackBitmap.Freeze();

			gameThread = new Thread(GameLoop);
			screenUpdateThread = new Thread(ScreenUpdateLoop);

			// Code to show the aim points for computers

			Rectangle rct;
			foreach (Vector2D vec in baseAims)
			{
				rct = new Rectangle
				{
					Fill = Brushes.Red,
					Width = 1,
					Height = 1,
					Margin = new Thickness(vec.X, vec.Y, 0, 0),
					HorizontalAlignment = HorizontalAlignment.Left,
					VerticalAlignment = VerticalAlignment.Top
				};
				grdTrack.Children.Add(rct);
			}



		}

		/// <summary>
		/// Randomises the given vector within the given range (X and Y randomised independently) with the given Random
		/// </summary>
		private Vector2D RandomizeVector2InRange(Vector2D vector, double range, Random random)
		{
			return new Vector2D(vector.X + (random.NextDouble() - 0.5) * 2 * range, vector.Y + (random.NextDouble() - 0.5) * 2 * range);
		}

		/// <summary>
		/// The primary game physics loop. Only exits whenever the window is exiting
		/// </summary>
		private void GameLoop()
		{
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			while (!toExit)
			{
				timeElapsed = stopwatch.Elapsed.TotalMilliseconds / 1000 * 1;
				stopwatch.Restart();

				PhysicsUpdate();
				UpdateFrameRates();
				if (carsToAddQueue.Count > 0)
				{
					foreach (Car c in carsToAddQueue)
					{
						cars.Add(c);
					}
					carsToAddQueue.Clear();

				}

				//while (stopwatch.Elapsed.Ticks < 1) { }
			}
		}

		/// <summary>
		/// Updates
		/// </summary>
		private void UpdateFrameRates()
		{
			frameRates[frameCount] = 1 / timeElapsed;
			frameCount++;
			frameCount %= frameRates.Length;
			//if (frameCount == frameRates.Length) frameCount = 0;
		}

		private void PhysicsUpdate()
		{
			foreach (Car currentCar in cars)
			{
				currentCar.UpdateRotation(timeElapsed);
				currentCar.UpdateVelocity(timeElapsed, GetMaterial(new Vector2D(currentCar.Position.X + 2.25, currentCar.Position.Y + 1.5)));
				currentCar.UpdatePosition(timeElapsed);
				SimulateCollision(currentCar);
				// Debug - teleports players to the centre if the 'R' key is held down
				if (currentCar is PlayerCar car)
				{
					if (car.KeysDown.Contains(Key.R))
					{
						currentCar.Position = new Vector2D(Constants.courseWidth / 2, Constants.courseHeight / 2);
					}
				}
			}
		}

		/// <summary>
		/// Simulates collision between cars
		/// Still has issues with cars occasionally cliping into one another
		/// Currently cars "stick" to eachother too much
		/// </summary>
		private void SimulateCollision(Car currentCar)
		{
			foreach (Car c in cars)
			{
				if (c == currentCar) continue;
				if (c.CarCorners == null) continue;
				foreach (Vector2D vector in c.CarCorners)
				{
					if ((currentCar.Position - c.Position).LengthSquared() < 26)
					{
						if (PointInQuad(vector, currentCar.CarCorners))
						{
							// Considers a player's car to weigh 5 times more, as this makes collisions more fun!
							double pMult = 1;
							double cMult = 1;
							if (currentCar is PlayerCar)
								pMult *= 10;
							if (c is PlayerCar)
								cMult *= 10;

							// Assuming perfect inelastic collision, i.e. conservation of momentum
							Vector2D newVelocity = (currentCar.Mass * pMult * currentCar.Velocity + c.Mass * cMult * c.Velocity) / (currentCar.Mass * pMult + c.Mass * cMult);
							currentCar.Velocity = newVelocity;
							c.Velocity = newVelocity;

							// Should also consider mass of car
							_ = DistToQuad(currentCar.CarCorners, vector, out Vector2D nearestPoint);
							currentCar.Position -= (nearestPoint - vector) * c.Mass / pMult / (currentCar.Mass * pMult + c.Mass * cMult);
							c.Position += (nearestPoint - vector) * currentCar.Mass / cMult / (currentCar.Mass * pMult + c.Mass * cMult);

							double angleDiff = AngleToNearestAngle(currentCar.Rotation - c.Rotation, 90);
							currentCar.ExternalTurningForces += angleDiff * 10000;
							c.ExternalTurningForces += angleDiff * 10000;
							//currentCar.Rotation += angleDiff / 2 * timeElapsed * 100;
							//c.Rotation -= angleDiff / 2 * timeElapsed * 100;
						}
					}
				}
			}
		}

		// Note: It is assumed that byAngle is positive, but angleToCheck can be either positive or negative.
		private double AngleToNearestAngle(double angleToCheck, double byAngle)
		{
			while (angleToCheck < -byAngle / 2 || angleToCheck > byAngle / 2)
			{
				if (angleToCheck > 0)
					angleToCheck -= byAngle;
				else angleToCheck += byAngle;
			}
			return angleToCheck;
		}

		private double DistToQuad(Vector2D[] corners, Vector2D point, out Vector2D closestPoint)
		{
			// Could probably be improved
			double closestDist = FindDistanceToSegment(point, corners[0], corners[1], out closestPoint);

			double compDist = FindDistanceToSegment(point, corners[1], corners[3], out Vector2D compPoint);
			if (compDist < closestDist)
			{
				closestDist = compDist;
				closestPoint = compPoint;
			}

			compDist = FindDistanceToSegment(point, corners[3], corners[2], out compPoint);
			if (compDist < closestDist)
			{
				closestDist = compDist;
				closestPoint = compPoint;
			}

			compDist = FindDistanceToSegment(point, corners[2], corners[0], out compPoint);
			if (compDist < closestDist)
			{
				closestDist = compDist;
				closestPoint = compPoint;
			}

			return closestDist;
		}

		// Calculate the distance between
		// point pt and the segment p1 --> p2.
		private double FindDistanceToSegment(
			Vector2D pt, Vector2D p1, Vector2D p2, out Vector2D closest)
		{
			double dx = p2.X - p1.X;
			double dy = p2.Y - p1.Y;
			if ((dx == 0) && (dy == 0))
			{
				// If p1 and p2 are the same point (Not a line)
				closest = p1;
				dx = pt.X - p1.X;
				dy = pt.Y - p1.Y;
				return Math.Sqrt(dx * dx + dy * dy);
			}

			// Calculate the t that minimizes the distance.
			double t = ((pt.X - p1.X) * dx + (pt.Y - p1.Y) * dy) /
				(dx * dx + dy * dy);

			// See if this represents one of the segment's
			// end points or a point in the middle.
			if (t < 0)
			{
				closest = new Vector2D(p1.X, p1.Y);
				dx = pt.X - p1.X;
				dy = pt.Y - p1.Y;
			}
			else if (t > 1)
			{
				closest = new Vector2D(p2.X, p2.Y);
				dx = pt.X - p2.X;
				dy = pt.Y - p2.Y;
			}
			else
			{
				closest = new Vector2D(p1.X + t * dx, p1.Y + t * dy);
				dx = pt.X - closest.X;
				dy = pt.Y - closest.Y;
			}

			return Math.Sqrt(dx * dx + dy * dy);
		}

		/// <summary>
		/// The loop used to update what appears on-screen
		/// </summary>
		private void ScreenUpdateLoop()
		{
			while (!toExit)
			{
				// Tyre tracks are done with a low a priority as possible
				Dispatcher.Invoke(() =>
				{
					try
					{
						foreach (Car c in cars)
						{
							c.Rectangle.Margin = new Thickness(c.Position.X, c.Position.Y, 0, 0);
							c.Rectangle.RenderTransform = new RotateTransform(c.Rotation);
						}
					}
					catch { return; }
					// Labels used for debugging
					lblPlayerPosition.Content = cars[0].AngleBetweenWheelsAndVelocity;
					lblSpeed.Content = cars[0].Velocity.Length();
					lblFrametime.Content = frameRates.Average();
					lblRotation.Content = cars[0].Rotation;

				});
				try
				{
					Dispatcher.BeginInvoke(new Action(UpdateTyreTracks), System.Windows.Threading.DispatcherPriority.SystemIdle);
				}
				catch
				{
					continue;
				}
				Thread.Sleep(7); // Needs tweaking due to performance issues
			}
		}

		/// <summary>
		/// Updates the tyre tracks from cars. Warning: Works, but has performance issues that need solved.
		/// </summary>
		private void UpdateTyreTracks()
		{
			try
			{
				foreach (Car car in cars)
				{
					UpdateTracksReturnData trackData = car.UpdateTracks();
					// null is returned by car.UpdateTracks() if no new line is needed
					if (trackData != null)
					{
						if (trackData.Position == null || trackData.LastLinePos == null) return;

						trackData.TrailBrush = trackData.TrailBrush.Clone();
						trackData.TrailBrush.Opacity = trackData.WheelMovementAngle;

						bool lineFound = false;
						foreach (UIElement element in grdTyreTracks.Children)
						{
							if (element is Line ln)
							{
								//Repositions LnName to where it should be
								if (ln.Name == trackData.LnName)
								{
									lineFound = true;
									ln.X1 = trackData.Position.X;
									ln.X2 = trackData.LastLinePos.X;
									ln.Y1 = trackData.Position.Y;
									ln.Y2 = trackData.LastLinePos.Y;
									ln.Stroke = trackData.TrailBrush;
									break;
								}
							}
						}

						// Creates a new line if LnName does not yet exist
						if (!lineFound && trackData.Position != null)
						{
							try
							{
								Line line = new Line
								{
									X1 = trackData.Position.X,
									X2 = trackData.LastLinePos.X,
									Y1 = trackData.Position.Y,
									Y2 = trackData.LastLinePos.Y,
									Stroke = trackData.TrailBrush,
									HorizontalAlignment = HorizontalAlignment.Left,
									VerticalAlignment = VerticalAlignment.Top,
									StrokeThickness = 0.5,
									Name = trackData.LnName,
									IsHitTestVisible = false
								};
								grdTyreTracks.Children.Add(line);
							}
							catch
							{
								return;
							}
						}
					}
				}
			}
			catch
			{
				return;
			}
		}

		/// <summary>
		/// Returns the track material at the given location
		/// </summary>
		private Material GetMaterial(Vector2D location)
		{
			try
			{
				int currentPixelColour = terrainPixelByteArray[(int)(location.X / Constants.courseWidth * 1920) * 4 + (int)(location.Y / Constants.courseHeight * 1080) * 1920 * 4];
				if (currentPixelColour == 68)
					return Materials.Tarmac;
				else return Materials.Grass;
			}
			catch
			{
				return Materials.Grass;
			}
		}

		/// <summary>
		/// Called when the user closes the window. This method lets any other threads know they should exit.
		/// </summary>
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			toExit = true;
			Application.Current.Shutdown();
		}

		/// <summary>
		/// Returns true if the quad quadPoints contains point
		/// </summary>
		private bool PointInQuad(Vector2D point, Vector2D[] quadPoints)
		{
			if (PointInTri(point, quadPoints[0], quadPoints[1], quadPoints[2]) || PointInTri(point, quadPoints[1], quadPoints[2], quadPoints[3]))
				return true;
			else
				return false;
		}

		/// <summary>
		/// Returns true if the triangle with corners t1, t2, t3 contains point.
		/// Uses Barycentric coordinate system
		/// </summary>
		private bool PointInTri(Vector2D point, Vector2D t1, Vector2D t2, Vector2D t3)
		{
			double a = ((t2.Y - t3.Y) * (point.X - t3.X) + (t3.X - t2.X) * (point.Y - t3.Y)) / ((t2.Y - t3.Y) * (t1.X - t3.X) + (t3.X - t2.X) * (t1.Y - t3.Y));
			double b = ((t3.Y - t1.Y) * (point.X - t3.X) + (t1.X - t3.X) * (point.Y - t3.Y)) / ((t2.Y - t3.Y) * (t1.X - t3.X) + (t3.X - t2.X) * (t1.Y - t3.Y));
			double c = 1 - a - b;
			return 0 <= a && a <= 1 && 0 <= b && b <= 1 && 0 <= c && c <= 1;
		}

		/// <summary>
		/// Updates the currently held down keys for each player, adds the new key
		/// </summary>
		private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			foreach (Car player in cars)
			{
				if (player is PlayerCar car)
					if (!car.KeysDown.Contains(e.Key))
						car.KeysDown.Add(e.Key);
			}

			if (e.Key == Key.C)
			{
				Vector2D pos = RandomizeVector2InRange(new Vector2D(60, 95), 7, random);
				Car toAdd = new ComputerCar(pos, Vector2D.Zero, 1500, "Car" + (cars.Count + 2), new SolidColorBrush(Color.FromRgb((byte)random.Next(0, 256), (byte)random.Next(0, 256), (byte)random.Next(0, 256))), baseAims)
				{
					FrameCount = 0,
					TrailBrush = Brushes.Black.Clone()
				};
				carsToAddQueue.Add(toAdd);
				grdTrack.Children.Add(toAdd.Rectangle);
			}
			// W - Accelerate
			// A - Rotate left
			// Space - Brake
			// D - Rotate right
		}

		/// <summary>
		/// Updates the currently held down keys for each player, removes the lifted key
		/// </summary>
		private void Window_KeyUp(object sender, KeyEventArgs e)
		{
			foreach (Car player in cars)
			{
				if (player is PlayerCar car)
					car.KeysDown.Remove(e.Key);
			}
		}

		/// <summary>
		/// Starts the game once the window is loaded
		/// </summary>
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			gameThread.Start();
			screenUpdateThread.Start();

			// Code for collision testing debugging
			//Thread.Sleep(10);
			//Rectangle rct;
			//cars[0].Rotation = 29.0123213;
			//for (float i = 118; i < 126; i += 0.1f)
			//{
			//	for (float j = 93; j < 100; j += 0.1f)
			//	{
			//		rct = new Rectangle
			//		{
			//			HorizontalAlignment = HorizontalAlignment.Left,
			//			VerticalAlignment = VerticalAlignment.Top,
			//			Width = 0.1,
			//			Height = 0.1,
			//			Margin = new Thickness(i, j, 0, 0)
			//			//Opacity = 0.5
			//		};
			//		if (PointInQuad(new Vector2D(i, j), cars[0].CarCorners))
			//		{
			//			rct.Fill = Brushes.Blue;
			//		}
			//		else rct.Fill = Brushes.Transparent;
			//		grdTrack.Children.Add(rct);
			//	}
			//}
		}
	}
}
