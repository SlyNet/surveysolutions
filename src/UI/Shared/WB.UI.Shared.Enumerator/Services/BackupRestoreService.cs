﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using SQLite;
using SQLitePCL;
using WB.Core.GenericSubdomains.Portable.Implementation;
using WB.Core.GenericSubdomains.Portable.Services;
using WB.Core.Infrastructure.FileSystem;
using WB.Core.Infrastructure.HttpServices.HttpClient;
using WB.Core.Infrastructure.HttpServices.Services;
using WB.Core.SharedKernels.DataCollection.Implementation;
using WB.Core.SharedKernels.DataCollection.Services;
using WB.Core.SharedKernels.Enumerator.Services;
using WB.Core.SharedKernels.Enumerator.Services.Infrastructure;
using WB.Core.SharedKernels.Enumerator.Utils;

namespace WB.UI.Shared.Enumerator.Services
{
    public class BackupRestoreService : IBackupRestoreService
    {
        private readonly IEnumeratorArchiveUtils archiver;
        private readonly IFileSystemAccessor fileSystemAccessor;
        private readonly ILogger logger;
        private readonly IPermissions permissions;
        private readonly IDeviceSettings deviceSettings;
        private readonly IRestService restService;
        private readonly IPrincipal principal;
        private readonly ISecureStorage secureStorage;
        private readonly IEncryptionService encryptionService;
        private readonly ISerializer serializer;
        private readonly IUserInteractionService userInteractionService;
        private readonly string sendTabletInfoRelativeUrl;
        private readonly string privateStorage;

        public BackupRestoreService(
            IEnumeratorArchiveUtils archiver,
            IFileSystemAccessor fileSystemAccessor,
            ILogger logger,
            string privateStorage, 
            IPermissions permissions,
            IDeviceSettings deviceSettings,
            IRestService restService,
            IPrincipal principal,
            ISecureStorage secureStorage,
            IEncryptionService encryptionService,
            ISerializer serializer,
            IUserInteractionService userInteractionService,
            string sendTabletInfoRelativeUrl)
        {
            this.archiver = archiver;
            this.fileSystemAccessor = fileSystemAccessor;
            this.logger = logger;
            this.privateStorage = privateStorage;
            this.permissions = permissions;
            this.deviceSettings = deviceSettings;
            this.restService = restService;
            this.principal = principal;
            this.secureStorage = secureStorage;
            this.encryptionService = encryptionService;
            this.serializer = serializer;
            this.userInteractionService = userInteractionService;
            this.sendTabletInfoRelativeUrl = sendTabletInfoRelativeUrl;
        }

        public async Task<string> BackupAsync()
        {
            await this.permissions.AssureHasPermissionOrThrow<StoragePermission>().ConfigureAwait(false);
            return await this.BackupAsync(this.privateStorage).ConfigureAwait(false);
        }

        public async Task<string> BackupAsync(string backupToFolderPath)
        {
            await this.permissions.AssureHasPermissionOrThrow<StoragePermission>().ConfigureAwait(false);

            if (!this.fileSystemAccessor.IsDirectoryExists(backupToFolderPath))
                this.fileSystemAccessor.CreateDirectory(backupToFolderPath);

            var timestamp = $"{DateTime.Now:s}".Replace(":", "_");
            var backupFilePath = this.fileSystemAccessor.CombinePath(backupToFolderPath, $"backup-{timestamp}.zip");

            var backupTempFolder = this.fileSystemAccessor.CombinePath(backupToFolderPath,  $"temp-backup-{timestamp}");

            if (!this.fileSystemAccessor.IsDirectoryExists(backupTempFolder))
                this.fileSystemAccessor.CreateDirectory(backupTempFolder);

            try
            {
                this.CreateDeviceInfoFile();
                this.CreateFilesListInfoFile();

                await Task.Run(this.BackupSqliteDbs).ConfigureAwait(false);

                this.fileSystemAccessor.CopyFileOrDirectory(this.privateStorage, backupTempFolder, false,
                    new[] {".log", ".dll", ".back", ".info", ".dat", ".attachment"});
                
                var backupFolderFilesPath = this.fileSystemAccessor.CombinePath(backupTempFolder, "files");

                this.EncryptKeyStore(backupFolderFilesPath);

                await this.archiver.ZipDirectoryToFileAsync(backupFolderFilesPath, backupFilePath)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex.Message, ex);
                throw;
            }
            finally
            {
                this.Cleanup();
                Directory.Delete(backupTempFolder, true);
            }

