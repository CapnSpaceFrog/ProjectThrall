using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;

public class KeywordState
{
	public enum KeywordUpdate
	{
		ParentTurnStart,
		EnemyTurnStart,
	}

	public Selectable Parent;
	public Entity Owner;
	public Keyword Key;
	public int Duration;
	public GameObject Visual;
	public KeywordUpdate UpdatePoint;

	#region Helper Functions
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
				case SelectionType.Unit:
					Unit u = (Unit)Parent.InstanceInfo;
					u.RemoveKeyword(Key);
					break;

				case SelectionType.Row:
					Row r = (Row)Parent;
					r.RemoveKeyword(Key);
					break;

				case SelectionType.Hero:

					break;
			}

			ClearKeyUpdateSub();
		}
	}

	public void ClearKeyUpdateSub()
	{
		switch (UpdatePoint)
		{
			case KeywordUpdate.ParentTurnStart:
				Owner.TurnStartTrigger -= UpdateKeywordVisual;
				break;

			case KeywordUpdate.EnemyTurnStart:
				Owner.EnemyEntity.TurnStartTrigger -= UpdateKeywordVisual;
				break;
		}
	}
	#endregion
}

public struct StatState
{
	public readonly int BaseHealth;
	public readonly int BaseAttack;

	public TMP_Text AttackText;
	public TMP_Text HealthText;

	#region Constructor
	public StatState(int baseHealth, int baseAttack)
	{
		BaseHealth = baseHealth;
		lostHealth = 0;
		TemporaryHealthBonus = 0;

		BaseAttack = baseAttack;
		LegionBonus = 0;
		TemporaryAttackBonus = 0;

		AttackText = null;
		HealthText = null;
	}
	#endregion

	#region Health
	public int CurrentHealth { get { return CurrentHealthCap - lostHealth; } }
	public readonly int CurrentHealthCap => BaseHealth + bonusHealth;
	public int TemporaryHealthBonus;
	
	private readonly int bonusHealth => TemporaryHealthBonus;
	private int lostHealth;

	public void AdjustLostHealth(int healthChange)
	{
		lostHealth += healthChange;

		if (lostHealth < 0)
			lostHealth = 0;
	}
	#endregion

	#region Attack
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
	#endregion

	#region Helper Functions
	public void UpdateStatVisuals()
	{
		HealthText.text = CurrentHealth.ToString();
		if (CurrentHealth == CurrentHealthCap && CurrentHealthCap > BaseHealth)
			HealthText.color = Color.green;
		else if (CurrentHealth < CurrentHealthCap)
			HealthText.color = Color.red;
		else if (CurrentHealth == BaseHealth)
			HealthText.color = Color.white;

		AttackText.text = CurrentAttack().ToString();
		if (CurrentAttack() > BaseAttack)
			AttackText.color = Color.green;
		else if (CurrentAttack() < BaseAttack)
			AttackText.color = Color.red;
		else if (CurrentAttack() == BaseAttack)
			AttackText.color = Color.white;
	}
	#endregion
}

public class Unit
{
	public struct State
	{
		public string Name;
		public string Description;
		public StatState Stats;
		public UnitRange CurrentRange;
		public bool hasAttacked;
		public bool isDead;
		public bool isSelected;
		public HashSet<KeywordState> ActiveKeywords;
		public SummonAbility[] Ability;

		public Entity Owner;
		public Row CurrentRow;

		public Damageable DamageableInstance;
		public SpriteRenderer SR;
		public Transform Transform;
	}

	private State WorldState;

	Vector3 positionOnRow;
	Quaternion defaultRotation;
	Vector3 defaultUnitScale;
	Vector3 hoveredUnitScale;

	event Action OnKillTrigger;
	event Action OnDeathTrigger;
	event Action OnDamageTrigger;
	event Action OnAttackTrigger;

