using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class EndGameManager : MonoBehaviour
{
    [SerializeField]
    public GameObject finalSplash;

    [SerializeField]
    TextMeshProUGUI rankingText,
        rankingTextShadow,
        amountOfKillsText;

    [SerializeField]
    GameObject defeatedByContainer,
        characterModelContainer;

    // Data to be added from front and back

    [SerializeField]
    TextMeshProUGUI defeaterPlayerName,
        defeaterCharacterName;

    [SerializeField]
    Image defeaterImage;

    private const int WINNER_POS = 1;
    private const int SECOND_PLACE_POS = 2;
    private const string ZONE_ID = "9999";
    CustomCharacter player;
    GameObject modelClone;

    void OnEnable()
    {
        ShowRankingDisplay();
        ShowMatchInfo();
    }

    public void SetDeathSplashCharacter()
    {
        player = Utils.GetCharacter(SocketConnectionManager.Instance.playerId);
        CoMCharacter character = KillFeedManager.instance.charactersScriptableObjects.Single(
            characterSO => characterSO.name.Contains(player.CharacterModel.name)
        );
        if (character)
        {
            GameObject characterModel = character.UIModel;
            modelClone = Instantiate(characterModel, characterModelContainer.transform);
        }
    }

    void ShowRankingDisplay()
    {
        var ranking = GetRanking();
        rankingText.text += " # " + ranking.ToString();
        rankingTextShadow.text += " # " + ranking.ToString();
    }

    private int GetRanking()
    {
        bool isWinner = SocketConnectionManager.Instance.PlayerIsWinner(
            SocketConnectionManager.Instance.playerId
        );

        // FIXME This is a temporal for the cases where the winner dies simultaneously
        // FIXME with other/s player/s
        if (isWinner)
        {
            return WINNER_POS;
        }
        if (Utils.GetAlivePlayers().Count() == 0)
        {
            return SECOND_PLACE_POS;
        }
        return Utils.GetAlivePlayers().Count() + 1;
    }

    void ShowMatchInfo()
    {
        // Kill count
        var killCount = GetKillCount();
        amountOfKillsText.text = killCount.ToString();

        // Defeated By
        if (
            player
            && SocketConnectionManager.Instance.PlayerIsWinner(
                SocketConnectionManager.Instance.playerId
            )
        )
        {
            defeatedByContainer.SetActive(false);
        }
        else
        {
            //defeaterPlayerName.text = GetDefeaterPlayerName();
            // Defeated By Image
            defeaterImage.sprite = GetDefeaterSprite();
            // Defeated By Name
            //defeaterCharacterName.text = GetDefeaterCharacterName();
        }
    }

    private ulong GetKillCount()
    {
        var playerId = SocketConnectionManager.Instance.playerId;
        var gamePlayer = Utils.GetGamePlayer(playerId);
        return gamePlayer.KillCount;
    }

    private string GetDefeaterPlayerName()
    {
        // TODO: get Defeater player name
        return "-";
    }

    private Sprite GetDefeaterSprite()
    {
        if (KillFeedManager.instance.myKillerId.ToString() == ZONE_ID)
        {
            return KillFeedManager.instance.zoneIcon;
        }
        else
        {
            CoMCharacter killerCharacter =
                KillFeedManager.instance.charactersScriptableObjects.Single(
                    characterSO =>
                        characterSO.name.Contains(
                            Utils
                                .GetPlayer(KillFeedManager.instance.myKillerId)
                                .GetComponent<CustomCharacter>()
                                .CharacterModel.name
                        )
                );
            return killerCharacter.UIIcon;
        }
    }

    private string GetDefeaterCharacterName()
    {
        // TODO: get defeater character name
        return "-";
    }

    public void ShowCharacterAnimation()
    {
        if (player)
        {
            if (
                SocketConnectionManager.Instance.PlayerIsWinner(
                    SocketConnectionManager.Instance.playerId
                )
            )
            {
                modelClone.GetComponentInChildren<Animator>().SetBool("Victory", true);
            }
            else
            {
                modelClone.GetComponentInChildren<Animator>().SetBool("Defeat", true);
            }
        }
    }
}
