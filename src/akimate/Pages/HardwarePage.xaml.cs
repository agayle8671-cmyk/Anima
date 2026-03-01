using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Management;

namespace akimate.Pages;

public sealed partial class HardwarePage : Page
{
    public HardwarePage()
    {
        this.InitializeComponent();
        DetectHardware();
    }

    private void DetectHardware()
    {
        try
        {
            // GPU Detection
            using var gpuSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (ManagementObject gpu in gpuSearcher.Get())
            {
                GpuName.Text = gpu["Name"]?.ToString() ?? "Unknown GPU";
                var vramBytes = Convert.ToInt64(gpu["AdapterRAM"] ?? 0);
                var vramGB = vramBytes / (1024.0 * 1024.0 * 1024.0);
                GpuVram.Text = $"VRAM: {vramGB:F1} GB";
                GpuDriver.Text = $"Driver: {gpu["DriverVersion"]}";

                // Model recommendation based on VRAM
                UpdateRecommendation(vramGB);
                break; // Use first GPU
            }

            // CPU Detection
            using var cpuSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (ManagementObject cpu in cpuSearcher.Get())
            {
                CpuName.Text = cpu["Name"]?.ToString() ?? "Unknown CPU";
                var cores = cpu["NumberOfCores"]?.ToString() ?? "?";
                var threads = cpu["NumberOfLogicalProcessors"]?.ToString() ?? "?";
                CpuCores.Text = $"{cores} Cores / {threads} Threads";
                break;
            }

            // RAM Detection
            using var ramSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (ManagementObject sys in ramSearcher.Get())
            {
                var ramBytes = Convert.ToInt64(sys["TotalPhysicalMemory"] ?? 0);
                var ramGB = ramBytes / (1024.0 * 1024.0 * 1024.0);
                RamInfo.Text = $"{ramGB:F1} GB";
                break;
            }
        }
        catch (Exception ex)
        {
            GpuName.Text = $"Detection failed: {ex.Message}";
        }
    }

    private void UpdateRecommendation(double vramGB)
    {
        RecommendationInfo.IsOpen = true;

        if (vramGB >= 24)
        {
            RecommendationInfo.Severity = InfoBarSeverity.Success;
            RecommendationInfo.Title = "Optimal: Full Precision";
            RecommendationInfo.Message = "Your GPU supports FP16 full-precision models. Maximum quality available for all local AI tasks.";
        }
        else if (vramGB >= 12)
        {
            RecommendationInfo.Severity = InfoBarSeverity.Success;
            RecommendationInfo.Title = "Good: 8-bit Quantization";
            RecommendationInfo.Message = "Your GPU supports 8-bit quantized models. Excellent quality for most local AI tasks.";
        }
        else if (vramGB >= 6)
        {
            RecommendationInfo.Severity = InfoBarSeverity.Informational;
            RecommendationInfo.Title = "Moderate: 4-bit Quantization";
            RecommendationInfo.Message = "Your GPU supports 4-bit GGUF models. Good quality, may need cloud API for demanding tasks.";
        }
        else
        {
            RecommendationInfo.Severity = InfoBarSeverity.Warning;
            RecommendationInfo.Title = "Limited: Cloud API Recommended";
            RecommendationInfo.Message = "Your GPU has limited VRAM. Cloud API mode (Runway Gen-3) is recommended for best results.";
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        DetectHardware();
    }
}
