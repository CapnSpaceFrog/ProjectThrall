using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;

public class KeywordState
{
	public enum KeywordUpdate
	{
		ParentStartOfTurn,
		ParentEndOfTurn,
		EnemyStartOfTurn,
		EnemyEndOfTurn
	}

	public Touchable Parent;
	public Entity Owner;
	public Keyword Key;
	public int Duration;
	public GameObject Visual;
	public KeywordUpdate Trigger;

	public void DecayDuration()
	{
		if (Duration == -1
			|| Key == Keyword.Reincarnate)
			return;
		else
			Duration -= 1;
	}

	public void UpdateKeywordVisual()
	{
		DecayDuration();

		if (Duration == 0)
		{
			switch (Parent.Type)
			{
				case TouchableType.Unit:
					Unit u = (Unit)Parent.InstanceInfo;
					u.RemoveKeyword(Key);
					break;

				case TouchableType.Row:
					Row r = (Row)Parent;
					r.RemoveKeyword(Key);
					break;

				case TouchableType.Hero:

					break;
			}

			ClearKeyUpdateSub();
		}
	}

	public void ClearKeyUpdateSub()
	{
		switch (Trigger)
		{
			case KeywordUpdate.ParentStartOfTurn:
				Owner.TurnStartTrigger -= UpdateKeywordVisual;
				break;

			case KeywordUpdate.ParentEndOfTurn:
				Owner.TurnEndTrigger -= UpdateKeywordVisual;
				break;

			case KeywordUpdate.EnemyStartOfTurn:
				Owner.EnemyEntity.TurnStartTrigger -= UpdateKeywordVisual;
				break;

			case KeywordUpdate.EnemyEndOfTurn:
				Owner.EnemyEntity.TurnEndTrigger -= UpdateKeywordVisual;
				break;
		}
	}
}

public class Unit
{
	public Damageable AITarget;

	public UnitData summonData { get; private set; }

	public Row RowUnitIsIn;

	public struct UnitState
	{
		public StatState Stats;
		public UnitRange CurrentRange;
		public bool hasAttacked;
		public bool isDead;
		public HashSet<KeywordState> ActiveKeywords;
		public Entity Owner;
		public Updateable Instance;
	}

	public struct StatState
	{
		public readonly int BaseHealth;
		public readonly int BaseAttack;

		public StatState(int baseHealth, int baseAttack)
		{
			BaseHealth = baseHealth;
			lostHealth = 0;
			TemporaryHealthBonus = 0;

			BaseAttack = baseAttack;
			LegionBonus = 0;
			TemporaryAttackBonus = 0;
		}

		//Health Variables
		private int lostHealth;
		public int CurrentHealth { get { return CurrentHealthCap - lostHealth; } }

		public readonly int CurrentHealthCap => BaseHealth + BonusHealth;
		public readonly int BonusHealth => TemporaryHealthBonus;

		public int TemporaryHealthBonus;

		public void AdjustLostHealth(int healthChange)
		{
			lostHealth += healthChange;

			if (lostHealth < 0)
				lostHealth = 0;
		}

		//Attack Variables
		public int CurrentAttack()
		{
			int temp = BaseAttack + BonusAttack;

			if (temp < 0)
				return 0;
			else
				return temp;
		}

		private int BonusAttack => TemporaryAttackBonus + LegionBonus;

		public int TemporaryAttackBonus;
		public int LegionBonus;
	}

	public UnitState WorldState;

	Vector3 positionOnRow;
	Quaternion defaultRotation;
	Vector3 defaultUnitScale;
	Vector3 hoveredUnitScale;

	SpriteRenderer summonSR;

	TMP_Text AttackText;
	TMP_Text HealthText;

	bool isHovered;

	event Action OnKillTrigger;
	event Action OnDeathTrigger;
	event Action OnDamageTrigger;
	event Action OnAttackTrigger;

	Damageable lastAttacked;
	List<Touchable> persistentTarget;

	#region Instantiation Functions
	public Unit(UnitData data)
	{
		summonData = data;

		defaultUnitScale = new Vector3(0.41f, 0.41f, 1);
		hoveredUnitScale = new Vector3(0.475f, 0.475f, 1);

		WorldState.Stats = new StatState(summonData.BaseHealth, summonData.BaseAttack);

		persistentTarget = new List<Touchable>();
	}

