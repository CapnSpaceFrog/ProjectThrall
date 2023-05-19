using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Deck
{
	public Queue<Card> CardsInDeck;
	public BaseSpellData[] dataForDeck;
	public Entity Entity;

	public Deck(BaseSpellData[] cardData, Entity entity)
	{
		Entity = entity;
		CardsInDeck = new Queue<Card>();
		dataForDeck = cardData;
	}

	private void PopulateDeck()
	{
		CardsInDeck.Clear();

		foreach (BaseSpellData data in dataForDeck)
		{
			Card card = new Card(data, Entity);
			CardsInDeck.Enqueue(card);
		}

		CardsInDeck = Shuffle(CardsInDeck);
		PrintDeck();
	}

	public void DrawCard(int amount)
	{
		for (int i = 1; i <= amount; i++)
		{
			if (CardsInDeck.Count == 0)
			{
				Entity.Fatigue();
				continue;
			}

			Card drawnCard = CardsInDeck.Peek();

			if (!Entity.Hand.AddNewCardToHand(drawnCard))
			{
				BurnCard(1);
				continue;
			}
			else
				CardsInDeck.Dequeue();
		}
	}

	public void BurnCard(int amount) => CardsInDeck.Dequeue();

	#region Shuffle Functions
	/// <summary>
	/// Given a Queue, shuffle the order of the queue randomly.
	/// </summary>
	public static Queue<T> Shuffle<T>(Queue<T> queueToShuffle)
	{
		List<T> newItemList = new List<T>();

		foreach (T type in queueToShuffle)
		{
			newItemList.Add(type);
		}

		queueToShuffle.Clear();

		for (int i = 0; i < newItemList.Count; i++)
		{
			int index = Random.Range(0, newItemList.Count);

			T itemToMove = newItemList[i];
			newItemList.Remove(newItemList[i]);
			newItemList.Insert(index, itemToMove);
		}

		foreach (T type in newItemList)
		{
			queueToShuffle.Enqueue(type);
		}

		return queueToShuffle;
	}

	/// <summary>
	/// Given a list, shuffle the order of the list randomly.
	/// </summary>
	public static List<T> Shuffle<T>(List<T> listToShuffle)
	{
		List<T> newItemList = new List<T>();

		foreach (T type in listToShuffle)
		{
			newItemList.Add(type);
		}

		listToShuffle.Clear();

		for (int i = 0; i < newItemList.Count; i++)
		{
			int index = Random.Range(0, newItemList.Count);

			T itemToMove = newItemList[i];
			newItemList.Remove(newItemList[i]);
			newItemList.Insert(index, itemToMove);
		}

		return newItemList;
	}

	/// <summary>
	/// Shuffle a specific card into the deck.
	/// </summary>
	/// <param name="cardToShuffle"> Card to shuffle into the deck. </param>
	public void Shuffle(Card cardToShuffle)
	{

	}
	#endregion

	#region Debugging
	private void PrintDeck()
	{
		string s = "";
		foreach (Card c in CardsInDeck)
		{
			s += c.Data.CardName + "; ";
		}

		Debug.Log($"<color=green>[Deck]</color> Contains: " + s);
	}

	public void Reset() => PopulateDeck();
	#endregion
}
