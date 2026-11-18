using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NaturplayCalculator.History;

namespace NaturplayCalculator.Presentation
{
    /// <summary>Atomic v3 storage shared by desktop hosts; damaged data is retained.</summary>
    public sealed class FileSessionStore
    {
        private readonly string directory;
        private readonly SemaphoreSlim writes = new SemaphoreSlim(1, 1);
        private bool primaryWasValid;
        public bool RecoveredFromBackup { get; private set; }
        public bool HadReadError { get; private set; }
        public const string FileName = "calculator-session-v3.json";
        public const string BackupName = "calculator-session-v3.backup.json";

        public FileSessionStore(string directory) => this.directory = directory;

        public async Task<CalculatorSession> LoadAsync()
        {
            var session = await ReadAsync(FileName);
            primaryWasValid = session != null;
            if (session != null) return session;
            session = await ReadAsync(BackupName);
            RecoveredFromBackup = session != null;
            return session ?? new CalculatorSession();
        }

        private async Task<CalculatorSession> ReadAsync(string name)
        {
            string path = Path.Combine(directory, name);
            if (!File.Exists(path)) return null;
            try
            {
                if (new FileInfo(path).Length > SessionCodec.MaxFileLength * 4L) throw new IOException("Session is too large.");
                string text = await File.ReadAllTextAsync(path);
                return await Task.Run(() => SessionCodec.Read(text));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException || ex is ArgumentException || ex is Newtonsoft.Json.JsonException || ex is InvalidCastException || ex is Model.MathException)
            {
                HadReadError = true;
                return null;
            }
        }

        public async Task SaveAsync(string snapshot)
        {
            await writes.WaitAsync();
            try
            {
                Directory.CreateDirectory(directory);
                string primary = Path.Combine(directory, FileName);
                string temporary = primary + ".tmp";
                await File.WriteAllTextAsync(temporary, snapshot);
                if (File.Exists(primary))
                    File.Copy(primary, Path.Combine(directory, primaryWasValid ? BackupName : "calculator-session-corrupt-" + DateTime.UtcNow.Ticks + ".json"), true);
                File.Move(temporary, primary, true);
                primaryWasValid = true;
            }
            finally { writes.Release(); }
        }
    }
}
