// ================== START SCREEN MANAGER ==================
// Handles config file selection, broker input, and simulation launch
// Attach to Canvas in StartScene
// Place this in Assets/Scripts/UI/StartScreenManager.cs

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.IO;
using UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class StartScreenManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private InputField brokerInputField;
    [SerializeField] private Text configStatusText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button selectConfigButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private InputField configPathInputField;


    [Header("Scene Management")]
    [SerializeField] private string simulationSceneName = "SimulationScene";

    private string selectedConfigPath = ""; // Default path for testing
    private SimulationConfig loadedConfig = null;

    // Static reference to pass data to simulation scene
    public static SimulationConfig LastLoadedConfig { get; private set; }
    public static string LastBrokerAddress { get; private set; }

    private void Start()
    {
        // Check for CLI arguments first. If found, skip the rest of Start()
        if (CheckHeadlessCLI()) return;

        // Setup UI listeners
        if (selectConfigButton != null)
            selectConfigButton.onClick.AddListener(OnSelectConfigClicked);

        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        // Set default broker
        if (brokerInputField != null)
            brokerInputField.text = "localhost:1883";

        UpdateStatusUI();

        RestoreLastConfig();

        Debug.Log("[START SCREEN] Initialized");
    }

    /// <summary>
    /// Open file picker to select config JSON
    /// </summary>
    private void OnSelectConfigClicked()
    {
        Debug.Log("[START SCREEN] Opening file picker...");

        #if UNITY_EDITOR
            // EDITOR: use OS file picker
            var lastUsedConfig = UserPreferences.Load().lastUsedConfigPath;
            var startDir = lastUsedConfig == null
                ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)
                : Path.GetDirectoryName(lastUsedConfig);

            var path = EditorUtility.OpenFilePanel("Select Robot Config JSON", startDir, "json");

            if (!string.IsNullOrEmpty(path))
            {
                // Also show it in the input field for convenience
                if (configPathInputField != null)
                    configPathInputField.text = path;

                LoadConfigFromPath(path);
            }
        #else
        // string path = System.IO.Path.Combine(
        //     Application.persistentDataPath,
        // "/Users/vedant/Documents/research/robot_config.json"
        // );

        // Debug.Log($"[START SCREEN] Using fixed config path in build: {path}");
        // LoadConfigFromPath(path);
        // BUILD: use path typed by the user
            string path = configPathInputField != null
                ? configPathInputField.text.Trim()
                : string.Empty;

                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning("[START SCREEN] No path entered in configPathInputField");
                    UpdateStatusUI("❌ Please enter full config file path", Color.red);
                    return;
                }

            Debug.Log($"[START SCREEN] Using user‑entered config path in build: {path}");
            LoadConfigFromPath(path);
        #endif
    }

    /// <summary>
    /// Load and validate config from file path
    /// </summary>
    private void LoadConfigFromPath(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"[START SCREEN] File not found: {path}");
            UpdateStatusUI("❌ File not found", Color.red);
            return;
        }

        loadedConfig = SimulationConfig.FromFile(path);

        if (loadedConfig == null)
        {
            Debug.LogError($"[START SCREEN] Failed to parse JSON");
            UpdateStatusUI("❌ Failed to parse JSON", Color.red);
            return;
        }

        // Validate config
        if (!loadedConfig.IsValid(out string errorMessage))
        {
            Debug.LogError($"[START SCREEN] Invalid config: {errorMessage}");
            UpdateStatusUI($"❌ {errorMessage}", Color.red);
            return;
        }

        selectedConfigPath = path;
        UserPreferences.SaveLastUsedConfigPath(path);
        string fileName = Path.GetFileName(path);
        string summary = $"✓ {fileName} ({loadedConfig.robots.Count} robots)";

        Debug.Log(loadedConfig.ToDebugString());
        UpdateStatusUI(summary, Color.green);
    }

    /// <summary>
    /// Start button clicked - validate and load simulation
    /// </summary>
    private void OnStartClicked()
    {
        if (loadedConfig == null)
        {
            Debug.LogWarning("[START SCREEN] No config loaded!");
            UpdateStatusUI("❌ Please select a config file first", Color.red);
            return;
        }

        string brokerAddress = brokerInputField != null ? brokerInputField.text.Trim() : "localhost:1883";

        if (string.IsNullOrEmpty(brokerAddress))
        {
            Debug.LogWarning("[START SCREEN] No broker address entered!");
            UpdateStatusUI("❌ Please enter broker address", Color.red);
            return;
        }

        // Store in static variables for SimulationScene to access
        LastLoadedConfig = loadedConfig;
        LastBrokerAddress = brokerAddress;

        Debug.Log($"[START SCREEN] Starting simulation with {loadedConfig.robots.Count} robots");
        Debug.Log($"[START SCREEN] Broker: {brokerAddress}");
        Debug.Log($"[START SCREEN] Loading scene: {simulationSceneName}");

        // Load simulation scene
        SceneManager.LoadScene(simulationSceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// Quit button clicked
    /// </summary>
    private void OnQuitClicked()
    {
        Debug.Log("[START SCREEN] Quit requested");

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    /// <summary>
    /// Update status display
    /// </summary>
    private void UpdateStatusUI(string message = "", Color? color = null)
    {
        if (configStatusText != null)
        {
            configStatusText.text = message;
            if (color.HasValue)
                configStatusText.color = color.Value;
        }

        // Enable/disable start button based on config load state
        if (startButton != null)
            startButton.interactable = (loadedConfig != null);
    }

    /// <summary>
    /// Get loaded config (for testing)
    /// </summary>
    public SimulationConfig GetLoadedConfig()
    {
        return loadedConfig;
    }

    /// <summary>
    /// Get broker address (for testing)
    /// </summary>
    public string GetBrokerAddress()
    {
        return brokerInputField != null ? brokerInputField.text : "";
    }

    /// <summary>
    /// Checks for CLI arguments and auto-launches if present, bypassing UI.
    /// </summary>
    private bool CheckHeadlessCLI()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        string configPath = null;
        string broker = "localhost:1883"; // Default
        bool hasConfigArg = false;

        // Parse arguments
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-config" && i + 1 < args.Length)
            {
                configPath = args[i + 1];
                hasConfigArg = true;
            }
            else if (args[i] == "-broker" && i + 1 < args.Length)
            {
                broker = args[i + 1];
            }
        }

        // If we found a config argument, or we are in headless mode
        if (hasConfigArg || Application.isBatchMode)
        {
            if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            {
                SimulationConfig config = SimulationConfig.FromFile(configPath);

                if (config != null && config.IsValid(out _))
                {
                    Debug.Log($"[CLI] Launching config: {configPath} with broker: {broker}");

                    // Manually set your static variables
                    LastLoadedConfig = config;
                    LastBrokerAddress = broker;

                    // Instantly load the main scene
                    SceneManager.LoadScene(simulationSceneName, LoadSceneMode.Single);
                    return true;
                }
            }

            // If we are strictly in headless mode and failed, kill the app to prevent a freeze
            if (Application.isBatchMode)
            {
                Debug.LogError("[CLI] Headless mode failed: Invalid or missing config file.");
                Application.Quit(1);
                return true;
            }
        }

        // Return false to let your existing UI code run normally
        return false;
    }

    private void RestoreLastConfig()
    {
        var saved = UserPreferences.Load().lastUsedConfigPath;
        Debug.Log($"[START SCREEN] saved prefs: {saved}");
        if (string.IsNullOrEmpty(saved))
            return;

        if (!File.Exists(saved))
        {
            Debug.Log($"[START SCREEN] Last used config no longer exists, skipping restore: {saved}");
            return;
        }

        if (configPathInputField != null)
            configPathInputField.text = saved;

        LoadConfigFromPath(saved);
        Debug.Log($"[START SCREEN] Restored last used config: {saved}");
    }
}
