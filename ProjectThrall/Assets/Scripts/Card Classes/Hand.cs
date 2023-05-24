using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SerializeField]
public struct HandValues
{
	public Bounds Bounds;
	public Quaternion Orientation;
}

public class Hand
{
	public List<Card> CardsInHand;

	public HandValues HandSpace;

	private static float yOffset = 0.1f;
	private static float PositionIncrement = 2.5f;
	private static float EvenStartingPositionIncrement = PositionIncrement / 2f;
	private static float RotationIncrement = 8f;

	private int maxHandSize = 7;

	public Hand(Bounds handBounds, Quaternion cardOrientation)
	{
		CardsInHand = new List<Card>();

		HandSpace.Bounds = handBounds;
		HandSpace.Orientation = cardOrientation;
	}

	#region Core Functions
	public bool AddNewCardToHand(Card cardToAdd)
	{
		if (CardsInHand.Count == maxHandSize)
			return false;

		CardsInHand.Insert(0, cardToAdd);

		cardToAdd.CreateCardWorldInstance(this);

		AdjustHandSpacing();

		return true;
	}

	public void RemoveCardFromHand(Card removeFromHand)
	{
		CardsInHand.Remove(removeFromHand);
		removeFromHand.RemoveCardFromWorld();
		AdjustHandSpacing();
	}

	public void AdjustHandSpacing()
	{
		if (CardsInHand.Count == 0)
			return;

		if (CardsInHand.Count == 1)
		{
			CardsInHand[0].SetCardOrientationVariables(HandSpace.Bounds.center, HandSpace.Orientation, Vector3.zero);
			CardsInHand[0].SetCardTransform();

			return;
		}

		bool evenTotal = false;

		if (CardsInHand.Count % 2 == 0)
			evenTotal = true;

		if (evenTotal)
		{
			float positionOffset = EvenStartingPositionIncrement;
			float rotationIncrement = RotationIncrement;
			int yOffsetIndex = 1;

			int half = CardsInHand.Count / 2;

			//All cards of this first half of the list
			for (int i = half - 1; i >= 0; i--)
			{
				CardsInHand[i].SetCardOrientationVariables(new Vector3(HandSpace.Bounds.center.x - positionOffset, 1 - (yOffset * yOffsetIndex), HandSpace.Bounds.center.z),
					HandSpace.Orientation * Quaternion.Euler(0, 0, rotationIncrement),
					Vector3.zero);
				CardsInHand[i].SetCardTransform();
				CardsInHand[i].CardSortingGroup.sortingOrder = i;

				positionOffset += PositionIncrement;
				rotationIncrement += RotationIncrement;
				yOffsetIndex++;
			}

			positionOffset = EvenStartingPositionIncrement;
			rotationIncrement = RotationIncrement;
			yOffsetIndex = 1;

			//All cards on the right half of the list
			for (int i = half; i < CardsInHand.Count; i++)
			{
				CardsInHand[i].SetCardOrientationVariables(new Vector3(HandSpace.Bounds.center.x + positionOffset, 1 + (yOffset * yOffsetIndex), HandSpace.Bounds.center.z),
					HandSpace.Orientation * Quaternion.Euler(0, 0, -rotationIncrement),
					Vector3.zero);
				CardsInHand[i].SetCardTransform();
				CardsInHand[i].CardSortingGroup.sortingOrder = i;

				positionOffset += PositionIncrement;
				rotationIncrement += RotationIncrement;
				yOffsetIndex++;
			}
		}
		else
		{
			int middleIndex = (int)Mathf.Ceil(CardsInHand.Count / 2f);
			Card middleCard = CardsInHand[middleIndex-1];
			middleCard.SetCardOrientationVariables(new Vector3(HandSpace.Bounds.center.x, 1, HandSpace.Bounds.center.z), HandSpace.Orientation, Vector3.zero);
			middleCard.SetCardTransform();
			middleCard.CardSortingGroup.sortingOrder = middleIndex;

			float positionOffset = PositionIncrement;
			float rotationIncrement = RotationIncrement;
			int yOffsetIndex = 1;

			//All cards to the left of the center
			for (int i = middleIndex - 2; i >= 0; i--)
			{
				CardsInHand[i].SetCardOrientationVariables(new Vector3(HandSpace.Bounds.center.x - (positionOffset), 1 - (yOffset * yOffsetIndex), HandSpace.Bounds.center.z),
					HandSpace.Orientation * Quaternion.Euler(0, 0, rotationIncrement),
					Vector3.zero);
				CardsInHand[i].SetCardTransform();
				CardsInHand[i].CardSortingGroup.sortingOrder = i;

				positionOffset += PositionIncrement;
				rotationIncrement += RotationIncrement;
				yOffsetIndex++;
			}

			positionOffset = PositionIncrement;
			rotationIncrement = RotationIncrement;
			yOffsetIndex = 1;

			//All cards to the right of the center
			for (int i = middleIndex; i < CardsInHand.Count; i++)
			{
				CardsInHand[i].SetCardOrientationVariables(new Vector3(HandSpace.Bounds.center.x + positionOffset, 1 + (yOffset * yOffsetIndex), HandSpace.Bounds.center.z),
					HandSpace.Orientation * Quaternion.Euler(0, 0, -rotationIncrement),
					Vector3.zero);
				CardsInHand[i].SetCardTransform();
				CardsInHand[i].CardSortingGroup.sortingOrder = i;

				positionOffset += PositionIncrement;
				rotationIncrement += RotationIncrement;
				yOffsetIndex++;
			}
		}
	}
	#endregion

	#region Combat Functions
	public void Burn(Card source, int amountToBurn)
	{
		//If theres one card in your hand, it's this card, so you won't burn anything.
		for (int i = 0; i < CardsInHand.Count; i++)
		{
			if (source == CardsInHand[i])
				CardsInHand.Remove(source);
		}

		for (int i = 0; i < amountToBurn; i++)
		{
			if (CardsInHand.Count == 0)
				return;

			int index = UnityEngine.Random.Range(0, CardsInHand.Count);

			RemoveCardFromHand(CardsInHand[index]);
		}
	}
	#endregion

	#region Debugging Functions
	public void PrintHand()
	{
		string s = "";
		foreach (Card c in CardsInHand)
		{
			s += c.Data.CardName + "; ";
		}

		Debug.Log($"<color=orange>[Hand]</color> Contains: " + s);
	}

	public void Reset()
	{
		List<Card> temp = CardsInHand;

		foreach (Card c in temp)
			c.RemoveCardFromWorld();

		CardsInHand.Clear();

		AdjustHandSpacing();
	}
	#endregion
}
