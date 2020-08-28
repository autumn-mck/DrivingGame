using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RacingTest
{
	/// <summary>
	/// Interaction logic for Controller.xaml
	/// </summary>
	public partial class Controller : Window
	{
		public GameWindow GameWindow { get; set; }

		public Controller()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// This Controller Window is currently only used to start the game.
			Hide();
			GameWindow = new GameWindow();
			GameWindow.Show();
		}
	}
}
