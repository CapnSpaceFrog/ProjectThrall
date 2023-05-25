using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Rendering;

public class Card
{
	public BaseSpellData Data;

	public Hand HandCardIsIn;
	private Vector3 currentPositionInHand;
	private Quaternion currentRotationInHand;
	private Vector3 defaultCardSize;

	public Moveable CardWorldInstance;

	public SortingGroup CardSortingGroup;

	private TMP_Text ManaText;
	private TMP_Text DescText;

	private Entity Owner;

	private List<Selectable> persistentTarget;

	public Card(BaseSpellData data, Entity owner)
	{
		this.Data = data;

		Owner = owner;

		//Default scale of the card
		defaultCardSize = new Vector3(1.55f, 1.65f, 1f);

		persistentTarget = new List<Selectable>();
	}

	public void CreateCardWorldInstance(Hand hand)
	{
		HandCardIsIn = hand;

		if (Owner == BattleManager.Instance.PlayerHero)
			CreatePlayerCard();
		else
			CreateEnemyCard();
	}

	void CreatePlayerCard()
	{
		#region Instantiation
		GameObject cardObj = GameObject.Instantiate(BattleManager.Instance.CardPrefab);
		cardObj.name = Data.CardName;

		CardWorldInstance = cardObj.AddComponent<Moveable>();
		CardWorldInstance.Type = SelectionType.Card;

		CardWorldInstance.InstanceInfo = this;

		cardObj.AddComponent<BoxCollider>().size = new Vector3(2.75f, 3.75f, 0.1f);

		cardObj.layer = LayerMask.NameToLayer("Player Card");

		currentRotationInHand = Quaternion.Euler(90, 0, 0);
		cardObj.transform.localScale = defaultCardSize;

		CardSortingGroup = cardObj.GetComponent<SortingGroup>();

		cardObj.transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = Data.CardArt;

		ManaText = cardObj.transform.GetChild(1).GetComponent<TextMeshPro>();
		ManaText.text = Data.ManaCost.ToString();

		DescText = cardObj.transform.GetChild(2).GetComponent<TextMeshPro>();
		DescText.text = Data.CardDescription;
		#endregion

		#region WhenTouched Delegate
		CardWorldInstance.ReceivedPrimary = () =>
		{
			//Specifics with on touch function go here.
			return true;
		};
		#endregion

		#region WhenTouchedUpdate Delegate
		CardWorldInstance.WhileSelected = (Vector3 mouseWorldPos) =>
		{
			BattleManager.Instance.Crosshair.transform.position = mouseWorldPos;

			//Specifics with on touch function go here.
			if (HandCardIsIn.HandSpace.Bounds.Contains(new Vector3(mouseWorldPos.x, 1f, mouseWorldPos.z)))
			{
				//The card is in the hand bounds, so follow the mouse cursor
				BattleManager.Instance.Crosshair.SetActive(false);
				SetCardTransform(new Vector3(mouseWorldPos.x, 11f, mouseWorldPos.z), defaultCardSize);
			}
			else
			{
				BattleManager.Instance.Crosshair.SetActive(true);

				SetCardTransform(BattleManager.Instance.HoverCardPos, Quaternion.Euler(90, 0, 0), BattleManager.Instance.HoverCardScale);
			}
		};
		#endregion

		#region WhenReleased Delegate
		CardWorldInstance.Released = () =>
		{
			if (IsInHandSpace())
			{
				FailedCast();
				return;
			}

			if (!Owner.CanCastSpell(Data.ManaCost))
			{
				FailedCast();
				return;
			}

			Cast();
		};
		#endregion

		#region WhenCancelled Delegate
		CardWorldInstance.ReceivedSecondary = () =>
		{
			BattleManager.Instance.Crosshair.SetActive(false);
			SetCardTransform();
		};
		#endregion
	}

	private void CreateEnemyCard()
	{
		GameObject cardObj = GameObject.Instantiate(BattleManager.Instance.BackOfCardPrefab);
		cardObj.name = Data.CardName;

		cardObj.AddComponent<BoxCollider>().size = new Vector3(2.75f, 3.75f, 0.1f);

		cardObj.layer = LayerMask.NameToLayer("Enemy Card");

		CardWorldInstance = cardObj.AddComponent<Moveable>();
		CardWorldInstance.Type = SelectionType.Card;

		CardWorldInstance.InstanceInfo = this;

		currentRotationInHand = HandCardIsIn.HandSpace.Orientation;
		cardObj.transform.localScale = defaultCardSize;

		CardSortingGroup = cardObj.GetComponent<SortingGroup>();
	}

