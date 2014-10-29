namespace Scholar.Common.Tools
{
    #region Using

    using System;
    using System.IO;
    using System.Reflection;
    using System.Text;

    #endregion

    /// <summary>
    ///   Статический класс для запипси сообщений в лог
    /// </summary>
    public sealed class Log : IDisposable
    {
        #region Constants and Fields

        private static readonly Log LogInstance = new Log(); 

        private readonly StreamWriter _logWriter;

        #endregion

        #region Constructors and Destructors

        public Log()
        {
            _logWriter = GetStreamWriter(DateTime.Now);
        }

        #endregion

        #region Private Methods

        private static StreamWriter GetStreamWriter(DateTime dateTime)
        {
            var path = Assembly.GetExecutingAssembly().Location;
            path = Path.GetDirectoryName(path);

            path = Path.Combine(path ?? "C:\\", "Logs");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            path = Path.Combine(path, string.Format("{0:yyyy-MM}", dateTime));
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            path = Path.Combine(path, string.Format("{0:dd}", dateTime));
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            path = Path.Combine(path, string.Format("{0:HH-mm-ss}.txt", dateTime));

            return new StreamWriter(path, true, Encoding.Unicode);
        }

        #endregion

        #region Public Methods

        public static Log Current
        {
            get
            {
                return LogInstance;
            }
        }

        /// <summary>
        ///   Ошибка - исключение
        /// </summary>
        /// <param name = "exception"></param>
        public void Error(Exception exception)
        {
            _logWriter.WriteLine("{0:yyyy-MM-dd HH:mm:ss} (Error): {1}", DateTime.Now, exception);
            _logWriter.Flush();
        }

        /// <summary>
        ///   Логическая ошибка
        /// </summary>
        /// <param name = "message"></param>
        public void Error(string message)
        {
            _logWriter.WriteLine("{0:yyyy-MM-dd HH:mm:ss} (Error): {1}", DateTime.Now, message);
            _logWriter.Flush();
        }

        /// <summary>
        ///   Информационное сообщение
        /// </summary>
        /// <param name = "message"></param>
        public void Info(string message)
        {
            _logWriter.WriteLine("{0:yyyy-MM-dd HH:mm:ss} (Info): {1}", DateTime.Now, message);
            _logWriter.Flush();
        }

        /// <summary>
        ///   Ошибка - исключение
        /// </summary>
        /// <param name = "exception"></param>
        /// <param name="dateTime"></param>
        public static void Error(Exception exception, DateTime dateTime)
        {
            using (var logWriter = GetStreamWriter(dateTime))
            {
                logWriter.WriteLine("{0:yyyy-MM-dd HH:mm:ss} (Error): {1}", DateTime.Now, exception);
                logWriter.Flush();
            }
        }

        public static void Error(string message, DateTime dateTime)
        {
            using (var logWriter = GetStreamWriter(dateTime))
            {
                logWriter.WriteLine("{0:yyyy-MM-dd HH:mm:ss} (Error): {1}", DateTime.Now, message);
                logWriter.Flush();
            }
        }

        public static void Info(string message, DateTime dateTime)
        {
            using (var logWriter = GetStreamWriter(dateTime))
            {
                logWriter.WriteLine("{0:yyyy-MM-dd HH:mm:ss} (Info): {1}", DateTime.Now, message);
                logWriter.Flush();
            }
        }

        public void Dispose()
        {
            _logWriter.Dispose();
        }

        #endregion
    }
}
