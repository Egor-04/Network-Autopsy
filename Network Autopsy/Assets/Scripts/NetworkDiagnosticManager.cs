using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

namespace NetworkDiagnostic
{
    public class NetworkDiagnosticManager : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text outputText;
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Button saveButton;
        [SerializeField] private TMP_InputField domainInput;
        [SerializeField] private Button addDomainButton;
        [SerializeField] private ScrollRect scrollView;

        private StringBuilder report = new StringBuilder();
        private bool isRunning = false;
        private List<string> customDomains = new List<string>();

        private string[] russianSites = { "vk.com", "rutracker.org", "yandex.ru", "mail.ru", "rambler.ru", "avito.ru", "ok.ru" };

        // Для отслеживания активных задач
        private List<Task> activeTasks = new List<Task>();

        void Start()
        {
            if (startButton != null)
                startButton.onClick.AddListener(StartDiagnostic);

            if (saveButton != null)
                saveButton.onClick.AddListener(SaveReport);

            if (addDomainButton != null)
                addDomainButton.onClick.AddListener(AddCustomDomain);

            customDomains.Add("vk.com");
            customDomains.Add("rutracker.org");
            customDomains.Add("github.com");
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
            UpdateProgress(0, "Подготовка...");

            StartCoroutine(RunDiagnostic());
        }

        public void AddCustomDomain()
        {
            if (string.IsNullOrWhiteSpace(domainInput.text))
                return;

            string domain = domainInput.text.Trim().ToLower();

            if (!domain.Contains("."))
            {
                AddLine($"Ошибка: '{domain}' не является доменом");
                return;
            }

            domain = RemoveProtocolAndWWW(domain);

            if (!customDomains.Contains(domain))
            {
                customDomains.Add(domain);
                AddLine($"Добавлен домен: {domain}");
                domainInput.text = "";
            }
            else
            {
                AddLine($"Домен {domain} уже в списке");
            }
        }

        private string RemoveProtocolAndWWW(string domain)
        {
            if (domain.StartsWith("http://"))
                domain = domain.Substring(7);
            else if (domain.StartsWith("https://"))
                domain = domain.Substring(8);

            if (domain.StartsWith("www."))
                domain = domain.Substring(4);

            return domain;
        }

        public void SaveReport()
        {
            if (report.Length == 0)
            {
                AddLine("Нет данных для сохранения");
                return;
            }

            try
            {
                string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string filename = $"network_diagnostic_{timestamp}.txt";
                string path = Path.Combine(Application.persistentDataPath, filename);

                string fullReport = "ОТЧЕТ ДИАГНОСТИКИ СЕТИ\n" +
                    "—————————————————————————————————\n" +
                    "Время: " + System.DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + "\n" +
                    "Устройство: " + SystemInfo.deviceModel + "\n" +
                    "ОС: " + SystemInfo.operatingSystem + "\n" +
                    "Unity: " + Application.unityVersion + "\n" +
                    "\n" + report.ToString();

                File.WriteAllText(path, fullReport, Encoding.UTF8);
                AddLine($"\nОтчет сохранен: {filename}");
                AddLine($"Путь: {path}");

                Debug.Log($"Report saved to: {path}");
            }
            catch (System.Exception e)
            {
                AddLine($"Ошибка сохранения: {e.Message}");
            }
        }

        private IEnumerator RunDiagnostic()
        {
            report.Clear();
            AddLine("=== ДИАГНОСТИКА СЕТИ ===");
            AddLine("");

            yield return null;

#if UNITY_ANDROID && !UNITY_EDITOR
            yield return RunAndroidDiagnostic();
#else
            yield return SimulateDiagnostic();
#endif

            UpdateProgress(100, "Завершено");
            ShowLoading(false);
            isRunning = false;
        }

