﻿using System;
using System.Collections.Generic;
using System.Linq;
using WB.Core.BoundedContexts.Interviewer.Views;
using WB.Core.Infrastructure.FileSystem;
using WB.Core.SharedKernels.Enumerator.Implementation.Services;
using WB.Core.SharedKernels.Enumerator.Services;
using WB.Core.SharedKernels.Enumerator.Services.Infrastructure.Storage;

namespace WB.UI.Interviewer.Migrations.Workspaces
{
    [Migration(202103101642)]
    public class M202103101642_MoveSqliteTablesInWorkspaceFolder : IMigration
    {
        private readonly IFileSystemAccessor fileSystemAccessor;
        private readonly IPlainStorage<InterviewerIdentity> usersStorage;
        private readonly SqliteSettings settings;

        public M202103101642_MoveSqliteTablesInWorkspaceFolder(IFileSystemAccessor fileSystemAccessor,
            IPlainStorage<InterviewerIdentity> usersStorage,
            SqliteSettings settings)
        {
            this.fileSystemAccessor = fileSystemAccessor;
            this.usersStorage = usersStorage;
            this.settings = settings;
        }

        public void Up()
        {
            var interviewerIdentity = usersStorage.LoadAll().SingleOrDefault();
            var workspace = interviewerIdentity?.Workspace ?? "primary";
            if (string.IsNullOrWhiteSpace(workspace))
                return;

            var workspaceFolder = fileSystemAccessor.CombinePath(settings.PathToRootDirectory, workspace);
            if (!fileSystemAccessor.IsDirectoryExists(workspaceFolder))
                fileSystemAccessor.CreateDirectory(workspaceFolder);

            void TryMoveDirectoryInWorkspaceDirectory(string dirName)
            {
                var dirPath = fileSystemAccessor.CombinePath(settings.PathToRootDirectory, dirName);
                if (!fileSystemAccessor.IsDirectoryExists(dirPath))
                    return;
                
                var newFolderPath = fileSystemAccessor.CombinePath(workspaceFolder, dirName);
                fileSystemAccessor.MoveDirectory(dirPath, newFolderPath);
            }

            // move "audio" and "assemblies" to workspace subfolder
            TryMoveDirectoryInWorkspaceDirectory("audio");
            TryMoveDirectoryInWorkspaceDirectory("assemblies");

            // create "data" folder in workspace
            var workspaceDataFolder = fileSystemAccessor.CombinePath(workspaceFolder, "data");
            if (!fileSystemAccessor.IsDirectoryExists(workspaceDataFolder))
                fileSystemAccessor.CreateDirectory(workspaceDataFolder);

            // move all subfolders in data like "interviews" to workspace
            var subFolders = fileSystemAccessor.GetDirectoriesInDirectory(settings.PathToDatabaseDirectory);
            foreach (var subFolder in subFolders)
            {
                var folderName = fileSystemAccessor.GetDirectoryName(subFolder);
                var newFolderPath = fileSystemAccessor.CombinePath(workspaceDataFolder, folderName);
                fileSystemAccessor.MoveDirectory(subFolder, newFolderPath);
            }
            
            // move workspace specific databases to workspace directory
            var files = fileSystemAccessor.GetFilesInDirectory(settings.PathToDatabaseDirectory, "*-data.sqlite3");

            var excludeFiles = new List<string>()
            {
                "Migration",
                "InterviewerIdentity",
                "SupervisorIdentity",
                "TesterUserIdentity",
                "WorkspaceView",
                "AuditLogSettingsView",
                "AuditLogRecordView",
                "AuditLogEntityView",
                "ApplicationSettingsView",
                "EnumeratorSettingsView",
            };
            
            foreach (var file in files)
            {
                var fileName = fileSystemAccessor.GetFileName(file);
                var entityTypeName = fileName.Replace("-data.sqlite3", "");
                if (excludeFiles.Contains(entityTypeName))
                    continue;

                var newFilePath = fileSystemAccessor.CombinePath(workspaceDataFolder, fileName);
                if(fileSystemAccessor.IsFileExists(newFilePath))
                    throw new InvalidOperationException($"Target file already exists. Entity: {entityTypeName}");

                fileSystemAccessor.MoveFile(file, newFilePath);
            }

            // duplicate file for save settings for offline sync
            var applicationSettingsPath = fileSystemAccessor.CombinePath(workspaceDataFolder, "ApplicationSettingsView");
            if (fileSystemAccessor.IsFileExists(applicationSettingsPath))
            {
                var applicationWorkspaceSettingsPath = fileSystemAccessor.CombinePath(workspaceDataFolder, "ApplicationWorkspaceSettingsView");
                fileSystemAccessor.CopyFileOrDirectory(applicationSettingsPath, applicationWorkspaceSettingsPath);
            }
        }
    }
}
