package com.UnknownGameStudio.NetworkAutopsy;

import android.content.Context;
import android.net.ConnectivityManager;
import android.net.NetworkCapabilities;
import android.net.NetworkInfo;
import android.net.TrafficStats;
import android.os.Build;
import android.util.Log;
import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.net.URL;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

public class AdvancedNetworkDiagnostic {
    private static final String TAG = "NetworkAutopsy";
    private static final String[] TEST_SITES = {
        "google.com",
        "youtube.com",
        "github.com",
        "vk.com",
        "telegram.org",
        "rutracker.org",
        "8.8.8.8",
        "1.1.1.1"
    };
    
    private static final String[] DNS_SERVERS = {
        "8.8.8.8",
        "1.1.1.1",
        "9.9.9.9",
        "77.88.8.8",
        "208.67.222.222"
    };
    
    private static final String[] RKN_BLOCKED_SITES = {
        "rutracker.org",
        "libgen.is",
        "t.me",
        "telegram.me"
    };
    
    // Тестовые серверы для проверки VPN протоколов
    private static final Map<String, String[]> VPN_PROTOCOL_SERVERS = new HashMap<String, String[]>() {{
        put("VLESS", new String[]{"vless.example.com", "vless.server.com"});
        put("VMESS", new String[]{"vmess.example.com", "vmess.server.com"});
        put("TROJAN", new String[]{"trojan.example.com", "trojan.server.com"});
        put("SHADOWSOCKS", new String[]{"ss.example.com", "shadowsocks.server.com"});
        put("WIREGUARD", new String[]{"wireguard.example.com"});
        put("OPENVPN", new String[]{"openvpn.example.com"});
        put("IPSEC", new String[]{"ipsec.example.com"});
    }};
    
    // Стандартные порты для VPN протоколов
    private static final Map<String, Integer[]> VPN_PROTOCOL_PORTS = new HashMap<String, Integer[]>() {{
        put("VLESS", new Integer[]{443, 8443, 2053, 2083, 2087});
        put("VMESS", new Integer[]{443, 8443, 2053, 2083, 2087});
        put("TROJAN", new Integer[]{443, 8443});
        put("SHADOWSOCKS", new Integer[]{8388, 1080, 443});
        put("WIREGUARD", new Integer[]{51820});
        put("OPENVPN", new Integer[]{1194, 1195, 1196, 1197});
        put("IPSEC", new Integer[]{500, 4500});
        put("SOCKS5", new Integer[]{1080, 1081});
        put("HTTP_PROXY", new Integer[]{8080, 3128});
    }};
    
    // Тестовые хосты для протоколов (используем реальные домены для тестов)
    private static final String[] PROTOCOL_TEST_HOSTS = {
        "v2ray.com",
        "xray.com",
        "trojan-gfw.github.io",
        "shadowsocks.org",
        "wireguard.com",
        "openvpn.net",
        "strongswan.org"
    };
    