        private IEnumerator RunAndroidDiagnostic()
        {
            UpdateProgress(10, "Инициализация Android...");
            yield return null;

            string javaResult = "";

            AndroidJavaObject context = null;
            try
            {
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                context = activity.Call<AndroidJavaObject>("getApplicationContext");
            }
            catch (System.Exception e)
            {
                AddLine($"Ошибка Android: {e.Message}");
                yield break;
            }

            UpdateProgress(30, "Загрузка Java модуля...");
            yield return null;

            yield return StartCoroutine(CallJavaCode(context, (result) =>
            {
                javaResult = result;
            }));

            UpdateProgress(70, "Обработка результатов...");
            yield return null;

            javaResult = FormatJavaResult(javaResult);

            if (!string.IsNullOrEmpty(javaResult))
            {
                AddLine(javaResult);
            }
            else
            {
                AddLine("Java-модуль не вернул данные");
            }

            if (customDomains.Count > 0)
            {
                UpdateProgress(80, "Проверка дополнительных доменов...");
                yield return null;

                AddLine("\n—— ДОПОЛНИТЕЛЬНЫЕ ДОМЕНЫ ——");

                // Используем неблокирующую проверку
                yield return StartCoroutine(CheckDomainsNonBlocking(customDomains.ToArray()));
            }

            UpdateProgress(95, "Формирование заключения...");
            yield return null;

            AddLine("\n———— ЗАКЛЮЧЕНИЕ ————");

            if (javaResult.Contains("YouTube: BLOCKED") || javaResult.Contains("Telegram: BLOCKED"))
            {
                AddLine("Выявлены блокировки на уровне провайдера.");
                AddLine("Рекомендуется использовать VPN.");
            }
            else if (javaResult.Contains("VPN: ACTIVE"))
            {
                AddLine("VPN подключен. Блокировки обходятся.");
            }
            else
            {
                AddLine("Проблем не обнаружено. Сеть работает стабильно.");
            }

            AddLine($"\nВсего проверено доменов: {customDomains.Count + 4}");

            yield return null;
        }

        private IEnumerator CheckDomainsNonBlocking(string[] domains)
        {
            activeTasks.Clear();
            Dictionary<string, string> results = new Dictionary<string, string>();

            foreach (string domain in domains)
            {
                var task = CheckDomainAsync(domain, results);
                activeTasks.Add(task);
            }

            int lastCompleted = 0;
            float startTime = Time.time;

            while (activeTasks.Count > 0 && Time.time - startTime < 30f) // Максимум 30 секунд
            {
                for (int i = activeTasks.Count - 1; i >= 0; i--)
                {
                    if (activeTasks[i].IsCompleted || activeTasks[i].IsFaulted || activeTasks[i].IsCanceled)
                    {
                        activeTasks.RemoveAt(i);
                    }
                }

                int completed = domains.Length - activeTasks.Count;
                if (completed > lastCompleted)
                {
                    lastCompleted = completed;
                    int progress = 80 + (int)((completed / (float)domains.Length) * 15);
                    UpdateProgress(progress, $"Проверено {completed} из {domains.Length} доменов");
                }

                yield return new WaitForSeconds(0.05f);
            }

            foreach (string domain in domains)
            {
                if (results.ContainsKey(domain))
                {
                    AddLine(results[domain]);
                }
            }

            foreach (var task in activeTasks)
            {
                if (!task.IsCompleted)
                {
                }
            }

            activeTasks.Clear();
        }

        private async Task CheckDomainAsync(string domain, Dictionary<string, string> results)
        {
            await Task.Run(async () =>
            {
                try
                {
                    foreach (string russianSite in russianSites)
                    {
                        if (domain.Contains(russianSite))
                        {
                            lock (results)
                            {
                                results[domain] = $"{domain}: ДОСТУПЕН (Российский сайт)";
                            }
                            return;
                        }
                    }

                    string result = await CheckDomainWithTimeoutAsync(domain);
                    lock (results)
                    {
                        results[domain] = result;
                    }
                }
                catch (System.Exception)
                {
                    lock (results)
                    {
                        results[domain] = $"{domain}: ОШИБКА проверки";
                    }
                }
            });
        }

        private async Task<string> CheckDomainWithTimeoutAsync(string domain)
        {
            using (var timeoutCts = new System.Threading.CancellationTokenSource(6000))
            {
                var checkTask = CheckHttpsHeadAsync(domain, timeoutCts.Token);
                var timeoutTask = Task.Delay(6000, timeoutCts.Token);

                var completedTask = await Task.WhenAny(checkTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    return $"{domain}: ТАЙМАУТ";
                }

                return await checkTask;
            }
        }

        private async Task<string> CheckHttpsHeadAsync(string domain, System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.Timeout = System.TimeSpan.FromSeconds(5);
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

                    var response = await httpClient.SendAsync(
                        new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, $"https://{domain}"),
                        cancellationToken
                    );

