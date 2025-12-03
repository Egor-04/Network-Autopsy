using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NetworkDiagnostic
{
    public class NetworkDiagnosticManager : MonoBehaviour
    {
        [System.Serializable]
        public class DomainCheck
        {
            public string domain;
            public string description;
            public bool isBlockedTest;
        }

        [Header("UI Elements")]
        [SerializeField] private TMP_Text outputText;
        [SerializeField] private ScrollRect scrollView;
        [SerializeField] private Button startButton;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private Button addDomainButton;
        [SerializeField] private TMP_InputField newDomainInput;
        [SerializeField] private Button saveReportButton;

        [Header("Domain List")]
        [SerializeField]
        private List<DomainCheck> domainsToCheck = new List<DomainCheck>
        {
            new DomainCheck { domain = "google.com", description = "Google", isBlockedTest = false },
            new DomainCheck { domain = "youtube.com", description = "YouTube", isBlockedTest = false },
            new DomainCheck { domain = "vk.com", description = "VK", isBlockedTest = true },
            new DomainCheck { domain = "telegram.org", description = "Telegram", isBlockedTest = true },
            new DomainCheck { domain = "github.com", description = "GitHub", isBlockedTest = false },
            new DomainCheck { domain = "rutracker.org", description = "RuTracker", isBlockedTest = true }
        };

        [Header("Protocol List")]
        [SerializeField]
        private List<string> protocolsToCheck = new List<string>
        {
            "HTTP (80)",
            "HTTPS (443)",
            "DNS (53)",
            "FTP (21)",
            "SSH (22)",
            "SMTP (25)",
            "POP3 (110)",
            "IMAP (143)",
            "RDP (3389)",
            "OpenVPN (1194)",
            "WireGuard (51820)",
            "Tor (9050)",
            "BitTorrent (6881)",
            "QUIC (443)",
            "WebSocket (80)"
        };

        private StringBuilder report = new StringBuilder();
        private bool isRunning = false;
        private Coroutine diagnosticCoroutine;

        void Start()
        {
            if (startButton != null)
                startButton.onClick.AddListener(StartDiagnostic);

            if (addDomainButton != null)
                addDomainButton.onClick.AddListener(AddNewDomain);

            if (saveReportButton != null)
                saveReportButton.onClick.AddListener(SaveCurrentReport);

            ClearOutput();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.D))
                StartDiagnostic();
        }

        public void StartDiagnostic()
        {
            if (isRunning) return;

            isRunning = true;
            ShowLoading(true);
            ClearOutput();

            if (diagnosticCoroutine != null)
                StopCoroutine(diagnosticCoroutine);

            diagnosticCoroutine = StartCoroutine(RunFullDiagnostic());
        }

        public void AddNewDomain()
        {
            if (string.IsNullOrEmpty(newDomainInput.text)) return;

            string domain = newDomainInput.text.Trim();
            domainsToCheck.Add(new DomainCheck
            {
                domain = domain,
                description = domain,
                isBlockedTest = false
            });

            newDomainInput.text = "";
            AppendOutput($"Добавлен домен: {domain}\n");
        }

        public void SaveCurrentReport()
        {
            if (report.Length == 0)
            {
                AppendOutput("Нет данных для сохранения\n");
                return;
            }

            SaveReport();
        }

        private IEnumerator RunFullDiagnostic()
        {
            report.Clear();
            AppendOutput("=== ДИАГНОСТИКА СЕТИ ===\n\n");

            // 1. Базовая информация
            yield return StartCoroutine(CheckBasicNetworkInfo());

            // 2. Скорость интернета
            yield return StartCoroutine(CheckNetworkSpeed());

            // 3. Проверка доменов
            yield return StartCoroutine(CheckDomains());

            // 4. Проверка протоколов
            yield return StartCoroutine(CheckProtocols());

            // 5. Детальная диагностика
            yield return StartCoroutine(DetailedDiagnostics());

            AppendOutput("\n=== ДИАГНОСТИКА ЗАВЕРШЕНА ===\n");

            SaveReport();

            ShowLoading(false);
            isRunning = false;
        }

        private IEnumerator CheckBasicNetworkInfo()
        {
            UpdateProgress(10, "Получение информации о сети");

            AppendOutput("1. ИНФОРМАЦИЯ О ПОДКЛЮЧЕНИИ:\n");
            AppendOutput("------------------------------\n");

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");
                
                AndroidJavaClass networkInfo = new AndroidJavaClass("com.UnknownGameStudio.NetworkAutopsy.NetworkInfo");
                string info = networkInfo.CallStatic<string>("getBasicInfo", context);
                AppendOutput(info + "\n");
            }
            catch (System.Exception e)
            {
                AppendOutput("[ERROR] " + e.Message + "\n");
            }