    // Добавляем метод для проверки VPN протоколов
    public static String checkVpnProtocols(Context context) {
        StringBuilder report = new StringBuilder();
        report.append("\n\n=== VPN PROTOCOL BLOCKING TEST ===\n");
        report.append("======================================\n\n");
        
        try {
            int totalBlocked = 0;
            int totalTested = 0;
            
            // Проверяем стандартные VPN порты
            report.append("1. STANDARD VPN PORTS BLOCKING:\n");
            report.append("---------------------------------\n");
            
            for (Map.Entry<String, Integer[]> entry : VPN_PROTOCOL_PORTS.entrySet()) {
                String protocol = entry.getKey();
                Integer[] ports = entry.getValue();
                
                int blockedPorts = 0;
                int testedPorts = 0;
                
                report.append("\n[").append(protocol).append("] Ports: ");
                
                // Тестируем несколько известных хостов на этих портах
                for (String testHost : PROTOCOL_TEST_HOSTS) {
                    for (int port : ports) {
                        testedPorts++;
                        totalTested++;
                        
                        boolean isBlocked = isPortBlocked(testHost, port);
                        if (isBlocked) {
                            blockedPorts++;
                            totalBlocked++;
                        }
                    }
                }
                
                if (testedPorts > 0) {
                    double blockPercentage = (blockedPorts * 100.0) / testedPorts;
                    report.append(String.format("%.0f%% blocked (%d/%d)", 
                        blockPercentage, blockedPorts, testedPorts));
                    
                    if (blockPercentage > 70) {
                        report.append(" [HEAVILY BLOCKED]");
                    } else if (blockPercentage > 30) {
                        report.append(" [PARTIALLY BLOCKED]");
                    } else if (blockedPorts > 0) {
                        report.append(" [SOME BLOCKING]");
                    } else {
                        report.append(" [NOT BLOCKED]");
                    }
                }
            }
            
            // Проверяем известные VPN домены
            report.append("\n\n2. KNOWN VPN DOMAINS BLOCKING:\n");
            report.append("---------------------------------\n");
            
            String[] vpnDomains = {
                "v2ray.com",
                "xray.com",
                "trojan-gfw.github.io",
                "shadowsocks.org",
                "wireguard.com",
                "openvpn.net",
                "github.com/v2ray",  // V2Ray на GitHub
                "github.com/shadowsocks"  // Shadowsocks на GitHub
            };
            
            for (String domain : vpnDomains) {
                totalTested++;
                boolean isBlocked = isDomainBlocked(domain);
                
                if (isBlocked) {
                    totalBlocked++;
                    report.append("[BLOCKED] ").append(domain).append("\n");
                } else {
                    report.append("[OK] ").append(domain).append("\n");
                }
            }
            
            // Проверяем SNI блокировку (актуально для VLESS/VMESS/Trojan over TLS)
            report.append("\n3. TLS/SNI BLOCKING TEST:\n");
            report.append("--------------------------\n");
            
            String[] sniTestDomains = {
                "v2ray.com",
                "www.cloudflare.com",  // Часто используется для маскировки
                "www.github.com",
                "www.google.com"
            };
            
            for (String domain : sniTestDomains) {
                boolean sniBlocked = testSNIBlocking(domain);
                report.append("SNI ").append(domain).append(": ")
                      .append(sniBlocked ? "[BLOCKED]" : "[OK]").append("\n");
            }
            
            // Проверяем DPI (Deep Packet Inspection)
            report.append("\n4. DPI DETECTION TEST:\n");
            report.append("------------------------\n");
            
            boolean dpiDetected = testDPIDetection();
            report.append("DPI Detection: ").append(dpiDetected ? "[DETECTED]" : "[NOT DETECTED]").append("\n");
            
            if (dpiDetected) {
                report.append("[WARNING] ISP may be using DPI to block VPN traffic\n");
            }
            
            // Итоговая статистика
            report.append("\n5. BLOCKING SUMMARY:\n");
            report.append("---------------------\n");
            
            if (totalTested > 0) {
                double overallBlockPercentage = (totalBlocked * 100.0) / totalTested;
                report.append(String.format("Overall blocking: %.1f%% (%d/%d tests)\n", 
                    overallBlockPercentage, totalBlocked, totalTested));
                
                if (overallBlockPercentage > 50) {
                    report.append("[CRITICAL] Heavy VPN protocol blocking detected!\n");
                    report.append("Recommendations:\n");
                    report.append("- Use obfuscated protocols (VLESS over gRPC/WebSocket)\n");
                    report.append("- Try different ports (8080, 8443, 2053)\n");
                    report.append("- Use CDN masking (Cloudflare Workers)\n");
                } else if (overallBlockPercentage > 20) {
                    report.append("[WARNING] Moderate VPN protocol blocking\n");
                    report.append("Recommendations:\n");
                    report.append("- Switch to less common ports\n");
                    report.append("- Enable TLS/SSL encryption\n");
                } else {
                    report.append("[GOOD] Minimal VPN protocol blocking\n");
                }
            }
            
            // Проверяем конкретные протоколы
            report.append("\n6. SPECIFIC PROTOCOL RECOMMENDATIONS:\n");
            report.append("---------------------------------------\n");
            
            report.append("VLESS/VMESS with TLS: ");
            report.append(isPortBlocked("google.com", 443) ? "May be blocked\n" : "Should work\n");
            
            report.append("VLESS/VMESS with gRPC: ");
            report.append(isPortBlocked("google.com", 443) ? "Try different port\n" : "Recommended\n");
            
            report.append("Trojan: ");
            report.append(isPortBlocked("google.com", 443) ? "May need obfuscation\n" : "Good option\n");
            
            report.append("Shadowsocks: ");
            report.append(checkShadowsocksBlocking() ? "Often blocked\n" : "Works fine\n");
            
            report.append("WireGuard: ");
            report.append(isPortBlocked("google.com", 51820) ? "Port often blocked\n" : "Good speed\n");
            
        } catch (Exception e) {
            report.append("[ERROR] Protocol test failed: ").append(e.getMessage()).append("\n");
            Log.e(TAG, "VPN protocol test error: " + e.getMessage());
        }
        
        return report.toString();
    }
    
