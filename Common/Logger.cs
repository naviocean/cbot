using System;

namespace RedWave.Common
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    public class CLogger
    {
        private string _moduleName;
        private LogLevel _level;
        private Action<string> _printAction;

        public CLogger()
        {
            _moduleName = "Common";
            _level = LogLevel.Info;
            _printAction = null;
        }

        public void Init(string moduleName, LogLevel level, Action<string> printAction)
        {
            _moduleName = moduleName;
            _level = level;
            _printAction = printAction;
        }

        private void WriteLog(LogLevel msgLevel, string message)
        {
            if (msgLevel < _level || _printAction == null)
                return;

            string timestamp = DateTime.UtcNow.ToString("yyyy.MM.dd HH:mm:ss.fff");
            string levelStr = msgLevel.ToString().ToUpper();
            string formattedMessage = $"[{timestamp}] [{_moduleName}] [{levelStr}] {message}";
            
            _printAction(formattedMessage);
        }

        public void Debug(string message) => WriteLog(LogLevel.Debug, message);
        public void Info(string message) => WriteLog(LogLevel.Info, message);
        public void Warn(string message) => WriteLog(LogLevel.Warn, message);
        public void Error(string message) => WriteLog(LogLevel.Error, message);
    }
}
