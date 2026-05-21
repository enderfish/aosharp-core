using System;

namespace AOSharp.Core
{
    public interface IAOPluginEntry
    {
        void Run(string pluginDir);
        void Teardown();
    }

    public abstract class AOPluginEntry : IAOPluginEntry
    {
        public string PluginDirectory { get; internal set; }
        public System.IO.DirectoryInfo PluginDataDirectory => new System.IO.DirectoryInfo(PluginDirectory);

        // Satisfies IAOPluginEntry.Run(string); calls Run() for plugins compiled against older AOSharp.Core
        public virtual void Run(string pluginDir) { PluginDirectory = pluginDir; Run(); }

        // Old signature — override this if compiled against an older AOSharp.Core that had Run()
        public virtual void Run() { }

        public virtual void Teardown() { }
    }
}