            return backupFilePath;
        }

        public async Task<RestorePackageInfo> GetRestorePackageInfo(string restoreFolder)
        {
            await this.permissions.AssureHasPermissionOrThrow<StoragePermission>().ConfigureAwait(false);

            if (!this.fileSystemAccessor.IsDirectoryExists(restoreFolder))
                this.fileSystemAccessor.CreateDirectory(restoreFolder);

            string[] filesInRestoreFolder = this.fileSystemAccessor.GetFilesInDirectory(restoreFolder);

            if (filesInRestoreFolder.Length > 0)
            {
                RestorePackageInfo result = new RestorePackageInfo();
                result.FileLocation = filesInRestoreFolder[0];
                result.FileSize = this.fileSystemAccessor.GetFileSize(result.FileLocation);
                result.FileCreationDate = this.fileSystemAccessor.GetCreationTime(result.FileLocation);
                return result;
            }

            return null;
        }

        public async Task SendBackupAsync(string filePath, CancellationToken token)
        {
            var backupHeaders = new Dictionary<string, string>()
            {
                {"DeviceId", this.deviceSettings.GetDeviceId()},
            };

            using (var fileStream = this.fileSystemAccessor.ReadFile(filePath))
            {
                try
                {
                    await this.restService.SendStreamAsync(
                        stream: fileStream,
                        customHeaders: backupHeaders,
                        url: this.sendTabletInfoRelativeUrl,
                        credentials:
                        this.principal.IsAuthenticated
                            ? new RestCredentials
                            {
                                Login = this.principal.CurrentUserIdentity.Name,
                                Token = this.principal.CurrentUserIdentity.Token,
                                Workspace = this.principal.CurrentUserIdentity.Workspace
                            }
                            : null,
                        token: token);
                }
                catch (RestException e)
                {
                    throw e.ToSynchronizationException();
                }
            }
        }

        private void Cleanup()
        {
            foreach (var tempBackupFile in Directory.GetFiles(this.privateStorage, "*.sqlite3.back", SearchOption.AllDirectories))
            {
                File.Delete(tempBackupFile);
            }
        }

        private void BackupSqliteDbs()
        {
            foreach (var dbPathToBackup in Directory.GetFiles(this.privateStorage, "*.sqlite3", SearchOption.AllDirectories))
            {
                try
                {
                    string destDBPath;
                    var backupConnectionString = new SQLiteConnectionString(dbPathToBackup, SQLiteOpenFlags.ReadOnly, true, null);
                    using (var connection = new SQLiteConnectionWithLock(backupConnectionString))
                    {
                        destDBPath = $"{connection.DatabasePath}.{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss-fff}";

                        CreateDatabaseBackup(connection, destDBPath);
                    }

                    var destFileName = Path.ChangeExtension(destDBPath, "back");
                    if (File.Exists(destFileName))
                    {
                        File.Delete(destFileName);
                    }

                    File.Move(destDBPath, destFileName);
                }
                catch (Exception ex)
                {
                    this.logger.Error(ex.Message, ex);
                    File.Copy(dbPathToBackup, $"{dbPathToBackup}.back");
                }
            }
        }

