using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class Commander : Entity
{
	[Header("AI Tunage")]
	[Range(0.5f, 1.65f)]
	public float DisplayTime;

	private bool Finished;

	public override void StartOfTurn()
	{
		base.StartOfTurn();

		//For each through the current cards in hand, find the ones you can play, then play them all, then remove the cards from the AI's hand
		if (Hand.CardsInHand.Count == 0)
		{
			FindTargetsForMinions();
			return;
		}

		if (CanCastSpell(Hand.CardsInHand[0].Data.ManaCost))
		{
			StartCoroutine(PlayCard(Hand.CardsInHand[0]));
			FindTargetsForMinions();
			return;
		}

		FindTargetsForMinions();
	}

	void FindTargetsForMinions()
	{
		foreach (Touchable ally in QueryForTargets(Target.Friendly | Target.AllRows))
		{
			Unit friendlyUnit = (Unit)ally.InstanceInfo;

			if (friendlyUnit.WorldState.hasAttacked == true)
				continue;

			//Check for any taunt minions first:
			foreach (Touchable taunt in EnemyEntity.QueryForTargets(Target.Enemy | Target.AllRows, Keyword.Defending))
			{
				Unit enemyTaunt = (Unit)taunt.InstanceInfo;

				if (!friendlyUnit.CheckValidTarget((Damageable)taunt))
					continue;

				friendlyUnit.AIAttack(enemyTaunt.WorldState.Instance);
				break;
			}

			if (friendlyUnit.WorldState.hasAttacked == true)
				continue;
			
			foreach (Touchable target in EnemyEntity.QueryForTargets(Target.Friendly | Target.AllRows))
			{
				Unit enemyUnit = (Unit)target.InstanceInfo;

				if (!friendlyUnit.CheckValidTarget((Damageable)target))
					continue;

				if (enemyUnit.WorldState.Stats.CurrentHealth == friendlyUnit.WorldState.Stats.CurrentAttack()
					|| friendlyUnit.WorldState.Stats.CurrentHealth >= enemyUnit.WorldState.Stats.CurrentAttack()
					|| enemyUnit.WorldState.Stats.CurrentAttack() > 5)
				{
					friendlyUnit.AIAttack(enemyUnit.WorldState.Instance);
					break;
				}
				else
				{
					friendlyUnit.AIAttack(EnemyEntity.EntityDamageable);
					break;
				}
			}
		}

		BattleManager.Instance.ActiveHeroEndedTurn = true;
	}

	public override void EndOfTurn()
	{
		base.EndOfTurn();

		Finished = false;
	}

	private IEnumerator PlayCard(Card cardToPlay)
	{
		StartCoroutine(DisplayCard(cardToPlay));

		yield return new WaitUntil(() => Finished == true);

		cardToPlay.Cast();
	}

	private IEnumerator DisplayCard(Card cardToDisplay)
	{
		GameObject displayCard = Instantiate(BattleManager.Instance.CardPrefab);

		displayCard.transform.GetChild(0).GetComponent<SpriteRenderer>().sprite = cardToDisplay.Data.CardArt;
		displayCard.transform.GetChild(1).GetComponent<TextMeshPro>().text = cardToDisplay.Data.ManaCost.ToString();
		displayCard.transform.GetChild(2).GetComponent<TextMeshPro>().text = cardToDisplay.Data.CardDescription;

		displayCard.transform.position = BattleManager.Instance.HoverCardPos;
		displayCard.transform.localScale = BattleManager.Instance.HoverCardScale;

		yield return new WaitForSeconds(DisplayTime);

		Destroy(displayCard);

		Finished = true;
	}
}