	public void CreateUnitInWorld()
	{
		#region Instantiation Info
		GameObject unitObj = GameObject.Instantiate(BattleManager.Instance.UnitPrefab);
		unitObj.name = summonData.SummonName;
		WorldState.Instance = unitObj.AddComponent<Updateable>();
		WorldState.Instance.Type = TouchableType.Unit;

		WorldState.Instance.InstanceInfo = this;

		unitObj.AddComponent<BoxCollider>().size = new Vector3(7.65f, 10.5f, 0.1f);

		defaultRotation = Quaternion.Euler(90, 0, 0);
		unitObj.transform.localScale = defaultUnitScale;

		summonSR = unitObj.GetComponent<SpriteRenderer>();
		unitObj.transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = summonData.SummonArt;

		AttackText = unitObj.transform.GetChild(1).GetComponent<TextMeshPro>();
		HealthText = unitObj.transform.GetChild(2).GetComponent<TextMeshPro>();

		UpdateStatVisuals();

		WorldState.hasAttacked = true;

		WorldState.CurrentRange = summonData.Range;
		#endregion

		if (WorldState.Owner == BattleManager.Instance.PlayerHero)
		{
			unitObj.layer = LayerMask.NameToLayer("Friendly Unit");

			#region Touched Delegate
			WorldState.Instance.Touched = () =>
			{
				//Specifics with on touch function go here.
				if (WorldState.hasAttacked == true || WorldState.Stats.CurrentAttack() == 0
				|| HasKeyword(Keyword.Stunned)
				|| HasKeyword(Keyword.Warden))
					return false;

				isHovered = true;
				UpdateUnitTransform();

				BattleManager.Instance.Crosshair.SetActive(true);

				return true;
			};
			#endregion

			#region WhileTouched Delegate
			WorldState.Instance.WhileTouched = (Vector3 mouseWorldPos) =>
			{
				BattleManager.Instance.Crosshair.transform.position = mouseWorldPos;
			};
			#endregion

			#region Released Delegate
			WorldState.Instance.Released = () =>
			{
				BattleManager.Instance.RaycastFromMousePosition(LayerMask.GetMask("Enemy Unit", "Enemy Hero"), out Touchable entityTouched);
				Damageable target = (Damageable)entityTouched;

				if (entityTouched == null)
					FailedAttack();
				else
				{
					//If the unit we are attacking with has confused, grab a random enemy target and smack it instead.
					if (HasKeyword(Keyword.Confused))
					{
						Damageable randomTarget = Confused(target);
						Debug.Log($"<color=orange>[Unit]</color>: Unit has confused, random target {randomTarget.gameObject.name} chosen instead of {target.gameObject.name}.");
						Attack(randomTarget);
					}
					else
						Attack(target);
				}
			};
			#endregion

			#region InputCancelled Delegate
			WorldState.Instance.InputCancelled = () =>
			{
				isHovered = false;
				UpdateUnitTransform();

				BattleManager.Instance.Crosshair.SetActive(false);
			};
			#endregion
		}
		else
			unitObj.layer = LayerMask.NameToLayer("Enemy Unit");

		//TODO: Find a better solution to damage and refactor the "touchable" class
		#region Damaged Delegate
		WorldState.Instance.Damaged = (damageReceived) =>
		{
			if (HasKeyword(Keyword.Shielded))
			{
				RemoveKeyword(Keyword.Shielded);
				Debug.Log($"<color=orange>[Unit]</color>: {summonData.name} blocked {damageReceived} points of damage with Shielded.");
				return false;
			}

			if (HasKeyword(Keyword.Vulnerable))
				damageReceived *= 2;

			Debug.Log($"<color=orange>[Unit]</color>: {summonData.name} received {damageReceived} points of damage.");
			WorldState.Stats.AdjustLostHealth(damageReceived);

			if (WorldState.Stats.CurrentHealth <= 0)
			{
				WorldState.isDead = true;
				OnDeath();
				return true;
			}
			else
			{
				UpdateStatVisuals();
				return false;
			}
		};
		#endregion

		#region Healed Delegate
		WorldState.Instance.Healed = (int healingReceived) =>
		{
			Debug.Log($"<color=orange>[Unit]</color>: {summonData.name} received {healingReceived} points of healing.");
			WorldState.Stats.AdjustLostHealth(-healingReceived);

			UpdateStatVisuals();
		};
		#endregion

		OnSummon();
	}

	public void OnSummon()
	{
		CheckForBaseKeywords(summonData.BaseKeywords);

		//Is there already an empowering unit in the row
		//our new unit was played at? If so, update them.
		CheckEmpoweringEffect();

		CheckGlobalEffects();

		if (summonData.AbilityEffects.Length == 0)
			return;

		switch (summonData.AbilityEffects[0].Trigger)
		{
			case Trigger.OnSummon:
				TriggerAbility();
				break;

			case Trigger.StartOfTurn:
				WorldState.Owner.TurnStartTrigger += TriggerAbility;
				break;

			case Trigger.EndOfTurn:
				WorldState.Owner.TurnEndTrigger += TriggerAbility;
				break;

			case Trigger.OnDamage:
				OnDamageTrigger += TriggerAbility;
				break;

			case Trigger.OnKill:
				OnKillTrigger += TriggerAbility;
				break;

			case Trigger.OnDeath:
				OnDeathTrigger += TriggerAbility;
				break;

			case Trigger.OnAttack:
				OnAttackTrigger += TriggerAbility;
				break;
		}
	}

	public void DestroyUnitInWorld()
	{
		GameObject.Destroy(WorldState.Instance.gameObject);
		
		WorldState.Instance = null;
	}
	#endregion

	#region Combat Functions
	void Attack(Damageable target)
	{
		if (CheckValidTarget(target))
		{
			OnAttackTrigger?.Invoke();

			if (target.Type == TouchableType.Hero)
			{
				Entity entityTargeted = (Entity)target.InstanceInfo;

				lastAttacked = target;

				if (target.Damaged(WorldState.Stats.CurrentAttack()))
					OnKillTrigger?.Invoke();

				OnDamageTrigger?.Invoke();
			}
			else
			{
				if (HasKeyword(Keyword.Shrouded))
					RemoveKeyword(Keyword.Shrouded);

				Unit unitTarget = (Unit)target.InstanceInfo;

				lastAttacked = target;
				unitTarget.lastAttacked = WorldState.Instance;

				target.Damaged(WorldState.Stats.CurrentAttack());

				WorldState.Instance.Damaged(unitTarget.WorldState.Stats.CurrentAttack());

				OnDamageTrigger?.Invoke();

				unitTarget.OnDamageTrigger?.Invoke();

				if (unitTarget.WorldState.isDead)
					OnKillTrigger?.Invoke();

				if (WorldState.isDead)
					unitTarget.OnKillTrigger?.Invoke();
			}
		}
		else
		{
			FailedAttack();
			return;
		}

		if (HasKeyword(Keyword.OnFire))
			OnFire();

		BattleManager.Instance.Crosshair.SetActive(false);

		if (!WorldState.isDead)
		{
			WorldState.hasAttacked = true;
			isHovered = false;
			UpdateUnitTransform();
		}
	}