#else
            AppendOutput("Тип подключения: Wi-Fi\n");
            AppendOutput("Статус: Подключено\n");
            AppendOutput("IP адрес: 192.168.1.100\n");
            AppendOutput("Провайдер: Ростелеком (симуляция)\n");
            AppendOutput("Страна: Россия\n");
            AppendOutput("Город: Москва\n");
#endif

            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator CheckNetworkSpeed()
        {
            UpdateProgress(30, "Проверка скорости интернета");

            AppendOutput("\n2. ТЕСТ СКОРОСТИ ИНТЕРНЕТА:\n");
            AppendOutput("-----------------------------\n");

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidJavaClass speedTest = new AndroidJavaClass("com.UnknownGameStudio.NetworkAutopsy.SpeedTest");
                
                AppendOutput("Загрузка: ");
                float downloadSpeed = speedTest.CallStatic<float>("testDownload");
                AppendOutput(downloadSpeed.ToString("F1") + " Мбит/с\n");
                
                AppendOutput("Отдача: ");
                float uploadSpeed = speedTest.CallStatic<float>("testUpload");
                AppendOutput(uploadSpeed.ToString("F1") + " Мбит/с\n");
                
                AppendOutput("Пинг: ");
                int ping = speedTest.CallStatic<int>("testPing", "8.8.8.8");
                AppendOutput(ping + " мс\n");
                
                // Оценка
                if (downloadSpeed < 1) AppendOutput("ОЦЕНКА: Очень медленно\n");
                else if (downloadSpeed < 10) AppendOutput("ОЦЕНКА: Средняя скорость\n");
                else if (downloadSpeed < 50) AppendOutput("ОЦЕНКА: Хорошая скорость\n");
                else AppendOutput("ОЦЕНКА: Отличная скорость\n");
            }
            catch (System.Exception e)
            {
                AppendOutput("[ERROR] " + e.Message + "\n");
            }
#else
            AppendOutput("Загрузка: 47.3 Мбит/с (симуляция)\n");
            AppendOutput("Отдача: 12.8 Мбит/с (симуляция)\n");
            AppendOutput("Пинг: 24 мс (симуляция)\n");
            AppendOutput("ОЦЕНКА: Отличная скорость\n");
