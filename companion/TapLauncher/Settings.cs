using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Xml.Linq;

partial class TapLauncher
{
    static string ConfigPath { get { return Path.Combine(Root,"TapLauncher.settings.xml"); } }
    class Settings
    {
        public string Target=File.Exists(Path.Combine(Root,"SteamPadLauncher.exe")) ? Path.Combine(Root,"SteamPadLauncher.exe") : "", Arguments="";
        public void Validate()
        {
            Target=Environment.ExpandEnvironmentVariables(Target.Trim().Trim('"'));
            if(!Path.IsPathRooted(Target) || !File.Exists(Target) || !string.Equals(Path.GetExtension(Target),".exe",StringComparison.OrdinalIgnoreCase))
                throw new Exception("Choose an existing .exe file using its full path.");
            Target=Path.GetFullPath(Target);
            if(string.Equals(Target,Path.GetFullPath(Application.ExecutablePath),StringComparison.OrdinalIgnoreCase))
                throw new Exception("Choose the application you want to launch, rather than TapLauncher itself.");
        }
        public static Settings Load(string path)
        {
            XElement root=XElement.Load(path);
            if(root.Name!="TapLauncher") throw new Exception("Invalid TapLauncher settings file.");
            return new Settings { Target=(string)root.Element("Target") ?? "", Arguments=(string)root.Element("Arguments") ?? "" };
        }
        public void Save(string path)
        {
            new XElement("TapLauncher",new XElement("Target",Target),new XElement("Arguments",Arguments)).Save(path);
        }
    }
    static int Configure()
    {
        Application.EnableVisualStyles();
        Settings settings=new Settings();
        if(File.Exists(ConfigPath)) { try { settings=Settings.Load(ConfigPath); } catch(Exception ex) { MessageBox.Show(ex.Message,"TapLauncher settings"); } }
        using(Form form=new Form())
        {
            form.Text="TapLauncher settings"; form.ClientSize=new Size(640,280);
            form.FormBorderStyle=FormBorderStyle.FixedDialog; form.MaximizeBox=false;
            form.StartPosition=FormStartPosition.CenterScreen; form.Font=new Font("Segoe UI",10);
            Label title=new Label { Text="Choose what a short PS button press launches",Left=20,Top=18,Width=600,Height=26 };
            Label exeLabel=new Label { Text="Application (.exe)",Left=20,Top=56,Width=300 };
            TextBox target=new TextBox { Text=settings.Target,Left=20,Top=81,Width=495 };
            Button browse=new Button { Text="Browse…",Left=525,Top=79,Width=95,Height=30 };
            Label argLabel=new Label { Text="Arguments (optional)",Left=20,Top=119,Width=300 };
            TextBox arguments=new TextBox { Text=settings.Arguments,Left=20,Top=144,Width=600 };
            Label note=new Label { Text="Requires DS4Windows UDP Server at 127.0.0.1:26760.\nWith multiple pads, PS must still be down when the helper starts reading.",Left=20,Top=182,Width=600,Height=48 };
            Button save=new Button { Text="Save",Left=420,Top=237,Width=95,Height=30 };
            Button cancel=new Button { Text="Cancel",Left=525,Top=237,Width=95,Height=30,DialogResult=DialogResult.Cancel };
            browse.Click+=delegate {
                using(OpenFileDialog dialog=new OpenFileDialog { Filter="Applications (*.exe)|*.exe",CheckFileExists=true,Title="Choose application" })
                { if(dialog.ShowDialog(form)==DialogResult.OK) target.Text=dialog.FileName; }
            };
            save.Click+=delegate {
                try {
                    Settings chosen=new Settings { Target=target.Text, Arguments=arguments.Text };
                    chosen.Validate(); chosen.Save(ConfigPath); form.DialogResult=DialogResult.OK; form.Close();
                } catch(Exception ex) { MessageBox.Show(form,ex.Message,"Could not save settings",MessageBoxButtons.OK,MessageBoxIcon.Error); }
            };
            form.Controls.AddRange(new Control[]{title,exeLabel,target,browse,argLabel,arguments,note,save,cancel});
            form.AcceptButton=save; form.CancelButton=cancel;
            return form.ShowDialog()==DialogResult.OK ? 0 : 1;
        }
    }
    static string Option(string[] args,string key)
    {
        int i=Array.IndexOf(args,key); if(i<0) return null;
        if(i+1>=args.Length) throw new Exception("Missing value after "+key);
        return args[i+1];
    }
    static void SettingsTest()
    {
        string temp=Path.Combine(Root,"settings-test.xml");
        try {
            Settings sample=new Settings { Target="C:\\A & B\\Example.exe",Arguments="--name \"A & B\"" };
            sample.Save(temp); Settings restored=Settings.Load(temp);
            if(restored.Target!=sample.Target || restored.Arguments!=sample.Arguments) throw new Exception("Settings round-trip failed");
            Log("TEST settings round-trip: PASS");
        } finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    [STAThread]
    static int Main(string[] args)
    {
        ClearStartupFeedback();
        try
        {
            if(Array.IndexOf(args,"--configure")>=0) return Configure();
            if(Array.IndexOf(args,"--self-test")>=0)
            {
                bool pass=true;
                foreach(string mode in new string[]{"tap","slowtap","released","hold","holdrelease","drop","disconnect","silent","invalid","multitap","multihold","multireleased"}) pass=Scenario(mode)&&pass;
                SettingsTest(); File.WriteAllText(Path.Combine(Root,"self-test.txt"),pass ? "PASS: twelve UDP scenarios (including multiple controllers) and settings round-trip." : "FAIL: see TapLauncher.log"); return pass ? 0 : 1;
            }
            bool diagnostic=Array.IndexOf(args,"--diagnose")>=0;
            Settings settings=null;
            if(!diagnostic)
            {
                string target=Option(args,"--target");
                if(target==null && !File.Exists(ConfigPath)) return Configure();
                settings=target==null ? Settings.Load(ConfigPath) : new Settings { Target=target,Arguments=Option(args,"--arguments") ?? "" };
                settings.Validate();
            }
            bool created;
            using(Mutex mutex=new Mutex(true,"Local\\ConfigurableTapLauncher",out created))
            {
                if(!created) return 0;
                string reason; bool launch=Observe(26760,diagnostic,out reason); Log(reason);
                if(!launch) return 0;
                if(Array.IndexOf(args,"--dry-run")>=0) { Log("DRY RUN: would launch "+settings.Target+" Arguments="+settings.Arguments); return 0; }
                Process.Start(new ProcessStartInfo(settings.Target,settings.Arguments) { WorkingDirectory=Path.GetDirectoryName(settings.Target),UseShellExecute=true });
                Log("Launched "+settings.Target); Thread.Sleep(500); return 0;
            }
        }
        catch(Exception ex) { Log("ERROR: "+ex.Message); return 1; }
    }
}