	#region Constructors
	public Unit(UnitData data, Entity owner, Row row)
	{
		defaultUnitScale = new Vector3(0.41f, 0.41f, 1);
		hoveredUnitScale = new Vector3(0.475f, 0.475f, 1);

		WorldState = new State();

		WorldState.Stats = new StatState(data.BaseHealth, data.BaseAttack);

		WorldState.CurrentRange = data.Range;
		WorldState.isDead = false;
		WorldState.isSelected = false;
		WorldState.Ability = data.AbilityEffects;
		WorldState.Owner = owner;
		WorldState.CurrentRow = row;
		WorldState.Name = data.SummonName;

		InstantiateUnitInWorld(data.SummonArt);

		OnSummon();
	}

	public Unit(State state)
	{
		WorldState = state;

		InstantiateUnitInWorld(state.SR.sprite);

		OnSummon();
	}
	#endregion

	#region Instantiation Functions
	private void InstantiateUnitInWorld(Sprite splash)
	{
		GameObject unitObj = GameObject.Instantiate(BattleManager.Instance.UnitPrefab);
		WorldState.Transform = unitObj.transform;
		unitObj.name = WorldState.Name;

		WorldState.DamageableInstance = unitObj.AddComponent<Damageable>();
		WorldState.DamageableInstance.Type = SelectionType.Unit;

		WorldState.DamageableInstance.InstanceInfo = this;

		unitObj.AddComponent<BoxCollider>().size = new Vector3(7.65f, 10.5f, 0.1f);

		defaultRotation = Quaternion.Euler(90, 0, 0);
		unitObj.transform.localScale = defaultUnitScale;

		WorldState.SR = unitObj.GetComponent<SpriteRenderer>();
		unitObj.transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = splash;

		WorldState.Stats.AttackText = unitObj.transform.GetChild(1).GetComponent<TextMeshPro>();
		WorldState.Stats.HealthText = unitObj.transform.GetChild(2).GetComponent<TextMeshPro>();

		WorldState.hasAttacked = true;

		AssignDelegates(unitObj);
	}

