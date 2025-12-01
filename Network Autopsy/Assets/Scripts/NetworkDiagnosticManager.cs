using UnityEngine;
using System.IO;
using TMPro;

namespace NetworkDiagnostic
{
    public class NetworkDiagnosticManager : MonoBehaviour
    {
        [System.Serializable]
        public class UISettings
        {
            [SerializeField] private bool useUI = true;
            [SerializeField] private TMP_Text resultText;
            [SerializeField] private TMP_Text statusText;
            [SerializeField] private TMP_Text timestampText;
            [SerializeField] private TMP_Text deviceInfoText;
            [SerializeField] private UnityEngine.UI.Button runButton;
            [SerializeField] private UnityEngine.UI.Button exportButton;
            [SerializeField] private UnityEngine.UI.Button clearButton;
            [SerializeField] private UnityEngine.UI.ScrollRect scrollView;
            [SerializeField] private GameObject loadingIndicator;

            public bool UseUI => useUI;
            public TMP_Text ResultText => resultText;
            public TMP_Text StatusText => statusText;
            public TMP_Text TimestampText => timestampText;
            public TMP_Text DeviceInfoText => deviceInfoText;
            public UnityEngine.UI.Button RunButton => runButton;
            public UnityEngine.UI.Button ExportButton => exportButton;
            public UnityEngine.UI.Button ClearButton => clearButton;
            public UnityEngine.UI.ScrollRect ScrollView => scrollView;
            public GameObject LoadingIndicator => loadingIndicator;
        }

        [System.Serializable]
        public class DiagnosticSettings
        {
            [SerializeField] private bool runOnStart = true;
            [SerializeField] private bool autoRefresh = false;
            [SerializeField] private float refreshInterval = 30f;
            [SerializeField] private bool saveToFile = true;
            [SerializeField] private bool showNotifications = true;

            public bool RunOnStart => runOnStart;
            public bool AutoRefresh => autoRefresh;
            public float RefreshInterval => refreshInterval;
            public bool SaveToFile => saveToFile;
            public bool ShowNotifications => showNotifications;
        }

        [System.Serializable]
        public class ColorSettings
        {
            [SerializeField] private Color successColor = Color.green;
            [SerializeField] private Color warningColor = Color.yellow;
            [SerializeField] private Color errorColor = Color.red;
            [SerializeField] private Color normalColor = Color.white;

            public Color SuccessColor => successColor;
            public Color WarningColor => warningColor;
            public Color ErrorColor => errorColor;
            public Color NormalColor => normalColor;
        }

        [Header("UI Configuration")]
        [SerializeField] private UISettings uiSettings = new UISettings();

        [Header("Diagnostic Configuration")]
        [SerializeField] private DiagnosticSettings diagnosticSettings = new DiagnosticSettings();

        [Header("Color Configuration")]
        [SerializeField] private ColorSettings colorSettings = new ColorSettings();

        private string lastReport = "";
        private float refreshTimer = 0f;
        private bool isRunning = false;

        #region Unity Lifecycle

        private void Start()
        {
            InitializeUI();

            if (diagnosticSettings.RunOnStart)
            {
                RunDiagnostic();
            }

            if (diagnosticSettings.AutoRefresh)
            {
                refreshTimer = diagnosticSettings.RefreshInterval;
            }
        }

        private void Update()
        {
            if (diagnosticSettings.AutoRefresh && !isRunning)
            {
                refreshTimer -= Time.deltaTime;
                if (refreshTimer <= 0f)
                {
                    RunDiagnostic();
                    refreshTimer = diagnosticSettings.RefreshInterval;
                }
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                RunDiagnostic();
            }
        }

        #endregion

        #region Public Methods

        public void RunDiagnostic()
        {
            if (isRunning) return;

            isRunning = true;
            ShowLoading(true);
            UpdateStatus("Running diagnostic...", colorSettings.WarningColor);

            Debug.Log("=== FULL NETWORK DIAGNOSTIC ===");

#if UNITY_ANDROID && !UNITY_EDITOR
            RunAndroidDiagnostic();
#else
            HandleResult("Diagnostic available only on Android devices");
#endif
        }

        public void ExportLastReport()
        {
            if (string.IsNullOrEmpty(lastReport))
            {
                ShowNotification("No report to export", colorSettings.WarningColor);
                return;
            }

            ExportReportToFile(lastReport);
        }

        public void ClearResults()
        {
            lastReport = "";
            UpdateResultText("");
            UpdateStatus("Results cleared", colorSettings.NormalColor);
        }

        #endregion

        #region Private Methods

