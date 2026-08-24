using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class MainMenuController : MonoBehaviour
{
    private const string ArenaSceneName = "M0";

    private Button localMultiplayerButton;
    private bool loadRequested;

    private void Start()
    {
        UIDocument document = GetComponent<UIDocument>();
        VisualElement root = document != null
            ? document.rootVisualElement
            : null;

        if (root == null)
        {
            Debug.LogError("Main menu requires a UIDocument.", this);
            enabled = false;
            return;
        }

        root.Q<Button>("ai-arena-button")?.SetEnabled(false);
        root.Q<Button>("single-player-button")?.SetEnabled(false);
        root.Q<Button>("online-multiplayer-button")?.SetEnabled(false);
        localMultiplayerButton =
            root.Q<Button>("local-multiplayer-button");

        if (localMultiplayerButton == null)
        {
            Debug.LogError(
                "MainMenuUI is missing the Local Multiplayer button.",
                this);
            enabled = false;
            return;
        }

        localMultiplayerButton.clicked += LoadLocalMultiplayer;
    }

    private void OnDisable()
    {
        if (localMultiplayerButton != null)
        {
            localMultiplayerButton.clicked -= LoadLocalMultiplayer;
        }
    }

    private void LoadLocalMultiplayer()
    {
        if (loadRequested)
        {
            return;
        }

        loadRequested = true;
        localMultiplayerButton.SetEnabled(false);
        SceneManager.LoadScene(ArenaSceneName, LoadSceneMode.Single);
    }
}