	private void AssignDelegates(GameObject unitObj)
	{
		if (WorldState.Owner == BattleManager.Instance.PlayerHero)
		{
			unitObj.layer = LayerMask.NameToLayer("Friendly Unit");

			#region Touched Delegate
			WorldState.DamageableInstance.ReceivedPrimary = () =>
			{
				//Specifics with on touch function go here.
				if (WorldState.hasAttacked == true || WorldState.Stats.CurrentAttack() == 0
				|| HasKeyword(Keyword.Stunned)
				|| HasKeyword(Keyword.Warden))
					return false;

				WorldState.isSelected = true;
				SetUnitTransform();

				BattleManager.Instance.Crosshair.SetActive(true);

				return true;
			};
			#endregion

			#region WhileTouched Delegate
			WorldState.DamageableInstance.WhileSelected = (Vector3 mouseWorldPos) =>
			{
				BattleManager.Instance.Crosshair.transform.position = mouseWorldPos;
			};
			#endregion

			#region Released Delegate
			WorldState.DamageableInstance.Released = () =>
			{
				BattleManager.Instance.RaycastFromMousePosition(LayerMask.GetMask("Enemy Unit", "Enemy Hero"), out Selectable entityTouched);
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
			WorldState.DamageableInstance.ReceivedSecondary = () =>
			{
				WorldState.isSelected = false;
				SetUnitTransform();

				BattleManager.Instance.Crosshair.SetActive(false);
			};
			#endregion
		}
		else
			unitObj.layer = LayerMask.NameToLayer("Enemy Unit");

		//TODO: Find a better solution to damage and refactor the "touchable" class
		#region Damaged Delegate
		WorldState.DamageableInstance.Damaged = (damageReceived) =>
		{
			if (HasKeyword(Keyword.Shielded))
			{
				RemoveKeyword(Keyword.Shielded);
				//Debug.Log($"<color=orange>[Unit]</color>: {summonData.name} blocked {damageReceived} points of damage with Shielded.");
				return false;
			}

			if (HasKeyword(Keyword.Vulnerable))
				damageReceived *= 2;

			//Debug.Log($"<color=orange>[Unit]</color>: {summonData.name} received {damageReceived} points of damage.");
			WorldState.Stats.AdjustLostHealth(damageReceived);

			if (WorldState.Stats.CurrentHealth <= 0)
			{
				WorldState.isDead = true;
				OnDeath();
				return true;
			}
			else
			{
				WorldState.Stats.UpdateStatVisuals();
				return false;
			}
		};
		#endregion

		#region Healed Delegate
		WorldState.DamageableInstance.Healed = (int healingReceived) =>
		{
			//Debug.Log($"<color=orange>[Unit]</color>: {summonData.name} received {healingReceived} points of healing.");
			WorldState.Stats.AdjustLostHealth(-healingReceived);

			WorldState.Stats.UpdateStatVisuals();
		};
		#endregion
	}

	public void OnSummon()
	{
		//CheckForBaseKeywords(summonData.BaseKeywords);

		//Is there already an empowering unit in the row
		//our new unit was played at? If so, update them.
		CheckEmpoweringEffect();

		CheckGlobalEffects();

		//if (summonData.AbilityEffects.Length == 0)
		//	return;

		//switch (summonData.AbilityEffects[0].Trigger)
		//{
		//	case Trigger.OnSummon:
		//		TriggerAbility();
		//		break;

		//	case Trigger.StartOfTurn:
		//		Owner.TurnStartTrigger += TriggerAbility;
		//		break;

		//	case Trigger.EndOfTurn:
		//		Owner.TurnEndTrigger += TriggerAbility;
		//		break;

		//	case Trigger.OnDamage:
		//		OnDamageTrigger += TriggerAbility;
		//		break;

		//	case Trigger.OnKill:
		//		OnKillTrigger += TriggerAbility;
		//		break;

		//	case Trigger.OnDeath:
		//		OnDeathTrigger += TriggerAbility;
		//		break;

		//	case Trigger.OnAttack:
		//		OnAttackTrigger += TriggerAbility;
		//		break;
		//}
	}

	public void DestroyUnitInWorld()
	{
		GameObject.Destroy(WorldState.Transform.gameObject);
		
		WorldState.DamageableInstance = null;
	}
	#endregion

	#region Combat Functions
	void Attack(Damageable target)
	{
		if (TargetingHandler.CheckValidTarget(WorldState, target))
		{
			OnAttackTrigger?.Invoke();

			if (target.Type == SelectionType.Hero)
			{
				Entity entityTargeted = (Entity)target.InstanceInfo;

				//lastAttacked = target;

				if (target.Damaged(WorldState.Stats.CurrentAttack()))
					OnKillTrigger?.Invoke();

				OnDamageTrigger?.Invoke();
			}
			else
			{
				if (HasKeyword(Keyword.Shrouded))
					RemoveKeyword(Keyword.Shrouded);

				Unit unitTarget = (Unit)target.InstanceInfo;

				//lastAttacked = target;
				//unitTarget.lastAttacked = Instance;

				target.Damaged(WorldState.Stats.CurrentAttack());

				WorldState.DamageableInstance.Damaged(unitTarget.WorldState.Stats.CurrentAttack());

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

		if (!WorldState.isDead)
			SuccessfulAttack();
	}

	void SuccessfulAttack()
	{
		WorldState.hasAttacked = true;
		WorldState.isSelected = false;
		BattleManager.Instance.Crosshair.SetActive(false);
		SetUnitTransform();
	}

	void FailedAttack()
	{
		WorldState.isSelected = false;
		BattleManager.Instance.Crosshair.SetActive(false);
		SetUnitTransform();
	}
	#endregion

	#region Trigger Ability Functions
	void TriggerAbility()
	{
		//Stunned units do not trigger their effects.
		if (HasKeyword(Keyword.Stunned))
			return;

		//foreach (SummonAbility ability in summonData.AbilityEffects)
		//{
		//	switch (ability.Effect)
		//	{
		//		case Effect.Inflict:
		//			Inflict(ability);
		//			break;

		//		case Effect.Heal:
		//			Heal(ability);
		//			break;

		//		case Effect.BuffAttack:
		//			BuffAttack(ability);
		//			break;

		//		case Effect.BuffHealth:
		//			BuffHealth(ability);
		//			break;

		//		case Effect.FlatDamageInstant:
		//			FlatDamageInstant(ability);
		//			break;

		//		case Effect.RequiredKeyword:
		//		case Effect.RequiredState:
		//			if (CheckRequiredState(ability))
		//				continue;
		//			else return;

		//		case Effect.Pull:
		//			Pull(ability);
		//			break;

		//		case Effect.Push:
		//			Push(ability);
		//			break;

		//		case Effect.Replenish:
		//			Owner.Replenish(ability.AttributeOne);
		//			break;

		//		case Effect.SummonCopy:
		//			SummonCopy(ability);
		//			break;
		//	}
		//}
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
					TargetingHandler.FindTargets(Target.Friendly | Target.AllRows, WorldState.Owner, Keyword.Legion, out List<Selectable> targets);
					int legionBonus = 0;
					foreach (Selectable t in targets)
					{
						Unit u = (Unit)t.InstanceInfo;

						if (targets.Count - 1 < 0)
							legionBonus = 0;
						else
							legionBonus = targets.Count - 1;
						u.WorldState.Stats.LegionBonus = legionBonus;
						u.WorldState.Stats.UpdateStatVisuals();
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
		
	}

	void Inflict(SummonAbility ability)
	{
		
	}

	public void BuffAttack(SummonAbility ability)
	{
		
	}

	public void BuffHealth(SummonAbility ability)
	{
		
	}

	void FlatDamageInstant(SummonAbility ability)
	{
		
	}

	public void Destroy() => OnDeath();

	void Pull(SummonAbility ability)
	{
		
	}

	void Push(SummonAbility spell)
	{
		
	}

	void SummonCopy(SummonAbility ability)
	{
		
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
			if (originalTarget.Type == SelectionType.Hero)
			{
				TargetingHandler.FindTargets((Target.Enemy | Target.AllRows), WorldState.Owner, out List<Selectable> targets);

				int index = UnityEngine.Random.Range(0, targets.Count);

				return (Damageable)targets[index];
			}
			else
			{
				Unit u = (Unit)originalTarget.InstanceInfo;

				List<Selectable> targets = u.WorldState.CurrentRow.GetTargetsFromRow();
				int index = UnityEngine.Random.Range(0, targets.Count);

				targets.Remove(originalTarget);

				return (Damageable)targets[index];
			}
		}
		else
		{
			//Return a random target excluding the row of the unit

			TargetingHandler.FindTargets((Target.Enemy | Target.AllRows | Target.Hero), WorldState.Owner, out List<Selectable> allTargets);

			if (originalTarget.Type == SelectionType.Hero)
			{
				allTargets.Remove(originalTarget);
				int index = UnityEngine.Random.Range(0, allTargets.Count);
				return (Damageable)allTargets[index];
			}
			else
			{
				Unit u = (Unit)originalTarget.InstanceInfo;

				List<Selectable> targetsFromRow = u.WorldState.CurrentRow.GetTargetsFromRow();

				foreach (Selectable t in targetsFromRow)
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
		
	}

	void Reincarnate()
	{
		
	}

	void OnFire()
	{
		
		Debug.Log($"<color=green>[Unit]</color>: Entity took {0} damage due to OnFire.");
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
							s.UpdatePoint = KeywordState.KeywordUpdate.ParentTurnStart;
						}
						else
						{
							WorldState.Owner.EnemyEntity.TurnStartTrigger += s.UpdateKeywordVisual;
							s.UpdatePoint = KeywordState.KeywordUpdate.EnemyTurnStart;
						}

						s.Duration += 1;
						//Debug.Log($"<color=orange>[Unit]</color>: Effect {keywordToAdd} already exists on {summonData.SummonName}. Increasing duration.");
						break;
				
				}
				return;
			}
		}

		KeywordState keyState = new KeywordState();
		keyState.Key = keywordToAdd.Key;
		keyState.Duration = keywordToAdd.Value;
		keyState.Parent = WorldState.DamageableInstance;
		keyState.Owner = WorldState.Owner;
		WorldState.ActiveKeywords.Add(keyState);

		AddVisualEffect(keyState);
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
		//if (ActiveKeywords.Remove(foundState))
			//Debug.Log($"<color=green>[Unit]</color>: Effect {keyToRemove} removed from {summonData.SummonName}.");
	}

	public bool HasKeyword(Keyword keywordToFind)
	{
		foreach (KeywordState state in WorldState.ActiveKeywords)
			if (state.Key == keywordToFind)
				return true;

		return false;
	}
	#endregion

	#region General Functions
	public void RefreshAttack() => WorldState.hasAttacked = false;

	public void NewTurn()
	{
		RefreshAttack();
	}

	public void OnDeath()
	{
		WorldState.Stats.UpdateStatVisuals();

		OnDeathTrigger?.Invoke();

		WorldState.CurrentRow.RemoveDead(this);

		CheckGlobalEffects();

		//if (HasKeyword(Keyword.Empowering))
		//	RemoveEmpoweringEffect(summonData.AbilityEffects[0]);

		if (HasKeyword(Keyword.Reincarnate))
			Reincarnate();

		DestroyUnitInWorld();
	}

	void CheckForBaseKeywords(Keyword baseKeywords)
	{
		
	}

	public void CheckEmpoweringEffect()
	{
		////If this unit has empowering, tell the other units
		////within this row to update themselves.

		//If any units currently in the row have an empowering effect,
		//update this unit to reflect that
	}

	public void RemoveEmpoweringEffect(SummonAbility ability)
	{
		Debug.Log($"<color=orange>[Unit]</color>: Removing empowering effect of {WorldState.Name} due to move or death.");
	}
	#endregion

	#region VFX/Animation Functions
	private void AddVisualEffect(KeywordState state)
	{
		state.Visual = GameObject.Instantiate(BattleManager.Instance.UnitEffectPrefab);
		state.Visual.transform.parent = WorldState.Transform;
		state.Visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
		state.Visual.transform.localScale = Vector3.one;

		SpriteRenderer effectRenderer = state.Visual.GetComponent<SpriteRenderer>();
		effectRenderer.sprite = GetUnitEffectSprite(state.Key);
		//Debug.Log($"<color=green>[Unit]</color>: Effect {state.Key} added to {summonData.SummonName}.");

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
			state.UpdatePoint = KeywordState.KeywordUpdate.ParentTurnStart;
		}
		else
		{
			WorldState.Owner.EnemyEntity.TurnStartTrigger += state.UpdateKeywordVisual;
			state.UpdatePoint = KeywordState.KeywordUpdate.EnemyTurnStart;
		}
	}

