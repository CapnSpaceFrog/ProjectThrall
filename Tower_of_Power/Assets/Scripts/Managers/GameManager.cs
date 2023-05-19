using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public BaseSpellData[] ActivePlayerDeck { get; private set; }

    /// <summary>
    /// This is the function you need to call Chris to update the current deck the Player is using.
    /// </summary>
    /// <param name="deckData">An array of spell data that will be set to the Active Player Deck.</param>
    public void SetActivePlayerDeck(BaseSpellData[] deckData) => ActivePlayerDeck = deckData;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