	public void AIAttack(Damageable target)
	{
		if (CheckValidTarget(target))
		{
			OnAttackTrigger?.Invoke();

			if (target.Type == TouchableType.Hero)
			{
				Entity entityTargeted = (Entity)target.InstanceInfo;

				lastAttacked = target;

				if (target.Damaged(WorldState.Stats.CurrentAttack()))
					OnKillTrigger?.Invoke();

				OnDamageTrigger?.Invoke();
			}
			else
			{
				if (HasKeyword(Keyword.Shrouded))
					RemoveKeyword(Keyword.Shrouded);

				Unit unitTarget = (Unit)target.InstanceInfo;

				lastAttacked = target;
				unitTarget.lastAttacked = WorldState.Instance;

				target.Damaged(WorldState.Stats.CurrentAttack());

				WorldState.Instance.Damaged(unitTarget.WorldState.Stats.CurrentAttack());

				OnDamageTrigger?.Invoke();

				unitTarget.OnDamageTrigger?.Invoke();

				if (unitTarget.WorldState.isDead)
					OnKillTrigger?.Invoke();

				if (WorldState.isDead)
					unitTarget.OnKillTrigger?.Invoke();
			}
		}
		else return;

		if (HasKeyword(Keyword.OnFire))
			OnFire();

		if (!WorldState.isDead)
			WorldState.hasAttacked = true;
	}

	void FailedAttack()
	{
		isHovered = false;
		BattleManager.Instance.Crosshair.SetActive(false);
		UpdateUnitTransform();
	}

	public bool CheckValidTarget(Damageable target)
	{
		if (target == null)
			return false;

		if (HasKeyword(Keyword.Flying) || HasKeyword(Keyword.Confused))
			return true;

		if (target.Type == TouchableType.Hero)
		{
			Entity e = (Entity)target.InstanceInfo;

			if ((e.MeleeRow.HasUnitWithKeyword(Keyword.Defending)
				|| e.RangedRow.HasUnitWithKeyword(Keyword.Defending)
				|| e.SiegeRow.HasUnitWithKeyword(Keyword.Defending)))
				return false;
			else return true;
		}
		else
		{
			Unit u = (Unit)target.InstanceInfo;

			if (u.HasKeyword(Keyword.Shrouded))
				return false;

			switch (WorldState.CurrentRange)
			{
				case UnitRange.Melee:
					//If our unit is of Melee range, is our target within the melee row?
					if (u.WorldState.Owner.MeleeRow.HasUnit(u))
					{
						//Target is within the melee row, does the row have any units with defending?
						if (u.HasKeyword(Keyword.Defending))
							return true;
						else if (u.WorldState.Owner.MeleeRow.HasUnitWithKeyword(Keyword.Defending))
							return false;
						else return true;
					}
					else return false;

				case UnitRange.Ranged:
					if (u.WorldState.Owner.MeleeRow.HasUnit(u) ||
						u.WorldState.Owner.RangedRow.HasUnit(u))
					{
						if (u.HasKeyword(Keyword.Defending))
							return true;
						else if (u.WorldState.Owner.MeleeRow.HasUnitWithKeyword(Keyword.Defending) ||
							u.WorldState.Owner.RangedRow.HasUnitWithKeyword(Keyword.Defending))
							return false;
						else return true;
					}
					else return false;

				case UnitRange.Siege:
					if (u.WorldState.Owner.MeleeRow.HasUnit(u) ||
						u.WorldState.Owner.RangedRow.HasUnit(u) ||
						u.WorldState.Owner.SiegeRow.HasUnit(u))
					{
						if (u.HasKeyword(Keyword.Defending))
							return true;
						else if (u.WorldState.Owner.MeleeRow.HasUnitWithKeyword(Keyword.Defending) ||
							u.WorldState.Owner.RangedRow.HasUnitWithKeyword(Keyword.Defending) ||
							u.WorldState.Owner.SiegeRow.HasUnitWithKeyword(Keyword.Defending))
							return false;
						else return true;
					}
					else return false;
			}
		}

		Debug.Log($"<color=red[Unit]</color>: Fallthrough on CheckValidTarget. Could not verify {target.gameObject.name} target passed.");
		return false;
	}
	#endregion

	#region Trigger Ability Functions
	void TriggerAbility()
	{
		//Stunned units do not trigger their effects.
		if (HasKeyword(Keyword.Stunned))
			return;

		foreach (SummonAbility ability in summonData.AbilityEffects)
		{
			switch (ability.Effect)
			{
				case Effect.Inflict:
					Inflict(ability);
					break;

				case Effect.Heal:
					Heal(ability);
					break;

				case Effect.BuffAttack:
					BuffAttack(ability);
					break;

				case Effect.BuffHealth:
					BuffHealth(ability);
					break;

				case Effect.FlatDamageInstant:
					FlatDamageInstant(ability);
					break;

				case Effect.RequiredKeyword:
				case Effect.RequiredState:
					if (CheckRequiredState(ability))
						continue;
					else return;

				case Effect.Pull:
					Pull(ability);
					break;

				case Effect.Push:
					Push(ability);
					break;

				case Effect.Replenish:
					WorldState.Owner.Replenish(ability.AttributeOne);
					break;

				case Effect.SummonCopy:
					SummonCopy(ability);
					break;
			}
		}
	}

