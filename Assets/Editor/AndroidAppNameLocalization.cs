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