	private static Sprite GetUnitEffectSprite(Keyword keywordToShow) => keywordToShow switch
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
	#endregion

	#region Transform Functions
	public void SetUnitTransform(Vector3 position, Quaternion rotation, Vector3 scale)
	{
		WorldState.Transform.SetPositionAndRotation(position, rotation);
		WorldState.Transform.localScale = scale;
	}

	public void SetUnitTransform()
	{
		WorldState.Transform.SetPositionAndRotation(positionOnRow, defaultRotation);

		if (WorldState.isSelected)
		{
			WorldState.Transform.localScale = hoveredUnitScale;
			WorldState.SR.sortingOrder = 2;
		}
		else
		{
			WorldState.Transform.localScale = defaultUnitScale;
			WorldState.SR.sortingOrder = 0;
		}
	}

	public void SetTransformVariables(Vector3 position, Quaternion rotation)
	{
		if (position != Vector3.zero)
			positionOnRow = position;

		if (rotation != Quaternion.identity)
			defaultRotation = rotation;
	}

	public void SetTransformVariables(Vector3 position) => positionOnRow = position;
	#endregion

	#region Debug Print Functions
	void PrintKeywords(HashSet<KeywordState> state)
	{
		string s = "";

		if (state != null)
			foreach (KeywordState k in state)
				s += $"{k.Key}; ";

		if (s == "")
			Debug.Log($"<color=orange>[Unit]</color>: {WorldState.Name} has no Keywords.");
		else
			Debug.Log($"<color=green>[Unit]</color>: {WorldState.Name} has the Base Keywords of " + s);
	}
	#endregion
}