	void CheckGlobalEffects()
	{
		//Does this unit have any active keywords?
		if (WorldState.ActiveKeywords.Count == 0)
			return;

		foreach (KeywordState keyState in WorldState.ActiveKeywords)
		{
			switch (keyState.Key)
			{
				case Keyword.Legion:
					FindTargets(Target.Friendly | Target.AllRows, Keyword.Legion, out List<Touchable> targets);
					int legionBonus = 0;
					foreach (Touchable t in targets)
					{
						Unit u = (Unit)t.InstanceInfo;

						if (targets.Count - 1 < 0)
							legionBonus = 0;
						else
							legionBonus = targets.Count - 1;
						u.WorldState.Stats.LegionBonus = legionBonus;
						u.UpdateStatVisuals();
					}
					return;

				case Keyword.Empowering:
					break;

				default:
					Debug.Log($"<color=yellow>[Unit]</color>: Keyword {keyState.Key} not a global effect.");
					break;
			}
		}

		return;
	}
	#endregion

	#region Summon Ability Functions
	void Heal(SummonAbility ability)
	{
		if (ability.Target == Target.Self)
		{
			if (!WorldState.isDead)
				WorldState.Instance.Healed(ability.AttributeOne);

			return;
		}

		if (!FindTargets(ability.Target, out List<Touchable> targets))
			return;

		Target targetInfo = ability.Target & (Target.Single | Target.Bounce);

		switch (targetInfo)
		{
			case Target.Single:

				foreach (Touchable t in targets)
				{
					Damageable d = (Damageable)t;

					if (d.Type == TouchableType.Hero)
					{
						d.Healed(ability.AttributeOne);
						return;
					}
					else
					{
						Unit u = (Unit)t.InstanceInfo;

						if (u.WorldState.Stats.CurrentHealth < u.WorldState.Stats.CurrentHealthCap)
						{
							u.WorldState.Instance.Healed(ability.AttributeOne);
							return;
						}
					}
				}
				break;

			case Target.Bounce:
				break;
		}
	}

	void Inflict(SummonAbility ability)
	{
		Keyword keywordToInflict = (Keyword)(1 << ability.AttributeTwo);

		if (ability.Target == Target.Self)
		{
			if (!WorldState.isDead)
				AddKeyword(new KeyValuePair<Keyword, int>(keywordToInflict, ability.AttributeThree));

			return;
		}

		if (!FindTargets(ability.Target, out List<Touchable> targets))
			return;

		Unit u;

		Target whatToTarget = ability.Target & (Target.Single | Target.Bounce | Target.Row | Target.Persistent | Target.LastAttacked);

		switch (whatToTarget)
		{
			case Target.Single:
				Debug.Log("Getting single target");
				int index = UnityEngine.Random.Range(0, targets.Count);
				u = (Unit)(targets[index].InstanceInfo);

				u.AddKeyword(new KeyValuePair<Keyword, int>(keywordToInflict, ability.AttributeThree));
				return;

			case Target.Row:
				u = (Unit)targets[0].InstanceInfo;
				u.RowUnitIsIn.AddKeyword(new KeyValuePair<Keyword, int>(keywordToInflict, ability.AttributeThree));
				return;

			case Target.Persistent:
				u = (Unit)(targets[0].InstanceInfo);

				u.AddKeyword(new KeyValuePair<Keyword, int>(keywordToInflict, ability.AttributeThree));
				return;

			case Target.LastAttacked:
				u = (Unit)(targets[0].InstanceInfo);

				u.AddKeyword(new KeyValuePair<Keyword, int>(keywordToInflict, ability.AttributeThree));
				return;

			default:
				Debug.Log("<color=red>[Unit]</color>: Defaulted in Inflict. Did you set the target parameter correctly?");
				return;
		}

		//foreach (Touchable t in targets)
		//{
		//	u = (Unit)t.InstanceInfo;

		//	u.AddKeyword(new KeyValuePair<Keyword, int>(keywordToInflict, ability.AttributeThree));
		//}
	}

	public void BuffAttack(SummonAbility ability)
	{
		if (!FindTargets(ability.Target, out List<Touchable> targets))
			return;

		Unit u;

		Target whatToTarget = ability.Target & (Target.Single | Target.Bounce | Target.Row);

		switch (whatToTarget)
		{
			case Target.Single:
				int index = UnityEngine.Random.Range(0, targets.Count);
				u = (Unit)(targets[index].InstanceInfo);

				u.WorldState.Stats.TemporaryAttackBonus += ability.AttributeTwo;
				u.UpdateStatVisuals();
				break;

			case Target.Row:
				foreach (Touchable t in targets)
				{
					u = (Unit)t.InstanceInfo;

					u.WorldState.Stats.TemporaryAttackBonus += ability.AttributeTwo;
					u.UpdateStatVisuals();
				}
				break;

			default:
				Debug.Log("<color=red>[Unit]</color>: Defaulted in BuffAttack. Did you set the target parameter correctly?");
				break;
		}
	}