	#region Casting Functions
	public void Cast()
	{
		foreach (SpellEffect spellEffect in Data.SpellEffects)
		{
			switch (spellEffect.Effect)
			{
				//case Effect.Summon:
				//	if (Summon(Data.SummonData, spellEffect.AttributeOne))
				//		continue;
				//	else
				//	{
				//		FailedCast();
				//		return;
				//	}

				case Effect.Inflict:
					if (Inflict(spellEffect))
						continue;
					else
					{
						FailedCast();
						return;
					}

				case Effect.Manathirst:
					if (Owner.Manathirst(spellEffect, Data.ManaCost))
						continue;
					else
					{
						SuccessfulCast();
						return;
					}

				case Effect.BuffAttack:
					if (BuffAttack(spellEffect))
						continue;
					else
					{
						FailedCast();
						return;
					}

				case Effect.BuffHealth:
					if (BuffHealth(spellEffect))
						continue;
					else
					{
						FailedCast();
						return;
					}

				case Effect.SummonCopy:
					if (SummonCopy(spellEffect))
						continue;
					else
					{
						FailedCast();
						return;
					}

				case Effect.DrawToHand:
					DrawToHand(spellEffect);
					continue;

				case Effect.FlatDamageInstant:
					if (FlatDamageInstant(spellEffect))
						continue;
					else
					{
						FailedCast();
						return;
					}

				case Effect.RandomDamageInstant:
					RandomDamageSpell();
					break;

				case Effect.BurnFromHand:
				case Effect.BurnFromDeck:
					BurnCard(spellEffect);
					break;

				case Effect.RequiredKeyword:
				case Effect.RequiredState:
					if (CheckRequiredState(spellEffect))
						continue;
					else
					{
						FailedCast();
						return;
					}

				case Effect.Push:
					if (Push(spellEffect))
						continue;
					else
					{
						FailedCast();
						return;
					}

				case Effect.Pull:
					if (Pull(spellEffect))
						continue;
					else
					{
						FailedCast();
						return;
					}

				case Effect.Replenish:
					Owner.Replenish(spellEffect.AttributeThree);
						continue;

				case Effect.Destroy:
					if (Destroy(spellEffect))
						continue;
					else
					{
						FailedCast();
						return;
					}
			}
		}

		SuccessfulCast();
	}

	
	#endregion

	#region Spell Functions
	bool Summon(UnitData summonData, int numberOfSummons)
	{
		switch (summonData.Range)
		{
			case UnitRange.Melee:
				if (Owner.MeleeRow.HasKeyword(Keyword.Locked))
					return false;
				else
					Owner.MeleeRow.SummonToRow(summonData, numberOfSummons);
				return true;

			case UnitRange.Ranged:
				if (Owner.RangedRow.HasKeyword(Keyword.Locked))
					return false;
				else
					Owner.RangedRow.SummonToRow(summonData, numberOfSummons);
				return true;

			case UnitRange.Siege:
				if (Owner.SiegeRow.HasKeyword(Keyword.Locked))
					return false;
				else
					Owner.SiegeRow.SummonToRow(summonData, numberOfSummons);
				return true;
		}

		return false;
	}

	bool Inflict(SpellEffect spell)
	{

		return false;
	}

	bool BuffAttack(SpellEffect spell)
	{
		return false;
	}

	bool BuffHealth(SpellEffect spell)
	{
		return false;
	}

	bool SummonCopy(SpellEffect spell)
	{
		return false;
	}

	void DrawToHand(SpellEffect spell)
	{
		Target desiredTarget = spell.Target & Target.Either;

		switch (desiredTarget)
		{
			case Target.Either:
				Owner.Deck.DrawCard(spell.AttributeThree);
				Owner.EnemyEntity.Deck.DrawCard(spell.AttributeThree);
				break;

			case Target.Friendly:
				Owner.Deck.DrawCard(spell.AttributeThree);
				break;

			case Target.Enemy:
				Owner.EnemyEntity.Deck.DrawCard(spell.AttributeThree);
				break;
		}
	}

	bool FlatDamageInstant(SpellEffect spell)
	{
		if (!TargetingHandler.FindTargets(spell.Target, Owner, out List<Selectable> targets))
			return false;

		Target whatToTarget = spell.Target & (Target.Single | Target.Bounce);

		Damageable d;

		switch (whatToTarget)
		{
			case Target.Single:
				d = (Damageable)targets[0];
				d.Damaged(spell.AttributeOne);
				return true;

			case Target.Bounce:
				int index = UnityEngine.Random.Range(0, targets.Count);
				Selectable lastTarget = targets[index];
				for (int i = 0; i < spell.AttributeFour; i++)
				{

					d = (Damageable)lastTarget;
					d.Damaged(spell.AttributeOne);

					targets.Remove(lastTarget);

					if (targets.Count == 0)
						break;

					index = UnityEngine.Random.Range(0, targets.Count);
					lastTarget = targets[index];
				}
				return true;
		}

		//If no target specification is provided, we just hit all the targets that we found.
		foreach (Selectable t in targets)
		{
			d = (Damageable)t;
			d.Damaged(spell.AttributeOne);
		}

		return true;
	}

