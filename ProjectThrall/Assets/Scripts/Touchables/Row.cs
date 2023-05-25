using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

public class Row : Selectable
{
	[Header("Row Specific Variables")]
	public UnitRange RowType;
	public Bounds RowSpace;

	public Entity Owner { get; private set; }

	private List<Unit> UnitsInRow;

	HashSet<KeywordState> ActiveKeywords;

	private static int MaxUnitsPerRow = 6;
	private static float UnitSpacing = 3.65f;
	private static float EvenUnitStartSpacing = UnitSpacing / 2f;

	public void Awake()
	{
		InstanceInfo = this;

		UnitsInRow = new List<Unit>();

		ActiveKeywords = new HashSet<KeywordState>();
	}

	#region Default Row Functions
	public void SummonToRow(UnitData unitToAdd, int numberOfSummons)
	{
		for (int i = 0; i < numberOfSummons; i++)
		{
			if (UnitsInRow.Count == MaxUnitsPerRow)
				continue;

			Unit u = new Unit(unitToAdd, Owner, this);

			UnitsInRow.Add(u);
		}

		AdjustUnitSpacing();
	}

	public void SummonToRow(Unit.State state)
	{
		if (UnitsInRow.Count == MaxUnitsPerRow)
			return;

		Unit newUnit = new Unit(state);

		UnitsInRow.Add(newUnit);

		AdjustUnitSpacing();
	}

	/// <summary>
	/// Do not use if trying to remove dead. Use RemoveDead instead.
	/// </summary>
	/// <param name="unitToRemove">The unit to remove from the row.</param>
	public void RemoveUnit(Unit unitToRemove)
	{
		UnitsInRow.Remove(unitToRemove);

		AdjustUnitSpacing();
	}

	public void AddUnit(Unit unitToAdd)
	{
		UnitsInRow.Add(unitToAdd);

		AdjustUnitSpacing();
	}

	public bool RemoveDead(Unit deadUnit)
	{
		if (UnitsInRow.Contains(deadUnit))
		{
			UnitsInRow.Remove(deadUnit);
			AdjustUnitSpacing();
			return true;
		}
		else return false;
	}

