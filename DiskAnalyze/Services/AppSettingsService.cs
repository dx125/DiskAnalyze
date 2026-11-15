using DiskAnalyze.Application.Settings;
using System.IO;

namespace DiskAnalyze.Services
{
    /// <summary>
    /// The app's current settings. A change takes effect immediately (through <see cref="Changed"/>) and is
    /// saved right away; there is no Save button.
    /// </summary>
    public sealed class AppSettingsService
    {
        private readonly SettingsStore _store;

        public AppSettingsService(SettingsStore store)
        {
            _store = store;
            Current = store.Load();
        }

        public AppSettings Current { get; private set; }

        public string FilePath => _store.FilePath;

        /// <summary>Raised with the new settings after every change, before they are saved.</summary>
        public event EventHandler<AppSettings>? Changed;

        /// <summary>
        /// Applies <paramref name="change"/> and saves the result. Returns null on success, or a message when
        /// the file couldn't be written; the change still applies for this session.
        /// </summary>
        public string? Update(Func<AppSettings, AppSettings> change)
        {
            var next = change(Current).Normalize();
            if (next == Current)
                return null;

            Current = next;
            Changed?.Invoke(this, next);

            try
            {
                _store.Save(next);
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return $"Couldn't save settings: {ex.Message}";
            }
        }
    }
}
