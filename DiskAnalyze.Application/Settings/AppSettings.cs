using System.Text.Json.Serialization;

namespace DiskAnalyze.Application.Settings
{
    /// <summary>Colour theme. <see cref="System"/> follows the Windows app mode and switches with it.</summary>
    public enum ThemePreference
    {
        System,
        Light,
        Dark
    }

    /// <summary>Photo content filters (Feature B). See DEVELOPMENT.md §6.3.</summary>
    public enum PhotoContentAnalysisMode
    {
        Auto,
        Enabled,
        Disabled
    }

    /// <summary>Whether the app may load ONNX Runtime and the CLIP model at all. See DEVELOPMENT.md §6.3.</summary>
    public enum ClipOnnxMode
    {
        Auto,
        Allow,
        Deny
    }

    /// <summary>ONNX Runtime backend for CLIP. See DEVELOPMENT.md §6.3.</summary>
    public enum ExecutionProviderPreference
    {
        Auto,
        [JsonStringEnumMemberName("directml")] DirectML,
        Cuda,
        Cpu
    }

    /// <summary>
    /// User settings, stored by <see cref="SettingsStore"/>. Immutable: change a value with <c>with</c>
    /// and save the copy.
    /// </summary>
    public sealed record AppSettings
    {
        public const int MinPieSlices = 3;

        /// <summary>The diagram palette has eight colours; more slices would repeat one.</summary>
        public const int MaxPieSlices = 8;

        public const int DefaultPieSlices = 5;

        public ThemePreference Theme { get; init; } = ThemePreference.System;

        /// <summary>Scan <see cref="StartupScanPath"/> as soon as the main window opens.</summary>
        public bool ScanOnStartup { get; init; } = true;

        /// <summary>Drive or folder scanned at startup; null means all fixed drives.</summary>
        public string? StartupScanPath { get; init; }

        /// <summary>Largest items shown as pie slices; the rest go to "Other".</summary>
        public int PieSlices { get; init; } = DefaultPieSlices;

        public PhotoAnalysisSettings PhotoAnalysis { get; init; } = new();

        /// <summary>Brings values from a hand-edited or older file back into range.</summary>
        public AppSettings Normalize() => this with
        {
            PieSlices = Math.Clamp(PieSlices, MinPieSlices, MaxPieSlices),
            StartupScanPath = string.IsNullOrWhiteSpace(StartupScanPath) ? null : StartupScanPath,
            PhotoAnalysis = PhotoAnalysis ?? new PhotoAnalysisSettings()
        };
    }

    /// <summary>
    /// The user-settings layer of the photo-analysis gate (DEVELOPMENT.md §6.3). Not used until the
    /// photo browser ships; the names match the <c>features</c> section of <c>machine.local.json</c>.
    /// </summary>
    public sealed record PhotoAnalysisSettings
    {
        public PhotoContentAnalysisMode PhotoContentAnalysis { get; init; } = PhotoContentAnalysisMode.Auto;

        public ClipOnnxMode ClipOnnx { get; init; } = ClipOnnxMode.Auto;

        public ExecutionProviderPreference ExecutionProvider { get; init; } = ExecutionProviderPreference.Auto;
    }
}