        private void InitializeUI()
        {
            if (!uiSettings.UseUI) return;

            if (uiSettings.RunButton != null)
            {
                uiSettings.RunButton.onClick.AddListener(RunDiagnostic);
            }

            if (uiSettings.ExportButton != null)
            {
                uiSettings.ExportButton.onClick.AddListener(ExportLastReport);
            }

            if (uiSettings.ClearButton != null)
            {
                uiSettings.ClearButton.onClick.AddListener(ClearResults);
            }

            UpdateTimestamp();
            UpdateDeviceInfo();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void RunAndroidDiagnostic()
        {
            try
            {
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");
                
                AndroidJavaObject diagnostic = new AndroidJavaObject(
                    "com.UnknownGameStudio.NetworkAutopsy.NetworkDiagnostic",
                    context
                );
                
                string basic = diagnostic.Call<string>("checkBasicConnectivity");
                string internet = diagnostic.Call<string>("checkInternetAccess");
                string ips = diagnostic.Call<string>("getIPAddresses");
                string fullReport = diagnostic.Call<string>("runFullDiagnostic");
                
                HandleResult(fullReport);
            }
            catch (System.Exception e)
            {
                string errorMessage = $"ERROR: {e.Message}\nStackTrace: {e.StackTrace}";
                Debug.LogError(errorMessage);
                HandleResult(errorMessage);
            }
        }
#endif

        private void HandleResult(string result)
        {
            lastReport = result;

            if (diagnosticSettings.SaveToFile)
            {
                SaveReportToFile(result);
            }

            if (uiSettings.UseUI)
            {
                UpdateResultText(result);
                UpdateStatus("Diagnostic complete", colorSettings.SuccessColor);

                bool hasErrors = result.Contains("ERROR") || result.Contains("FAILED");
                UpdateStatus("Diagnostic complete" + (hasErrors ? " with errors" : ""),
                           hasErrors ? colorSettings.ErrorColor : colorSettings.SuccessColor);
            }

            if (diagnosticSettings.ShowNotifications)
            {
                ShowNotification("Diagnostic complete", colorSettings.SuccessColor);
            }

            ShowLoading(false);
            isRunning = false;
        }

        private void SaveReportToFile(string content)
        {
            try
            {
                string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string filename = $"network_report_{timestamp}.txt";
                string path = Path.Combine(Application.persistentDataPath, filename);

                File.WriteAllText(path, content);
                Debug.Log($"Report saved to: {path}");

                if (uiSettings.UseUI && diagnosticSettings.ShowNotifications)
                {
                    ShowNotification($"Report saved: {Path.GetFileName(path)}", colorSettings.SuccessColor);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not save report: {e.Message}");

                if (uiSettings.UseUI && diagnosticSettings.ShowNotifications)
                {
                    ShowNotification($"Save failed: {e.Message}", colorSettings.ErrorColor);
                }
            }
        }

        private void ExportReportToFile(string content)
        {
            try
            {
                string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string filename = $"network_export_{timestamp}.txt";
                string path = Path.Combine(Application.persistentDataPath, filename);

                File.WriteAllText(path, content);

                if (uiSettings.UseUI)
                {
                    ShowNotification($"Exported: {Path.GetFileName(path)}", colorSettings.SuccessColor);
                }

                // На Android можно открыть диалог "поделиться"
#if UNITY_ANDROID
                ShareFile(path);
#endif
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Export failed: {e.Message}");

                if (uiSettings.UseUI)
                {
                    ShowNotification($"Export failed: {e.Message}", colorSettings.ErrorColor);
                }
            }
        }

#if UNITY_ANDROID
        private void ShareFile(string filePath)
        {
            try
            {
                AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
                AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent");

                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");

                AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri");
                AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("parse", "file://" + filePath);
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), uri);
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), "Network Diagnostic Report");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), "Network diagnostic report attached");

                AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject currentActivity = unity.GetStatic<AndroidJavaObject>("currentActivity");

                AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser",
                    intent, "Share Network Report");
                currentActivity.Call("startActivity", chooser);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Share failed: {e.Message}");
            }
        }
#endif

        #endregion

        #region UI Update Methods

        private void UpdateResultText(string text)
        {
            if (uiSettings.UseUI && uiSettings.ResultText != null)
            {
                uiSettings.ResultText.text = text;

                // Автоскролл вниз
                if (uiSettings.ScrollView != null)
                {
                    Canvas.ForceUpdateCanvases();
                    uiSettings.ScrollView.verticalNormalizedPosition = 0f;
                }
            }
        }

        private void UpdateStatus(string message, Color color)
        {
            if (uiSettings.UseUI && uiSettings.StatusText != null)
            {
                uiSettings.StatusText.text = message;
                uiSettings.StatusText.color = color;
            }
        }

        private void UpdateTimestamp()
        {
            if (uiSettings.UseUI && uiSettings.TimestampText != null)
            {
                uiSettings.TimestampText.text = $"Last run: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            }
        }

        private void UpdateDeviceInfo()
        {
            if (uiSettings.UseUI && uiSettings.DeviceInfoText != null)
            {
                string deviceInfo = $"Device: {SystemInfo.deviceModel}\n" +
                                   $"OS: {SystemInfo.operatingSystem}\n" +
                                   $"Unity: {Application.unityVersion}";
                uiSettings.DeviceInfoText.text = deviceInfo;
            }
        }

        private void ShowLoading(bool show)
        {
            if (uiSettings.UseUI && uiSettings.LoadingIndicator != null)
            {
                uiSettings.LoadingIndicator.SetActive(show);
            }

            if (uiSettings.UseUI && uiSettings.RunButton != null)
            {
                uiSettings.RunButton.interactable = !show;
            }
        }

        private void ShowNotification(string message, Color color)
        {
            if (!diagnosticSettings.ShowNotifications) return;

            Debug.Log($"Notification: {message}");

            if (uiSettings.UseUI && uiSettings.StatusText != null)
            {
                uiSettings.StatusText.text = message;
                uiSettings.StatusText.color = color;
            }
        }

        #endregion
    }
}