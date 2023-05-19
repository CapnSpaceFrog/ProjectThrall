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

	public Updateable CardWorldInstance;

	public SortingGroup CardSortingGroup;

	private TMP_Text ManaText;
	private TMP_Text DescText;

	private Entity Owner;

	private List<Touchable> persistentTarget;

	public Card(BaseSpellData data, Entity owner)
	{
		this.Data = data;

		Owner = owner;

		//Default scale of the card
		defaultCardSize = new Vector3(1.55f, 1.65f, 1f);

		persistentTarget = new List<Touchable>();
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

		CardWorldInstance = cardObj.AddComponent<Updateable>();
		CardWorldInstance.Type = TouchableType.Card;

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
		CardWorldInstance.Touched = () =>
		{
			//Specifics with on touch function go here.
			return true;
		};
		#endregion

		#region WhenTouchedUpdate Delegate
		CardWorldInstance.WhileTouched = (Vector3 mouseWorldPos) =>
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
		CardWorldInstance.InputCancelled = () =>
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

		CardWorldInstance = cardObj.AddComponent<Updateable>();
		CardWorldInstance.Type = TouchableType.Card;

		CardWorldInstance.InstanceInfo = this;

		currentRotationInHand = HandCardIsIn.HandSpace.Orientation;
		cardObj.transform.localScale = defaultCardSize;

		CardSortingGroup = cardObj.GetComponent<SortingGroup>();
	}

	#region Casting Functions
	public void Cast()
	{
		persistentTarget.Clear();

		foreach (SpellEffect spellEffect in Data.SpellEffects)
		{
			switch (spellEffect.Effect)
			{
				case Effect.Summon:
					if (Summon(Data.SummonData, spellEffect.AttributeOne))
						continue;
					else
					{
						FailedCast();
						return;
					}

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
		Keyword keywordToInflict = (Keyword)(1 << spell.AttributeThree);
		
		if (!FindTargets(spell.Target, out List<Touchable> targets))
			return false;

		Target whatToHit = spell.Target & (Target.Single | Target.Row);

		Unit u;

		switch (whatToHit)
		{
			case Target.Single:
				foreach (Touchable t in targets)
				{
					u = (Unit)targets[0].InstanceInfo;
					u.AddKeyword(new KeyValuePair<Keyword, int>(keywordToInflict, spell.AttributeFour));

					if ((spell.Target & Target.SaveForPersistent) == Target.SaveForPersistent)
						persistentTarget.Add(t);
				}
				return true;

			case Target.Row:
				foreach (Touchable t in targets)
				{
					u = (Unit)t.InstanceInfo;

					if (keywordToInflict == Keyword.Locked)
					{
						if (!u.RowUnitIsIn.HasKeyword(Keyword.Locked))
							u.RowUnitIsIn.AddKeyword(new KeyValuePair<Keyword, int>(keywordToInflict, spell.AttributeFour));
					}
					else
					{
						u = (Unit)t.InstanceInfo;
						u.AddKeyword(new KeyValuePair<Keyword, int>(keywordToInflict, spell.AttributeFour));
					}
				}
				return true;
		}

		foreach (Touchable t in targets)
		{
			u = (Unit)t.InstanceInfo;

			if (!u.WorldState.isDead)
				u.AddKeyword(new KeyValuePair<Keyword, int>(keywordToInflict, spell.AttributeFour));
		}
		return true;
	}

	bool BuffAttack(SpellEffect spell)
	{
		if (!FindTargets(spell.Target, out List<Touchable> targets))
			return false;

		Unit u;

		Target whatToTarget = spell.Target & (Target.Single | Target.Bounce | Target.Row);

		switch (whatToTarget)
		{
			case Target.Single:
				int index = UnityEngine.Random.Range(0, targets.Count);
				u = (Unit)(targets[index].InstanceInfo);

				u.WorldState.Stats.TemporaryAttackBonus += spell.AttributeFour;
				u.UpdateStatVisuals();
				return true;

			case Target.Row:
				foreach (Touchable t in targets)
				{
					u = (Unit)t.InstanceInfo;

					u.WorldState.Stats.TemporaryAttackBonus += spell.AttributeFour;
					u.UpdateStatVisuals();
				}
				return true;
		}

		foreach (Touchable t in targets)
		{
			u = (Unit)t.InstanceInfo;

			u.WorldState.Stats.TemporaryAttackBonus += spell.AttributeFour;
			u.UpdateStatVisuals();
		}
		return true;
	}

	bool BuffHealth(SpellEffect spell)
	{
		if (!FindTargets(spell.Target, out List<Touchable> targets))
			return false;

		Unit u;

		Target whatToTarget = spell.Target & (Target.Single | Target.Bounce | Target.Row);

		switch (whatToTarget)
		{
			case Target.Single:
				int index = UnityEngine.Random.Range(0, targets.Count);
				u = (Unit)(targets[index].InstanceInfo);

				u.WorldState.Stats.TemporaryHealthBonus += spell.AttributeThree;
				u.UpdateStatVisuals();
				return true;

			case Target.Row:
				foreach (Touchable t in targets)
				{
					u = (Unit)t.InstanceInfo;

					u.WorldState.Stats.TemporaryHealthBonus += spell.AttributeThree;
					u.UpdateStatVisuals();
				}
				return true;
		}

		foreach (Touchable t in targets)
		{
			u = (Unit)t.InstanceInfo;

			u.WorldState.Stats.TemporaryHealthBonus += spell.AttributeThree;
			u.UpdateStatVisuals();
		}
		return true;
	}

	bool SummonCopy(SpellEffect spell)
	{
		if (!FindTargets(spell.Target, out List<Touchable> targets))
			return false;

		Unit u;

		Target whatToTarget = spell.Target & (Target.Single | Target.Row);

		switch (whatToTarget)
		{
			case Target.Single:
				u = (Unit)targets[0].InstanceInfo;
				Summon(u.summonData, spell.AttributeOne);
				break;

			case Target.Row:
				Debug.Log("Row Copying not implemented");
				break;

			default:
				Debug.Log("<color=red>[Unit]</color>: Defaulted in BuffAttack. Did you set the target parameter correctly?");
				return false;
		}

		return true;
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
		if (!FindTargets(spell.Target, out List<Touchable> targets))
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
				Touchable lastTarget = targets[index];
				for (int i = 0; i < spell.AttributeFour; i++)
				{

					d = (Damageable)lastTarget;
					d.Damaged(spell.AttributeOne);

					if ((spell.Target & Target.SaveForPersistent) == Target.SaveForPersistent)
						persistentTarget.Add(lastTarget);

					targets.Remove(lastTarget);

					if (targets.Count == 0)
						break;

					index = UnityEngine.Random.Range(0, targets.Count);
					lastTarget = targets[index];
				}
				return true;
		}

		//If no target specification is provided, we just hit all the targets that we found.
		foreach (Touchable t in targets)
		{
			d = (Damageable)t;
			d.Damaged(spell.AttributeOne);
		}

		if ((spell.Target & Target.SaveForPersistent) == Target.SaveForPersistent)
			persistentTarget = targets;

		return true;
	}

	void RandomDamageSpell()
	{

	}

	bool CheckRequiredState(SpellEffect spell)
	{
		List<Touchable> targets;

		Target whatToTarget = spell.Target & (Target.Persistent);

		switch (whatToTarget)
		{
			case Target.Persistent:
				Keyword key = (Keyword)(1 << spell.AttributeThree);

				foreach (Touchable t in persistentTarget)
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

				if (!FindTargets(spell.Target, key, out targets))
					return false;

				foreach (Touchable t in targets)
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
		if (!FindTargets(spell.Target, out List<Touchable> targets))
			return false;

		Target whatToTarget = spell.Target & (Target.Single | Target.Persistent);

		Unit u;

		switch (whatToTarget)
		{
			case Target.Single:
				u = (Unit)targets[0].InstanceInfo;
				Debug.Log("single pushing target");
				switch (u.WorldState.CurrentRange)
				{
					case UnitRange.Melee:
						u.WorldState.CurrentRange = UnitRange.Ranged;
						u.WorldState.Owner.RangedRow.MoveToRow(u);
						break;

					case UnitRange.Ranged:
						u.WorldState.CurrentRange = UnitRange.Siege;
						u.WorldState.Owner.SiegeRow.MoveToRow(u);
						break;
				}

				if ((spell.Target & Target.SaveForPersistent) == Target.SaveForPersistent)
					persistentTarget.Add(targets[0]);
				return true;
		}

		//If no target specification is provided, we just hit all the targets that we found.
		foreach (Touchable t in targets)
		{
			u = (Unit)t.InstanceInfo;

			switch (u.WorldState.CurrentRange)
			{
				case UnitRange.Melee:
					u.WorldState.CurrentRange = UnitRange.Ranged;
					u.WorldState.Owner.RangedRow.MoveToRow(u);
					break;

				case UnitRange.Ranged:
					u.WorldState.CurrentRange = UnitRange.Siege;
					u.WorldState.Owner.SiegeRow.MoveToRow(u);
					break;
			}

			if ((spell.Target & Target.SaveForPersistent) == Target.SaveForPersistent)
				persistentTarget.Add(t);
		}
		return true;
	}

	bool Pull(SpellEffect spell)
	{
		if (!FindTargets(spell.Target, out List<Touchable> targets))
			return false;

		Target whatToTarget = spell.Target & (Target.Single | Target.Persistent);

		Unit u;
		Debug.Log("Pulling");
		switch (whatToTarget)
		{
			case Target.Single:
				u = (Unit)targets[0].InstanceInfo;

				switch (u.WorldState.CurrentRange)
				{
					case UnitRange.Ranged:
						u.WorldState.CurrentRange = UnitRange.Melee;
						u.WorldState.Owner.MeleeRow.MoveToRow(u);
						break;

					case UnitRange.Siege:
						u.WorldState.CurrentRange = UnitRange.Ranged;
						u.WorldState.Owner.RangedRow.MoveToRow(u);
						break;
				}

				if ((spell.Target & Target.SaveForPersistent) == Target.SaveForPersistent)
					persistentTarget.Add(targets[0]);

				return true;
		}

		//If no target specification is provided, we just hit all the targets that we found.
		foreach (Touchable t in targets)
		{
			u = (Unit)t.InstanceInfo;

			switch (u.WorldState.CurrentRange)
			{
				case UnitRange.Ranged:
					u.WorldState.CurrentRange = UnitRange.Melee;
					u.WorldState.Owner.MeleeRow.MoveToRow(u);
					break;

				case UnitRange.Siege:
					u.WorldState.CurrentRange = UnitRange.Ranged;
					u.WorldState.Owner.RangedRow.MoveToRow(u);
					break;
			}

			if ((spell.Target & Target.SaveForPersistent) == Target.SaveForPersistent)
				persistentTarget.Add(t);
		}
		return true;
	}

	bool Destroy(SpellEffect spell)
	{
		List<Touchable> targets = new List<Touchable>();

		if (!FindTargets(spell.Target, out targets))
			return false;

		foreach (Touchable t in targets)
		{
			Unit u = (Unit)t.InstanceInfo;
			u.Destroy();
		}
		return true;
	}
	#endregion

	#region Targeting Functions
	/// <summary>
	/// The spell version of FindTargets. Returns a list of targets based on the parameter passed.
	/// </summary>
	/// <param name="targetInfo">Can handle: Persistent, Chosen, Friendly, Enemy, Either, </param>
	/// <returns></returns>
	bool FindTargets(Target targetInfo, out List<Touchable> targets)
	{
		targets = new List<Touchable>();

		Target preCheck = targetInfo & (Target.Persistent | Target.Chosen);

		switch (preCheck)
		{
			case Target.Chosen:
				Touchable hitObj = GetChosenTarget(targetInfo);

				if (hitObj == null)
					return false;
				else if (hitObj.Type == TouchableType.Row)
				{
					Row row = (Row)hitObj;

					if (row.IsEmpty())
						return false;
					else
						targets = row.GetTargetsFromRow();
					return true;
				}
				else
				{
					targets.Add(hitObj);
					return true;
				}

			case Target.Persistent:
				targets = persistentTarget;
				return true;
		}

		Target mainCheck = targetInfo & Target.Either;

		switch (mainCheck)
		{
			case Target.Friendly:
				targets = Owner.QueryForTargets(targetInfo);
				break;

			case Target.Enemy:
				targets = Owner.EnemyEntity.QueryForTargets(targetInfo);
				break;

			case Target.Either:
				foreach (Touchable target in Owner.QueryForTargets(targetInfo))
					targets.Add(target);

				foreach (Touchable target in Owner.EnemyEntity.QueryForTargets(targetInfo))
					targets.Add(target);
				break;

			default:
				Debug.Log($"<color=red>[Card]</color> on {Data.CardName}: Invalid target parameter passed. Please verify the target enum.");
				return false;
		}

		if (targets.Count == 0)
			return false;
		else
		{
			targets = Deck.Shuffle(targets);
			return true;
		}
	}

	bool FindTargets(Target targetInfo, Keyword keywordToFind, out List<Touchable> targets)
	{
		targets = new List<Touchable>(0);

		Target preCheck = targetInfo & (Target.Persistent);

		switch (preCheck)
		{
			case Target.Persistent:
				targets = persistentTarget;
				return true;
		}

		Target mainCheck = targetInfo & Target.Either;

		switch (mainCheck)
		{
			case Target.Friendly:
				targets = Owner.QueryForTargets(targetInfo, keywordToFind);
				break;

			case Target.Enemy:
				targets = Owner.EnemyEntity.QueryForTargets(targetInfo, keywordToFind);
				break;

			case Target.Either:
				foreach (Touchable t in Owner.QueryForTargets(targetInfo, keywordToFind))
					targets.Add(t);

				foreach (Touchable t in Owner.EnemyEntity.QueryForTargets(targetInfo, keywordToFind))
					targets.Add(t);
				break;

			default:
				Debug.Log($"<color=red>[Card]</color> on {Data.CardName}: Invalid target parameter passed. Please verify the target enum.");
				return false;
		}

		if (targets.Count == 0)
			return false;
		else
		{
			targets = Deck.Shuffle(targets);
			return true;
		}
	}

	Touchable GetChosenTarget(Target targetInfo)
	{
		if (BattleManager.Instance.RaycastFromMousePosition(BattleManager.GetLayerFromTarget(targetInfo), out Touchable touchedEntity))
			return touchedEntity;
		else return null;
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

	void PrintTargets(List<Touchable> targets)
	{
		if (targets == null)
		{
			Debug.Log("<color=orange>[Unit]</color>: No targets found.");
			return;
		}

		string s = "";

		foreach (Touchable target in targets)
			s += $"{target.gameObject.name}; ";

		Debug.Log("<color=green>[Unit]</color>: Targets found: " + s);
	}
}
