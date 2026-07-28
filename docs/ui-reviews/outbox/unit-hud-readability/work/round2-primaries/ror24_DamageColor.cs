using UnityEngine;

namespace RoR2;

public static class DamageColor
{
	private static Color[] colors;

	static DamageColor()
	{
		colors = new Color[14];
		colors[0] = Color.white;
		colors[1] = new Color(28f / 85f, 84f / 85f, 0.1764706f);
		colors[2] = new Color(0.79607844f, 16f / 85f, 16f / 85f);
		colors[8] = new Color(0.9372549f, 8f / 85f, 8f / 85f);
		colors[9] = new Color32(237, 127, 205, byte.MaxValue);
		colors[3] = new Color(0.827451f, 0.7490196f, 16f / 51f);
		colors[4] = new Color(0.76862746f, 0.96862745f, 0.34901962f);
		colors[5] = new Color(0.9372549f, 44f / 85f, 0.20392157f);
		colors[7] = new Color(0.6392157f, 0.2f, 0.20784314f);
		colors[10] = new Color(47f / 51f, 23f / 51f, 0.827451f);
		colors[11] = new Color(1f, 47f / 51f, 32f / 51f);
		colors[12] = new Color(1f, 8f / 15f, 0.54509807f);
		colors[13] = Color.yellow;
	}

	public static Color FindColor(DamageColorIndex colorIndex)
	{
		if ((int)colorIndex < 0 || (int)colorIndex >= 14)
		{
			return Color.white;
		}
		return colors[(uint)colorIndex];
	}
}