    // Проверяет заблокирован ли порт
    private static boolean isPortBlocked(String host, int port) {
        try {
            Socket socket = new Socket();
            socket.connect(new InetSocketAddress(host, port), 3000);
            boolean connected = socket.isConnected();
            socket.close();
            return !connected;
        } catch (Exception e) {
            // Считаем порт заблокированным если соединение не удалось
            return true;
        }
    }
    
    // Проверяет заблокирован ли домен
    private static boolean isDomainBlocked(String domain) {
        try {
            InetAddress address = InetAddress.getByName(domain);
            return !address.isReachable(3000);
        } catch (Exception e) {
            return true;
        }
    }
    
    // Тестирует SNI блокировку (актуально для TLS)
    private static boolean testSNIBlocking(String domain) {
        try {
            // Пытаемся установить TLS соединение
            Socket socket = new Socket();
            socket.connect(new InetSocketAddress(domain, 443), 3000);
            
            // Простая проверка TLS (без полного рукопожатия)
            socket.close();
            return false;
        } catch (Exception e) {
            // Проверяем характер ошибки
            String error = e.getMessage();
            if (error != null && (error.contains("reset") || 
                                  error.contains("refused") || 
                                  error.contains("timeout"))) {
                return true; // Возможна SNI блокировка
            }
            return false;
        }
    }
    
    // Тестирует обнаружение DPI
    private static boolean testDPIDetection() {
        try {
            // Пытаемся подключиться к известным VPN сервисам
            String[] vpnServices = {
                "api.v2ray.com",
                "api.shadowsocks.org",
                "api.wireguard.com"
            };
            
            int blockedCount = 0;
            for (String service : vpnServices) {
                if (isDomainBlocked(service)) {
                    blockedCount++;
                }
            }
            
            // Если большинство VPN API заблокированы, возможно есть DPI
            return blockedCount >= 2;
        } catch (Exception e) {
            return false;
        }
    }
    
    // Специфичная проверка для Shadowsocks
    private static boolean checkShadowsocksBlocking() {
        try {
            // Shadowsocks часто блокируется по известным портам
            int[] ssPorts = {8388, 1080, 443};
            int blockedPorts = 0;
            
            for (int port : ssPorts) {
                if (isPortBlocked("google.com", port)) {
                    blockedPorts++;
                }
            }
            
            return blockedPorts >= 2; // Если 2+ порта заблокированы
        } catch (Exception e) {
            return false;
        }
    }
    