        private void CreateDatabaseBackup(SQLiteConnectionWithLock connection, string destDBPath)
        {
            sqlite3 destDB;
            SQLite3.Result destKbResult = (SQLite3.Result)
                raw.sqlite3_open_v2(destDBPath, out destDB, (int) (SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite),
                    null);

            if (destKbResult != SQLite3.Result.OK)
            {
                throw SQLiteException.New(destKbResult,
                    $"Could not open backup database file: {destDBPath} (raw sqlite result: {destKbResult})");
            }
            /* Open the backup object used to accomplish the transfer */
            var bHandle = raw.sqlite3_backup_init(destDB, "main", connection.Handle, "main");

            if (bHandle == null)
            {
                // Close the database connection 
                raw.sqlite3_close_v2(destDB);

                throw SQLiteException.New(destKbResult, $"Could not initiate backup process: {destDBPath}");
            }

            /* Each iteration of this loop copies 5 database pages from database
            ** pDb to the backup database. If the return value of backup_step()
            ** indicates that there are still further pages to copy, sleep for
            ** 250 ms before repeating. */
            do
            {
                destKbResult = (SQLite3.Result) raw.sqlite3_backup_step(bHandle, 30);

                if (destKbResult == SQLite3.Result.OK || destKbResult == SQLite3.Result.Busy ||
                    destKbResult == SQLite3.Result.Locked)
                {
                    Thread.Sleep(250);
                }
            } while (destKbResult == SQLite3.Result.OK || destKbResult == SQLite3.Result.Busy ||
                     destKbResult == SQLite3.Result.Locked);

            /* Release resources allocated by backup_init(). */
            destKbResult = (SQLite3.Result) raw.sqlite3_backup_finish(bHandle);

            if (destKbResult != SQLite3.Result.OK)
            {
                // Close the database connection 
                raw.sqlite3_close_v2(destDB);

                throw SQLiteException.New(destKbResult,
                    $"Could not finish backup process: {destDBPath} (sqlite result: {destKbResult})");
            }

            // Close the database connection 
            raw.sqlite3_close_v2(destDB);
        }

        public async Task RestoreAsync(string backupFilePath)
        {
            await this.permissions.AssureHasPermissionOrThrow<StoragePermission>().ConfigureAwait(false);

            if (this.fileSystemAccessor.IsFileExists(backupFilePath))
            {
                this.ReCreatePrivateDirectory();

                await this.archiver.UnzipAsync(backupFilePath, this.privateStorage, true);

                foreach (var tempBackupFile in Directory.GetFiles(this.privateStorage, "*.sqlite3.back", SearchOption.AllDirectories))
                {
                    var destFileName = Path.ChangeExtension(tempBackupFile, null);
                    File.Move(tempBackupFile, destFileName);
                }

                try
                {
                    this.DecryptKeyStore();
                }
                catch (Exception e)
                {
                    this.logger.Error("Restore unhandled exception", e);

                    this.ReCreatePrivateDirectory();

                    await this.userInteractionService.AlertAsync("Could not restore encrypted data", "Warning");
                }
            }
        }

        public async Task SendLogsAsync(CancellationToken token)
        {
            var backupTo =
                this.fileSystemAccessor.CombinePath(privateStorage, "logs.zip");
            try
            {
                var backupHeaders = new Dictionary<string, string>()
                {
                    {"DeviceId", this.deviceSettings.GetDeviceId()},
                };

                var path = this.fileSystemAccessor.CombinePath(privateStorage, "Logs");

                if (this.fileSystemAccessor.IsFileExists(backupTo))
                {
                    this.fileSystemAccessor.DeleteFile(backupTo);
                }

                await this.archiver.ZipDirectoryToFileAsync(path, backupTo)
                    .ConfigureAwait(false);

                if (token.IsCancellationRequested) return;

                using (var logsFileStream = this.fileSystemAccessor.ReadFile(backupTo))
                {
                    await this.restService.SendStreamAsync(
                        stream: logsFileStream,
                        customHeaders: backupHeaders,
                        url: "api/enumerator/logs",
                        credentials:
                        this.principal.IsAuthenticated
                            ? new RestCredentials
                            {
                                Login = this.principal.CurrentUserIdentity.Name,
                                Token = this.principal.CurrentUserIdentity.Token,
                                Workspace = this.principal.CurrentUserIdentity.Workspace
                            }
                            : null,
                        token: token).ConfigureAwait(false);
                }
            }
            catch (RestException e)
            {
                throw e.ToSynchronizationException();
            }
            finally
            {
                if (this.fileSystemAccessor.IsFileExists(backupTo))
                {
                    this.fileSystemAccessor.DeleteFile(backupTo);
                }
            }
        }

