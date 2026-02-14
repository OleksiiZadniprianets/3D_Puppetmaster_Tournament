using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameStateController : MonoBehaviour
{
    public enum GameState
    {
        ShowingMatch,
        MatchResult,
        TournamentFinished
    }

    [Header("Core State")]
    public GameState currentState = GameState.ShowingMatch;

    [Header("Tournament Participants (8)")]
    public List<TournamentParticipant> participants = new List<TournamentParticipant>();

    [Header("Main Buttons")]
    public Button proceedButton;

    [Header("Top Texts")]
    public TMP_Text stateText;
    public TMP_Text logText;
    public TMP_Text missionText;

    [Header("Name Texts")]
    public TMP_Text leftNameText;
    public TMP_Text rightNameText;

    [Header("Stats Text Above Avatars")]
    public TMP_Text leftStatsText;
    public TMP_Text rightStatsText;

    [Header("Battle Panel UI")]
    public GameObject battlePanel;
    public Image leftAvatarImage;
    public Image rightAvatarImage;
    public TMP_Text vsText;
    public TMP_Text winnerText;

    [Header("HP Bars")]
    public Image leftHPFill;
    public Image rightHPFill;

    [Header("Spectator UI")]
    public TMP_Text actionsText;

    [Header("HP Animation")]
    public float hpAnimDuration = 0.25f;
    public float hitDelay = 0.35f;

    [Header("Participants List UI (Right Column)")]
    public Transform participantsListParent;
    public GameObject participantEntryPrefab;

    [Header("Random Stats Settings")]
    public bool randomizeStatsOnStart = true;
    public Vector2Int hpRange = new Vector2Int(80, 160);
    public Vector2Int dmgRange = new Vector2Int(8, 25);

    [Header("Battle Settings")]
    public bool randomFirstHit = true;
    public bool resetWinnerHPBetweenMatches = true;

    [Header("Mission Settings")]
    public bool enableMissionTarget = true;

    [Header("Final Effects")]
    public Image finalOverlay;
    public AudioSource victoryAudio;
    public AudioClip victoryClip;

    [Header("Mission FX")]
    public Color missionSuccessColor = new Color(1f, 0.84f, 0f, 0.6f); // Gold
    public Color missionFailColor = new Color(0.5f, 0f, 0f, 0.7f);     // Dark red
    public float missionOverlayDuration = 1.2f;


    [Header("Spectator Control")]
    public int hpStep = 20;
    public int dmgStep = 7;
    public int maxHPAllowed = 150;
    public int maxDMGAllowed = 50;
    public int minHPAllowed = 1;
    public int minDMGAllowed = 1;

    [Header("Spectator Buttons")]
    public Button addHPButton;
    public Button subHPButton;
    public Button addDMGButton;
    public Button subDMGButton;

    [Header("Buff Flash FX")]
    public Color positiveFlashColor = new Color(0.2f, 1f, 0.2f, 0.7f);
    public Color negativeFlashColor = new Color(1f, 0.2f, 0.2f, 0.7f);
    public float flashDuration = 0.2f;

    [Header("Restart")]
    public Button restartButton;

    private readonly Dictionary<string, ParticipantEntryUI> _entryByName = new Dictionary<string, ParticipantEntryUI>();
    private readonly HashSet<string> _eliminated = new HashSet<string>();

    private List<TournamentParticipant> _currentPool = new List<TournamentParticipant>();
    private List<TournamentParticipant> _nextPool = new List<TournamentParticipant>();

    private int _roundIndex = 1;
    private int _matchIndex = 0;

    private TournamentParticipant _left;
    private TournamentParticipant _right;

    private string _missionTargetName = "";

    private bool _battleRunning = false;
    private Coroutine _leftBarRoutine;
    private Coroutine _rightBarRoutine;

    private TournamentParticipant _selectedParticipant;

    private int _actionsRemaining = 0;

    private void Start()
    {

        if (finalOverlay != null)
        {
            finalOverlay.color = new Color(1f, 0.84f, 0f, 0f);
            finalOverlay.gameObject.SetActive(false);
        }

        if (proceedButton != null)
            proceedButton.onClick.AddListener(OnProceedClicked);
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartTournament);


        if (randomizeStatsOnStart)
        {
            foreach (var p in participants)
            {
                p.maxHP = Random.Range(hpRange.x, hpRange.y + 1);
                p.damage = Random.Range(dmgRange.x, dmgRange.y + 1);
                p.currentHP = p.maxHP;
            }
        }
        else
        {
            foreach (var p in participants)
            {
                if (p.maxHP <= 0) p.maxHP = 100;
                if (p.damage <= 0) p.damage = 10;
                p.currentHP = p.maxHP;
            }
        }

        _currentPool = new List<TournamentParticipant>(participants);
        ShuffleParticipants(_currentPool);
        if (_currentPool.Count > 0)
        {
            int randomIndex = Random.Range(0, _currentPool.Count);
            _missionTargetName = _currentPool[randomIndex].name;

            if (missionText != null)
                missionText.text = $"Mission: Help {_missionTargetName} win the tournament";
        }


        if (enableMissionTarget && _currentPool.Count > 0)
            _missionTargetName = _currentPool[Random.Range(0, _currentPool.Count)].name;
        _missionFailed = false;
        _missionResolved = false;

        foreach (var p in participants)
        {
            p.baseHP = p.maxHP;
            p.baseDamage = p.damage;
        }


        BuildParticipantsListUI();
        StartRound(1);
    }

    private void OnProceedClicked()
    {
        if (_battleRunning) return;

        StartCoroutine(PressEffect(proceedButton));

        if (currentState == GameState.ShowingMatch)
        {
            StartCoroutine(ResolveCurrentMatchCoroutine());
            return;
        }

        if (currentState == GameState.MatchResult)
        {
            GoNextMatchOrRound();
            return;
        }
    }
    private IEnumerator HitFlash(Image bar)
    {
        if (bar == null) yield break;

        Color original = bar.color;
        bar.color = Color.white;

        yield return new WaitForSeconds(0.1f);

        bar.color = original;
    }


    private void StartRound(int round)
    {
        _roundIndex = round;
        _matchIndex = 0;
        _nextPool.Clear();

        currentState = GameState.ShowingMatch;

        if (stateText != null)
            stateText.text = $"Round {_roundIndex}";

        if (winnerText != null)
            winnerText.text = "";

        ShowMatch();
        UpdateUI();
    }

    private void ShowMatch()
    {
        if (_currentPool.Count <= 1)
        {
            FinishTournament(_currentPool.Count == 1 ? _currentPool[0] : null);
            return;
        }

        int i = _matchIndex * 2;
        _left = _currentPool[i];
        _right = _currentPool[i + 1];

        // Reset to base stats each match
        _left.maxHP = _left.baseHP;
        _right.maxHP = _right.baseHP;

        _left.damage = _left.baseDamage;
        _right.damage = _right.baseDamage;

        _left.currentHP = _left.maxHP;
        _right.currentHP = _right.maxHP;


        _selectedParticipant = _left;

        if (battlePanel != null) battlePanel.SetActive(true);

        if (leftAvatarImage != null)
        {
            leftAvatarImage.sprite = _left.avatar;
            leftAvatarImage.preserveAspect = true;
            leftAvatarImage.color = Color.white;
            leftAvatarImage.transform.localScale = Vector3.one;
        }

        if (rightAvatarImage != null)
        {
            rightAvatarImage.sprite = _right.avatar;
            rightAvatarImage.preserveAspect = true;
            rightAvatarImage.color = Color.white;
            rightAvatarImage.transform.localScale = Vector3.one;
        }

        if (leftNameText != null)
            leftNameText.text = _left.name;

        if (rightNameText != null)
            rightNameText.text = _right.name;

        if (vsText != null) vsText.text = "VS";
        if (winnerText != null) winnerText.text = "";

        SetHPBarsFull();

        UpdateStatsUIFull();

        _actionsRemaining = Random.Range(1, 4);
        UpdateActionsText();

        EnableSpectatorButtons();
    }

    private IEnumerator ResolveCurrentMatchCoroutine()
    {
        _battleRunning = true;
        if (proceedButton != null) proceedButton.interactable = false;

        int leftHP = _left.currentHP;
        int rightHP = _right.currentHP;

        int leftDMG = Mathf.Max(1, _left.damage);
        int rightDMG = Mathf.Max(1, _right.damage);

        bool leftTurn = randomFirstHit ? (Random.value > 0.5f) : true;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Battle log:");

        int safety = 0;

        while (leftHP > 0 && rightHP > 0 && safety < 200)
        {
            safety++;

            if (leftTurn)
            {
                rightHP -= leftDMG;
                StartCoroutine(PunchScale(rightAvatarImage.transform));
                sb.AppendLine($"- {_left.name} hits {_right.name} for {leftDMG}. ({Mathf.Max(0, rightHP)} HP left)");
            }
            else
            {
                leftHP -= rightDMG;
                StartCoroutine(PunchScale(leftAvatarImage.transform));
                sb.AppendLine($"- {_right.name} hits {_left.name} for {rightDMG}. ({Mathf.Max(0, leftHP)} HP left)");
            }

            UpdateHPBarsAnimated(leftHP, rightHP);

            if (leftStatsText != null)
                leftStatsText.text = $"HP: {Mathf.Max(0, leftHP)}\nDMG: {leftDMG}";
            if (rightStatsText != null)
                rightStatsText.text = $"HP: {Mathf.Max(0, rightHP)}\nDMG: {rightDMG}";

            if (logText != null)
                logText.text = sb.ToString();

            yield return new WaitForSeconds(hitDelay);

            leftTurn = !leftTurn;
        }

        TournamentParticipant winner;
        TournamentParticipant loser;

        if (leftHP <= 0 && rightHP <= 0)
        {
            winner = Random.value > 0.5f ? _left : _right;
            loser = (winner == _left) ? _right : _left;
        }
        else if (rightHP <= 0)
        {
            winner = _left;
            loser = _right;
        }
        else
        {
            winner = _right;
            loser = _left;
        }

        _nextPool.Add(winner);
        _eliminated.Add(loser.name);
        if (loser.name == _missionTargetName)
        {
            if (missionText != null)
                missionText.text = $"Mission Failed: {_missionTargetName} was eliminated";
        }
        if (enableMissionTarget && !_missionResolved && !string.IsNullOrEmpty(_missionTargetName))
        {
            if (loser.name == _missionTargetName)
                _missionFailed = true; // Тільки фіксуємо, НЕ пишемо текст зараз
        }



        _left.currentHP = Mathf.Max(0, leftHP);
        _right.currentHP = Mathf.Max(0, rightHP);

        SetEliminatedUI(loser.name, true);
        SetEliminatedUI(winner.name, false);

        if (winnerText != null)
            winnerText.text = $"Winner: {winner.name}";
        if (winner != null && winner.name == _missionTargetName)
        {
            if (missionText != null)
                missionText.text = $"Mission Complete! {_missionTargetName} won!";
        }
        else
        {
            if (missionText != null && !missionText.text.Contains("Failed"))
                missionText.text = $"Mission Failed: {_missionTargetName} did not win";
        }


        sb.AppendLine();
        sb.AppendLine($"Winner: {winner.name}");
        sb.AppendLine("Press Proceed to continue.");

        if (logText != null)
            logText.text = sb.ToString();

        currentState = (_currentPool.Count == 1 && _nextPool.Count == 0)
            ? GameState.TournamentFinished
            : GameState.MatchResult;

        _battleRunning = false;
        if (proceedButton != null) proceedButton.interactable = true;

        // 🔴 Mission fail instantly if target eliminated
        if (enableMissionTarget && loser.name == _missionTargetName)
        {
            if (missionText != null)
                missionText.text = $"MISSION FAILED: {_missionTargetName} eliminated";

            if (finalOverlay != null)
            {
                finalOverlay.color = new Color(0.6f, 0f, 0f, 0f);
                StartCoroutine(PlayCustomOverlay(new Color(0.6f, 0f, 0f, 0.6f)));
            }
        }
        StartCoroutine(HitFlash(rightHPFill));
        StartCoroutine(HitFlash(leftHPFill));


        UpdateUI();
    }
    private IEnumerator PunchScale(Transform target)
    {
        Vector3 original = target.localScale;
        Vector3 punch = original * 1.15f;

        float t = 0f;
        float duration = 0.1f;

        while (t < duration)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.Lerp(original, punch, t / duration);
            yield return null;
        }

        t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.Lerp(punch, original, t / duration);
            yield return null;
        }

        target.localScale = original;
    }

    private IEnumerator PlayCustomOverlay(Color targetColor)
    {
        if (finalOverlay == null) yield break;

        finalOverlay.gameObject.SetActive(true);

        Color start = finalOverlay.color;
        start.a = 0f;
        finalOverlay.color = start;

        float time = 0f;
        float duration = 0.8f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            finalOverlay.color = Color.Lerp(start, targetColor, t);
            yield return null;
        }

        finalOverlay.color = targetColor;
    }

    private void GoNextMatchOrRound()
    {
        _matchIndex++;

        int totalMatchesInRound = _currentPool.Count / 2;
        if (_matchIndex < totalMatchesInRound)
        {
            currentState = GameState.ShowingMatch;
            ShowMatch();
            UpdateUI();
            return;
        }

        _currentPool = new List<TournamentParticipant>(_nextPool);

        if (_currentPool.Count == 1)
        {
            FinishTournament(_currentPool[0]);
            return;
        }

        StartRound(_roundIndex + 1);
    }

    private void FinishTournament(TournamentParticipant winner)
    {
        currentState = GameState.TournamentFinished;

        if (battlePanel != null) battlePanel.SetActive(true);

        if (vsText != null) vsText.text = "";
        if (winnerText != null) winnerText.text = winner != null ? $"WINNER: {winner.name}" : "WINNER: -";

        if (stateText != null) stateText.text = "Tournament Complete";

        MarkActiveMatchInList("", "");
        UpdateUI();
        if (missionText != null && enableMissionTarget && !_missionResolved && !string.IsNullOrEmpty(_missionTargetName))
        {
            _missionResolved = true;

            if (!_missionFailed && winner != null && winner.name == _missionTargetName)
                missionText.text = $"Mission Complete: {_missionTargetName} won the tournament!";
            else
                missionText.text = $"Mission Failed: {_missionTargetName} did not win the tournament.";
        }

        if (finalOverlay != null)
            StartCoroutine(PlayFinalEffects());
        // Підняти переможця поверх overlay
        Image winnerAvatar = (winner == _left) ? leftAvatarImage : rightAvatarImage;

        if (winnerAvatar != null)
            winnerAvatar.transform.SetAsLastSibling();


        if (victoryAudio != null)
        {
            if (victoryClip != null) victoryAudio.PlayOneShot(victoryClip);
            else victoryAudio.Play();
        }
        if (enableMissionTarget && winner != null)
        {
            if (winner.name == _missionTargetName)
            {
                if (missionText != null)
                    missionText.text = $"MISSION COMPLETE: {_missionTargetName} WON!";

                StartCoroutine(PlayMissionOverlay(missionSuccessColor));
            }
            else
            {
                if (missionText != null)
                    missionText.text = $"MISSION FAILED: {_missionTargetName} did not win.";

                StartCoroutine(PlayMissionOverlay(missionFailColor));
            }
        }

    }

    public void SelectLeftParticipant()
    {
        if (currentState != GameState.ShowingMatch || _battleRunning) return;

        _selectedParticipant = _left;

        leftAvatarImage.transform.localScale = Vector3.one * 1.15f;
        rightAvatarImage.transform.localScale = Vector3.one;

        leftAvatarImage.color = Color.white;
        rightAvatarImage.color = new Color(0.8f, 0.8f, 0.8f);

        Debug.Log("Selected LEFT");
    }

    public void SelectRightParticipant()
    {
        if (currentState != GameState.ShowingMatch || _battleRunning) return;

        _selectedParticipant = _right;

        rightAvatarImage.transform.localScale = Vector3.one * 1.15f;
        leftAvatarImage.transform.localScale = Vector3.one;

        rightAvatarImage.color = Color.white;
        leftAvatarImage.color = new Color(0.8f, 0.8f, 0.8f);

        Debug.Log("Selected RIGHT");
    }

    public void SpectatorAddHP() => ApplySelectedHP(+hpStep);
    public void SpectatorSubHP() => ApplySelectedHP(-hpStep);
    public void SpectatorAddDMG() => ApplySelectedDMG(+dmgStep);
    public void SpectatorSubDMG() => ApplySelectedDMG(-dmgStep);

    private void ApplySelectedHP(int delta)
    {
        if (!CanSpectatorEdit()) return;

        _selectedParticipant.maxHP = Mathf.Clamp(
            _selectedParticipant.maxHP + delta,
            minHPAllowed,
            maxHPAllowed
        );

        UpdateStatsUIFull();

        Image avatar = (_selectedParticipant == _left) ? leftAvatarImage : rightAvatarImage;

        StartCoroutine(FlashAvatar(avatar, delta > 0 ? positiveFlashColor : negativeFlashColor));

        ConsumeAction();
    }

    private void ApplySelectedDMG(int delta)
    {
        if (!CanSpectatorEdit()) return;

        _selectedParticipant.damage = Mathf.Clamp(
            _selectedParticipant.damage + delta,
            minDMGAllowed,
            maxDMGAllowed
        );

        UpdateStatsUIFull();

        Image avatar = (_selectedParticipant == _left) ? leftAvatarImage : rightAvatarImage;

        StartCoroutine(FlashAvatar(avatar, delta > 0 ? positiveFlashColor : negativeFlashColor));

        ConsumeAction();
    }

    private bool CanSpectatorEdit()
    {
        if (currentState != GameState.ShowingMatch) return false;
        if (_battleRunning) return false;
        if (_selectedParticipant == null) return false;
        if (_left == null || _right == null) return false;
        if (_actionsRemaining <= 0) return false;
        return true;
    }

    private void UpdateStatsUIFull()
    {
        if (_left != null && leftStatsText != null)
            leftStatsText.text = $"HP: {_left.maxHP}\nDMG: {_left.damage}";

        if (_right != null && rightStatsText != null)
            rightStatsText.text = $"HP: {_right.maxHP}\nDMG: {_right.damage}";

        if (logText != null && currentState == GameState.ShowingMatch && !_battleRunning)
        {
            logText.text =
                $"{_left.name}  HP:{_left.maxHP}  DMG:{_left.damage}\n" +
                $"{_right.name} HP:{_right.maxHP} DMG:{_right.damage}\n\n" +
                "Select a fighter by clicking their avatar,\nthen use HP/DMG buttons, then press Proceed.";
        }
    }

    private void UpdateUI()
    {
        if (missionText != null && enableMissionTarget && !string.IsNullOrEmpty(_missionTargetName))
        {
            if (_missionResolved) return; // Якщо місія вже показана — не чіпаємо

            missionText.text = $"Mission: Help {_missionTargetName} reach the final";
        }
        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
        {
            Debug.Log("Restarting...");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
    private void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }


    private void SetHPBarsFull()
    {
        if (leftHPFill != null) leftHPFill.fillAmount = 1f;
        if (rightHPFill != null) rightHPFill.fillAmount = 1f;
    }

    private void UpdateHPBarsAnimated(int leftHP, int rightHP)
    {
        if (_left == null || _right == null) return;

        float leftTarget = Mathf.Clamp01((float)Mathf.Max(0, leftHP) / Mathf.Max(1, _left.maxHP));
        float rightTarget = Mathf.Clamp01((float)Mathf.Max(0, rightHP) / Mathf.Max(1, _right.maxHP));

        if (leftHPFill != null)
        {
            if (_leftBarRoutine != null) StopCoroutine(_leftBarRoutine);
            _leftBarRoutine = StartCoroutine(AnimateFill(leftHPFill, leftTarget));
        }

        if (rightHPFill != null)
        {
            if (_rightBarRoutine != null) StopCoroutine(_rightBarRoutine);
            _rightBarRoutine = StartCoroutine(AnimateFill(rightHPFill, rightTarget));
        }
    }

    private IEnumerator AnimateFill(Image bar, float target)
    {
        float start = bar.fillAmount;
        float time = 0f;

        while (time < hpAnimDuration)
        {
            time += Time.deltaTime;
            float t = time / hpAnimDuration;
            bar.fillAmount = Mathf.Lerp(start, target, t);
            yield return null;
        }

        bar.fillAmount = target;
    }

    private IEnumerator PlayFinalEffects()
    {
        if (finalOverlay == null) yield break;

        finalOverlay.gameObject.SetActive(true);

        Color start = finalOverlay.color;
        start.a = 0f;
        finalOverlay.color = start;

        Color target = new Color(1f, 0.84f, 0f, 0.6f);

        float time = 0f;
        float duration = 1.2f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            finalOverlay.color = Color.Lerp(start, target, t);
            yield return null;
        }

        finalOverlay.color = target;
    }

    private void BuildParticipantsListUI()
    {
        _entryByName.Clear();

        if (participantsListParent == null || participantEntryPrefab == null) return;

        foreach (Transform child in participantsListParent)
            Destroy(child.gameObject);

        foreach (var p in participants)
        {
            GameObject go = Instantiate(participantEntryPrefab, participantsListParent);

            ParticipantEntryUI ui = go.GetComponent<ParticipantEntryUI>();
            if (ui != null)
            {
                ui.Setup(p.name, p.avatar);
                _entryByName[p.name] = ui;

                if (enableMissionTarget && p.name == _missionTargetName)
                    ui.SetHighlighted(true);
            }
        }
    }

    private void SetEliminatedUI(string name, bool eliminated)
    {
        if (_entryByName.TryGetValue(name, out var ui))
            ui.SetEliminated(eliminated);
    }

    private void MarkActiveMatchInList(string leftName, string rightName)
    {
        foreach (var kv in _entryByName)
        {
            bool isLeft = kv.Key == leftName;
            bool isRight = kv.Key == rightName;
            kv.Value.SetActiveMatchSide(isLeft, isRight);
        }
    }

    private void ShuffleParticipants(List<TournamentParticipant> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);
            var temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    private void ConsumeAction()
    {
        _actionsRemaining--;
        UpdateActionsText();

        if (_actionsRemaining <= 0)
            DisableSpectatorButtons();
    }

    private void DisableSpectatorButtons()
    {
        if (addHPButton != null) addHPButton.interactable = false;
        if (subHPButton != null) subHPButton.interactable = false;
        if (addDMGButton != null) addDMGButton.interactable = false;
        if (subDMGButton != null) subDMGButton.interactable = false;
    }

    private void EnableSpectatorButtons()
    {
        if (addHPButton != null) addHPButton.interactable = true;
        if (subHPButton != null) subHPButton.interactable = true;
        if (addDMGButton != null) addDMGButton.interactable = true;
        if (subDMGButton != null) subDMGButton.interactable = true;
    }

    private void UpdateActionsText()
    {
        if (actionsText == null) return;

        actionsText.text = $"Actions left: {_actionsRemaining}";
        actionsText.color = (_actionsRemaining <= 0) ? Color.red : Color.white;
    }

    private IEnumerator FlashAvatar(Image avatar, Color flashColor)
    {
        if (avatar == null) yield break;

        Color originalColor = avatar.color;

        avatar.color = flashColor;
        yield return new WaitForSeconds(flashDuration);

        avatar.color = originalColor;
    }
    private bool _missionFailed = false;   // Rocky вже вилетів
    private bool _missionResolved = false; // Місія вже показана (в кінці)

    private IEnumerator PlayMissionOverlay(Color targetColor)
    {
        if (finalOverlay == null) yield break;

        finalOverlay.gameObject.SetActive(true);

        Color start = finalOverlay.color;
        start.a = 0f;
        finalOverlay.color = start;

        float time = 0f;

        while (time < missionOverlayDuration)
        {
            time += Time.deltaTime;
            float t = time / missionOverlayDuration;
            finalOverlay.color = Color.Lerp(start, targetColor, t);
            yield return null;
        }

        finalOverlay.color = targetColor;
    }
    private IEnumerator PressEffect(Button btn)
    {
        if (btn == null) yield break;

        Transform t = btn.transform;
        Vector3 original = t.localScale;

        t.localScale = original * 0.9f;
        yield return new WaitForSeconds(0.1f);
        t.localScale = original;
    }
    private void RestartTournament()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }

}