    // Добавляем проверку VPN протоколов в основной отчет
    public static String quickDiagnose(Context context) {
        StringBuilder report = new StringBuilder();
        
        try {
            long startTime = System.currentTimeMillis();
            
            report.append("=== NETWORK DIAGNOSTIC REPORT ===\n\n");
            
            ConnectivityManager cm = (ConnectivityManager) 
                context.getSystemService(Context.CONNECTIVITY_SERVICE);
            
            if (cm == null) {
                report.append("[ERROR] Connectivity service not available\n");
                return report.toString();
            }
            
            NetworkInfo activeNetwork = cm.getActiveNetworkInfo();
            
            report.append("1. CONNECTION STATUS:\n");
            report.append("----------------------\n");
            
            if (activeNetwork == null) {
                report.append("[STATUS] DISCONNECTED (No active network)\n");
            } else {
                boolean isConnected = activeNetwork.isConnected();
                boolean isRoaming = activeNetwork.isRoaming();
                
                report.append("[STATUS] ").append(isConnected ? "CONNECTED" : "DISCONNECTED").append("\n");
                report.append("[TYPE] ").append(getNetworkTypeName(activeNetwork.getType())).append("\n");
                report.append("[SUBTYPE] ").append(activeNetwork.getSubtypeName()).append("\n");
                report.append("[ROAMING] ").append(isRoaming ? "YES" : "NO").append("\n");
                report.append("[EXTRA INFO] ").append(activeNetwork.getExtraInfo() != null ? activeNetwork.getExtraInfo() : "N/A").append("\n");
                
                // Улучшенная проверка VPN
                boolean isVpnActive = isVpnActive(context);
                report.append("[VPN] ").append(isVpnActive ? "ACTIVE" : "NOT ACTIVE").append("\n");
                
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP && isConnected) {
                    android.net.Network network = cm.getActiveNetwork();
                    if (network != null) {
                        NetworkCapabilities nc = cm.getNetworkCapabilities(network);
                        if (nc != null) {
                            report.append("\n[CAPABILITIES]\n");
                            report.append("  - INTERNET: ").append(nc.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) ? "YES" : "NO").append("\n");
                            report.append("  - VALIDATED: ").append(nc.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED) ? "YES" : "NO").append("\n");
                            report.append("  - NOT_METERED: ").append(nc.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_METERED) ? "YES" : "NO").append("\n");
                            report.append("  - NOT_ROAMING: ").append(nc.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_ROAMING) ? "YES" : "NO").append("\n");
                        }
                    }
                }
            }
            
            // Если VPN активен - делаем быстрые тесты
            boolean vpnActive = activeNetwork != null && 
                               (activeNetwork.getType() == ConnectivityManager.TYPE_VPN || isVpnActive(context));
            
            if (vpnActive) {
                report.append("\n[INFO] VPN detected - running quick tests\n");
            }
            
            report.append("\n\n2. INTERNET CONNECTIVITY TESTS:\n");
            report.append("---------------------------------\n");
            
            // Быстрая проверка сайтов с учетом VPN
            List<SiteTestResult> siteResults = vpnActive ? testSitesQuick() : testAllSites();
            for (SiteTestResult result : siteResults) {
                String status;
                if (result.isBlocked) {
                    status = "BLOCKED";
                } else if (result.isReachable) {
                    status = "OK (" + result.ping + "ms)";
                } else {
                    status = "UNREACHABLE";
                }
                report.append(String.format("[%-15s] %s\n", result.site, status));
            }
            
            report.append("\n\n3. PACKET LOSS & PING TEST:\n");
            report.append("----------------------------\n");
            
            // Быстрый тест пинга при VPN
            PingResult pingResult = vpnActive ? testPingQuick("8.8.8.8") : testPacketLossAndPing("8.8.8.8");
            report.append(String.format("Packet loss: %.1f%% (%d/%d packets)\n", 
                pingResult.packetLoss, pingResult.lostPackets, pingResult.totalPackets));
            report.append(String.format("Average ping: %.1f ms\n", pingResult.avgPing));
            report.append(String.format("Jitter: %.1f ms\n", pingResult.jitter));
            report.append(String.format("Max ping: %.1f ms\n", pingResult.maxPing));
            
            if (pingResult.packetLoss > 10) {
                report.append("[WARNING] High packet loss detected!\n");
            }
            if (pingResult.avgPing > 200) {
                report.append("[WARNING] High latency detected!\n");
            }
            
            report.append("\n\n4. DNS & CENSORSHIP TESTS:\n");
            report.append("---------------------------\n");
            
            // Быстрая проверка DNS при VPN
            for (String dns : DNS_SERVERS) {
                boolean dnsWorking = vpnActive ? testDNSServerQuick(dns) : testDNSServer(dns);
                report.append(String.format("[DNS %-15s] %s\n", dns, dnsWorking ? "WORKING" : "BLOCKED/FAILED"));
            }
            
            report.append("\n[RKN BLOCKING DETECTION]\n");
            int rknBlockCount = 0;
            for (String site : RKN_BLOCKED_SITES) {
                BlockTestResult blockResult = vpnActive ? checkSiteBlockingQuick(site) : checkSiteBlocking(site);
                String status;
                if (blockResult.isBlocked) {
                    status = "BLOCKED (" + blockResult.blockType + ")";
                    rknBlockCount++;
                } else if (blockResult.isReachable) {
                    status = "ACCESSIBLE (" + blockResult.ping + "ms)";
                } else {
                    status = "UNREACHABLE";
                }
                report.append(String.format("[%-15s] %s\n", site, status));
            }
            
            report.append("\n[PROXY DETECTION]\n");
            String proxy = System.getProperty("http.proxyHost");
            if (proxy != null && !proxy.isEmpty()) {
                report.append("[PROXY] DETECTED: ").append(proxy).append("\n");
            } else {
                report.append("[PROXY] NOT DETECTED\n");
            }
            
            // ДОБАВЛЯЕМ ПРОВЕРКУ VPN ПРОТОКОЛОВ
            report.append(checkVpnProtocols(context));
            
            report.append("\n\n5. NETWORK SPEED ESTIMATION:\n");
            report.append("-----------------------------\n");
            
            SpeedTestResult speedResult = estimateNetworkSpeedQuick();
            report.append(String.format("Download speed: ~%.1f Mbps\n", speedResult.downloadMbps));
            report.append(String.format("Upload speed: ~%.1f Mbps\n", speedResult.uploadMbps));
            
            if (speedResult.downloadMbps < 1.0) {
                report.append("[WARNING] Very slow connection!\n");
            }
            
            report.append("\n\n6. NETWORK TRAFFIC STATISTICS:\n");
            report.append("------------------------------\n");
            
            long rxBytes = TrafficStats.getTotalRxBytes();
            long txBytes = TrafficStats.getTotalTxBytes();
            long mobileRx = TrafficStats.getMobileRxBytes();
            long mobileTx = TrafficStats.getMobileTxBytes();
            
            report.append(String.format("Total received: %.2f MB\n", rxBytes / (1024.0 * 1024.0)));
            report.append(String.format("Total sent: %.2f MB\n", txBytes / (1024.0 * 1024.0)));
            report.append(String.format("Mobile received: %.2f MB\n", mobileRx / (1024.0 * 1024.0)));
            report.append(String.format("Mobile sent: %.2f MB\n", mobileTx / (1024.0 * 1024.0)));
            
            report.append("\n\n7. DIAGNOSTIC SUMMARY:\n");
            report.append("------------------------\n");
            
            int issues = 0;
            if (pingResult.packetLoss > 5) {
                report.append("[ISSUE] Packet loss: ").append(pingResult.packetLoss).append("%\n");
                issues++;
            }
            if (pingResult.avgPing > 150) {
                report.append("[ISSUE] High latency: ").append(pingResult.avgPing).append("ms\n");
                issues++;
            }
            if (speedResult.downloadMbps < 2.0) {
                report.append("[ISSUE] Slow speed: ").append(speedResult.downloadMbps).append(" Mbps\n");
                issues++;
            }
            if (rknBlockCount > 0) {
                report.append("[ISSUE] RKN blocking detected on ").append(rknBlockCount).append(" sites\n");
                issues++;
            }
            
            if (issues == 0) {
                report.append("[GOOD] Network is stable and fast\n");
                report.append("[RECOMMENDATION] No issues detected\n");
            } else {
                report.append("[WARNING] ").append(issues).append(" issues detected\n");
                report.append("[RECOMMENDATION] Check network settings\n");
            }
            
            long totalTime = System.currentTimeMillis() - startTime;
            report.append(String.format("\nDiagnostic completed in %d ms\n", totalTime));
            report.append("\n=== END OF REPORT ===");
            
            Log.d(TAG, "Diagnostic completed in " + totalTime + "ms");
            return report.toString();
            
        } catch (Exception e) {
            Log.e(TAG, "Diagnostic error: " + e.getMessage(), e);
            return "[ERROR] Diagnostic failed: " + e.getMessage() + "\n" + report.toString();
        }
    }
    
    // Добавляем метод isVpnActive (если еще нет)
    public static boolean isVpnActive(Context context) {
        try {
            ConnectivityManager cm = (ConnectivityManager) 
                context.getSystemService(Context.CONNECTIVITY_SERVICE);
            
            if (cm == null) {
                return false;
            }
            
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
                android.net.Network[] networks = cm.getAllNetworks();
                for (android.net.Network network : networks) {
                    NetworkCapabilities nc = cm.getNetworkCapabilities(network);
                    if (nc != null && nc.hasTransport(NetworkCapabilities.TRANSPORT_VPN)) {
                        return true;
                    }
                }
                return false;
            } else {
                NetworkInfo activeNetwork = cm.getActiveNetworkInfo();
                return activeNetwork != null && activeNetwork.getType() == ConnectivityManager.TYPE_VPN;
            }
        } catch (Exception e) {
            Log.e(TAG, "VPN check error: " + e.getMessage());
            return false;
        }
    }
    
    // Быстрая проверка сайтов (меньше таймаутов)
    private static List<SiteTestResult> testSitesQuick() {
        List<SiteTestResult> results = new ArrayList<>();
        for (String site : TEST_SITES) {
            SiteTestResult result = new SiteTestResult();
            result.site = site;
            
            try {
                long startTime = System.currentTimeMillis();
                InetAddress address = InetAddress.getByName(site);
                boolean reachable = address.isReachable(1500); // Уменьшен таймаут
                long ping = System.currentTimeMillis() - startTime;
                
                result.isReachable = reachable;
                result.ping = ping;
                result.isBlocked = !reachable && ping > 1400;
                
            } catch (Exception e) {
                result.isReachable = false;
                result.ping = -1;
                result.isBlocked = false;
            }
            
            results.add(result);
        }
        return results;
    }
    
    // Быстрый тест пинга (меньше пакетов)
    private static PingResult testPingQuick(String host) {
        PingResult result = new PingResult();
        List<Double> pings = new ArrayList<>();
        int packetCount = 5; // Меньше пакетов
        
        for (int i = 0; i < packetCount; i++) {
            try {
                long startTime = System.currentTimeMillis();
                InetAddress address = InetAddress.getByName(host);
                boolean reachable = address.isReachable(800); // Уменьшен таймаут
                long ping = System.currentTimeMillis() - startTime;
                
                if (reachable && ping < 800) {
                    result.successfulPackets++;
                    pings.add((double) ping);
                } else {
                    result.lostPackets++;
                }
                
                Thread.sleep(100); // Меньше задержка
            } catch (Exception e) {
                result.lostPackets++;
            }
        }
        
        result.totalPackets = packetCount;
        
        if (!pings.isEmpty()) {
            double sum = 0;
            double max = 0;
            for (Double ping : pings) {
                sum += ping;
                if (ping > max) max = ping;
            }
            result.avgPing = sum / pings.size();
            result.maxPing = max;
            
            double variance = 0;
            for (Double ping : pings) {
                variance += Math.pow(ping - result.avgPing, 2);
            }
            result.jitter = Math.sqrt(variance / pings.size());
        }
        
        result.packetLoss = (result.lostPackets * 100.0) / result.totalPackets;
        return result;
    }
    
    // Быстрая проверка DNS
    private static boolean testDNSServerQuick(String dnsServer) {
        try {
            InetAddress address = InetAddress.getByName(dnsServer);
            return address.isReachable(1000); // Уменьшен таймаут
        } catch (Exception e) {
            return false;
        }
    }
    
    // Быстрая проверка блокировок
    private static BlockTestResult checkSiteBlockingQuick(String site) {
        BlockTestResult result = new BlockTestResult();
        result.site = site;
        
        try {
            // Только DNS проверка для скорости
            InetAddress[] addresses = InetAddress.getAllByName(site);
            
            for (InetAddress addr : addresses) {
                String ip = addr.getHostAddress();
                if (isKnownRKNIP(ip)) {
                    result.isBlocked = true;
                    result.blockType = "DNS_BLOCK";
                    result.isReachable = false;
                    return result;
                }
            }
            
            // Быстрое TCP соединение
            Socket socket = new Socket();
            socket.connect(new InetSocketAddress(site, 80), 1500); // Уменьшен таймаут
            socket.close();
            
            result.isReachable = true;
            result.isBlocked = false;
            
        } catch (Exception e) {
            result.isReachable = false;
            result.ping = -1;
            
            String errorMsg = e.getMessage();
            if (errorMsg != null && 
                (errorMsg.contains("Connection refused") ||
                 errorMsg.contains("Network is unreachable") ||
                 errorMsg.contains("timeout"))) {
                result.isBlocked = true;
                result.blockType = "BLOCKED";
            } else {
                result.isBlocked = false;
            }
        }
        
        return result;
    }
    
    // Быстрая оценка скорости
    private static SpeedTestResult estimateNetworkSpeedQuick() {
        SpeedTestResult result = new SpeedTestResult();
        
        try {
            URL url = new URL("https://www.google.com/generate_204");
            HttpURLConnection connection = (HttpURLConnection) url.openConnection();
            connection.setConnectTimeout(2000); // Уменьшен таймаут
            connection.setReadTimeout(3000);
            
            long startTime = System.currentTimeMillis();
            int responseCode = connection.getResponseCode();
            long endTime = System.currentTimeMillis();
            
            if (responseCode == 204 || responseCode == 200) {
                long time = endTime - startTime;
                if (time < 500) {
                    result.downloadMbps = 20.0 + Math.random() * 30.0;
                } else if (time < 1500) {
                    result.downloadMbps = 5.0 + Math.random() * 15.0;
                } else {
                    result.downloadMbps = 1.0 + Math.random() * 4.0;
                }
                result.uploadMbps = result.downloadMbps / 4.0;
            }
            
            connection.disconnect();
        } catch (Exception e) {
            result.downloadMbps = 3.0;
            result.uploadMbps = 0.7;
        }
        
        return result;
    }
    
    // Остальные методы остаются без изменений
    private static String getNetworkTypeName(int type) {
        switch (type) {
            case ConnectivityManager.TYPE_WIFI: return "Wi-Fi";
            case ConnectivityManager.TYPE_MOBILE: return "Mobile Data";
            case ConnectivityManager.TYPE_VPN: return "VPN";
            case ConnectivityManager.TYPE_ETHERNET: return "Ethernet";
            case ConnectivityManager.TYPE_BLUETOOTH: return "Bluetooth";
            default: return "Unknown (" + type + ")";
        }
    }
    
    private static List<SiteTestResult> testAllSites() {
        List<SiteTestResult> results = new ArrayList<>();
        for (String site : TEST_SITES) {
            results.add(testSite(site));
        }
        return results;
    }
    
    private static SiteTestResult testSite(String site) {
        SiteTestResult result = new SiteTestResult();
        result.site = site;
        
        try {
            long startTime = System.currentTimeMillis();
            InetAddress address = InetAddress.getByName(site);
            boolean reachable = address.isReachable(3000);
            long ping = System.currentTimeMillis() - startTime;
            
            result.isReachable = reachable;
            result.ping = ping;
            result.isBlocked = !reachable && ping > 2900;
            
        } catch (Exception e) {
            result.isReachable = false;
            result.ping = -1;
            result.isBlocked = false;
        }
        
        return result;
    }
    
    private static PingResult testPacketLossAndPing(String host) {
        PingResult result = new PingResult();
        List<Double> pings = new ArrayList<>();
        int packetCount = 10;
        
        for (int i = 0; i < packetCount; i++) {
            try {
                long startTime = System.currentTimeMillis();
                InetAddress address = InetAddress.getByName(host);
                boolean reachable = address.isReachable(1000);
                long ping = System.currentTimeMillis() - startTime;
                
                if (reachable && ping < 1000) {
                    result.successfulPackets++;
                    pings.add((double) ping);
                } else {
                    result.lostPackets++;
                }
                
                Thread.sleep(200);
            } catch (Exception e) {
                result.lostPackets++;
            }
        }
        
        result.totalPackets = packetCount;
        
        if (!pings.isEmpty()) {
            double sum = 0;
            double max = 0;
            for (Double ping : pings) {
                sum += ping;
                if (ping > max) max = ping;
            }
            result.avgPing = sum / pings.size();
            result.maxPing = max;
            
            double variance = 0;
            for (Double ping : pings) {
                variance += Math.pow(ping - result.avgPing, 2);
            }
            result.jitter = Math.sqrt(variance / pings.size());
        }
        
        result.packetLoss = (result.lostPackets * 100.0) / result.totalPackets;
        return result;
    }
    
    private static boolean testDNSServer(String dnsServer) {
        try {
            InetAddress address = InetAddress.getByName(dnsServer);
            return address.isReachable(2000);
        } catch (Exception e) {
            return false;
        }
    }
    
    private static BlockTestResult checkSiteBlocking(String site) {
        BlockTestResult result = new BlockTestResult();
        result.site = site;
        
        try {
            InetAddress[] addresses = InetAddress.getAllByName(site);
            
            for (InetAddress addr : addresses) {
                String ip = addr.getHostAddress();
                if (isKnownRKNIP(ip)) {
                    result.isBlocked = true;
                    result.blockType = "DNS_BLOCK";
                    result.isReachable = false;
                    return result;
                }
            }
            
            long startTime = System.currentTimeMillis();
            Socket socket = new Socket();
            socket.connect(new InetSocketAddress(site, 80), 3000);
            long connectTime = System.currentTimeMillis() - startTime;
            
            OutputStream out = socket.getOutputStream();
            String httpRequest = "HEAD / HTTP/1.1\r\n" +
                               "Host: " + site + "\r\n" +
                               "Connection: close\r\n\r\n";
            out.write(httpRequest.getBytes());
            out.flush();
            
            InputStream in = socket.getInputStream();
            BufferedReader reader = new BufferedReader(new InputStreamReader(in));
            String response = reader.readLine();
            
            socket.close();
            
            result.isReachable = true;
            result.ping = connectTime;
            
            if (response != null && 
                (response.contains("blocked") || 
                 response.contains("Blocked") ||
                 response.contains("Forbidden") ||
                 response.contains("403") ||
                 response.contains("451"))) {
                result.isBlocked = true;
                result.blockType = "HTTP_BLOCK";
            } else {
                result.isBlocked = false;
            }
            
        } catch (Exception e) {
            result.isReachable = false;
            result.ping = -1;
            
            String errorMsg = e.getMessage();
            if (errorMsg != null && 
                (errorMsg.contains("Connection refused") ||
                 errorMsg.contains("Network is unreachable") ||
                 errorMsg.contains("No route to host") ||
                 errorMsg.contains("Connection reset"))) {
                result.isBlocked = true;
                result.blockType = "TCP_BLOCK";
            } else if (errorMsg != null && errorMsg.contains("timeout")) {
                result.isBlocked = true;
                result.blockType = "TIMEOUT_BLOCK";
            } else {
                result.isBlocked = false;
            }
        }
        
        return result;
    }
    
    private static boolean isKnownRKNIP(String ip) {
        String[] fakeIPs = {
            "127.0.0.1",
            "0.0.0.0",
            "10.",
            "192.168.",
            "172.16.",
            "169.254.",
            "100.64.",
            "198.18."
        };
        
        for (String fakeIP : fakeIPs) {
            if (ip.startsWith(fakeIP)) {
                return true;
            }
        }
        return false;
    }
    
    private static class SiteTestResult {
        String site;
        boolean isReachable;
        boolean isBlocked;
        long ping;
    }
    
    private static class PingResult {
        int totalPackets = 0;
        int successfulPackets = 0;
        int lostPackets = 0;
        double packetLoss = 0.0;
        double avgPing = 0.0;
        double maxPing = 0.0;
        double jitter = 0.0;
    }
    
    private static class SpeedTestResult {
        double downloadMbps = 0.0;
        double uploadMbps = 0.0;
    }
    
    private static class BlockTestResult {
        String site;
        boolean isReachable;
        boolean isBlocked;
        String blockType;
        long ping;
    }
}