using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

namespace NetworkDiagnostic
{
    public class NetworkDiagnosticManager : MonoBehaviour
    {
        [System.Serializable]
        public class UISettings
        {
            [SerializeField] private bool useUI = true;
            [SerializeField] private TMP_Text diagnosticText;
            [SerializeField] private TMP_Text summaryText;
            [SerializeField] private TMP_Text statusText;
            [SerializeField] private TMP_Text timestampText;
            [SerializeField] private UnityEngine.UI.Button diagnoseButton;
            [SerializeField] private UnityEngine.UI.Button saveButton;
            [SerializeField] private UnityEngine.UI.Button clearButton;
            [SerializeField] private UnityEngine.UI.ScrollRect scrollView;
            [SerializeField] private GameObject loadingPanel;

            public bool UseUI => useUI;
            public TMP_Text DiagnosticText => diagnosticText;
            public TMP_Text SummaryText => summaryText;
            public TMP_Text StatusText => statusText;
            public TMP_Text TimestampText => timestampText;
            public UnityEngine.UI.Button DiagnoseButton => diagnoseButton;
            public UnityEngine.UI.Button SaveButton => saveButton;
            public UnityEngine.UI.Button ClearButton => clearButton;
            public UnityEngine.UI.ScrollRect ScrollView => scrollView;
            public GameObject LoadingPanel => loadingPanel;
        }

        [System.Serializable]
        public class DiagnosticSettings
        {
            [SerializeField] private bool autoDiagnoseOnStart = true;
            [SerializeField] private bool saveReports = true;
            [SerializeField] private bool showNotifications = true;
            [SerializeField] private bool useAdvancedDiagnostics = true;
            [SerializeField] private float autoRefreshInterval = 0f;

            public bool AutoDiagnoseOnStart => autoDiagnoseOnStart;
            public bool SaveReports => saveReports;
            public bool ShowNotifications => showNotifications;
            public bool UseAdvancedDiagnostics => useAdvancedDiagnostics;
            public float AutoRefreshInterval => autoRefreshInterval;
        }

        [Header("UI Settings")]
        [SerializeField] private UISettings uiSettings = new UISettings();

        [Header("Diagnostic Settings")]
        [SerializeField] private DiagnosticSettings diagnosticSettings = new DiagnosticSettings();

        private string lastReport = "";
        private bool isDiagnosing = false;
        private float refreshTimer = 0f;

        #region Unity Lifecycle

        private void Start()
        {
            InitializeUI();

            if (diagnosticSettings.AutoDiagnoseOnStart)
            {
                StartDiagnostic();
            }

            if (diagnosticSettings.AutoRefreshInterval > 0)
            {
                refreshTimer = diagnosticSettings.AutoRefreshInterval;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.D))
            {
                StartDiagnostic();
            }

            if (diagnosticSettings.AutoRefreshInterval > 0 && !isDiagnosing)
            {
                refreshTimer -= Time.deltaTime;
                if (refreshTimer <= 0f)
                {
                    StartDiagnostic();
                    refreshTimer = diagnosticSettings.AutoRefreshInterval;
                }
            }
        }

        #endregion

        #region Public Methods

        public void StartDiagnostic()
        {
            if (isDiagnosing) return;

            isDiagnosing = true;
            ShowLoading(true);
            UpdateStatus("Starting network diagnostic...");
            UpdateTimestamp();

            Debug.Log("=== STARTING NETWORK DIAGNOSTIC ===");

#if UNITY_ANDROID && !UNITY_EDITOR
            RunAndroidDiagnostic();
#else
            ShowEditorDiagnostic();
#endif
        }

        public void SaveCurrentReport()
        {
            if (string.IsNullOrEmpty(lastReport))
            {
                ShowNotification("No data to save");
                return;
            }

            SaveReportToFile(lastReport);
        }

        public void ClearResults()
        {
            lastReport = "";
            UpdateDiagnosticText("");
            UpdateStatus("Results cleared");
            ShowNotification("Results cleared");
        }

        #endregion

        #region Private Methods

