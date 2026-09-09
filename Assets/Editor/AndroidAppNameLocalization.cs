using System.IO;
using System.Text;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEditor.Build;

/// <summary>Localizes the Android launcher label each time Unity generates its Gradle project.</summary>
public sealed class AndroidAppNameLocalization : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 100;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // Unity passes the unityLibrary module path; launcher is its sibling.
        string root = Directory.GetParent(path)?.FullName;
        string launcher = Path.Combine(root ?? path, "launcher", "src", "main");
        if (!Directory.Exists(launcher))
            throw new BuildFailedException("Cannot find the Android launcher module for app-name localization: " + launcher);

        SetAppName(Path.Combine(launcher, "res", "values", "strings.xml"), "SOBOK");
        SetAppName(Path.Combine(launcher, "res", "values-ko", "strings.xml"), "소복");
        ConfigureSharing(launcher);
        // This method is called through Unity JNI, so shrinking must retain it.
        string proguard = Path.Combine(path, "proguard-unity.txt");
        const string shareKeep = "-keep class androidx.core.content.FileProvider { *; }";
        if (!File.Exists(proguard) || !File.ReadAllText(proguard).Contains(shareKeep))
            File.AppendAllText(proguard, "\n" + shareKeep + "\n");
    }

    private static void ConfigureSharing(string launcher)
    {
        string manifestPath = Path.Combine(launcher, "AndroidManifest.xml");
        var manifest = XDocument.Load(manifestPath);
        XNamespace android = "http://schemas.android.com/apk/res/android";
        var application = manifest.Root?.Element("application");
        if (application == null) throw new BuildFailedException("Android application manifest is missing.");
        const string authority = "${applicationId}.sobok.share";
        XElement provider = null;
        foreach (var existing in application.Elements("provider"))
            if ((string)existing.Attribute(android + "authorities") == authority) provider = existing;
        if (provider != null) provider.Remove();
        application.Add(new XElement("provider",
            new XAttribute(android + "name", "androidx.core.content.FileProvider"),
            new XAttribute(android + "authorities", authority),
            new XAttribute(android + "exported", "false"),
            new XAttribute(android + "grantUriPermissions", "true"),
            new XElement("meta-data",
                new XAttribute(android + "name", "android.support.FILE_PROVIDER_PATHS"),
                new XAttribute(android + "resource", "@xml/sobok_share_paths"))));
        manifest.Save(manifestPath);
        string xmlDirectory = Path.Combine(launcher, "res", "xml");
        Directory.CreateDirectory(xmlDirectory);
        new XDocument(new XElement("paths",
            new XElement("cache-path", new XAttribute("name", "sobok_screenshots"),
                new XAttribute("path", "sobok-share/")),
            new XElement("external-cache-path", new XAttribute("name", "sobok_external_screenshots"),
                new XAttribute("path", "sobok-share/"))))
            .Save(Path.Combine(xmlDirectory, "sobok_share_paths.xml"));
    }

    private static void SetAppName(string path, string name)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var document = File.Exists(path) ? XDocument.Load(path) : new XDocument(new XElement("resources"));
        XElement root = document.Root;
        if (root == null || root.Name != "resources")
            throw new BuildFailedException("Invalid Android string resource: " + path);

        XElement label = null;
        foreach (var element in root.Elements("string"))
        {
            if ((string)element.Attribute("name") == "app_name")
            {
                label = element;
                break;
            }
        }
        if (label == null)
        {
            label = new XElement("string", new XAttribute("name", "app_name"));
            root.Add(label);
        }
        label.Value = name;
        using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            document.Save(writer);
    }
}
