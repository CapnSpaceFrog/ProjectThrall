using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

#region Card Enums
public enum School
{
	Creation = 1,
	Fire = 1 << 2,
	Water = 1 << 3,
	Chaos = 1 << 4,
	Balance = (Creation | Fire),
	Restoration = (Creation | Water),
	Conjuration = (Creation | Chaos),
	Elemental = (Fire | Water),
	Light = (Fire | Chaos),
	Dark = (Water | Chaos),
	Commander,
}

public enum Commanders
{
	EasternPrince,
	Summoner,
	DarkLord,
}

public enum CardTier
{
	Novice,
	Adept,
	Master,
	Legendary
}
#endregion

public class CardDatabase : MonoBehaviour
{
	public static CardDatabase Instance;

	[Header("Sorted Card Sets")]
	public BaseSpellData[] DebugCardData;

	[Header("Sorted Card Sets")]
	public BaseSpellData[] CreationCardData;
	public BaseSpellData[] FireCardData;
	public BaseSpellData[] WaterCardData;
	public BaseSpellData[] ChaosCardData;
	public BaseSpellData[] RestorationCardData;
	public BaseSpellData[] ConjurationCardData;
	public BaseSpellData[] ElementalCardData;
	public BaseSpellData[] LightCardData;
	public BaseSpellData[] DarkCardData;

	[Header("Generals Cards")]
	public BaseSpellData[] EasternPrinceCardData;
	public BaseSpellData[] SummonerCardData;
	public BaseSpellData[] DarkLordCardData;

	private void Awake()
	{
		Instance = this;
        ValidateCardData(CreationCardData);
		ValidateCardData(FireCardData);
		ValidateCardData(EasternPrinceCardData);
		ValidateCardData(SummonerCardData);
		ValidateCardData(DarkLordCardData);
	}

	public BaseSpellData[] GetCardData(Commanders commander) => commander switch
	{
		Commanders.EasternPrince => EasternPrinceCardData,
		Commanders.Summoner => SummonerCardData,
		Commanders.DarkLord => DarkLordCardData,
		_ => null,
	};

	#region Debugging Functions
	private void ValidateCardData(BaseSpellData[] cards)
	{
		bool isValid = false;

		if (cards.Length == 0 || cards == null)
		{
			Debug.Log($"<color=red>[Card Database]</color> Card Data Array is empty. Did you forget to assign Card Data on Card Database?");
			return;
		}

		foreach (BaseSpellData card in cards)
		{
			if (card == null)
			{
				Debug.Log($"<color=red>[Card Database]</color> An instance of Card Data is null. Did you forget to assign Card Data on Card Database?");
				break;
			}
		}

		if (isValid)
			PrintCardSet(cards);
	}

	private void PrintCardSet(BaseSpellData[] cards)
	{
		string s = "";
		foreach (BaseSpellData card in cards)
		{
			s += card.CardName + "; ";
		}

		Debug.Log($"<color=green>[Card Database]</color> {cards[0].CardSchool} Data: " + s);
	}
	#endregion
}