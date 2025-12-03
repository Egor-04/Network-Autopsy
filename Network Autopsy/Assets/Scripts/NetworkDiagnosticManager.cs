using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace NetworkDiagnostic
{
    public class NetworkDiagnosticManager : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text outputText;
        [SerializeField] private ScrollRect scrollView;
        [SerializeField] private Button startButton;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private Button saveReportButton;

        private StringBuilder report = new StringBuilder();
        private bool isRunning = false;
        private float startDiagnosticTime;

        // Структура для хранения результатов диагностики
        private class DiagnosticResults
        {
            public string connectionType = "";
            public string networkProvider = "";
            public bool isVpnActive = false;
            public string vpnProtocol = "";
            public float downloadSpeed = 0f;
            public float uploadSpeed = 0f;
            public float ping = 0f;
            public float packetLoss = 0f;
            public bool youtubeBlocked = false;
            public bool telegramBlocked = false;
            public bool vkBlocked = false;
            public List<string> blockedProtocols = new List<string>();
            public List<string> blockedDomains = new List<string>();
            public bool hasInternet = true;
            public bool isRoaming = false;
            public bool dpiDetected = false;
            public bool sniBlocked = false;
        }

        void Start()
        {
            if (startButton != null)
                startButton.onClick.AddListener(StartDiagnostic);

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
            StartCoroutine(RunFullDiagnostic());
        }

        public void SaveCurrentReport()
        {
            if (report.Length == 0)
            {
                report.Append("Нет данных для сохранения\n");
                outputText.text = report.ToString();
                return;
            }

            SaveReport();
        }

        private IEnumerator RunFullDiagnostic()
        {
            report.Clear();
            report.Append("ПОЛНАЯ ДИАГНОСТИКА СЕТИ\n");
            report.Append("==========================\n\n");
            outputText.text = report.ToString();
            startDiagnosticTime = Time.time;

            yield return null;

            DiagnosticResults results = new DiagnosticResults();

#if UNITY_ANDROID && !UNITY_EDITOR
            yield return StartCoroutine(RunAndroidDiagnostic(results));
#else
            yield return StartCoroutine(SimulateEditorDiagnostic(results));
#endif

            // Формируем понятный отчет
            yield return StartCoroutine(GenerateHumanReadableReport(results));

            SaveReport();
            ShowLoading(false);
            isRunning = false;
        }

        private IEnumerator RunAndroidDiagnostic(DiagnosticResults results)
        {
            UpdateProgress(10, "Получение информации о сети...");
            yield return null;

            AndroidJavaObject context = null;

            try
            {
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                context = activity.Call<AndroidJavaObject>("getApplicationContext");
            }
            catch (System.Exception e)
            {
                report.Append($"[ОШИБКА] Не удалось получить доступ к Android: {e.Message}\n");
                outputText.text = report.ToString();
                yield break;
            }

            UpdateProgress(30, "Запуск диагностики...");
            yield return null;

            string javaReport = "";
            yield return StartCoroutine(CallJavaDiagnostic(context, result => {
                javaReport = result;
            }));

            // Парсим результаты из Java отчета
            ParseJavaReport(javaReport, results);

            UpdateProgress(70, "Проверка доступности сайтов...");
            yield return null;

            // Проверяем YouTube
            yield return StartCoroutine(CheckWebsite("https://www.youtube.com", isBlocked => {
                results.youtubeBlocked = !isBlocked;
            }));

            // Проверяем Telegram
            yield return StartCoroutine(CheckWebsite("https://web.telegram.org", isBlocked => {
                results.telegramBlocked = !isBlocked;
            }));

            // Проверяем VK
            yield return StartCoroutine(CheckWebsite("https://vk.com", isBlocked => {
                results.vkBlocked = !isBlocked;
            }));

            UpdateProgress(90, "Анализ результатов...");
            yield return null;
        }

        private IEnumerator CallJavaDiagnostic(AndroidJavaObject context, System.Action<string> onComplete)
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
                result = $"[ОШИБКА] Диагностика не удалась: {e.Message}";
            }

            onComplete?.Invoke(result);
            yield return null;
        }

        private void ParseJavaReport(string javaReport, DiagnosticResults results)
        {
            try
            {
                // Парсим тип подключения
                if (javaReport.Contains("[TYPE] Wi-Fi"))
                {
                    results.connectionType = "Wi-Fi";
                }
                else if (javaReport.Contains("[TYPE] Mobile"))
                {
                    results.connectionType = "Мобильная сеть (LTE/4G/5G)";
                }
                else if (javaReport.Contains("[TYPE] VPN"))
                {
                    results.connectionType = "VPN";
                    results.isVpnActive = true;
                }

                // Парсим VPN статус
                if (javaReport.Contains("[VPN] ACTIVE") || javaReport.Contains("VPN connection active"))
                {
                    results.isVpnActive = true;
                }

                // Парсим потерю пакетов
                if (javaReport.Contains("Packet loss:"))
                {
                    int start = javaReport.IndexOf("Packet loss:") + 12;
                    int end = javaReport.IndexOf("%", start);
                    if (end > start)
                    {
                        string lossStr = javaReport.Substring(start, end - start).Trim();
                        if (float.TryParse(lossStr, out float loss))
                        {
                            results.packetLoss = loss;
                        }
                    }
                }

                // Парсим пинг
                if (javaReport.Contains("Average ping:"))
                {
                    int start = javaReport.IndexOf("Average ping:") + 13;
                    int end = javaReport.IndexOf("ms", start);
                    if (end > start)
                    {
                        string pingStr = javaReport.Substring(start, end - start).Trim();
                        if (float.TryParse(pingStr, out float ping))
                        {
                            results.ping = ping;
                        }
                    }
                }

                // Парсим скорость
                if (javaReport.Contains("Download speed:"))
                {
                    int start = javaReport.IndexOf("Download speed:") + 15;
                    int end = javaReport.IndexOf("Mbps", start);
                    if (end > start)
                    {
                        string speedStr = javaReport.Substring(start, end - start).Trim();
                        if (float.TryParse(speedStr, out float speed))
                        {
                            results.downloadSpeed = speed;
                        }
                    }
                }

                // Парсим скорость отдачи
                if (javaReport.Contains("Upload speed:"))
                {
                    int start = javaReport.IndexOf("Upload speed:") + 13;
                    int end = javaReport.IndexOf("Mbps", start);
                    if (end > start)
                    {
                        string speedStr = javaReport.Substring(start, end - start).Trim();
                        if (float.TryParse(speedStr, out float speed))
                        {
                            results.uploadSpeed = speed;
                        }
                    }
                }

                // Определяем провайдера по extra info
                if (javaReport.Contains("[EXTRA INFO]"))
                {
                    int start = javaReport.IndexOf("[EXTRA INFO]") + 13;
                    int end = javaReport.IndexOf("\n", start);
                    if (end > start)
                    {
                        string extraInfo = javaReport.Substring(start, end - start).Trim();
                        if (!string.IsNullOrEmpty(extraInfo) && extraInfo != "N/A")
                        {
                            results.networkProvider = extraInfo;
                        }
                    }
                }

                // Если провайдер не определился, ставим по умолчанию
                if (string.IsNullOrEmpty(results.networkProvider))
                {
                    if (results.connectionType.Contains("Мобильная"))
                        results.networkProvider = "Мобильный оператор";
                    else
                        results.networkProvider = "Локальный провайдер";
                }

                // Определяем протокол VPN (если есть)
                if (javaReport.Contains("VPN Protocol:"))
                {
                    int start = javaReport.IndexOf("VPN Protocol:") + 13;
                    int end = javaReport.IndexOf("\n", start);
                    if (end > start)
                    {
                        results.vpnProtocol = javaReport.Substring(start, end - start).Trim();
                    }
                }

                // Парсим DPI и SNI блокировки
                if (javaReport.Contains("DPI Detection: [DETECTED]"))
                {
                    results.dpiDetected = true;
                }

                if (javaReport.Contains("SNI") && javaReport.Contains("[BLOCKED]"))
                {
                    results.sniBlocked = true;
                }

                // Ищем заблокированные протоколы
                string[] protocolsToCheck = { "VLESS", "VMESS", "TROJAN", "SHADOWSOCKS", "WIREGUARD", "OPENVPN" };
                foreach (var protocol in protocolsToCheck)
                {
                    if (javaReport.Contains($"[{protocol}]") &&
                        (javaReport.Contains("BLOCKED") || javaReport.Contains("HEAVILY BLOCKED") || javaReport.Contains("PARTIALLY BLOCKED")))
                    {
                        results.blockedProtocols.Add(protocol);
                    }
                }

                // Ищем заблокированные домены
                if (javaReport.Contains("rutracker.org") && javaReport.Contains("BLOCKED"))
                {
                    results.blockedDomains.Add("RuTracker");
                }
                if (javaReport.Contains("vk.com") && javaReport.Contains("BLOCKED"))
                {
                    results.blockedDomains.Add("ВКонтакте");
                }
                if (javaReport.Contains("telegram.org") && javaReport.Contains("BLOCKED"))
                {
                    results.blockedDomains.Add("Telegram");
                }

                report.Append("Получены данные от системы\n");
            }
            catch (System.Exception e)
            {
                report.Append($"[ОШИБКА] Не удалось разобрать отчет: {e.Message}\n");
            }

            outputText.text = report.ToString();
        }

        private IEnumerator CheckWebsite(string url, System.Action<bool> onComplete)
        {
            bool isAccessible = false;
            UnityWebRequest www = UnityWebRequest.Head(url);
            www.timeout = 5;

            var operation = www.SendWebRequest();
            float startTime = Time.time;

            while (!operation.isDone && Time.time - startTime < 6f)
            {
                yield return null;
            }

            if (www.result == UnityWebRequest.Result.Success)
            {
                isAccessible = true;
            }

            www.Dispose();
            onComplete?.Invoke(isAccessible);
        }

        private IEnumerator GenerateHumanReadableReport(DiagnosticResults results)
        {
            UpdateProgress(95, "Формирование отчета...");

            report.Append("\nРЕЗУЛЬТАТЫ ДИАГНОСТИКИ\n");
            report.Append("==========================\n\n");

            // 1. Основная информация о подключении
            report.Append("1. ТИП ПОДКЛЮЧЕНИЯ:\n");
            report.Append("-------------------\n");
            report.Append($"• Тип сети: {results.connectionType}\n");
            report.Append($"• Провайдер: {results.networkProvider}\n");
            report.Append($"• Роуминг: {(results.isRoaming ? "Да" : "Нет")}\n");
            report.Append($"• VPN: {(results.isVpnActive ? "Активен" : "Не активен")}\n");
            if (results.isVpnActive && !string.IsNullOrEmpty(results.vpnProtocol))
            {
                report.Append($"• Протокол VPN: {results.vpnProtocol}\n");
            }
            report.Append("\n");

            // 2. Скорость интернета
            report.Append("2. СКОРОСТЬ ИНТЕРНЕТА:\n");
            report.Append("-------------------\n");
            report.Append($"• Загрузка: {results.downloadSpeed:F1} Мбит/с\n");
            report.Append($"• Отдача: {results.uploadSpeed:F1} Мбит/с\n");
            report.Append($"• Пинг: {results.ping:F0} мс\n");

            // Оценка скорости
            string speedRating = "";
            if (results.downloadSpeed < 5) speedRating = "Медленно";
            else if (results.downloadSpeed < 20) speedRating = "Средняя";
            else if (results.downloadSpeed < 100) speedRating = "Быстрая";
            else speedRating = "Очень быстрая";

            report.Append($"• Оценка: {speedRating}\n");
            report.Append("\n");

            // 3. Качество соединения
            report.Append("3. КАЧЕСТВО СОЕДИНЕНИЯ:\n");
            report.Append("-------------------\n");
            report.Append($"• Потеря пакетов: {results.packetLoss:F1}%\n");

            string packetLossRating = "";
            if (results.packetLoss < 1) packetLossRating = "Отличное";
            else if (results.packetLoss < 5) packetLossRating = "Хорошее";
            else if (results.packetLoss < 10) packetLossRating = "Среднее";
            else packetLossRating = "Плохое";

            report.Append($"• Стабильность: {packetLossRating}\n");
            report.Append("\n");

            // 4. Доступность сайтов
            report.Append("4. ДОСТУПНОСТЬ САЙТОВ:\n");
            report.Append("-------------------\n");
            report.Append($"• YouTube: {(results.youtubeBlocked ? "Заблокирован" : "Доступен")}\n");
            report.Append($"• Telegram: {(results.telegramBlocked ? "Заблокирован" : "Доступен")}\n");
            report.Append($"• ВКонтакте: {(results.vkBlocked ? "Заблокирован" : "Доступен")}\n");

            if (results.blockedDomains.Count > 0)
            {
                report.Append($"• Заблокировано по РКН: {string.Join(", ", results.blockedDomains)}\n");
            }
            report.Append("\n");

            // 5. Блокировка VPN протоколов
            report.Append("5. БЛОКИРОВКА VPN ПРОТОКОЛОВ:\n");
            report.Append("-------------------\n");

            if (results.blockedProtocols.Count > 0)
            {
                report.Append($"• Заблокированные протоколы: {string.Join(", ", results.blockedProtocols)}\n");
            }
            else
            {
                report.Append("• VPN протоколы не блокируются\n");
            }

            if (results.dpiDetected)
            {
                report.Append("• Обнаружена система DPI (глубокая проверка пакетов)\n");
            }

            if (results.sniBlocked)
            {
                report.Append("• Обнаружена SNI-блокировка (блокировка по имени домена)\n");
            }
            report.Append("\n");

            // 6. ЗАКЛЮЧЕНИЕ И РЕКОМЕНДАЦИИ
            report.Append("6. ЗАКЛЮЧЕНИЕ:\n");
            report.Append("-------------------\n");

            List<string> issues = new List<string>();
            List<string> recommendations = new List<string>();

            // Проверяем проблемы со скоростью
            if (results.downloadSpeed < 5)
            {
                issues.Add("Очень низкая скорость интернета");
                recommendations.Add("Попробуйте переключиться на другую сеть (Wi-Fi/мобильную)");
                recommendations.Add("Перезагрузите роутер или модем");
            }
            else if (results.downloadSpeed < 20 && results.connectionType.Contains("Wi-Fi"))
            {
                issues.Add("Низкая скорость Wi-Fi соединения");
                recommendations.Add("Подойдите ближе к роутеру");
                recommendations.Add("Проверьте, не перегружена ли Wi-Fi сеть");
            }

            // Проверяем потерю пакетов
            if (results.packetLoss > 10)
            {
                issues.Add("Высокая потеря пакетов данных");
                recommendations.Add("Возможно нестабильное соединение с провайдером");
                recommendations.Add("Попробуйте перезагрузить сетевое оборудование");
            }
            else if (results.packetLoss > 5)
            {
                issues.Add("Умеренная потеря пакетов");
                recommendations.Add("Соединение может быть нестабильным при звонках или играх");
            }

            // Проверяем пинг
            if (results.ping > 150)
            {
                issues.Add("Высокий пинг (задержка)");
                recommendations.Add("Могут быть проблемы с онлайн-играми и видеозвонками");
            }

            // Проверяем блокировки
            if (results.youtubeBlocked || results.telegramBlocked || results.vkBlocked)
            {
                if (results.youtubeBlocked && results.telegramBlocked)
                {
                    issues.Add("Выявлены блокировки на уровне DPI/SNI");
                    recommendations.Add("Используйте VPN с obfuscation (маскировкой трафика)");
                    recommendations.Add("Попробуйте VPN протоколы: VLESS over gRPC или Trojan over TLS");
                }
                else if (results.youtubeBlocked)
                {
                    issues.Add("Блокировка YouTube (возможно РКН или провайдер)");
                }
                else if (results.telegramBlocked)
                {
                    issues.Add("Блокировка Telegram (стандартная блокировка РКН)");
                }
            }

            // Проверяем блокировку VPN протоколов
            if (results.blockedProtocols.Count > 0)
            {
                issues.Add($"Провайдер блокирует VPN протоколы: {string.Join(", ", results.blockedProtocols)}");

                if (results.blockedProtocols.Contains("SHADOWSOCKS"))
                {
                    recommendations.Add("Shadowsocks часто блокируется, попробуйте VLESS или Trojan");
                }

                if (results.blockedProtocols.Contains("WIREGUARD"))
                {
                    recommendations.Add("WireGuard блокируется по порту 51820, попробуйте порт 443");
                }

                recommendations.Add("Используйте менее распространенные порты (2053, 2083, 8443)");
            }

            // Проверяем DPI
            if (results.dpiDetected)
            {
                issues.Add("Провайдер использует DPI (глубокая проверка пакетов)");
                recommendations.Add("Используйте протоколы с маскировкой: VLESS over gRPC/WebSocket");
                recommendations.Add("Включите TLS/SSL шифрование во всех протоколах");
            }

            // Проверяем SNI блокировку
            if (results.sniBlocked)
            {
                issues.Add("Обнаружена SNI-блокировка (блокировка по имени домена)");
                recommendations.Add("Используйте ECH (Encrypted Client Hello) если поддерживается");
                recommendations.Add("Настройте маскировку под обычный HTTPS трафик");
            }

            // Если VPN активен, но есть блокировки
            if (results.isVpnActive && (results.youtubeBlocked || results.telegramBlocked))
            {
                issues.Add("VPN не справляется с обходом блокировок");
                recommendations.Add("Попробуйте другой VPN сервер или протокол");
                recommendations.Add("Убедитесь, что VPN правильно настроен");
            }

            // Формируем заключение
            if (issues.Count == 0)
            {
                report.Append("Ваше интернет-соединение в отличном состоянии!\n");
                report.Append("Скорость высокая, пинг низкий, блокировок не обнаружено.\n");
            }
            else
            {
                report.Append("Обнаружены следующие проблемы:\n");
                foreach (var issue in issues)
                {
                    report.Append($"• {issue}\n");
                }
                report.Append("\n");

                report.Append("Рекомендации:\n");
                foreach (var recommendation in recommendations)
                {
                    report.Append($"• {recommendation}\n");
                }
            }

            // Добавляем итоговую информацию
            report.Append("\n==========================\n");
            float diagnosticTime = Time.time - startDiagnosticTime;
            report.Append($"Диагностика выполнена за {diagnosticTime:F1} секунд\n");

            outputText.text = report.ToString();
            yield return null;
        }

        private IEnumerator SimulateEditorDiagnostic(DiagnosticResults results)
        {
            UpdateProgress(20, "Симуляция...");
            yield return new WaitForSeconds(0.3f);

            UpdateProgress(50, "Проверка сети...");
            yield return new WaitForSeconds(0.3f);

            UpdateProgress(80, "Тестирование...");
            yield return new WaitForSeconds(0.3f);

            // Заполняем тестовые данные
            results.connectionType = "Wi-Fi";
            results.networkProvider = "Ростелеком";
            results.isVpnActive = false;
            results.downloadSpeed = 47.3f;
            results.uploadSpeed = 12.8f;
            results.ping = 24f;
            results.packetLoss = 0.5f;
            results.youtubeBlocked = true;
            results.telegramBlocked = true;
            results.vkBlocked = false;
            results.blockedProtocols = new List<string> { "SHADOWSOCKS", "WIREGUARD" };
            results.blockedDomains = new List<string> { "RuTracker", "Telegram" };
            results.dpiDetected = true;

            UpdateProgress(100, "Готово");
            yield return null;
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
            if (outputText != null)
                outputText.text = "";
        }

        private void UpdateProgress(int progress, string message)
        {
            if (progressSlider != null)
                progressSlider.value = progress;

            if (progressText != null)
                progressText.text = $"{progress}% - {message}";
        }

        private void ShowLoading(bool show)
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(show);

            if (startButton != null)
                startButton.interactable = !show;

            if (saveReportButton != null)
                saveReportButton.interactable = !show;
        }

        private void SaveReport()
        {
            try
            {
                string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string filename = $"network_diagnostic_{timestamp}.txt";
                string path = Path.Combine(Application.persistentDataPath, filename);

                string fullReport = "ОТЧЕТ ДИАГНОСТИКИ СЕТИ\n" +
                    "==========================\n" +
                    "Время: " + System.DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + "\n" +
                    "Устройство: " + SystemInfo.deviceModel + "\n" +
                    "ОС: " + SystemInfo.operatingSystem + "\n" +
                    "\n" + report.ToString();

                File.WriteAllText(path, fullReport);
                report.Append($"\nОтчет сохранен: {filename}\n");
                outputText.text = report.ToString();
            }
            catch (System.Exception e)
            {
                report.Append($"\nОшибка сохранения: {e.Message}\n");
                outputText.text = report.ToString();
            }
        }
    }
}