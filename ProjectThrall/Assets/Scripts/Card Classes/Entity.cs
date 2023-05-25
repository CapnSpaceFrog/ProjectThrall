using System.Collections;
using System.Collections.Generic;
using TMPro;
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Entity : MonoBehaviour
{
	[Header("Board Space Variables")]
	public Bounds EntityHandSpace;
	[Range(-90, 90)]
	public float CardOrientation;
	public Row MeleeRow;
	public Row RangedRow;
	public Row SiegeRow;

	public TMP_Text HealthText;
	public TMP_Text ManaText;

	public Damageable EntityDamageable;

	public Deck Deck { get; private set; }
	public Hand Hand { get; private set; }

	[Header("Entity Variables")]
	[Range(25, 50)]
	public int HitpointsCap;
	[Range(10, 20)]
	public int ManaCap;
	[Range(1, 2)]
	public int manaGrowthPerTurn;
	[Range(1, 4)]
	public int manaRegenPerTurn;

	public int currentHitpoints { get; private set; }
	protected int currentHitpointsCap;
	protected int currentManaCap;
	protected int currentUsableMana;

	private int fatigueCounter;

	public event Action TurnStartTrigger;
	public event Action TurnEndTrigger;

	public Entity EnemyEntity;

	public SpriteRenderer PortraitSR;

	#region Core Functions
	private void Awake()
	{
		EntityDamageable.Damaged = (damageReceived) =>
		{
			currentHitpoints -= damageReceived;

			if (currentHitpoints < currentHitpointsCap)
				HealthText.color = Color.red;

			if (currentHitpoints <= 0)
			{
				HealthText.text = currentHitpoints.ToString();
				//Trigger end state
				Debug.Log("<color=red>[Entity]</color>: Entity died. Death not implemented.");
				BattleManager.Instance.EndBattle();
				return true;
			}
			else
			{
				HealthText.text = currentHitpoints.ToString();
				return false;
			}
		};

		EntityDamageable.Healed = (int healingReceived) =>
		{
			Debug.Log("Entity Healed");
		};

		Hand = new Hand(EntityHandSpace, Quaternion.Euler(CardOrientation, 0, 0));

		MeleeRow.SetParentEntity(this);
		RangedRow.SetParentEntity(this);
		SiegeRow.SetParentEntity(this);

		EntityDamageable.InstanceInfo = this;
	}

	public void InstantiateDeck(BaseSpellData[] deckData) => Deck = new Deck(deckData, this);

	public void ResetEntity()
	{
		Deck.Reset();
		Hand.Reset();

		currentHitpointsCap = HitpointsCap;
		currentHitpoints = currentHitpointsCap;

		//What are the turn default values for mana?
		currentManaCap = 0;
		currentUsableMana = 0;

		fatigueCounter = 0;

		HealthText.text = currentHitpoints.ToString();
		HealthText.color = Color.white;
		ManaText.text = currentUsableMana.ToString();

		MeleeRow.Reset();
		RangedRow.Reset();
		SiegeRow.Reset();
	}
	#endregion

	#region Targeting Functions
	public List<Selectable> QueryForTargets(Target targetInfo)
	{
		List<Selectable> validTargets = new List<Selectable>(0);

		if ((targetInfo & Target.Hero) == Target.Hero)
			validTargets.Add(EntityDamageable);

		Target rowToTarget = targetInfo & Target.AllRows;

		switch (rowToTarget)
		{
			case Target.MeleeRow:
				foreach (Selectable t in MeleeRow.GetTargetsFromRow())
					validTargets.Add(t);
				break;

			case Target.RangedRow:
				foreach (Selectable t in RangedRow.GetTargetsFromRow())
					validTargets.Add(t);
				break;

			case Target.SiegeRow:
				foreach (Selectable t in SiegeRow.GetTargetsFromRow())
					validTargets.Add(t);
				break;

			case (Target.RangedRow | Target.SiegeRow):
				foreach (Selectable t in RangedRow.GetTargetsFromRow())
					validTargets.Add(t);

				foreach (Selectable t in SiegeRow.GetTargetsFromRow())
					validTargets.Add(t);
				break;

			case Target.AllRows:
				foreach (Selectable t in MeleeRow.GetTargetsFromRow())
					validTargets.Add(t);

				foreach (Selectable t in RangedRow.GetTargetsFromRow())
					validTargets.Add(t);

				foreach (Selectable t in SiegeRow.GetTargetsFromRow())
					validTargets.Add(t);
				break;

			default:
				Debug.Log("<color=red>[Entity]</color>: Fallthrough occured. Was this intended?");
				return validTargets;
		}

		return validTargets;
	}

	public List<Selectable> QueryForTargets(Target target, Keyword keywordToFind)
	{
		List<Selectable> validTargets = new List<Selectable>(0);

		if ((target & Target.Hero) == Target.Hero)
			validTargets.Add(EntityDamageable);

		Target rowToTarget = target & Target.AllRows;

		switch (rowToTarget)
		{
			case Target.MeleeRow:
				foreach (Selectable t in MeleeRow.GetTargetsFromRow(keywordToFind))
					validTargets.Add(t);
				break;

			case Target.RangedRow:
				foreach (Selectable t in RangedRow.GetTargetsFromRow(keywordToFind))
					validTargets.Add(t);
				break;

			case Target.SiegeRow:
				foreach (Selectable t in SiegeRow.GetTargetsFromRow(keywordToFind))
					validTargets.Add(t);
				break;

			case Target.AllRows:
				foreach (Selectable t in MeleeRow.GetTargetsFromRow(keywordToFind))
					validTargets.Add(t);

				foreach (Selectable t in RangedRow.GetTargetsFromRow(keywordToFind))
					validTargets.Add(t);

				foreach (Selectable t in SiegeRow.GetTargetsFromRow(keywordToFind))
					validTargets.Add(t);
				break;

			default:
				Debug.Log("<color=red>[Entity]</color>: Missing valid row target.");
				return validTargets;
		}

		return validTargets;
	}
	#endregion

	#region Mana Helper Functions
	protected void ManaTurnIncrement()
	{
		if (currentManaCap < ManaCap)
			currentManaCap += manaGrowthPerTurn;

		currentUsableMana += manaRegenPerTurn;

		currentUsableMana = Mathf.Clamp(currentUsableMana, 0, currentManaCap);

		ManaText.text = currentUsableMana.ToString();
	}

	public void IncreaseMana(int manaToGain)
	{
		currentUsableMana += manaToGain;

		currentUsableMana = Mathf.Clamp(currentUsableMana, 0, currentManaCap);

		ManaText.text = currentUsableMana.ToString();
	}

	public void ConsumeMana(int manaConsumed)
	{
		currentUsableMana -= manaConsumed;
		ManaText.text = currentUsableMana.ToString();
	}

	public bool CanCastSpell(int manaCost)
	{
		if (currentUsableMana - manaCost < 0)
			return false;

		return true;
	}

	public bool Manathirst(SpellEffect spellEffect, int spellCost)
	{
		int leftoverMana = currentUsableMana - spellCost;

		if (leftoverMana >= spellEffect.AttributeOne)
		{
			ConsumeMana(spellEffect.AttributeOne);
			return true;
		}
		else
			return false;
	}

	public void Replenish(int amountToReplenish)
	{
		currentUsableMana += amountToReplenish;

		currentUsableMana = Mathf.Clamp(currentUsableMana, 0, currentManaCap);

		ManaText.text = currentUsableMana.ToString();
	}
	#endregion

	#region Turn Helper Functions
	public virtual void StartOfTurn()
	{
		Deck.DrawCard(1);

		ManaTurnIncrement();

		TurnStartTrigger?.Invoke();
	}

	public virtual void EndOfTurn()
	{
		TurnEndTrigger?.Invoke();
	}

	public void Fatigue()
	{
		int damage = (int)Mathf.Pow(2, fatigueCounter);

		fatigueCounter++;

		EntityDamageable.Damaged(damage);
	}
	#endregion

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube(EntityHandSpace.center, EntityHandSpace.extents * 2);

		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(EntityHandSpace.center, 0.25f);
	}
}
