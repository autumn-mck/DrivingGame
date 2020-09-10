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
			cars.Add(new PlayerCar(new Vector2D(120, 95), Vector2D.Zero, 10, 1500, "Car1", Brushes.Red, Key.W, Key.A, Key.D));
			cars.Add(new ComputerCar(new Vector2D(140, 90), Vector2D.Zero, 10, 1500, "Car2", Brushes.Orange, baseAims));
			cars.Add(new ComputerCar(new Vector2D(130, 95), Vector2D.Zero, 10, 1500, "Car3", Brushes.Yellow, baseAims));
			cars.Add(new ComputerCar(new Vector2D(120, 90), Vector2D.Zero, 10, 1500, "Car4", Brushes.Green, baseAims));
			cars.Add(new ComputerCar(new Vector2D(100, 95), Vector2D.Zero, 10, 1500, "Car5", Brushes.Blue, baseAims));
			cars.Add(new ComputerCar(new Vector2D(80, 90), Vector2D.Zero, 10, 1500, "Car6", Brushes.Indigo, baseAims));
			cars.Add(new ComputerCar(new Vector2D(60, 95), Vector2D.Zero, 10, 1500, "Car7", Brushes.Violet, baseAims));

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
			{
				//Rectangle rct;
				//foreach (Vector2D vec in baseAims)
				//{
				//	rct = new Rectangle
				//	{
				//		Fill = Brushes.Red,
				//		Width = 1,
				//		Height = 1,
				//		Margin = new Thickness(vec.X, vec.Y, 0, 0),
				//		HorizontalAlignment = HorizontalAlignment.Left,
				//		VerticalAlignment = VerticalAlignment.Top
				//	};
				//	grdTrack.Children.Add(rct);
				//}
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
				timeElapsed = stopwatch.Elapsed.TotalMilliseconds / 1000;
				stopwatch.Restart();

				PhysicsUpdate();
				UpdateFrameRates();
				if (carsToAddQueue.Count > 0)
				{
					foreach (Car c in carsToAddQueue)
					{
						cars.Add(c);
					}
					//Thread.Sleep(1);
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
		/// Simulates collision between cars - makes significant assumptions and should be significantly improved
		/// </summary>
		private void SimulateCollision(Car currentCar)
		{
			foreach (Car c in cars)
			{
				if (c == currentCar) continue;
				if (c.CarCorners == null) continue;
				foreach (Vector2D vector in c.CarCorners)
				{
					if ((currentCar.Position - c.Position).Length() < 5)
					{
						if (PointInQuad(vector, currentCar.CarCorners))
						{
							// Considers a player's car to weigh 5 times more, as this makes collisions more fun!
							double pMult = 1;
							double cMult = 1;
							if (currentCar is PlayerCar)
								pMult *= 5;
							if (c is PlayerCar)
								cMult *= 5;

							// Assuming perfect inelastic collision, i.e. conservation of momentum
							Vector2D newVelocity = (currentCar.Mass * currentCar.Velocity * pMult + c.Mass * c.Velocity * cMult) / (currentCar.Mass * pMult + c.Mass * cMult);
							currentCar.Velocity = newVelocity * timeElapsed * 100 + currentCar.Velocity * (1 - timeElapsed * 100);
							c.Velocity = newVelocity * timeElapsed * 100 + c.Velocity * (1 - timeElapsed * 100);

							// Moves the cars slightly further away from each other.
							Vector2D playerCvector = (currentCar.Position - c.Position).Normalised();
							c.ExternalForces -= playerCvector * (c.Mass / cMult * pMult * (currentCar.Velocity - c.Velocity).Length() / timeElapsed) / 50;
							currentCar.ExternalForces += playerCvector * (currentCar.Mass / pMult * cMult * (c.Velocity - currentCar.Velocity).Length() / timeElapsed) / 50;
							currentCar.Position += playerCvector * timeElapsed * 2;
							c.Position -= playerCvector * timeElapsed * 2;
						}
					}
				}
			}
		}

		/// <summary>
		/// The loop used to update what appears on-screen
		/// </summary>
		private void ScreenUpdateLoop()
		{
			while (!toExit)
			{
				// Tyre tracks are done with a low a priority as possible
				try
				{
					Dispatcher.BeginInvoke(new Action(UpdateTyreTracks), System.Windows.Threading.DispatcherPriority.SystemIdle);
				}
				catch
				{
					continue;
				}

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
					catch
					{
						return;
					}
					// Labels used for debugging
					lblPlayerPosition.Content = cars[0].Position;
					lblSpeed.Content = cars[0].Velocity.Length();
					lblFrametime.Content = frameRates.Average();
					lblRotation.Content = cars[0].Rotation;

				});				
				Thread.Sleep(5); // Needs tweaking due to performance issues
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
		/// Warning: is inaccurate!
		/// Works well enough for now for short-range collision checks, but must be fixed in future, especially before it is used for anything other than basic collision checks
		/// </summary>
		private bool PointInTri(Vector2D point, Vector2D t1, Vector2D t2, Vector2D t3)
		{
			double TotalArea = CalcTriArea(t1, t2, t3);
			double Area1 = CalcTriArea(point, t2, t3);
			double Area2 = CalcTriArea(point, t1, t3);
			double Area3 = CalcTriArea(point, t1, t2);

			if (Area1 + Area2 + Area3 >  TotalArea)
				return false;
			else
				return true;
		}

		/// <summary>
		/// Returns area of the given triangle
		/// </summary>
		double CalcTriArea(Vector2D p1, Vector2D p2, Vector2D p3)
		{
			return Math.Abs(0.5 * (p1.X * (p2.Y - p3.Y) + p2.X * (p3.Y - p1.Y) + p3.X * (p1.Y - p2.Y)));
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
				Car toAdd = new ComputerCar(new Vector2D(60, 95), Vector2D.Zero, 10, 1500, "Car" + (cars.Count + 2), new SolidColorBrush(Color.FromRgb((byte)random.Next(0, 256), (byte)random.Next(0, 256), (byte)random.Next(0, 256))), baseAims);
				toAdd.FrameCount = 0;
				toAdd.TrailBrush = Brushes.Black.Clone();
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
		}
	}
}
