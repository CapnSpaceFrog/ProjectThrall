using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Trigger
{
	None,
	OnSummon = 1,
	OnDeath = 1 << 1,
	OnKill = 1 << 2,
	OnDamage = 1 << 3,
	OnAttack = 1 << 4,
	StartOfTurn = 1 << 5,
	EndOfTurn = 1 << 6,
}

public enum UnitRange
{
	Melee,
	Ranged,
	Siege
}

[System.Serializable]
public struct SummonAbility
{
	public Trigger Trigger;
	public Effect Effect;
	public Target Target;
	[Tooltip("Assign Heal, Draw, Replenish/Siphon, Required Keyword/State to this value.")]
	[Range(-5, 20)]
	public sbyte AttributeOne;
	[Tooltip("Assign Attack Buff, Ability Damage, Infliction, to this value.")]
	[Range(-5, 20)]
	public sbyte AttributeTwo;
	[Tooltip("Assign Health Buff, Infliction Duration to this value.")]
	[Range(-5, 20)]
	public sbyte AttributeThree;
}

[CreateAssetMenu(fileName = "UnitData", menuName = "Cards/Unit Data")]
public class UnitData : BaseSpellData
{
	[Header("Summon Variables")]
	public string SummonName;
	public Sprite SummonArt;
	[Range(0, 16)]
	public byte BaseAttack;
	[Range(1, 16)]
	public byte BaseHealth;
	public UnitRange Range;
	public Keyword BaseKeywords;
	public SummonAbility[] AbilityEffects;
}
