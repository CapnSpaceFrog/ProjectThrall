using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleManager : MonoBehaviour
{
	public static BattleManager Instance;

	[Header("Camera & Mouse Variables")]
	public Camera BattleCam;
	[Range(10f, 15f)]
	public float MouseWorldYOffset;
	public GameObject Crosshair;
	public static LayerMask Touchables;
	private Moveable CurrentSelectedMoveable;
	private Vector3 MouseWorldPosition => BattleCam.ScreenToWorldPoint(InputHandler.MousePosition);
	private Vector3 MouseOffsetWorldPosition => new Vector3(MouseWorldPosition.x, MouseWorldYOffset, MouseWorldPosition.z);

	[Header("General Board Space Variables")]
	public Vector3 HoverCardPos;
	public Vector3 HoverCardScale;

	[Header("Entity Variables")]
	public Entity PlayerHero;
	public Commander EnemyHero;

	[Header("Battle Variables")]
	public int StartingDraw;
	public GameObject CardPrefab;
	public GameObject BackOfCardPrefab;
	public GameObject UnitPrefab;
	public GameObject UnitEffectPrefab;
	public GameObject RowEffectPrefab;
	[Tooltip("Turn time limit in seconds")]
	[Range(60, 120)]
	public int TurnTimeLimit;
	private float turnTimer;
	[HideInInspector]
	public bool ActiveHeroEndedTurn;
	public static Action StartOfTurn;
	public static Action EndOfTurn;
	public Entity ActiveHero { get; private set; }
	public Sprite[] CommanderPortraits;

	public Commanders currentCommander;

	private void Awake()
	{
		Instance = this;

		StartBattle(Commanders.Summoner);
	}

	#region Core Battle Functions
	public void StartBattle(Commanders opponent)
    {
		PlayerHero.InstantiateDeck(CardDatabase.Instance.DebugCardData);
		SelectEnemyHero(opponent);

		PlayerHero.EnemyEntity = EnemyHero;
		EnemyHero.EnemyEntity = PlayerHero;

		InputHandler.OnLeftMousePress += OnPlayerLeftMousePress;
		InputHandler.OnLeftMouseCancel += OnPlayerLeftMouseCancel;
		InputHandler.OnRightMousePress += OnPlayerRightMousePress;

		PlayerHero.ResetEntity();
		EnemyHero.ResetEntity();

		//PlayerHero.Deck.DrawCard(StartingDraw);

		ActiveHero = PlayerHero;

		StartCoroutine( TurnBattleCoroutine() );
	}

	void SelectEnemyHero(Commanders opponent)
	{
		switch (opponent)
		{
			case Commanders.EasternPrince:
				EnemyHero.PortraitSR.sprite = CommanderPortraits[0];
				EnemyHero.InstantiateDeck(CardDatabase.Instance.EasternPrinceCardData);
				break;

			case Commanders.Summoner:
				EnemyHero.PortraitSR.sprite = CommanderPortraits[1];
				EnemyHero.InstantiateDeck(CardDatabase.Instance.SummonerCardData);
				break;
		}
	}

	public void EndBattle()
	{
		StopAllCoroutines();

		InputHandler.OnLeftMousePress -= OnPlayerLeftMousePress;
		InputHandler.OnLeftMouseCancel -= OnPlayerLeftMouseCancel;
		InputHandler.OnRightMousePress -= OnPlayerRightMousePress;

		ActiveHeroEndedTurn = false;
	}
	#endregion

	#region Turn Helper Functions
	IEnumerator TurnBattleCoroutine()
	{
		ActiveHero.StartOfTurn();

		yield return new WaitUntil(() => (ActiveHeroEndedTurn == true || turnTimer > TurnTimeLimit));

		ActiveHero.EndOfTurn();
		SwitchActiveHero();

		StartCoroutine(TurnBattleCoroutine());
	}

	private void SwitchActiveHero()
	{
		ActiveHero = ActiveHero.EnemyEntity;

		Debug.Log($"<color=green>[BattleManager]</color>: {ActiveHero.gameObject.name} is now Active.");
		turnTimer = 0;
		ActiveHeroEndedTurn = false;
	}
	#endregion

	void Update()
	{
		turnTimer += Time.deltaTime;

		if (CurrentSelectedMoveable != null)
			CurrentSelectedMoveable.WhileSelected(MouseOffsetWorldPosition);
	}

	#region Input Functions
	void OnPlayerLeftMousePress()
	{
		if (!(ActiveHero == PlayerHero))
		{
			//Space for behavior for clicking when out of turn
			Debug.Log("<color=orange>[BattleManager]</color>: It is not your turn. Input discarded.");
			return;
		}

		LayerMask PlayerTouchable = LayerMask.GetMask("Friendly Unit", "Player Card", "Friendly Hero", "Enemy Hero", "Turn Button");

		RaycastFromMousePosition(PlayerTouchable, out Selectable objTouched);

		if (objTouched == null)
			return;

		switch (objTouched.Type)
		{
			case SelectionType.Card:
				CurrentSelectedMoveable = (Moveable)objTouched;
				CurrentSelectedMoveable.ReceivedPrimary();
				break;

			case SelectionType.Unit:
				Moveable updateable = (Moveable)objTouched;
				if (updateable.ReceivedPrimary())
					CurrentSelectedMoveable = updateable;
				break;

			case SelectionType.Hero:
				Debug.Log("Touched Entity");
				break;

			case SelectionType.Button:
				ActiveHeroEndedTurn = true;
				break;
		}
	}

	void OnPlayerLeftMouseCancel()
	{
		if (!(ActiveHero == PlayerHero))
		{
			//Space for behavior for clicking when out of turn
			Debug.Log("<color=orange>[BattleManager]</color>: It is not your turn. Input discarded.");
			return;
		}

		if (CurrentSelectedMoveable == null)
			return;

		CurrentSelectedMoveable.Released();
		CurrentSelectedMoveable = null;
	}

	void OnPlayerRightMousePress()
	{
		if (!(ActiveHero == PlayerHero))
		{
			//Space for behavior for clicking when out of turn
			Debug.Log("<color=orange>[BattleManager]</color>: It is not your turn. Input discarded.");
			return;
		}

		if (CurrentSelectedMoveable != null)
		{
			CurrentSelectedMoveable.ReceivedSecondary();
			CurrentSelectedMoveable = null;
		}
	}

	/// <summary>
	/// Given a TargetType, return a touchable based on the value of TargetType.
	/// </summary>
	/// <param name="validObjMask">The LayerMask of the Card currently being held.</param>
	/// <returns>A valid Touchable based on the validObjMask. Returns null if nothing was hit.</returns>
	public bool RaycastFromMousePosition(LayerMask validObjMask, out Selectable objTouched)
	{
		objTouched = null;

		if (validObjMask == (LayerMask)0)
			return false;

		Ray mouseToPlayboard = new Ray(MouseOffsetWorldPosition, Vector3.down);

		if (Physics.Raycast(mouseToPlayboard, out RaycastHit hitObj, MouseWorldYOffset, validObjMask))
		{
			objTouched = hitObj.transform.GetComponent<Selectable>();
			return true;
		}

		return false;
	}

	public static LayerMask GetLayerFromTarget(Target targetInfo)
	{
		Target whoToTarget = targetInfo & (Target.Either);

		Target whatToTarget = targetInfo & (Target.Row | Target.Single | Target.Hero);

		switch (whatToTarget)
		{
			case Target.Row:
				switch (whoToTarget)
				{
					case Target.Friendly:
						return LayerMask.GetMask("Friendly Row");

					case Target.Enemy:
						return LayerMask.GetMask("Enemy Row");

					case Target.Either:
						return LayerMask.GetMask("Friendly Row", "Enemy Row");
				}
				break;

			case Target.Single:
				switch (whoToTarget)
				{
					case Target.Friendly:
						return LayerMask.GetMask("Friendly Unit");

					case Target.Enemy:
						return LayerMask.GetMask("Enemy Unit");

					case Target.Either:
						return LayerMask.GetMask("Friendly Unit", "Enemy Unit");
				}
				break;

			case Target.Hero:
				switch (whoToTarget)
				{
					case Target.Friendly:
						return LayerMask.GetMask("Friendly Hero");

					case Target.Enemy:
						return LayerMask.GetMask("Enemy Hero");

					case Target.Either:
						return LayerMask.GetMask("Friendly Hero", "Enemy Hero");
				}
				break;

			case (Target.Hero | Target.Single):
				switch (whoToTarget)
				{
					case Target.Friendly:
						return LayerMask.GetMask("Friendly Hero", "Friendly Unit");

					case Target.Enemy:
						return LayerMask.GetMask("Enemy Hero", "Enemy Unit");

					case Target.Either:
						return LayerMask.GetMask("Friendly Hero", "Enemy Hero", "Friendly Unit", "Enemy Unit");
				}
				break;
		}

		Debug.Log("Didn't find layer in BattleManager.");
		return LayerMask.GetMask("Default");
	}
	#endregion

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.green;
		Gizmos.DrawWireSphere(HoverCardPos, 0.25f);

		if (Application.isPlaying)
		{
			Gizmos.color = Color.yellow;

			Gizmos.DrawRay(MouseOffsetWorldPosition, Vector3.down * MouseWorldYOffset);
			Gizmos.DrawWireSphere(MouseOffsetWorldPosition, 0.25f);
		}
	}
}