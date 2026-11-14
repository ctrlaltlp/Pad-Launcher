using System.Windows;
using System.Windows.Controls;
using System;
using System.IO;
using System.Linq;

namespace PadLauncher {
    public partial class Launcher {
        internal static string DisplayLauncherPath(string value) {
            if(String.IsNullOrWhiteSpace(value)) return "";
            string normalized=Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')).Replace('/','\\');
            try {
                normalized=Path.GetFullPath(normalized);
                string root=Path.GetPathRoot(normalized),current=root.Length==3 && root[1]==':' ? Char.ToUpperInvariant(root[0])+root.Substring(1) : root;
                foreach(string part in normalized.Substring(root.Length).Split(new[]{'\\'},StringSplitOptions.RemoveEmptyEntries)) {
                    if(part.IndexOfAny(Path.GetInvalidFileNameChars())>=0) return normalized;
                    string actual=Directory.Exists(current) ? Directory.EnumerateFileSystemEntries(current,part).FirstOrDefault() : null;
                    current=actual ?? Path.Combine(current,part);
                }
                return current;
            } catch(IOException) {} catch(UnauthorizedAccessException) {} catch(ArgumentException) {} catch(System.Security.SecurityException) {} catch(NotSupportedException) {}
            return normalized;
        }
        void AddLauncherPathField(StackPanel panel,Window owner,string title,string name,Func<Settings,string> get,Action<Settings,string> set,Func<string> detect,string[] executables) {
            var caption=Label(title+" path",14,mint); caption.Margin=new Thickness(0,10,0,8); panel.Children.Add(caption);
            var row=new DockPanel(); panel.Children.Add(row);
            var browse=new Button {Content="Browse…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; DockPanel.SetDock(browse,Dock.Right); row.Children.Add(browse);
            var field=new TextBox {Name=name,Text=DisplayLauncherPath(get(settings)),Padding=new Thickness(8)};
            var box=new Grid(); box.Children.Add(field); row.Children.Add(box);
            string detected=DisplayLauncherPath(detect());
            var hint=Label(String.IsNullOrEmpty(detected) ? "Automatic detection" : detected,12,muted); hint.Margin=new Thickness(19,0,9,0); hint.IsHitTestVisible=false; hint.TextTrimming=TextTrimming.CharacterEllipsis; box.Children.Add(hint); box.ToolTip=hint.Text;
            Action update=() => hint.Visibility=String.IsNullOrEmpty(field.Text) ? Visibility.Visible : Visibility.Collapsed; field.TextChanged+=(s,e) => update(); update();
            var status=Label("",12,muted); status.TextWrapping=TextWrapping.Wrap;
            var save=new Button {Content="Save path",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,8)}; panel.Children.Add(save); panel.Children.Add(status);
            save.Click+=(s,e) => {try {
                string path=String.IsNullOrWhiteSpace(field.Text) ? null : DisplayLauncherPath(field.Text);
                if(path!=null && (!File.Exists(path) || !executables.Contains(Path.GetFileName(path),StringComparer.OrdinalIgnoreCase))) throw new ArgumentException("Choose "+String.Join(" or ",executables)+", or leave empty for automatic detection.");
                var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); set(next,path); Storage.Write("settings.json",next); settings=next; field.Text=path ?? ""; status.Text=path==null ? "Automatic detection enabled." : "Custom launcher path saved."; RefreshLibrary(true);
            } catch(Exception error) {status.Text=error.Message;}};
            browse.Click+=(s,e) => {var file=new Microsoft.Win32.OpenFileDialog {Filter=title+" executable|"+String.Join(";",executables),CheckFileExists=true}; if(file.ShowDialog(owner)==true) {field.Text=file.FileName; save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}};
        }
        void ApplySettingsTheme(Window dialog) {
            using(var theme=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SettingsTheme.xaml")) {
                var resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Load(theme);
                dialog.Resources.MergedDictionaries.Add(resources);
                dialog.Resources.Add(typeof(SettingsComboBox),resources[typeof(ComboBox)]);
            }
            string inputStyle=@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type INPUT}'>
                <Setter Property='Background' Value='#1C303D'/><Setter Property='Foreground' Value='#ECF3F2'/>
                <Setter Property='BorderBrush' Value='#3A5260'/><Setter Property='BorderThickness' Value='1'/>
                <Setter Property='CaretBrush' Value='#A3F0CD'/><Setter Property='SelectionBrush' Value='#497D75'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type INPUT}'>
                    <Border x:Name='InputBorder' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6' Padding='{TemplateBinding Padding}'>
                        <ScrollViewer x:Name='PART_ContentHost' Focusable='False' HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='Hidden'/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='InputBorder' Property='BorderBrush' Value='#64848E'/></Trigger>
                        <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='InputBorder' Property='BorderBrush' Value='#A3F0CD'/></Trigger>
                        <Trigger Property='IsEnabled' Value='False'><Setter TargetName='InputBorder' Property='Opacity' Value='0.5'/></Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value></Setter>
            </Style>";
            var textStyle=(Style)System.Windows.Markup.XamlReader.Parse(inputStyle.Replace("INPUT","TextBox"));
            textStyle.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty,dialog.FindResource("PadTextMenu")));
            dialog.Resources.Add(typeof(TextBox),textStyle);
            var passwordStyle=(Style)System.Windows.Markup.XamlReader.Parse(inputStyle.Replace("INPUT","PasswordBox"));
            passwordStyle.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty,dialog.FindResource("PadPasswordMenu")));
            dialog.Resources.Add(typeof(PasswordBox),passwordStyle);
            dialog.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar),System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'>
                <Setter Property='ContextMenuService.IsEnabled' Value='False'/>
                <Setter Property='Width' Value='6'/><Setter Property='MinWidth' Value='6'/><Setter Property='Background' Value='Transparent'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'>
                    <Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='Vertical' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}'>
                        <Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton>
                        <Track.Thumb><Thumb x:Name='MintThumb' Background='#A3F0CD' MinHeight='24'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='{TemplateBinding Background}' CornerRadius='3'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                        <Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton>
                    </Track></Grid>
                </ControlTemplate></Setter.Value></Setter>
            </Style>"));
        }
    }
}
