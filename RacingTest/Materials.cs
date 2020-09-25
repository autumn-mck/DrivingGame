using System;
using System.Collections.Generic;
using System.Text;

namespace RacingTest
{
	static class Materials
	{
		public static Material Grass = new Material(0.3f, 0.8f, 2, "Grass"); // Although 2 times air resistance is unrealistic, it provides a greater penalty for driving off the track
		public static Material Tarmac = new Material(0.03f, 0.8f, 1, "Tarmac");
	}

	public class Material
	{
		public float RollingResistance;
		public float GroundResistance;
		public float AirResMult;
		public string Name;

		public Material(float _rollingResistance, float _groundResistance, float _airResMult, string _name)
		{
			RollingResistance = _rollingResistance;
			GroundResistance = _groundResistance;
			AirResMult = _airResMult;
			Name = _name;
		}
	}
}
