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

		// A writeable bitmap that was being used for testing, however is not currently used due to performance issues
		WriteableBitmap wrCarBitmap = new WriteableBitmap(640, 360, 96, 96, PixelFormats.Bgr32, null);

		string trackDataLocation = Directory.GetCurrentDirectory() + "//track2//";

		// The image that shows what material a part of the track is made of
		BitmapImage trackBitmap;
		// The data from trackBitmap in byte[] form
		byte[] terrainPixelByteArray;
		
		public GameWindow()
		{
			InitializeComponent();

			List<Vector2> baseAims = new List<Vector2>();
			// Reads the location aims for computers in from TrackData.txt to baseAims
			{
				FileStream fs = new FileStream(trackDataLocation + "TrackData.txt", FileMode.Open);
				StreamReader sr = new StreamReader(fs);
				string line;
				string lineSoFar;
				float vec1 = float.NaN;
				float vec2 = float.NaN;

				while ((line = sr.ReadLine()) != null)
				{
					lineSoFar = "";
					foreach (Char c in line)
					{
						if (c != ',')
							lineSoFar += c;
						else
						{
							vec1 = (float)Convert.ToDouble(lineSoFar);
							lineSoFar = "";
						}
					}
					vec2 = (float)Convert.ToDouble(lineSoFar);
					baseAims.Add(new Vector2(vec1, vec2));
				}
				sr.Close();
			}
			
			// Add 7 cars - 1 player and 6 computers. Currently also requires you to add an element to GameWindow.xml to represent the car and ScreenUpdateLoop().
			cars.Add(new PlayerCar(new Vector2(120, 95), Vector2.Zero, 10, 1500, "Car1", Key.W, Key.A, Key.D));
			cars.Add(new ComputerCar(new Vector2(140, 90), Vector2.Zero, 10, 1500, "Car2", baseAims));
			cars.Add(new ComputerCar(new Vector2(130, 95), Vector2.Zero, 10, 1500, "Car3", baseAims));
			cars.Add(new ComputerCar(new Vector2(120, 90), Vector2.Zero, 10, 1500, "Car4", baseAims));
			cars.Add(new ComputerCar(new Vector2(100, 95), Vector2.Zero, 10, 1500, "Car5", baseAims));
			cars.Add(new ComputerCar(new Vector2(80, 90), Vector2.Zero, 10, 1500, "Car6", baseAims));
			cars.Add(new ComputerCar(new Vector2(60, 95), Vector2.Zero, 10, 1500, "Car7", baseAims));

			// For each computer, slightly randomize its aim locations by +-2. This is done to prevent all computers taking exactly the same path
			{
				Random random = new Random();
				for (int i = 0; i < cars.Count; i++)
				{
					cars[i].FrameCount = i;
					cars[i].TrailBrush = new ImageBrush(new BitmapImage(new Uri(Directory.GetCurrentDirectory() + "//testTrail.png")));

					if (cars[i] is ComputerCar c)
					{
						List<Vector2> randomisedAims = new List<Vector2>();
						foreach (Vector2 vector in c.AimList)
						{
							randomisedAims.Add(RandomizeVector2InRange(vector, 2, random));
						}
						c.AimList = randomisedAims;
					}
				}
			}

			// Add  trackBitmap as the window background so the user can see the terrain
			trackBitmap = new BitmapImage(new Uri(Directory.GetCurrentDirectory() + "//track2//terrain.png"));
			Image image = new Image
			{
				Source = trackBitmap
			};
			grd.Children.Add(image);

			// Copy the data from trackBitmap to terrainPixelByteArray
			int height = trackBitmap.PixelHeight;
			int width = trackBitmap.PixelWidth;
			int nStride = (trackBitmap.PixelWidth * trackBitmap.Format.BitsPerPixel + 7) / 8;
			terrainPixelByteArray = new byte[trackBitmap.PixelHeight * nStride];
			trackBitmap.CopyPixels(terrainPixelByteArray, nStride, 0);


			gameThread = new Thread(GameLoop);
			screenUpdateThread = new Thread(ScreenUpdateLoop);

			// Code to show the aim points for computers
			{
				//Rectangle rct;
				//foreach (Vector2 vec in baseAims)
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

			// Show the experimental writeable bitmap
			//imgWrBitmapCars.Source = wrCarBitmap;
		}

		/// <summary>
		/// Randomises the given vector within the given range (X and Y randomised independently) with the given Random
		/// </summary>
		private Vector2 RandomizeVector2InRange(Vector2 vector, float range, Random random)
		{
			return new Vector2(vector.X + (float)(random.NextDouble() - 0.5) * 2 * range, vector.Y + (float)(random.NextDouble() - 0.5) * 2 * range);
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
				PhysicsUpdate();
				UpdateFrameRates();
				stopwatch.Restart();
				while (stopwatch.Elapsed.Ticks < 999) { }
			}
		}

		/// <summary>
		/// Updates
		/// </summary>
		private void UpdateFrameRates()
		{
			frameRates[frameCount] = 1 / timeElapsed;
			frameCount++;
			if (frameCount == frameRates.Length) frameCount = 0;
		}

		private void PhysicsUpdate()
		{
			foreach (Car currentCar in cars)
			{
				currentCar.UpdateRotation(timeElapsed);
				currentCar.UpdateVelocity(timeElapsed, GetMaterial(new Vector2(currentCar.Position.X + 2.25f, currentCar.Position.Y + 1.5f)));
				currentCar.UpdatePosition(timeElapsed);
				SimulateCollision(currentCar);

				// Debug - teleports all players to the centre if the 'R' key is held down
				if (currentCar is PlayerCar car)
				{
					if (car.KeysDown.Contains(Key.R))
					{
						currentCar.Position = new Vector2(Constants.courseWidth / 2, Constants.courseHeight / 2);
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
				foreach (Vector2 vector in c.CarCorners)
				{
					if ((currentCar.Position - c.Position).Length() < 5)
					{
						if (PointInQuad(vector, currentCar.CarCorners))
						{
							// Considers a player's car to weigh 1.3 times more, as this makes collisions more fun!
							float pMult = 1;
							float cMult = 1;
							if (currentCar is PlayerCar)
								pMult *= 1.3f;
							if (c is PlayerCar)
								cMult *= 1.3f;

							// Assuming perfect inelastic collision, i.e. conservation of momentum
							currentCar.Velocity = (currentCar.Mass * currentCar.Velocity * pMult + c.Mass * c.Velocity * cMult) / (currentCar.Mass * pMult + c.Mass * cMult);
							c.Velocity = currentCar.Velocity;

							// Moves the cars slightly further away from each other.
							Vector2 playerCvector = currentCar.Position - c.Position;
							currentCar.Position += playerCvector * (float)timeElapsed * 2;
							c.Position -= playerCvector * (float)timeElapsed * 2;
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
				UpdateTyreTracks();

				// Output results
				Dispatcher.Invoke(() =>
				{
					// *Absolutely* needs improvement.
					rctRectP1.Margin = new Thickness(cars[0].Position.X, cars[0].Position.Y, 0, 0);
					rctRectP1.RenderTransform = new RotateTransform(cars[0].Rotation);
					rctRectP2.Margin = new Thickness(cars[1].Position.X, cars[1].Position.Y, 0, 0);
					rctRectP2.RenderTransform = new RotateTransform(cars[1].Rotation);
					rctRectP3.Margin = new Thickness(cars[2].Position.X, cars[2].Position.Y, 0, 0);
					rctRectP3.RenderTransform = new RotateTransform(cars[2].Rotation);
					rctRectP4.Margin = new Thickness(cars[3].Position.X, cars[3].Position.Y, 0, 0);
					rctRectP4.RenderTransform = new RotateTransform(cars[3].Rotation);
					rctRectP5.Margin = new Thickness(cars[4].Position.X, cars[4].Position.Y, 0, 0);
					rctRectP5.RenderTransform = new RotateTransform(cars[4].Rotation);
					rctRectP6.Margin = new Thickness(cars[5].Position.X, cars[5].Position.Y, 0, 0);
					rctRectP6.RenderTransform = new RotateTransform(cars[5].Rotation);
					rctRectP7.Margin = new Thickness(cars[6].Position.X, cars[6].Position.Y, 0, 0);
					rctRectP7.RenderTransform = new RotateTransform(cars[6].Rotation);

					// Labels used for debugging
					lblPlayerPosition.Content = cars[0].Position;
					lblSpeed.Content = cars[0].Velocity.Length();
					lblFrametime.Content = frameRates.Average();
					lblRotation.Content = cars[0].Rotation;

					// Test to see if a WritableBitmap could be used to increase rendering performance. However, the following code currently performs worse and does not work well enough due to inaccuracies with PoimtInQuad()
					//try
					//{
					//	wrCarBitmap.Lock();

					//	for (int i = 0; i < wrCarBitmap.PixelWidth; i++)
					//	{
					//		for (int j = 0; j < wrCarBitmap.PixelHeight; j++)
					//		{
					//			unsafe
					//			{
					//				IntPtr pBackBuffer = wrCarBitmap.BackBuffer;

					//				// Find the address of the pixel to draw.
					//				pBackBuffer += j * wrCarBitmap.BackBufferStride;
					//				pBackBuffer += i * 4;

					//				// Compute the pixel's colour.
					//				int color_data = 160 << 16; // R
					//				color_data |= 128 << 8;   // G
					//				if (PointInQuad(new Vector2(i + 0.5f, j + 0.5f), cars[0].CarCorners))
					//					color_data |= 255 << 0;   // B
					//				else color_data |= 0 << 0;

					//				// Assign the colour data to the pixel.
					//				*((int*)pBackBuffer) = color_data;
					//			}
					//			// Specify the area of the bitmap that changed.
					//		}
					//	}
					//	wrCarBitmap.AddDirtyRect(new Int32Rect(0, 0, wrCarBitmap.PixelWidth, wrCarBitmap.PixelHeight));

					//}
					//finally
					//{
					//	wrCarBitmap.Unlock();
					//}
				});
				Thread.Sleep(12); // Around 12 is recommended due to performance issues
			}
		}

		/// <summary>
		/// Updates the tyre tracks from cars. Warning: Works, but has performance issues that need solved.
		/// </summary>
		private void UpdateTyreTracks()
		{
			foreach (Car car in cars)
			{
				UpdateTracksReturnData trackData = car.UpdateTracks();
				// null is returned by car.UpdateTracks() if no new line is needed
				if (trackData != null)
				{
					Dispatcher.Invoke(() =>
					{
						bool lineFound = false;
						foreach (UIElement element in grdTyreTracks.Children)
						{
							if (element is Line ln)
							{
							// Repositions LnName to where it should be
								if (ln.Name == trackData.LnName)
								{
									lineFound = true;
									ln.X1 = trackData.Position.X;
									ln.X2 = trackData.LastLinePos.X;
									ln.Y1 = trackData.Position.Y;
									ln.Y2 = trackData.LastLinePos.Y;
									ln.Opacity = trackData.WheelMovementAngle;
									break;
								}
							}
						}

						// Creates a new line if LnName does not yet exist
						if (!lineFound)
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
								Opacity = trackData.WheelMovementAngle
							};
							grdTyreTracks.Children.Add(line);
						}
					});
				}
			}
		}

		/// <summary>
		/// Returns the track material at the given location
		/// </summary>
		private Material GetMaterial(Vector2 location)
		{
			int currentPixelColour = terrainPixelByteArray[(int)(location.X / Constants.courseWidth * 1920) * 4 + (int)(location.Y / Constants.courseHeight * 1080) * 1920 * 4];
			if (currentPixelColour == 68)
				return Materials.Tarmac;
			else return Materials.Grass;
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
		private bool PointInQuad(Vector2 point, Vector2[] quadPoints)
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
		private bool PointInTri(Vector2 point, Vector2 t1, Vector2 t2, Vector2 t3)
		{
			double TotalArea = CalcTriArea(t1, t2, t3);
			double Area1 = CalcTriArea(point, t2, t3);
			double Area2 = CalcTriArea(point, t1, t3);
			double Area3 = CalcTriArea(point, t1, t2);

			if ((Area1 + Area2 + Area3) > TotalArea)
				return false;
			else
				return true;
		}

		/// <summary>
		/// Returns area of the given triangle
		/// </summary>
		double CalcTriArea(Vector2 p1, Vector2 p2, Vector2 p3)
		{
			double a = (p1 - p2).LengthSquared();
			double b = (p2 - p3).LengthSquared();
			double c = (p1 - p3).LengthSquared();
			return 0.25 * Math.Sqrt(4 * a * b - (a + b - c) * (a + b - c));
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