        private void ReCreatePrivateDirectory()
        {
            if (!this.fileSystemAccessor.IsDirectoryExists(this.privateStorage)) return;

            this.fileSystemAccessor.DeleteDirectory(this.privateStorage);
            this.fileSystemAccessor.CreateDirectory(this.privateStorage);
        }

        private void CreateDeviceInfoFile()
        {
            var tabletInfoFilePath = this.fileSystemAccessor.CombinePath(this.privateStorage, "device.info");
            var deviceTechnicalInformation = this.deviceSettings.GetDeviceTechnicalInformation();
            this.fileSystemAccessor.WriteAllText(tabletInfoFilePath, deviceTechnicalInformation);

        }

        private void CreateFilesListInfoFile()
        {
            var internalFiles = this.fileSystemAccessor.CombinePath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            var tabletInfoFilePath = this.fileSystemAccessor.CombinePath(this.privateStorage, "files.info");

            var files = this.fileSystemAccessor.GetFilesInDirectory(internalFiles, true);
            var sb = new StringBuilder();
            sb.AppendLine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + " files list:");
            sb.AppendLine();

            foreach(var file in files)
            {
                var fi = new FileInfo(file);
                sb.Append($"\t{file}");

                // we want to always read hash for .apk files or add hash if it's already exists 
                if (file.EndsWith(".apk") || this.fileSystemAccessor.IsFileExists(file + ".md5"))
                {
                    var hash = this.fileSystemAccessor.ReadHash(file);
                    sb.Append($"\t[md5:{Convert.ToBase64String(hash)}]");
                }

                sb.Append($"\t{fi.Length}({fi.Length.Bytes().ToString("0.00")})\t{fi.LastWriteTimeUtc}");
                sb.AppendLine();
            }

            this.fileSystemAccessor.WriteAllText(tabletInfoFilePath, sb.ToString());
        }

        private void DecryptKeyStore()
        {
            var keyStorePasswordFile = this.fileSystemAccessor.CombinePath(this.privateStorage, "keystore.info");
            if (!this.fileSystemAccessor.IsFileExists(keyStorePasswordFile)) return;

            var keyStorePasswordFileText = this.fileSystemAccessor.ReadAllText(keyStorePasswordFile);


            var serializedKeyChain = this.serializer.Deserialize<KeyChain>(keyStorePasswordFileText);

            this.secureStorage.Store("key", Convert.FromBase64String(serializedKeyChain.Key));
            this.secureStorage.Store("iv", Convert.FromBase64String(serializedKeyChain.Iv));
        }

        private void EncryptKeyStore(string backupFolderFilesPath)
        {
            if (!this.secureStorage.Contains(RsaEncryptionService.PublicKey)) return;

            this.fileSystemAccessor.DeleteFile(this.fileSystemAccessor.CombinePath(backupFolderFilesPath, "keystore.dat"));

            var securedKeyChain = new KeyChain
            {
                Key = Convert.ToBase64String(this.secureStorage.Retrieve("key")),
                Iv = Convert.ToBase64String(this.secureStorage.Retrieve("iv"))
            };

            var serializedKeyChain = this.serializer.Serialize(securedKeyChain);

            var keyStorePasswordFile = this.fileSystemAccessor.CombinePath(backupFolderFilesPath, "keystore.info");

            this.fileSystemAccessor.WriteAllText(keyStorePasswordFile,
                this.encryptionService.Encrypt(serializedKeyChain));
        }

        private class KeyChain
        {
            public string Key { get; set; }
            public string Iv { get; set; }
        }
    }
}
