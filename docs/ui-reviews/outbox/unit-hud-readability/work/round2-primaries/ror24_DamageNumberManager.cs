using System.Collections.Generic;
using UnityEngine;

namespace RoR2;

[ExecuteAlways]
public class DamageNumberManager : MonoBehaviour
{
	private List<Vector4> customData = new List<Vector4>();

	private ParticleSystem ps;

	private const int maxDamageNums = int.MaxValue;

	public static DamageNumberManager instance { get; private set; }

	private void OnEnable()
	{
		instance = SingletonHelper.Assign(instance, this);
	}

	private void OnDisable()
	{
		instance = SingletonHelper.Unassign(instance, this);
	}

	private void Awake()
	{
		ps = GetComponent<ParticleSystem>();
	}

	private void Update()
	{
	}

	public void SpawnDamageNumber(float amount, Vector3 position, bool crit, TeamIndex teamIndex, DamageColorIndex damageColorIndex)
	{
		Color color = DamageColor.FindColor(damageColorIndex);
		Color color2 = Color.white;
		switch (teamIndex)
		{
		case TeamIndex.None:
			color2 = Color.gray;
			break;
		case TeamIndex.Monster:
			color2 = new Color(0.5568628f, 0.29411766f, 0.6039216f);
			break;
		}
		ps.Emit(new ParticleSystem.EmitParams
		{
			position = position,
			startColor = color * color2,
			applyShapeToPosition = true
		}, 1);
		ps.GetCustomParticleData(customData, ParticleSystemCustomData.Custom1);
		customData[customData.Count - 1] = new Vector4(1f, 0f, amount, crit ? 1f : 0f);
		ps.SetCustomParticleData(customData, ParticleSystemCustomData.Custom1);
	}
}
