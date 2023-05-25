using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TargetingHandler
{
   public static bool FindTargets(Target targetInfo, Entity Owner, out List<Selectable> targets)
   {
		targets = new List<Selectable>();

      return false;
   }

	public static bool FindTargets(Target targetInfo, Entity Owner, Keyword searchKeyword, out List<Selectable> targets)
	{
		targets = new List<Selectable>();

		return false;
	}

	private static Selectable GetChosenTarget(Target targetInfo)
	{
		if (BattleManager.Instance.RaycastFromMousePosition(BattleManager.GetLayerFromTarget(targetInfo), out Selectable touchedEntity))
			return touchedEntity;
		else return null;
	}

	private static bool CheckRequiredState(SummonAbility ability)
	{
		return false;
	}

	public static bool CheckValidTarget(Unit.State source, Damageable target)
	{
		if (target == null)
			return false;

		//if (HasKeyword(Keyword.Flying) || HasKeyword(Keyword.Confused))
		//	return true;

		if (target.Type == SelectionType.Hero)
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

			switch (source.CurrentRange)
			{
				case UnitRange.Melee:
					//If our unit is of Melee range, is our target within the melee row?
					if (source.Owner.MeleeRow.HasUnit(u))
					{
						//Target is within the melee row, does the row have any units with defending?
						if (u.HasKeyword(Keyword.Defending))
							return true;
						else if (source.Owner.MeleeRow.HasUnitWithKeyword(Keyword.Defending))
							return false;
						else return true;
					}
					else return false;

				case UnitRange.Ranged:
					if (source.Owner.MeleeRow.HasUnit(u) ||
						source.Owner.RangedRow.HasUnit(u))
					{
						if (u.HasKeyword(Keyword.Defending))
							return true;
						else if (source.Owner.MeleeRow.HasUnitWithKeyword(Keyword.Defending) ||
							source.Owner.RangedRow.HasUnitWithKeyword(Keyword.Defending))
							return false;
						else return true;
					}
					else return false;

				case UnitRange.Siege:
					if (source.Owner.MeleeRow.HasUnit(u) ||
						source.Owner.RangedRow.HasUnit(u) ||
						source.Owner.SiegeRow.HasUnit(u))
					{
						if (u.HasKeyword(Keyword.Defending))
							return true;
						else if (source.Owner.MeleeRow.HasUnitWithKeyword(Keyword.Defending) ||
							source.Owner.RangedRow.HasUnitWithKeyword(Keyword.Defending) ||
							source.Owner.SiegeRow.HasUnitWithKeyword(Keyword.Defending))
							return false;
						else return true;
					}
					else return false;
			}
		}

		Debug.Log($"<color=red[Unit]</color>: Fallthrough on CheckValidTarget. Could not verify {target.gameObject.name} target passed.");
		return false;
	}

	#region Debug Print Functions
	public static void PrintTargets(List<Selectable> targets)
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
	#endregion
}