                    if (response.IsSuccessStatusCode || (int)response.StatusCode < 500)
                    {
                        return $"{domain}: ДОСТУПЕН";
                    }
                    else
                    {
                        return $"{domain}: ЗАБЛОКИРОВАН (HTTP {(int)response.StatusCode})";
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException httpEx) when (httpEx.InnerException is System.Net.Sockets.SocketException)
            {
                return $"{domain}: ЗАБЛОКИРОВАН";
            }
            catch (System.Net.Sockets.SocketException)
            {
                return $"{domain}: ЗАБЛОКИРОВАН";
            }
            catch (System.OperationCanceledException)
            {
                return $"{domain}: ТАЙМАУТ";
            }
            catch (System.Exception)
            {
                return await CheckHttpAsFallbackAsync(domain, cancellationToken);
            }
        }

        private async Task<string> CheckHttpAsFallbackAsync(string domain, System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.Timeout = System.TimeSpan.FromSeconds(3);
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

                    var response = await httpClient.SendAsync(
                        new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, $"http://{domain}"),
                        cancellationToken
                    );

                    if (response.IsSuccessStatusCode || (int)response.StatusCode < 500)
                    {
                        return $"{domain}: ДОСТУПЕН (только HTTP)";
                    }
                }
            }
            catch
            {
            }

            return $"{domain}: ЗАБЛОКИРОВАН (HTTPS/HTTP)";
        }

        private string FormatJavaResult(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string[] lines = input.Split('\n');
            List<string> formattedLines = new List<string>();
            HashSet<string> seenLines = new HashSet<string>();

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                if (string.IsNullOrEmpty(trimmedLine))
                    continue;

                if (seenLines.Contains(trimmedLine))
                    continue;

                seenLines.Add(trimmedLine);

                if (trimmedLine.StartsWith("VPN PROTOCOLS:"))
                {
                    if (formattedLines.Count > 0)
                    {
                        formattedLines.Add("");
                    }
                    formattedLines.Add(trimmedLine);
                }
                else if (trimmedLine.StartsWith("PING TEST:"))
                {
                    if (formattedLines.Count > 0 && !formattedLines[formattedLines.Count - 1].Equals(""))
                    {
                        formattedLines.Add("");
                    }
                    formattedLines.Add(trimmedLine);
                }
                else
                {
                    formattedLines.Add(trimmedLine);
                }
            }

            return string.Join("\n", formattedLines);
        }

        private IEnumerator CallJavaCode(AndroidJavaObject context, System.Action<string> callback)
        {
            string result = "";

            try
            {
                AndroidJavaClass diagnosticClass = new AndroidJavaClass(
                    "com.UnknownGameStudio.NetworkAutopsy.AdvancedNetworkDiagnostic"
                );

                result = diagnosticClass.CallStatic<string>("quickDiagnose", context);
            }
            catch (System.Exception e)
            {
                result = $"Ошибка Java: {e.Message}";
            }

            callback?.Invoke(result);
            yield return null;
        }

        private string GetProviderInfo()
        {
            try
            {
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");

                AndroidJavaClass connectivityManagerClass = new AndroidJavaClass("android.net.ConnectivityManager");
                AndroidJavaObject connectivityManager = context.Call<AndroidJavaObject>("getSystemService", "connectivity");

                AndroidJavaObject networkInfo = connectivityManager.Call<AndroidJavaObject>("getActiveNetworkInfo");

                if (networkInfo != null && networkInfo.Call<bool>("isConnected"))
                {
                    AndroidJavaObject telephonyManager = context.Call<AndroidJavaObject>("getSystemService", "phone");
                    string networkOperatorName = telephonyManager.Call<string>("getNetworkOperatorName");

                    if (!string.IsNullOrEmpty(networkOperatorName))
                        return networkOperatorName;
                }
            }
            catch (System.Exception)
            {
            }

            return "Unknown ISP";
        }

        private string GetConnectionType()
        {
            try
            {
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");

                AndroidJavaClass connectivityManagerClass = new AndroidJavaClass("android.net.ConnectivityManager");
                AndroidJavaObject connectivityManager = context.Call<AndroidJavaObject>("getSystemService", "connectivity");
                AndroidJavaObject networkInfo = connectivityManager.Call<AndroidJavaObject>("getActiveNetworkInfo");

                if (networkInfo != null)
                {
                    int type = networkInfo.Call<int>("getType");

                    switch (type)
                    {
                        case 0:
                            return "Mobile Data";
                        case 1:
                            return "Wi-Fi";
                        case 9:
                            return "Ethernet";
                        default:
                            return "Unknown";
                    }
                }
            }
            catch (System.Exception)
            {
            }

            return "Unknown";
        }

        private IEnumerator SimulateDiagnostic()
        {
            UpdateProgress(20, "Проверка сети...");
            yield return new WaitForSeconds(0.3f);

            UpdateProgress(40, "Тестирование сайтов...");
            yield return new WaitForSeconds(0.3f);

            UpdateProgress(60, "Проверка протоколов...");
            yield return new WaitForSeconds(0.3f);

            UpdateProgress(80, "Анализ результатов...");
            yield return new WaitForSeconds(0.3f);

            AddLine("CONNECTION TYPE:");
            AddLine(GetConnectionType());
            AddLine("STATUS: CONNECTED");
            AddLine("PROVIDER: Rostelecom (simulated)");
            AddLine("VPN: NOT ACTIVE");
            AddLine("");

            AddLine("INTERNET ACCESS:");
            AddLine("YouTube: BLOCKED");
            AddLine("Discord: BLOCKED");
            AddLine("Telegram: BLOCKED");
            AddLine("Google: ACCESSIBLE");
            AddLine("");

            AddLine("");
            AddLine("VPN PROTOCOLS:");
            AddLine("VLESS: OPEN");
            AddLine("VMESS: BLOCKED");
            AddLine("TROJAN: OPEN");
            AddLine("SHADOWSOCKS: BLOCKED");
            AddLine("WIREGUARD: BLOCKED");
            AddLine("");

            AddLine("PING TEST:");
            AddLine("Google DNS: 45 ms");
            AddLine("Cloudflare DNS: 52 ms");
            AddLine("");
            AddLine("PACKET LOSS ESTIMATION:");
            AddLine("Packet loss: 0.0%");
            AddLine("");

            if (customDomains.Count > 0)
            {
                AddLine("———— ДОПОЛНИТЕЛЬНЫЕ ДОМЕНЫ ————");
                foreach (string domain in customDomains)
                {
                    if (domain.Contains("vk.com") || domain.Contains("rutracker.org"))
                        AddLine($"{domain}: ДОСТУПЕН (Российский сайт)");
                    else
                        AddLine($"{domain}: ДОСТУПЕН");
                }
            }

            AddLine("\n———— ЗАКЛЮЧЕНИЕ ————");
            AddLine("Выявлены блокировки YouTube и Telegram.");
            AddLine("Провайдер блокирует протоколы: VMESS, SHADOWSOCKS, WIREGUARD.");
            AddLine("Рекомендуется использовать VPN с протоколом VLESS или TROJAN.");

            yield return new WaitForSeconds(0.5f);
        }

        private void AddLine(string text)
        {
            if (UnityMainThreadDispatcher.Instance != null)
            {
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    report.AppendLine(text);
                    outputText.text = report.ToString();

                    if (scrollView != null)
                    {
                        Canvas.ForceUpdateCanvases();
                        scrollView.verticalNormalizedPosition = 0f;
                    }
                });
            }
            else
            {
                report.AppendLine(text);
                outputText.text = report.ToString();

                if (scrollView != null)
                {
                    Canvas.ForceUpdateCanvases();
                    scrollView.verticalNormalizedPosition = 0f;
                }
            }
        }

        private void ClearOutput()
        {
            report.Clear();
            if (outputText != null)
                outputText.text = "";
        }

        private void UpdateProgress(int progress, string message)
        {
            if (UnityMainThreadDispatcher.Instance != null)
            {
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    if (progressSlider != null)
                        progressSlider.value = progress;

                    if (progressText != null)
                        progressText.text = $"{progress}% - {message}";
                });
            }
            else
            {
                if (progressSlider != null)
                    progressSlider.value = progress;

                if (progressText != null)
                    progressText.text = $"{progress}% - {message}";
            }
        }

        private void ShowLoading(bool show)
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(show);

            if (startButton != null)
                startButton.interactable = !show;

            if (saveButton != null)
                saveButton.interactable = !show && report.Length > 0;

            if (addDomainButton != null)
                addDomainButton.interactable = !show;

            if (domainInput != null)
                domainInput.interactable = !show;
        }
    }
}