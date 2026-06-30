namespace DiskAnalyze.Core
{
    public static class FileSizeFormatter
    {
        private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

        public static string Format(long bytes)
        {
            double value = bytes;
            int unit = 0;

            while (value >= 1024 && unit < Units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{value:0.##} {Units[unit]}";
        }
    }
}
