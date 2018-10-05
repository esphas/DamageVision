using System;
using System.IO;

namespace DamageVision {
    public static class Utils {

        private static readonly string logFile = $@"{Environment.CurrentDirectory}\DamageVision.log";
        private static readonly string[] logLevels = { "DEBUG", "INFO", "WARNING", "ERROR", "FATAL" };

        public static void ClearLog() {
            File.Delete(logFile);
        }

        public static void Log(int level, string message) {
            //
        }
    }
}