	void RandomDamageSpell()
	{

	}

	bool CheckRequiredState(SpellEffect spell)
	{
		List<Selectable> targets;

		Target whatToTarget = spell.Target & (Target.Persistent);

		switch (whatToTarget)
		{
			case Target.Persistent:
				Keyword key = (Keyword)(1 << spell.AttributeThree);

				foreach (Selectable t in persistentTarget)
				{
					Unit u = (Unit)t.InstanceInfo;
					if (u.HasKeyword(key))
						return true;
					else return false;
				}
				return false;
		}

		switch (spell.Effect)
		{
			case Effect.RequiredKeyword:
				Keyword key = (Keyword)(1 << spell.AttributeThree);

				if (!TargetingHandler.FindTargets(spell.Target, Owner, key, out targets))
					return false;

				foreach (Selectable t in targets)
					persistentTarget.Add(t);

				PrintTargets(targets);

				return true;

			case Effect.RequiredState:
				Debug.Log("RequiredState not implemented");
				return false;

			default:
				Debug.Log("Defaulted in CheckRequiredState");
				return false;
		}
	}

	void BurnCard(SpellEffect spell)
	{
		switch (spell.Target)
		{
			case Target.Friendly:
				Owner.Hand.Burn(this, spell.AttributeOne);
				break;

			case Target.Enemy:
				Owner.EnemyEntity.Hand.Burn(this, spell.AttributeOne);
				break;
		}
	}
	
	bool Push(SpellEffect spell)
	{
		
		return false;
	}

	bool Pull(SpellEffect spell)
	{
		
		return false;
	}

	bool Destroy(SpellEffect spell)
	{

		return false;
	}
	#endregion

	#region General Helper Funcitons
	bool IsInHandSpace() => HandCardIsIn.HandSpace.Bounds.Contains(new Vector3(CardWorldInstance.transform.position.x, 1f, CardWorldInstance.transform.position.z));

	public void RemoveCardFromWorld()
	{
		if (CardWorldInstance == null)
			return;

		GameObject.Destroy(CardWorldInstance.gameObject);

		CardWorldInstance = null;
	}

	void FailedCast()
	{
		BattleManager.Instance.Crosshair.SetActive(false);
		SetCardTransform();
	}

	void SuccessfulCast()
	{
		BattleManager.Instance.ActiveHero.ConsumeMana(Data.ManaCost);
		HandCardIsIn.RemoveCardFromHand(this);
		BattleManager.Instance.Crosshair.SetActive(false);
	}
	#endregion

	#region Transform Functions
	/// <summary>
	/// Set the card orientation to a specific position, rotation, and scale.
	/// </summary>
	public void SetCardTransform(Vector3 position, Quaternion rotation, Vector3 scale)
	{
		if (CardWorldInstance == null)
			return;

		CardWorldInstance.transform.SetPositionAndRotation(position, rotation);
		CardWorldInstance.transform.localScale = scale;
	}

	/// <summary>
	/// Set the card orientation to a specific position and scale.
	/// </summary>
	public void SetCardTransform(Vector3 position, Vector3 scale)
	{
		if (CardWorldInstance == null)
			return;

		CardWorldInstance.transform.position = position;
		CardWorldInstance.transform.localScale = scale;
	}

	/// <summary>
	/// Resets the card transform to the variables stored in the card class.
	/// </summary>
	public void SetCardTransform()
	{
		if (CardWorldInstance == null)
			return;

		CardWorldInstance.transform.SetPositionAndRotation(currentPositionInHand, currentRotationInHand);
		CardWorldInstance.transform.localScale = defaultCardSize;
	}

	public void SetCardOrientationVariables(Vector3 position, Quaternion rotation, Vector3 scale)
	{
		if (position != Vector3.zero)
			currentPositionInHand = position;

		if (rotation != Quaternion.identity)
			currentRotationInHand = rotation;

		if (scale != Vector3.zero)
			defaultCardSize = scale;
	}
	#endregion

	void PrintTargets(List<Selectable> targets)
	{
		if (targets == null)
		{
			Debug.Log("<color=orange>[Unit]</color>: No targets found.");
			return;
		}

		string s = "";

		foreach (Selectable target in targets)
			s += $"{target.gameObject.name}; ";

		Debug.Log("<color=green>[Unit]</color>: Targets found: " + s);
	}
}