	public void BuffHealth(SummonAbility ability)
	{
		if (!FindTargets(ability.Target, out List<Touchable> targets))
			return;

		Unit u;

		Target whatToTarget = ability.Target & (Target.Single | Target.Row | Target.Persistent);

		switch (whatToTarget)
		{
			case Target.Single:
				int index = UnityEngine.Random.Range(0, targets.Count);
				u = (Unit)(targets[index].InstanceInfo);

				u.WorldState.Stats.TemporaryHealthBonus += ability.AttributeThree;
				u.UpdateStatVisuals();
				return;

			case Target.Row:
				foreach (Touchable t in targets)
				{
					u = (Unit)t.InstanceInfo;

					u.WorldState.Stats.TemporaryHealthBonus += ability.AttributeThree;
					u.UpdateStatVisuals();
				}
				return;
		}

		foreach (Touchable t in targets)
		{
			u = (Unit)t.InstanceInfo;

			u.WorldState.Stats.TemporaryHealthBonus += ability.AttributeThree;
			u.UpdateStatVisuals();
		}
	}

	void FlatDamageInstant(SummonAbility ability)
	{
		if (!FindTargets(ability.Target, out List<Touchable> targets))
			return;

		Target whatToTarget = ability.Target & (Target.Single | Target.Bounce);

		Damageable d;

		switch (whatToTarget)
		{
			case Target.Single:
				int index = UnityEngine.Random.Range(0, targets.Count);
				d = (Damageable)targets[index];

				d.Damaged(ability.AttributeTwo);
				return;
		}

		//If no target specification is provided, we just hit all the targets that we found.
		foreach (Touchable t in targets)
		{
			d = (Damageable)t;
			d.Damaged(ability.AttributeTwo);
		}
	}

	public void Destroy() => OnDeath();

	void Pull(SummonAbility ability)
	{
		if (!FindTargets(ability.Target, out List<Touchable> targets))
			return;

		Target whatToTarget = ability.Target & (Target.Single | Target.Persistent);

		Unit u;

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

				if ((ability.Target & Target.SaveForPersistent) == Target.SaveForPersistent)
					persistentTarget.Add(targets[0]);

				return;
		}