	public bool HasUnit(Unit unitToCheck)
	{
		foreach (Unit unit in UnitsInRow)
		{
			if (unit.Equals(unitToCheck))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Given a keyword, return targets in the row that share the keyword.
	/// </summary>
	/// <param name="keywordToFind">The keyword to find on units in the row.</param>
	/// <returns></returns>
	public List<Selectable> GetTargetsFromRow(Keyword keywordToFind)
	{
		return null;
		//TODO: Redo targeting
	}

	public List<Selectable> GetTargetsFromRow()
	{
		return null;
		//TODO: Redo targeting
	}

	public bool IsEmpty() => UnitsInRow.Count == 0;

	private void StartOfTurn()
	{
		foreach (Unit unit in UnitsInRow)
			unit.NewTurn();
	}

	private void EndOfTurn()
	{

	}

	public void Reset()
	{
		foreach (Unit unit in UnitsInRow)
			unit.DestroyUnitInWorld();

		UnitsInRow.Clear();
	}
	#endregion

	#region Row Combat Functions
	public bool HasUnitWithKeyword(Keyword keyToCheck)
	{
		foreach (Unit unit in UnitsInRow)
			if (unit.HasKeyword(keyToCheck))
				return true;

		return false;
	}

	public void UpdateEmpowering(Unit source, SummonAbility empoweringEffect)
	{
		foreach (Unit u in UnitsInRow)
		{
			if (u == source)
				continue;

			u.Empowering(empoweringEffect);
		}
	}

	public void MoveToRow(Unit unitToMove)
	{
		if (unitToMove.HasKeyword(Keyword.Empowering))
			//unitToMove.RemoveEmpoweringEffect(unitToMove.summonData.AbilityEffects[0]);

		//unitToMove.WorldState.CurrentRow.RemoveUnit(unitToMove);

		AddUnit(unitToMove);

		unitToMove.CheckEmpoweringEffect();
	}
	#endregion

	#region Keyword Helper Functions
	public void AddKeyword(KeyValuePair<Keyword, int> keywordToAdd)
	{
		foreach (KeywordState s in ActiveKeywords)
		{
			if (s.Key == keywordToAdd.Key)
			{
				switch (s.Key)
				{
					case Keyword.Locked:
						s.ClearKeyUpdateSub();

						if (BattleManager.Instance.ActiveHero == Owner)
						{
							Owner.TurnStartTrigger += s.UpdateKeywordVisual;
							s.UpdatePoint = KeywordState.KeywordUpdate.ParentTurnStart;
						}
						else
						{
							Owner.EnemyEntity.TurnStartTrigger += s.UpdateKeywordVisual;
							s.UpdatePoint = KeywordState.KeywordUpdate.EnemyTurnStart;
						}

						s.Duration += 1;
						return;
				}
			}
		}

		KeywordState keyState = new KeywordState();
		keyState.Key = keywordToAdd.Key;
		keyState.Duration = keywordToAdd.Value;
		keyState.Parent = this;
		keyState.Owner = Owner;
		ActiveKeywords.Add(keyState);

		AddVisualEffect(keyState);
	}

	public void RemoveKeyword(Keyword keyToRemove)
	{
		KeywordState foundState = null;

		foreach (KeywordState s in ActiveKeywords)
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
		if (ActiveKeywords.Remove(foundState))
			Debug.Log($"<color=green>[Unit]</color>: Effect {keyToRemove} removed from Row.");
	}

	public bool HasKeyword(Keyword keyToFind)
	{
		foreach (KeywordState state in ActiveKeywords)
			if (state.Key == keyToFind)
				return true;

		return false;
	}

	private void AddVisualEffect(KeywordState state)
	{
		state.Visual = GameObject.Instantiate(BattleManager.Instance.RowEffectPrefab);
		state.Visual.transform.parent = transform;
		state.Visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(90, 0, 0));
		state.Visual.transform.localScale = new Vector3(0.26f, 1f, 1f);

		SpriteRenderer effectRenderer = state.Visual.GetComponent<SpriteRenderer>();
		effectRenderer.sprite = GetRowEffectSprite(state.Key);
		Debug.Log($"<color=green>[Unit]</color>: Effect {state.Key} added to Row.");

		//Add a switch statement to properly layer the effects.
		switch (state.Key)
		{
			case Keyword.Locked:
				effectRenderer.sortingOrder = 0;
				break;

			default:
				Debug.Log("<color=red>[Unit]</color>: Failed to find a valid effect to base sorting order. Defaulted to 0.");
				break;
		}

		//Need to specify WHEN this unit should update its timer- either at the END of its owners turn, or at the start of the NEXT owners turn
		if (BattleManager.Instance.ActiveHero == Owner)
		{
			Owner.TurnStartTrigger += state.UpdateKeywordVisual;
			state.UpdatePoint = KeywordState.KeywordUpdate.ParentTurnStart;
		}
		else
		{
			Owner.EnemyEntity.TurnStartTrigger += state.UpdateKeywordVisual;
			state.UpdatePoint = KeywordState.KeywordUpdate.EnemyTurnStart;
		}
	}

	private Sprite GetRowEffectSprite(Keyword keywordToShow) => keywordToShow switch
	{
		Keyword.Locked => Resources.Load<Sprite>("Row Effects/Locked"),
		_ => null,
	};
	#endregion

	#region Helper Functions
	public void AdjustUnitSpacing()
	{
		if (UnitsInRow.Count == 0)
			return;

		if (UnitsInRow.Count == 1)
		{
			UnitsInRow[0].SetTransformVariables(RowSpace.center);
			UnitsInRow[0].SetUnitTransform();

			return;
		}

		bool evenTotal = false;

		if (UnitsInRow.Count % 2 == 0)
			evenTotal = true;

		if (evenTotal)
		{
			float positionOffset = EvenUnitStartSpacing;

			int half = UnitsInRow.Count / 2;

			//All cards of this first half of the list
			for (int i = half - 1; i >= 0; i--)
			{
				UnitsInRow[i].SetTransformVariables(new Vector3(RowSpace.center.x - (positionOffset), 1, RowSpace.center.z));
				UnitsInRow[i].SetUnitTransform();

				positionOffset += UnitSpacing;
			}

			positionOffset = EvenUnitStartSpacing;

			//All cards on the right half of the list
			for (int i = half; i < UnitsInRow.Count; i++)
			{
				UnitsInRow[i].SetTransformVariables(new Vector3(RowSpace.center.x + positionOffset, 1, RowSpace.center.z));
				UnitsInRow[i].SetUnitTransform();

				positionOffset += UnitSpacing;
			}
		}
		else
		{
			int middleIndex = (int)Mathf.Ceil(UnitsInRow.Count / 2f);
			Unit middleUnit = UnitsInRow[middleIndex - 1];
			middleUnit.SetTransformVariables(new Vector3(RowSpace.center.x, 1, RowSpace.center.z));
			middleUnit.SetUnitTransform();

			float positionOffset = UnitSpacing;

			//All cards to the left of the center
			for (int i = middleIndex - 2; i >= 0; i--)
			{
				UnitsInRow[i].SetTransformVariables(new Vector3(RowSpace.center.x - (positionOffset), 1, RowSpace.center.z));
				UnitsInRow[i].SetUnitTransform();

				positionOffset += UnitSpacing;
			}

			positionOffset = UnitSpacing;

			//All cards to the right of the center
			for (int i = middleIndex; i < UnitsInRow.Count; i++)
			{
				UnitsInRow[i].SetTransformVariables(new Vector3(RowSpace.center.x + positionOffset, 1, RowSpace.center.z));
				UnitsInRow[i].SetUnitTransform();

				positionOffset += UnitSpacing;
			}
		}
	}

	public void SetParentEntity(Entity parentEntity)
	{
		parentEntity.TurnStartTrigger += StartOfTurn;
		parentEntity.TurnEndTrigger += EndOfTurn;

		Owner = parentEntity;
	}
	#endregion

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube(RowSpace.center, RowSpace.extents * 2);

		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(RowSpace.center, 0.25f);
	}
}
