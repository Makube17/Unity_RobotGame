using UnityEngine;
using UnityEngine.UI;

public enum GameState
{
    Title,
    Spawning,
    ReadyToScan,
    Scanning,
    CandidateSelect,
    Grasping,
    Result,
    FinalResult
}

public class UIFlowManager : MonoBehaviour
{
    [Header("画面パネル")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject howToPanel;
    [SerializeField] private GameObject gameUIPanel;

    [Header("ゲーム管理")]
    [SerializeField] private ScanManager scanManager;

    [Header("操作ボタン")]
    [SerializeField] private Button scanButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button graspButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button titleButton;

    [Header("ゲーム開始時に有効化したいオブジェクト")]
    [SerializeField] private GameObject[] gameObjectsToEnable;

    [Header("タイトル表示中に無効化したいオブジェクト")]
    [SerializeField] private GameObject[] gameObjectsToDisableAtTitle;

    public GameState CurrentState { get; private set; } = GameState.Title;

    private bool canExecuteGrasp;

    private void Start()
    {
        ShowTitle();
    }

    public void SetState(GameState nextState)
    {
        CurrentState = nextState;
        UpdateButtons();
        Debug.Log("GameState: " + CurrentState);
    }

    public void SetGraspAvailable(bool isAvailable)
    {
        canExecuteGrasp = isAvailable;
        UpdateButtons();
    }

    public void UpdateButtons()
    {
        LoadButtonReferencesFromScanManager();

        bool canScan = CurrentState == GameState.ReadyToScan;

        bool canReset =
            CurrentState == GameState.ReadyToScan ||
            CurrentState == GameState.CandidateSelect ||
            CurrentState == GameState.Result ||
            CurrentState == GameState.FinalResult;

        bool canGrasp =
            CurrentState == GameState.CandidateSelect &&
            canExecuteGrasp;

        bool canNext = CurrentState == GameState.Result;
        bool canRetry = CurrentState == GameState.FinalResult;
        bool canTitle = CurrentState == GameState.FinalResult;

        if (scanButton != null)
            scanButton.interactable = canScan;

        if (resetButton != null)
            resetButton.interactable = canReset;

        if (graspButton != null)
            graspButton.interactable = canGrasp;

        if (nextButton != null)
            nextButton.interactable = canNext;

        if (retryButton != null)
            retryButton.interactable = canRetry;

        if (titleButton != null)
            titleButton.interactable = canTitle;
    }

    private void LoadButtonReferencesFromScanManager()
    {
        if (scanManager == null)
        {
            return;
        }

        if (scanButton == null)
            scanButton = scanManager.ScanButtonReference;

        if (resetButton == null)
            resetButton = scanManager.ResetButtonReference;

        if (graspButton == null)
            graspButton = scanManager.GraspButtonReference;

        if (nextButton == null)
            nextButton = scanManager.NextButtonReference;

        if (retryButton == null)
            retryButton = scanManager.RetryButtonReference;

        if (titleButton == null)
            titleButton = scanManager.TitleButtonReference;
    }
    public void ShowTitle()
    {
        if (titlePanel != null)
            titlePanel.SetActive(true);

        if (howToPanel != null)
            howToPanel.SetActive(false);

        if (gameUIPanel != null)
            gameUIPanel.SetActive(false);

        SetObjectsActive(gameObjectsToEnable, false);
        SetObjectsActive(gameObjectsToDisableAtTitle, false);
        SetGraspAvailable(false);
        SetState(GameState.Title);
    }

    public void ShowHowTo()
    {
        if (titlePanel != null)
            titlePanel.SetActive(false);

        if (howToPanel != null)
            howToPanel.SetActive(true);

        if (gameUIPanel != null)
            gameUIPanel.SetActive(false);
    }

    public void StartGame()
    {
        if (titlePanel != null)
            titlePanel.SetActive(false);

        if (howToPanel != null)
            howToPanel.SetActive(false);

        if (gameUIPanel != null)
            gameUIPanel.SetActive(true);

        SetObjectsActive(gameObjectsToEnable, true);
        SetObjectsActive(gameObjectsToDisableAtTitle, true);
        SetGraspAvailable(false);

        if (scanManager != null)
        {
            scanManager.StartNewGameFromTitle();
        }
        else
        {
            Debug.LogWarning("UIFlowManager: ScanManager が設定されていません。");
        }
    }

    public void QuitGame()
    {
        Debug.Log("ゲームを終了します");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetObjectsActive(GameObject[] objects, bool isActive)
    {
        foreach (GameObject obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(isActive);
            }
        }
    }
}