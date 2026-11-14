using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NaturplayCalculator.History;
using Windows.Storage;

namespace NaturplayCalculator.Services
{
    public sealed class SessionStore
    {
        private const string FileName = "calculator-session-v3.json";
        private const string BackupName = "calculator-session-v3.backup.json";
        private readonly SemaphoreSlim writes = new SemaphoreSlim(1, 1);
        private bool primaryWasValid;
        public bool RecoveredFromBackup { get; private set; }
        public bool HadReadError { get; private set; }

        public async Task<CalculatorSession> LoadAsync()
        {
            var session = await ReadAsync(FileName);
            primaryWasValid = session != null;
            if (session != null) return session;
            session = await ReadAsync(BackupName);
            RecoveredFromBackup = session != null;
            return session ?? await ReadAsync("calculator-session-v2.json") ?? await ReadAsync("calculator-session-v2.backup.json");
        }
        private async Task<CalculatorSession> ReadAsync(string name)
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.TryGetItemAsync(name) as StorageFile;
                if (file == null) return null;
                if ((await file.GetBasicPropertiesAsync()).Size > SessionCodec.MaxFileLength * 4L) throw new IOException("Session file is too large.");
                string text = await FileIO.ReadTextAsync(file);
                return await Task.Run(() => SessionCodec.Read(text));
            }
            catch (Exception ex)
            {
                HadReadError = true;
                Settings.ReportError("Load session " + name, ex);
                return null;
            }
        }
        public async Task SaveAsync(string snapshot)
        {
            await writes.WaitAsync();
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                var temporary = await folder.CreateFileAsync(FileName + ".tmp", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(temporary, snapshot);
                var previous = await folder.TryGetItemAsync(FileName) as StorageFile;
                if (previous != null)
                {
                    if (primaryWasValid) await previous.CopyAsync(folder, BackupName, NameCollisionOption.ReplaceExisting);
                    else await previous.CopyAsync(folder, "calculator-session-corrupt-" + DateTime.UtcNow.Ticks + ".json", NameCollisionOption.GenerateUniqueName);
                }
                await temporary.RenameAsync(FileName, NameCollisionOption.ReplaceExisting);
                primaryWasValid = true;
            }
            finally { writes.Release(); }
        }
    }
}