		//If no target specification is provided, we just hit all the targets that we found.
		foreach (Touchable t in targets)
		{
			if (t.Type == TouchableType.Hero)
				continue;

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

			if ((ability.Target & Target.SaveForPersistent) == Target.SaveForPersistent)
				persistentTarget.Add(t);
		}
		return;
	}

	void Push(SummonAbility spell)
	{
		if (!FindTargets(spell.Target, out List<Touchable> targets))
			return;

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

				return;
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
		return;
	}

	void SummonCopy(SummonAbility ability)
	{
		if (!FindTargets(ability.Target, out List<Touchable> targets))
			return;

		Unit u;

		Target whatToTarget = ability.Target & (Target.LastAttacked);

		switch (whatToTarget)
		{
			case Target.LastAttacked:
				
				if (lastAttacked.Type == TouchableType.Unit)
				{
					u = (Unit)lastAttacked.InstanceInfo;
					Unit newUnit = new Unit(u.summonData);

					switch (u.WorldState.CurrentRange)
					{
						case UnitRange.Melee:
							
							WorldState.Owner.MeleeRow.SummonToRow(newUnit);
							break;

						case UnitRange.Ranged:
							WorldState.Owner.RangedRow.SummonToRow(newUnit);
							break;

						case UnitRange.Siege:
							WorldState.Owner.SiegeRow.SummonToRow(newUnit);
							break;
					}
				}
				return;
		}
		return;
	}
	#endregion

	#region Keyword Functions
		Damageable Confused(Damageable originalTarget)
	{
		int chance = UnityEngine.Random.Range(1, 101);

		//Hit original target
		if (chance <= 10)
			return originalTarget;
		else if (chance < 50)
		{
			//Hit unit within same row as target, or if ur targeting the hero, just a random unit
			if (originalTarget.Type == TouchableType.Hero)
			{
				FindTargets((Target.Enemy | Target.AllRows), out List<Touchable> targets);

				int index = UnityEngine.Random.Range(0, targets.Count);

				return (Damageable)targets[index];
			}
			else
			{
				Unit u = (Unit)originalTarget.InstanceInfo;

				List<Touchable> targets = u.RowUnitIsIn.GetTargetsFromRow();
				int index = UnityEngine.Random.Range(0, targets.Count);

				targets.Remove(originalTarget);

				return (Damageable)targets[index];
			}
		}
		else
		{
			//Return a random target excluding the row of the unit

			FindTargets((Target.Enemy | Target.AllRows | Target.Hero), out List<Touchable> allTargets);

			if (originalTarget.Type == TouchableType.Hero)
			{
				allTargets.Remove(originalTarget);
				int index = UnityEngine.Random.Range(0, allTargets.Count);
				return (Damageable)allTargets[index];
			}
			else
			{
				Unit u = (Unit)originalTarget.InstanceInfo;

				List<Touchable> targetsFromRow = u.RowUnitIsIn.GetTargetsFromRow();

				foreach (Touchable t in targetsFromRow)
				{
					if (allTargets.Contains(t))
						allTargets.Remove(t);
				}

				int index = UnityEngine.Random.Range(0, allTargets.Count);

				return (Damageable)allTargets[index];
			}
		}
	}

	public void Empowering(SummonAbility ability)
	{
		switch (ability.Effect)
		{
			case Effect.BuffAttack:
				WorldState.Stats.TemporaryAttackBonus += ability.AttributeTwo;

				UpdateStatVisuals();
				break;
		}
	}

	void Reincarnate()
	{
		int numberOfSummons = 1;

		foreach (KeywordState state in WorldState.ActiveKeywords)
			if (state.Key == Keyword.Reincarnate)
				numberOfSummons = state.Duration;

		for (int i = 0; i < numberOfSummons; i++)
		{
			Unit unit = new Unit(summonData);
			unit.WorldState.Stats.AdjustLostHealth(unit.WorldState.Stats.CurrentHealthCap - 1);

			RowUnitIsIn.SummonToRow(unit);

			unit.RemoveKeyword(Keyword.Reincarnate);
		}
	}

	void OnFire()
	{
		int onFireDamage = 1;

		foreach (KeywordState state in WorldState.ActiveKeywords)
			if (state.Key == Keyword.OnFire)
				onFireDamage = state.Duration;

		WorldState.Owner.EntityDamageable.Damaged(onFireDamage);
		Debug.Log($"<color=green>[Unit]</color>: Entity took {onFireDamage} damage due to OnFire.");
	}
	#endregion

	#region Keyword Helper Functions
	public void AddKeyword(KeyValuePair<Keyword, int> keywordToAdd)
	{
		foreach (KeywordState s in WorldState.ActiveKeywords)
		{
			if (s.Key == keywordToAdd.Key)
			{
				switch (s.Key)
				{
					case Keyword.Confused:
					case Keyword.Vulnerable:
					case Keyword.Stunned:
					case Keyword.Reincarnate:
					case Keyword.OnFire:
						s.ClearKeyUpdateSub();

						if (BattleManager.Instance.ActiveHero == WorldState.Owner)
						{
							WorldState.Owner.TurnStartTrigger += s.UpdateKeywordVisual;
							s.Trigger = KeywordState.KeywordUpdate.ParentStartOfTurn;
						}
						else
						{
							WorldState.Owner.EnemyEntity.TurnStartTrigger += s.UpdateKeywordVisual;
							s.Trigger = KeywordState.KeywordUpdate.ParentStartOfTurn;
						}

						s.Duration += 1;
						Debug.Log($"<color=orange>[Unit]</color>: Effect {keywordToAdd} already exists on {summonData.SummonName}. Increasing duration.");
						break;
				
				}
				return;
			}
		}

		KeywordState keyState = new KeywordState();
		keyState.Key = keywordToAdd.Key;
		keyState.Duration = keywordToAdd.Value;
		keyState.Parent = WorldState.Instance;
		keyState.Owner = WorldState.Owner;
		WorldState.ActiveKeywords.Add(keyState);

		AddVisualEffect(keyState);
	}

	private void AddVisualEffect(KeywordState state)
	{
		state.Visual = GameObject.Instantiate(BattleManager.Instance.UnitEffectPrefab);
		state.Visual.transform.parent = WorldState.Instance.transform;
		state.Visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
		state.Visual.transform.localScale = Vector3.one;

		SpriteRenderer effectRenderer = state.Visual.GetComponent<SpriteRenderer>();
		effectRenderer.sprite = GetUnitEffectSprite(state.Key);
		Debug.Log($"<color=green>[Unit]</color>: Effect {state.Key} added to {summonData.SummonName}.");

		//Add a switch statement to properly layer the effects.
		switch (state.Key)
		{
			case Keyword.Defending:
				effectRenderer.sortingOrder = 0;
				break;

			case Keyword.Flying:
				effectRenderer.sortingOrder = 1;
				break;

			case Keyword.Confused:
				effectRenderer.sortingOrder = 2;
				break;

			case Keyword.OnFire:
				effectRenderer.sortingOrder = 3;
				break;

			case Keyword.Shrouded:
				effectRenderer.sortingOrder = 4;
				break;

			case Keyword.Vulnerable:
				effectRenderer.sortingOrder = 5;
				break;

			case Keyword.Shielded:
				effectRenderer.sortingOrder = 6;
				break;

			case Keyword.Stunned:
				effectRenderer.sortingOrder = 7;
				break;

			default:
				Debug.Log("<color=red>[Unit]</color>: Failed to find a valid effect to base sorting order. Defaulted to 0.");
				break;
		}

		if (state.Duration == -1)
			return;

		//Need to specify WHEN this unit should update its timer- either at the END of its owners turn, or at the start of the NEXT owners turn
		if (BattleManager.Instance.ActiveHero == WorldState.Owner)
		{
			WorldState.Owner.TurnStartTrigger += state.UpdateKeywordVisual;
			state.Trigger = KeywordState.KeywordUpdate.ParentStartOfTurn;
		}
		else
		{
			WorldState.Owner.EnemyEntity.TurnStartTrigger += state.UpdateKeywordVisual;
			state.Trigger = KeywordState.KeywordUpdate.ParentStartOfTurn;
		}
	}

	public void RemoveKeyword(Keyword keyToRemove)
	{
		KeywordState foundState = null;
		
		foreach (KeywordState s in WorldState.ActiveKeywords)
		{
			if (s.Key == keyToRemove)
			{
				foundState = s;
				break;
			}
		}

		if (foundState == null)
			return;

		GameObject.Destroy(foundState.Visual);
		if (WorldState.ActiveKeywords.Remove(foundState))
			Debug.Log($"<color=green>[Unit]</color>: Effect {keyToRemove} removed from {summonData.SummonName}.");
	}

	public bool HasKeyword(Keyword keywordToFind)
	{
		foreach (KeywordState state in WorldState.ActiveKeywords)
			if (state.Key == keywordToFind)
				return true;

		return false;
	}
	#endregion

	#region General Helper Functions
	public void OnDeath()
	{
		UpdateStatVisuals();

		OnDeathTrigger?.Invoke();

		RowUnitIsIn.RemoveDead(this);

		CheckGlobalEffects();

		if (HasKeyword(Keyword.Empowering))
			RemoveEmpoweringEffect(summonData.AbilityEffects[0]);

		if (HasKeyword(Keyword.Reincarnate))
			Reincarnate();

		DestroyUnitInWorld();
	}

	public void UpdateStatVisuals()
	{
		HealthText.text = WorldState.Stats.CurrentHealth.ToString();
		if (WorldState.Stats.CurrentHealth == WorldState.Stats.CurrentHealthCap && WorldState.Stats.CurrentHealthCap > WorldState.Stats.BaseHealth)
			HealthText.color = Color.green;
		else if (WorldState.Stats.CurrentHealth < WorldState.Stats.CurrentHealthCap)
			HealthText.color = Color.red;
		else if (WorldState.Stats.CurrentHealth == WorldState.Stats.BaseHealth)
			HealthText.color = Color.white;

		AttackText.text = WorldState.Stats.CurrentAttack().ToString();
		if (WorldState.Stats.CurrentAttack() > WorldState.Stats.BaseAttack)
			AttackText.color = Color.green;
		else if (WorldState.Stats.CurrentAttack() < WorldState.Stats.BaseAttack)
			AttackText.color = Color.red;
		else if (WorldState.Stats.CurrentAttack() == WorldState.Stats.BaseAttack)
			AttackText.color = Color.white;
	}

	private Sprite GetUnitEffectSprite(Keyword keywordToShow) => keywordToShow switch
	{
		Keyword.Defending => Resources.Load<Sprite>("Unit Effects/Defending"),
		Keyword.Flying => Resources.Load<Sprite>("Unit Effects/Flying"),
		Keyword.Confused => Resources.Load<Sprite>("Unit Effects/Confused"),
		Keyword.OnFire => Resources.Load<Sprite>("Unit Effects/OnFire"),
		Keyword.Stunned => Resources.Load<Sprite>("Unit Effects/Stunned"),
		Keyword.Shrouded => Resources.Load<Sprite>("Unit Effects/Shrouded"),
		Keyword.Shielded => Resources.Load<Sprite>("Unit Effects/Shielded"),
		Keyword.Vulnerable => Resources.Load<Sprite>("Unit Effects/Vulnerable"),
		Keyword.Reincarnate => Resources.Load<Sprite>("Unit Effects/Reincarnate"),
		Keyword.Legion => Resources.Load<Sprite>("Unit Effects/Legion"),
		Keyword.Cleave => Resources.Load<Sprite>("Unit Effects/Cleave"),
		Keyword.Siphon => Resources.Load<Sprite>("Unit Effects/Siphon"),
		Keyword.Warden => Resources.Load<Sprite>("Unit Effects/Warden"),
		_ => null,
	};

	void CheckForBaseKeywords(Keyword baseKeywords)
	{
		WorldState.ActiveKeywords = new HashSet<KeywordState>(0);

		if (baseKeywords == 0)
			return;

		for (int keyIndex = 0; keyIndex < (int)Keyword.Max; keyIndex++)
		{
			Keyword indexedKeyword = (Keyword)(1 << keyIndex);

			if ((baseKeywords & indexedKeyword) == indexedKeyword)
				AddKeyword(new KeyValuePair<Keyword, int>(indexedKeyword, -1));
		}
			
		PrintKeywords(WorldState.ActiveKeywords);
	}

	public void CheckEmpoweringEffect()
	{
		////If this unit has empowering, tell the other units
		////within this row to update themselves.
		if (HasKeyword(Keyword.Empowering))
			RowUnitIsIn.UpdateEmpowering(this, summonData.AbilityEffects[0]);

		//If any units currently in the row have an empowering effect,
		//update this unit to reflect that
		if (!RowUnitIsIn.HasUnitWithKeyword(Keyword.Empowering))
			return;

		List<Touchable> empowering = RowUnitIsIn.GetTargetsFromRow(Keyword.Empowering);

		if (empowering.Contains(WorldState.Instance))
			empowering.Remove(WorldState.Instance);

		if (empowering.Count != 0)
			foreach (Touchable t in empowering)
			{
				switch (t.Type)
				{
					case TouchableType.Hero:
						Debug.Log("Empowering effects for Heros not implemented");
						break;

					case TouchableType.Unit:
						Unit u = (Unit)t.InstanceInfo;
						Empowering(u.summonData.AbilityEffects[0]);
						break;
				}
			}
	}

	public void RemoveEmpoweringEffect(SummonAbility ability)
	{
		Debug.Log($"<color=orange>[Unit]</color>: Removing empowering effect of {summonData.SummonName} due to move or death.");

		if (FindTargets(ability.Target, out List<Touchable> targets))
			return;

		HashSet<Unit> affectedUnits = new HashSet<Unit>();

		foreach (Touchable t in targets)
			affectedUnits.Add((Unit)t.InstanceInfo);

		switch (ability.Effect)
		{
			case Effect.BuffAttack:
				foreach (Unit u in affectedUnits)
				{
					u.WorldState.Stats.TemporaryAttackBonus -= ability.AttributeTwo;
					u.UpdateStatVisuals();
				}
				break;
		}
	}
	#endregion

	#region Targeting Functions
	/// <summary>
	/// The Unit version of FindTargets. Returns a list of targets based on the parameter passed.
	/// </summary>
	/// <param name="targetInfo">Handles: CurrentRow, Last Damaged, Chosen, Friendly, Enemy, Either</param>
	/// <returns></returns>
	bool FindTargets(Target targetInfo, out List<Touchable> targets)
	{
		targets = new List<Touchable>(0);

		Target preCheck = targetInfo & (Target.Persistent | Target.Chosen | Target.CurrentRow | Target.LastAttacked);

		switch (preCheck)
		{
			case Target.CurrentRow:
				foreach (Touchable t in RowUnitIsIn.GetTargetsFromRow())
					targets.Add(t);

				if ((targetInfo & Target.Self) != Target.Self)
					targets.Remove(WorldState.Instance);

				return true;

			case Target.LastAttacked:
				if (lastAttacked.Type == TouchableType.Unit)
				{
					Unit u = (Unit)lastAttacked.InstanceInfo;
					if (u.WorldState.isDead)
						return false;
					else
					{
						targets.Add(lastAttacked);
						return true;
					}
				}
				else
				{
					targets.Add(lastAttacked);
					return true;
				}

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
				targets = WorldState.Owner.QueryForTargets(targetInfo);
				break;

			case Target.Enemy:
				targets = WorldState.Owner.EnemyEntity.QueryForTargets(targetInfo);
				break;

			case Target.Either:
				foreach (Touchable t in WorldState.Owner.QueryForTargets(targetInfo))
					targets.Add(t);

				foreach (Touchable t in WorldState.Owner.EnemyEntity.QueryForTargets(targetInfo))
					targets.Add(t);
				break;

			default:
				Debug.Log($"<color=red>[Card]</color> on {summonData.SummonName}: Invalid target parameter passed. Please verify the target enum.");
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

		Target targetsToFind = targetInfo & Target.Either;

		switch (targetsToFind)
		{
			case Target.Friendly:
				targets = WorldState.Owner.QueryForTargets(targetInfo, keywordToFind);
				break;

			case Target.Enemy:
				targets = WorldState.Owner.EnemyEntity.QueryForTargets(targetInfo, keywordToFind);
				break;

			case Target.Either:
				foreach (Touchable t in WorldState.Owner.QueryForTargets(targetInfo, keywordToFind))
					targets.Add(t);

				foreach (Touchable t in WorldState.Owner.EnemyEntity.QueryForTargets(targetInfo, keywordToFind))
					targets.Add(t);
				break;

			default:
				Debug.Log($"<color=red>[Card]</color> on {summonData.SummonName}: Invalid target parameter passed. Please verify the target enum.");
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

	bool CheckRequiredState(SummonAbility ability)
	{
		persistentTarget.Clear();

		List<Touchable> targets;

		switch (ability.Effect)
		{
			case Effect.RequiredKeyword:
				Keyword key = (Keyword)(1 << ability.AttributeOne);

				if (!FindTargets(ability.Target, key, out targets))
					return false;

				foreach (Touchable t in targets)
					persistentTarget.Add(t);

				if ((ability.Target & Target.Self) != Target.Self)
					persistentTarget.Remove(WorldState.Instance);

				PrintTargets(persistentTarget);

				return true;

			case Effect.RequiredState:
				Debug.Log("RequiredState not implemented");
				return false;

			default:
				Debug.Log("Defaulted in CheckRequiredState");
				return false;
		}
	}
	#endregion

	#region UnitTransform Functions
	public void UpdateUnitTransform(Vector3 position, Quaternion rotation, Vector3 scale)
	{
		WorldState.Instance.transform.SetPositionAndRotation(position, rotation);
		WorldState.Instance.transform.localScale = scale;
	}

	public void UpdateUnitTransform()
	{
		WorldState.Instance.transform.SetPositionAndRotation(positionOnRow, defaultRotation);

		if (isHovered)
		{
			WorldState.Instance.transform.localScale = hoveredUnitScale;
			summonSR.sortingOrder = 2;
		}
		else
		{
			WorldState.Instance.transform.localScale = defaultUnitScale;
			summonSR.sortingOrder = 0;
		}
	}
	#endregion

	#region Unit Orientation Functions
	public void SetUnitOrientation(Vector3 position, Quaternion rotation)
    {
		if (position != Vector3.zero)
			positionOnRow = position;

		if (rotation != Quaternion.identity)
			defaultRotation = rotation;
	}

	public void SetUnitOrientation(Vector3 position) => positionOnRow = position;
	#endregion

	#region Debug Print Functions
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

	void PrintKeywords(HashSet<KeywordState> state)
	{
		string s = "";

		if (state != null)
			foreach (KeywordState k in state)
				s += $"{k.Key}; ";

		if (s == "")
			Debug.Log($"<color=orange>[Unit]</color>: {WorldState.Instance.gameObject.name} has no Keywords.");
		else
			Debug.Log($"<color=green>[Unit]</color>: {WorldState.Instance.gameObject.name} has the Base Keywords of " + s);
	}
	#endregion
}