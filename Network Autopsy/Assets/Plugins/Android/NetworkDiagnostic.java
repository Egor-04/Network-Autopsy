package com.UnknownGameStudio.NetworkAutopsy;

import android.content.Context;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.net.NetworkInfo;
import android.net.NetworkRequest;
import android.os.Build;
import android.util.Log;
import java.net.InetAddress;
import java.net.NetworkInterface;
import java.util.Collections;
import java.util.List;

public class NetworkDiagnostic {
    
    private static final String TAG = "UnityNetworkDiagnostic"; // ДОБАВЬТЕ КАВЫЧКИ!
    private Context unityContext;
    private ConnectivityManager connectivityManager;
    
    // Конструктор
    public NetworkDiagnostic(Context context) {
        this.unityContext = context;
        this.connectivityManager = (ConnectivityManager) 
            context.getSystemService(Context.CONNECTIVITY_SERVICE);
    }
    
    // 1. Проверка базовой доступности сети
    public String checkBasicConnectivity() {
        try {
            if (connectivityManager == null) {
                return "ERROR: ConnectivityManager is null";
            }
            
            NetworkInfo activeNetwork = connectivityManager.getActiveNetworkInfo();
            boolean isConnected = activeNetwork != null && activeNetwork.isConnectedOrConnecting();
            
            StringBuilder result = new StringBuilder();
            result.append("Basic Connectivity: ").append(isConnected ? "CONNECTED" : "DISCONNECTED").append("\n");
            
            if (activeNetwork != null) {
                result.append("Network Type: ").append(activeNetwork.getTypeName()).append("\n");
                result.append("Subtype: ").append(activeNetwork.getSubtypeName()).append("\n");
                result.append("Detailed State: ").append(activeNetwork.getDetailedState()).append("\n");
            }
            
            return result.toString();
        } catch (Exception e) {
            return "ERROR in checkBasicConnectivity: " + e.getMessage();
        }
    }
    
    // 2. Проверка доступности интернета (не просто сети)
    public String checkInternetAccess() {
        StringBuilder result = new StringBuilder();
        
        try {
            // Проверка через DNS
            InetAddress ipAddr = InetAddress.getByName("google.com");
            boolean hasInternet = !ipAddr.equals("");
            result.append("DNS Test (google.com): ").append(hasInternet ? "SUCCESS" : "FAILED").append("\n");
            
            // Проверка через NetworkCapabilities (API 21+)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                Network network = connectivityManager.getActiveNetwork();
                if (network != null) {
                    NetworkCapabilities capabilities = 
                        connectivityManager.getNetworkCapabilities(network);
                    
                    if (capabilities != null) {
                        result.append("Network Capabilities:\n");
                        result.append("  - INTERNET: ").append(capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)).append("\n");
                        result.append("  - VALIDATED: ").append(capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED)).append("\n");
                        result.append("  - NOT_METERED: ").append(capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_METERED)).append("\n");
                        result.append("  - NOT_ROAMING: ").append(capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_ROAMING)).append("\n");
                    }
                }
            }
        } catch (Exception e) {
            result.append("ERROR in checkInternetAccess: ").append(e.getMessage()).append("\n");
        }
        
        return result.toString();
    }
    
    // 3. Получение IP адресов
    public String getIPAddresses() {
        StringBuilder result = new StringBuilder();
        
        try {
            List<NetworkInterface> interfaces = Collections.list(NetworkInterface.getNetworkInterfaces());
            for (NetworkInterface intf : interfaces) {
                List<InetAddress> addrs = Collections.list(intf.getInetAddresses());
                for (InetAddress addr : addrs) {
                    if (!addr.isLoopbackAddress()) {
                        String sAddr = addr.getHostAddress();
                        boolean isIPv4 = sAddr.indexOf(':') < 0;
                        
                        if (isIPv4) {
                            result.append("IPv4: ").append(sAddr).append(" (Interface: ").append(intf.getName()).append(")\n");
                        } else {
                            // IPv6
                            int delim = sAddr.indexOf('%');
                            String ipv6Addr = delim < 0 ? sAddr : sAddr.substring(0, delim);
                            result.append("IPv6: ").append(ipv6Addr).append("\n");
                        }
                    }
                }
            }
        } catch (Exception e) {
            result.append("ERROR getting IP addresses: ").append(e.getMessage()).append("\n");
        }
        
        return result.toString().isEmpty() ? "No IP addresses found" : result.toString();
    }
    
    // 4. Расширенная диагностика
    public String runFullDiagnostic() {
        StringBuilder report = new StringBuilder();
        report.append("=== NETWORK DIAGNOSTIC REPORT ===\n\n");
        
        report.append("1. BASIC CONNECTIVITY:\n");
        report.append(checkBasicConnectivity()).append("\n");
        
        report.append("2. INTERNET ACCESS:\n");
        report.append(checkInternetAccess()).append("\n");
        
        report.append("3. IP ADDRESSES:\n");
        report.append(getIPAddresses()).append("\n");
        
        report.append("4. DEVICE INFO:\n");
        report.append("SDK Version: ").append(Build.VERSION.SDK_INT).append("\n");
        report.append("Device: ").append(Build.MANUFACTURER).append(" ").append(Build.MODEL).append("\n");
        
        report.append("\n=== END REPORT ===");
        
        // Логируем в logcat
        Log.i(TAG, report.toString());
        
        return report.toString();
    }
    
    // 5. Вспомогательный метод для Unity (статический вызов)
    public static String quickDiagnose(Context context) {
        NetworkDiagnostic diagnostic = new NetworkDiagnostic(context);
        return diagnostic.runFullDiagnostic();
    }
}