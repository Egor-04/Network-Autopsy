package com.UnknownGameStudio.NetworkAutopsy;

import android.content.Context;
import android.net.ConnectivityManager;
import android.net.NetworkCapabilities;
import android.net.NetworkInfo;
import android.telephony.TelephonyManager;
import android.util.Log;
import java.net.HttpURLConnection;
import java.net.InetAddress;
import java.net.Socket;
import java.net.URL;

public class AdvancedNetworkDiagnostic {
    private static final String TAG = "NetworkDiag";
    
    public static String quickDiagnose(Context context) {
        StringBuilder report = new StringBuilder();
        
        try {
            ConnectivityManager cm = (ConnectivityManager) 
                context.getSystemService(Context.CONNECTIVITY_SERVICE);
            
            if (cm == null) {
                return "ERROR: No network service\n";
            }
            
            NetworkInfo activeNetwork = cm.getActiveNetworkInfo();
            
            report.append("CONNECTION TYPE:\n");
            if (activeNetwork != null) {
                String type = getNetworkType(activeNetwork.getType());
                report.append(type).append("\n");
                
                boolean isConnected = activeNetwork.isConnected();
                report.append("STATUS: ").append(isConnected ? "CONNECTED" : "DISCONNECTED").append("\n");
                
                String providerName = getProviderName(context, activeNetwork.getType());
                report.append("PROVIDER: ").append(providerName).append("\n");
                
                boolean isVpn = false;
                if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.LOLLIPOP) {
                    android.net.Network[] networks = cm.getAllNetworks();
                    for (android.net.Network network : networks) {
                        NetworkCapabilities nc = cm.getNetworkCapabilities(network);
                        if (nc != null && nc.hasTransport(NetworkCapabilities.TRANSPORT_VPN)) {
                            isVpn = true;
                            break;
                        }
                    }
                } else {
                    isVpn = activeNetwork.getType() == ConnectivityManager.TYPE_VPN;
                }
                
                report.append("VPN: ").append(isVpn ? "ACTIVE" : "NOT ACTIVE").append("\n");
            } else {
                report.append("No active connection\n");
            }
            
            report.append("\n");
            
            report.append("\nINTERNET ACCESS:\n");
            
            boolean youtubeAccess = testWebsiteAccess("https://www.youtube.com", 5000);
            report.append("YouTube: ").append(youtubeAccess ? "ACCESSIBLE" : "BLOCKED").append("\n");
            
            boolean discordAccess = testWebsiteAccess("https://discord.com", 5000);
            report.append("Discord: ").append(discordAccess ? "ACCESSIBLE" : "BLOCKED").append("\n");
            
            boolean telegramAccess = testWebsiteAccess("https://web.telegram.org", 5000);
            report.append("Telegram: ").append(telegramAccess ? "ACCESSIBLE" : "BLOCKED").append("\n");
            
            boolean googleAccess = testWebsiteAccess("https://www.google.com", 3000);
            report.append("Google: ").append(googleAccess ? "ACCESSIBLE" : "BLOCKED").append("\n");
            
            report.append("\n");
            
            report.append("VPN PROTOCOLS:\n");
            checkVpnProtocol(report, "VLESS", 443);
            checkVpnProtocol(report, "VMESS", 8443);
            checkVpnProtocol(report, "TROJAN", 443);
            checkVpnProtocol(report, "SHADOWSOCKS", 8388);
            checkVpnProtocol(report, "WIREGUARD", 51820);
            
            report.append("\n");
            
            report.append("PING TEST:\n");
            long googlePing = testPing("8.8.8.8");
            report.append("Google DNS: ").append(googlePing > 0 ? googlePing + " ms" : "TIMEOUT").append("\n");
            
            long cloudflarePing = testPing("1.1.1.1");
            report.append("Cloudflare DNS: ").append(cloudflarePing > 0 ? cloudflarePing + " ms" : "TIMEOUT").append("\n");
            
            report.append("\nPACKET LOSS ESTIMATION:\n");
            int packetsSent = 5;
            int packetsReceived = 0;
            
            for (int i = 0; i < packetsSent; i++) {
                if (testPing("8.8.8.8") > 0) {
                    packetsReceived++;
                }
                try {
                    Thread.sleep(200);
                } catch (InterruptedException e) {
                }
            }
            
            float packetLoss = 100f * (packetsSent - packetsReceived) / packetsSent;
            report.append("Packet loss: ").append(String.format("%.1f", packetLoss)).append("%\n");
            
            if (packetLoss > 10) {
                report.append("WARNING: High packet loss\n");
            }
            
        } catch (Exception e) {
            report.append("ERROR: ").append(e.getMessage());
            Log.e(TAG, "Diagnostic error: " + e.getMessage());
        }
        
