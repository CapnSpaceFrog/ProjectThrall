using UnityEngine;

public enum Effect
{
	Summon = 1,
	FlatDamageInstant = 1 << 1,
	RandomDamageInstant = 1 << 2,
	DrawToHand = 1 << 3,
	DrawAndPlay = 1 << 4,
	Replenish = 1 << 5,
	BurnFromHand = 1 << 6,
	BurnFromDeck = 1 << 7,

	Inflict = 1 << 8,
	Heal = 1 << 9,
	BuffHealth = 1 << 10,
	BuffAttack = 1 << 11,
	SwapStats = 1 << 12,

	Destroy = 1 << 13,
	Manathirst = 1 << 14,
	SummonCopy = 1 << 15,
	GainCopy = 1 << 16,
	Pull = 1 << 17,
	Push = 1 << 18,

	//These variables denote a requirement for a spell effect to continue 
	RequiredState = 1 << 19,
	RequiredKeyword = 1 << 20,
}

[System.Flags]
public enum Keyword
{
	Stunned = 1,
	Vulnerable = 1 << 1,
	OnFire = 1 << 2,
	Confused = 1 << 3,
	Flying = 1 << 4,
	Shielded = 1 << 5,
	Warden = 1 << 6,
	Defending = 1 << 7,
	Legion = 1 << 8,
	Cleave = 1 << 9,
	Silenced = 1 << 10,
	Siphon = 1 << 11,
	Shrouded = 1 << 12,
	Reincarnate = 1 << 13,
	Reveal = 1 << 14,
	Locked = 1 << 15,
	Empowering = 1 << 16,
	Max = 17,
}

public enum RequiredState
{
	Alive = 1,
	Dead = 1 << 1,
}

[System.Flags]
public enum Target
{
	//Handled Manually on effect basis
	Single = 1,
	Bounce = 1 << 1,
	Self = 1 << 2,

	//Handled by FindTarget Function
	Chosen = 1 << 3,

	Hero = 1 << 4,
	Row = 1 << 5,

	MeleeRow = 1 << 6,
	RangedRow = 1 << 7,
	SiegeRow = 1 << 8,
	AllRows = (MeleeRow | RangedRow | SiegeRow),

	Friendly = 1 << 9,
	Enemy= 1 << 10,
	Either = (Friendly | Enemy),

	CurrentRow = 1 << 11,
	Persistent = 1 << 12,
}

[System.Serializable]
public struct SpellEffect
{
	public Effect Effect;
	public Target Target;
	[Tooltip("Assign Number of Summons, Spell Damage, Random Min Damage, Manathirst, Burn Amount, Required Keyword/State to this value.")]
	[Range(0, 20)]
	public byte AttributeOne;
	[Tooltip("Assign Random Max Damage, Number of Copies, Conditional Target to this value.")]
	[Range(0, 20)]
	public byte AttributeTwo;
	[Tooltip("Assign Heal, Draw, Replenish Amount, Health Buff, Infliction, to this value.")]
	[Range(0, 20)]
	public byte AttributeThree;
	[Tooltip("Assign Bounce Amount, Attack Buff, Infliction Duration, to this value.")]
	[Range(-1, 20)]
	public sbyte AttributeFour;
}

[CreateAssetMenu(fileName = "BaseSpellData", menuName = "Cards/Base Spell Data")]
public class BaseSpellData : ScriptableObject
{
	[Header("General Variables")]
	public string CardName;
	public string CardDescription;
	public Sprite CardArt;
	public School CardSchool;
	public CardTier CardTier;
	[Range(0, 20)]
	public int ManaCost;

	[Header("Spell Effects")]
	public SpellEffect[] SpellEffects;

	[Header("Crafting")]
	private int placeholder;
}