#endif

            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator CheckDomains()
        {
            UpdateProgress(50, "Проверка доступности сайтов");

            AppendOutput("\n3. ПРОВЕРКА ДОСТУПНОСТИ САЙТОВ:\n");
            AppendOutput("---------------------------------\n");

            int total = domainsToCheck.Count;
            int checkedCount = 0;
            int blockedCount = 0;

            foreach (var domainCheck in domainsToCheck)
            {
                checkedCount++;
                UpdateProgress(50 + (int)(30f * checkedCount / total),
                    "Проверка " + domainCheck.domain);

#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    AndroidJavaClass checker = new AndroidJavaClass("com.UnknownGameStudio.NetworkAutopsy.DomainChecker");
                    bool isAvailable = checker.CallStatic<bool>("checkDomain", domainCheck.domain);
                    bool isBlocked = domainCheck.isBlockedTest && !isAvailable;
                    
                    string status = isAvailable ? "[OK]" : "[BLOCKED]";
                    string description = isBlocked ? " (БЛОКИРОВКА!)" : "";
                    
                    AppendOutput(status + " " + domainCheck.description + 
                        " (" + domainCheck.domain + ")" + description + "\n");
                    
                    if (isBlocked) blockedCount++;
                }
                catch
                {
                    AppendOutput("[ERROR] " + domainCheck.description + " (ошибка проверки)\n");
                }
#else
                bool isBlocked = domainCheck.isBlockedTest &&
                    (domainCheck.domain.Contains("vk") ||
                     domainCheck.domain.Contains("telegram") ||
                     domainCheck.domain.Contains("rutracker"));

                string status = isBlocked ? "[BLOCKED]" : "[OK]";
                string description = isBlocked ? " (БЛОКИРОВКА РКН)" : "";

                AppendOutput(status + " " + domainCheck.description +
                    " (" + domainCheck.domain + ")" + description + "\n");

                if (isBlocked) blockedCount++;
#endif

                yield return new WaitForSeconds(0.1f);
            }

            AppendOutput("\nИТОГО: " + blockedCount + " из " + total + " сайтов заблокировано\n");
        }

        private IEnumerator CheckProtocols()
        {
            UpdateProgress(80, "Проверка протоколов");

            AppendOutput("\n4. ПРОВЕРКА ПРОТОКОЛОВ:\n");
            AppendOutput("-------------------------\n");

            int total = protocolsToCheck.Count;
            int checkedCount = 0;
            int blockedCount = 0;

            foreach (var protocol in protocolsToCheck)
            {
                checkedCount++;
                UpdateProgress(80 + (int)(15f * checkedCount / total),
                    "Проверка " + protocol);

#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    AndroidJavaClass protocolChecker = new AndroidJavaClass("com.UnknownGameStudio.NetworkAutopsy.ProtocolChecker");
                    bool isBlocked = protocolChecker.CallStatic<bool>("checkProtocol", protocol);
                    
                    string status = isBlocked ? "[BLOCKED]" : "[OPEN]";
                    AppendOutput(status + " " + protocol + "\n");
                    
                    if (isBlocked) blockedCount++;
                }
                catch
                {
                    AppendOutput("[ERROR] " + protocol + " (ошибка проверки)\n");
                }
#else
                bool isBlocked = protocol.Contains("Tor") ||
                                 protocol.Contains("BitTorrent") ||
                                 protocol.Contains("OpenVPN");

                string status = isBlocked ? "[BLOCKED]" : "[OPEN]";
                string reason = isBlocked ? " (блокировка провайдера)" : "";

                AppendOutput(status + " " + protocol + reason + "\n");

                if (isBlocked) blockedCount++;
#endif

                yield return new WaitForSeconds(0.1f);
            }

            AppendOutput("\nИТОГО: " + blockedCount + " из " + total + " протоколов заблокировано\n");
        }

        private IEnumerator DetailedDiagnostics()
        {
            UpdateProgress(95, "Детальная диагностика");

            AppendOutput("\n5. ДЕТАЛЬНАЯ ДИАГНОСТИКА:\n");
            AppendOutput("---------------------------\n");

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidJavaClass diagnostic = new AndroidJavaClass("com.UnknownGameStudio.NetworkAutopsy.NetworkDiagnostic");
                string details = diagnostic.CallStatic<string>("getDetailedInfo");
                AppendOutput(details + "\n");
            }
            catch (System.Exception e)
            {
                AppendOutput("[ERROR] " + e.Message + "\n");
            }
#else
            AppendOutput("VPN обнаружено: Нет\n");
            AppendOutput("Прокси обнаружено: Нет\n");
            AppendOutput("Пакетная потеря: 0.2%\n");
            AppendOutput("Джиттер: 5 мс\n");
            AppendOutput("Максимальный пинг: 87 мс\n");
            AppendOutput("DNS утечки: Нет\n");
            AppendOutput("WebRTC утечки: Нет\n");
            AppendOutput("IPv6 доступен: Да\n");
#endif

            yield return new WaitForSeconds(0.3f);
        }

        private void AppendOutput(string text)
        {
            report.Append(text);
            outputText.text = report.ToString();

            if (scrollView != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollView.verticalNormalizedPosition = 0f;
            }
        }

        private void ClearOutput()
        {
            report.Clear();
            outputText.text = "";
        }

        private void UpdateProgress(int progress, string message)
        {
            if (progressSlider != null)
                progressSlider.value = progress;

            if (progressText != null)
                progressText.text = progress + "% - " + message;
        }

        private void ShowLoading(bool show)
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(show);

            if (startButton != null)
                startButton.interactable = !show;

            if (addDomainButton != null)
                addDomainButton.interactable = !show;

            if (saveReportButton != null)
                saveReportButton.interactable = !show;
        }

        private void SaveReport()
        {
            try
            {
                string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string filename = "network_diagnostic_" + timestamp + ".txt";
                string path = Path.Combine(Application.persistentDataPath, filename);

                string fullReport = "ОТЧЕТ ДИАГНОСТИКИ СЕТИ\n" +
                    "==========================\n" +
                    "Время: " + System.DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + "\n" +
                    "Устройство: " + SystemInfo.deviceModel + "\n" +
                    "ОС: " + SystemInfo.operatingSystem + "\n" +
                    "\n" + report.ToString();

                File.WriteAllText(path, fullReport);
                AppendOutput("\nОтчет сохранен: " + filename + "\n");
            }
            catch (System.Exception e)
            {
                AppendOutput("\nОшибка сохранения: " + e.Message + "\n");
            }
        }
    }
}