        return report.toString();
    }
    
    private static String getNetworkType(int type) {
        switch (type) {
            case ConnectivityManager.TYPE_WIFI: return "Wi-Fi";
            case ConnectivityManager.TYPE_MOBILE: return "Mobile";
            case ConnectivityManager.TYPE_VPN: return "VPN";
            case ConnectivityManager.TYPE_ETHERNET: return "Ethernet";
            default: return "Unknown";
        }
    }
    
    private static String getProviderName(Context context, int networkType) {
        try {
            if (networkType == ConnectivityManager.TYPE_WIFI) {
                android.net.wifi.WifiManager wifiManager = 
                    (android.net.wifi.WifiManager) context.getApplicationContext().getSystemService(Context.WIFI_SERVICE);
                if (wifiManager != null) {
                    android.net.wifi.WifiInfo wifiInfo = wifiManager.getConnectionInfo();
                    if (wifiInfo != null) {
                        String ssid = wifiInfo.getSSID();
                        if (ssid != null && !ssid.equals("<unknown ssid>") && !ssid.equals("0x")) {
                            return ssid.replace("\"", "") + " (Wi-Fi)";
                        }
                    }
                }
                return "Wi-Fi Network";
                
            } else if (networkType == ConnectivityManager.TYPE_MOBILE) {
                TelephonyManager telephonyManager = 
                    (TelephonyManager) context.getSystemService(Context.TELEPHONY_SERVICE);
                if (telephonyManager != null) {
                    String networkOperator = telephonyManager.getNetworkOperatorName();
                    if (networkOperator != null && !networkOperator.isEmpty()) {
                        return networkOperator;
                    }
                }
                return "Mobile Network";
            }
        } catch (Exception e) {
            Log.e(TAG, "Error getting provider name: " + e.getMessage());
        }
        return "Unknown";
    }
    
    private static boolean testWebsiteAccess(String url, int timeout) {
        HttpURLConnection connection = null;
        try {
            URL website = new URL(url);
            connection = (HttpURLConnection) website.openConnection();
            connection.setRequestMethod("HEAD");
            connection.setConnectTimeout(timeout);
            connection.setReadTimeout(timeout);
            connection.setRequestProperty("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            connection.setRequestProperty("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            connection.setRequestProperty("Accept-Language", "en-US,en;q=0.5");
            
            int responseCode = connection.getResponseCode();
            return responseCode == 200 || responseCode == 204 || responseCode == 301 || responseCode == 302;
        } catch (Exception e) {
            Log.w(TAG, "Failed to access " + url + ": " + e.getMessage());
            
            return testWebsiteAccessWithGet(url, timeout);
        } finally {
            if (connection != null) {
                connection.disconnect();
            }
        }
    }
    
    private static boolean testWebsiteAccessWithGet(String url, int timeout) {
        HttpURLConnection connection = null;
        try {
            URL website = new URL(url);
            connection = (HttpURLConnection) website.openConnection();
            connection.setRequestMethod("GET");
            connection.setConnectTimeout(timeout);
            connection.setReadTimeout(timeout);
            connection.setInstanceFollowRedirects(true);
            connection.setRequestProperty("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            int responseCode = connection.getResponseCode();
            return responseCode == 200 || responseCode == 301 || responseCode == 302;
        } catch (Exception e) {
            Log.w(TAG, "GET also failed for " + url + ": " + e.getMessage());
            return false;
        } finally {
            if (connection != null) {
                connection.disconnect();
            }
        }
    }
    
    private static void checkVpnProtocol(StringBuilder report, String protocol, int port) {
        try {
            Socket socket = new Socket();
            socket.connect(new java.net.InetSocketAddress("google.com", port), 2000);
            boolean connected = socket.isConnected();
            socket.close();
            report.append(protocol).append(": ").append(connected ? "OPEN" : "FILTERED").append("\n");
        } catch (Exception e) {
            report.append(protocol).append(": BLOCKED\n");
        }
    }
    
    private static long testPing(String host) {
        try {
            long start = System.currentTimeMillis();
            InetAddress address = InetAddress.getByName(host);
            boolean reachable = address.isReachable(3000);
            long end = System.currentTimeMillis();
            return reachable ? (end - start) : -1;
        } catch (Exception e) {
            return -1;
        }
    }
}