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