        private void InitializeUI()
        {
            if (!uiSettings.UseUI) return;

            if (uiSettings.DiagnoseButton != null)
            {
                uiSettings.DiagnoseButton.onClick.AddListener(StartDiagnostic);
            }

            if (uiSettings.SaveButton != null)
            {
                uiSettings.SaveButton.onClick.AddListener(SaveCurrentReport);
            }

            if (uiSettings.ClearButton != null)
            {
                uiSettings.ClearButton.onClick.AddListener(ClearResults);
            }

            UpdateTimestamp();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void RunAndroidDiagnostic()
        {
            try
            {
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");
                
                string result;
                
                // ВСЕГДА используем AdvancedNetworkDiagnostic
                AndroidJavaClass diagnosticClass = new AndroidJavaClass(
                    "com.UnknownGameStudio.NetworkAutopsy.AdvancedNetworkDiagnostic"
                );
                
                // Вызываем статический метод
                result = diagnosticClass.CallStatic<string>("quickDiagnose", context);
                
                ProcessDiagnosticResult(result);
            }
            catch (System.Exception e)
            {
                string errorMessage = FormatErrorMessage(e);
                ProcessDiagnosticResult(errorMessage);
            }
        }
#endif

        private string FormatErrorMessage(System.Exception e)
        {
            StringBuilder error = new StringBuilder();
            error.AppendLine("========================================");
            error.AppendLine("DIAGNOSTIC ERROR");
            error.AppendLine("========================================");
            error.AppendLine();
            error.AppendLine("Error type: " + e.GetType().Name);
            error.AppendLine("Message: " + e.Message);
            error.AppendLine();
            error.AppendLine("Recommendations:");
            error.AppendLine("- Check app permissions");
            error.AppendLine("- Restart the app");
            error.AppendLine("- Update the app");
            error.AppendLine("========================================");

            return error.ToString();
        }

        private void ShowEditorDiagnostic()
        {
            StringBuilder mockResult = new StringBuilder();
            mockResult.AppendLine("========================================");
            mockResult.AppendLine("NETWORK DIAGNOSTIC (EDITOR)");
            mockResult.AppendLine("========================================");
            mockResult.AppendLine();
            mockResult.AppendLine("1. BASIC INFORMATION:");
            mockResult.AppendLine("----------------------------------------");
            mockResult.AppendLine("[STATUS] CONNECTED (simulation)");
            mockResult.AppendLine("[TYPE] Wi-Fi");
            mockResult.AppendLine("[SIGNAL] Good (simulation)");
            mockResult.AppendLine();

            mockResult.AppendLine("2. CONNECTION DETAILS:");
            mockResult.AppendLine("----------------------------------------");
            mockResult.AppendLine("IPv4: 192.168.1.100 (interface: wlan0)");
            mockResult.AppendLine("IPv6: fe80::abcd:1234 (interface: wlan0)");
            mockResult.AppendLine("DNS servers:");
            mockResult.AppendLine("  - 8.8.8.8");
            mockResult.AppendLine("  - 1.1.1.1");
            mockResult.AppendLine();

            mockResult.AppendLine("3. DETECTED ISSUES:");
            mockResult.AppendLine("----------------------------------------");
            mockResult.AppendLine("[WARNING] VPN detected (simulation)");
            mockResult.AppendLine("[WARNING] High ping: 250 ms");
            mockResult.AppendLine("[SUCCESS] No critical issues");
            mockResult.AppendLine();

            mockResult.AppendLine("4. RECOMMENDATIONS:");
            mockResult.AppendLine("----------------------------------------");
            mockResult.AppendLine("- Check VPN settings");
            mockResult.AppendLine("- Try different DNS");
            mockResult.AppendLine("- Restart router");
            mockResult.AppendLine();

            mockResult.AppendLine("5. AVAILABILITY TESTS:");
            mockResult.AppendLine("----------------------------------------");
            mockResult.AppendLine("[OK] Google: working");
            mockResult.AppendLine("[OK] YouTube: working");
            mockResult.AppendLine("[OK] VK: working");
            mockResult.AppendLine("[OK] Yandex: working");
            mockResult.AppendLine("[OK] GitHub: working");
            mockResult.AppendLine();
            mockResult.AppendLine("========================================");
            mockResult.AppendLine("DIAGNOSTIC COMPLETED");
            mockResult.AppendLine("========================================");

            ProcessDiagnosticResult(mockResult.ToString());
        }

        private void ProcessDiagnosticResult(string result)
        {
            lastReport = result;

            DisplayResults(result);

            if (diagnosticSettings.SaveReports)
            {
                SaveReportToFile(result);
            }

            UpdateStatus("Diagnostic completed");
            UpdateSummary(ExtractSummary(result));

            ShowLoading(false);
            isDiagnosing = false;

            if (diagnosticSettings.ShowNotifications)
            {
                ShowNotification("Diagnostic completed");
            }
        }

        private void DisplayResults(string result)
        {
            if (!uiSettings.UseUI) return;

            UpdateDiagnosticText(result);

            if (uiSettings.ScrollView != null)
            {
                Canvas.ForceUpdateCanvases();
                uiSettings.ScrollView.verticalNormalizedPosition = 0f;
            }
        }

        private string ExtractSummary(string report)
        {
            // Анализируем английский отчет
            if (report.Contains("[ERROR]") || report.Contains("ERROR"))
            {
                return "Critical errors detected";
            }
            else if (report.Contains("[WARNING]") || report.Contains("WARNING"))
            {
                return "Warnings detected";
            }
            else if (report.Contains("[GOOD]") || report.Contains("SUCCESS"))
            {
                return "Network working normally";
            }
            else if (report.Contains("BLOCKED"))
            {
                return "Blocking detected";
            }

            return "Diagnostic completed";
        }

        private void SaveReportToFile(string report)
        {
            try
            {
                string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string filename = $"network_diagnostic_{timestamp}.txt";
                string path = Path.Combine(Application.persistentDataPath, filename);

                StringBuilder fullReport = new StringBuilder();
                fullReport.AppendLine("NETWORK DIAGNOSTIC REPORT");
                fullReport.AppendLine("==========================");
                fullReport.AppendLine("Time: " + System.DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
                fullReport.AppendLine("Device: " + SystemInfo.deviceModel);
                fullReport.AppendLine("OS: " + SystemInfo.operatingSystem);
                fullReport.AppendLine("Unity version: " + Application.unityVersion);
                fullReport.AppendLine();
                fullReport.AppendLine(report);

                File.WriteAllText(path, fullReport.ToString());

                Debug.Log("Report saved: " + path);

                if (diagnosticSettings.ShowNotifications)
                {
                    ShowNotification("Report saved: " + filename);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Error saving report: " + e.Message);

                if (diagnosticSettings.ShowNotifications)
                {
                    ShowNotification("Save error: " + e.Message);
                }
            }
        }

        #endregion

        #region UI Update Methods

        private void UpdateDiagnosticText(string text)
        {
            if (uiSettings.UseUI && uiSettings.DiagnosticText != null)
            {
                uiSettings.DiagnosticText.text = text;
            }
        }

        private void UpdateSummary(string text)
        {
            if (uiSettings.UseUI && uiSettings.SummaryText != null)
            {
                uiSettings.SummaryText.text = text;
            }
        }

        private void UpdateStatus(string text)
        {
            if (uiSettings.UseUI && uiSettings.StatusText != null)
            {
                uiSettings.StatusText.text = text;
            }
        }

        private void UpdateTimestamp()
        {
            if (uiSettings.UseUI && uiSettings.TimestampText != null)
            {
                uiSettings.TimestampText.text = "Updated: " +
                    System.DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            }
        }

        private void ShowLoading(bool show)
        {
            if (!uiSettings.UseUI) return;

            if (uiSettings.LoadingPanel != null)
            {
                uiSettings.LoadingPanel.SetActive(show);
            }

            if (uiSettings.DiagnoseButton != null)
            {
                uiSettings.DiagnoseButton.interactable = !show;
            }
        }

        private void ShowNotification(string message)
        {
            if (!diagnosticSettings.ShowNotifications) return;

            Debug.Log("Notification: " + message);

            if (uiSettings.UseUI && uiSettings.StatusText != null)
            {
                uiSettings.StatusText.text = message;
            }
        }

        #endregion
    }
}