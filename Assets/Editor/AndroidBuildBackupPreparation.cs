using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Preserve old generated files before Unity's Android postprocessor tries to delete them.
public sealed class AndroidBuildBackupPreparation : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        string output = Path.GetFullPath(report.summary.outputPath);
        string backup = Path.Combine(Path.GetDirectoryName(output),
            Path.GetFileNameWithoutExtension(output) + "_BackUpThisFolder_ButDontShipItWithYourGame");
        if (!Directory.Exists(backup))
            return;

        if ((File.GetAttributes(backup) & FileAttributes.ReparsePoint) != 0)
            throw new BuildFailedException("Build backup must not be a symbolic link: " + backup);

        string archive = Path.Combine(Path.GetDirectoryName(output), "obj", "build-backups");
        string destination = Path.Combine(archive,
            Path.GetFileName(backup) + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(archive);
            Directory.Move(backup, destination);
            Debug.Log("[SOBOK] Previous Android build backup preserved at: " + destination);
        }
        catch (IOException exception)
        {
            throw new BuildFailedException("Cannot archive the previous Android backup. Close programs using " + backup + "\n" + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new BuildFailedException("Cannot access the previous Android backup: " + backup + "\n" + exception.Message);
        }
    }
}
