﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using SelectingItemsControl = Avalonia.Controls.Primitives.SelectingItemsControl;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using Humanizer;

using Apollo.Binary;
using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Helpers;
using Apollo.Viewers;

namespace Apollo.Windows {
    public class SplashWindow: Window {
        static Image SplashImage = (Image)Application.Current.Styles.FindResource("SplashImage");

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            
            Root = this.Get<Grid>("Root");
            CrashPanel = this.Get<Grid>("CrashPanel");

            TabControl = this.Get<TabControl>("TabControl");
            Recents = this.Get<StackPanel>("Recents");

            GithubVersion = this.Get<TextBlock>("GithubVersion");
            GithubBody = this.Get<TextBlock>("GithubBody");
            GithubLink = this.Get<TextBlock>("GithubLink");
        }

        Grid Root, CrashPanel;
        TabControl TabControl;
        StackPanel Recents;
        TextBlock GithubVersion, GithubBody, GithubLink;

        bool openDialog = false;

        void UpdateTopmost(bool value) => Topmost = value;

        public SplashWindow() {
            InitializeComponent();
            #if DEBUG
                this.AttachDevTools();
            #endif
            
            UpdateTopmost(Preferences.AlwaysOnTop);
            Preferences.AlwaysOnTopChanged += UpdateTopmost;

            this.Get<PreferencesButton>("PreferencesButton").HoleFill = Background;
            
            Root.Children.Add(SplashImage);

            TabControl.GetObservable(SelectingItemsControl.SelectedIndexProperty).Subscribe(TabChanged);

            Preferences.RecentsCleared += Clear;

            if (Preferences.CrashName != "") {
                CrashPanel.Opacity = 1;
                CrashPanel.IsHitTestVisible = true;
                CrashPanel.ZIndex = 1;
            }
        }

        async void Loaded(object sender, EventArgs e) {
            Position = new PixelPoint(Position.X, Math.Max(0, Position.Y));

            if (Launchpad.CFWIncompatible == CFWIncompatibleState.Show) Launchpad.CFWError(this);

            if (Program.Args?.Length > 0)
                ReadFile(Program.Args[0]);
            
            Program.Args = null;

            Octokit.Release latest;

            try {
                latest = await Github.LatestRelease();
            } catch {
                GithubBody.Text = "Failed to fetch release data from GitHub.";
                return;
            }
            
            GithubVersion.Text = $"{latest.Name} - published {latest.PublishedAt.Humanize()}";
            GithubBody.Text = String.Join('\n', latest.Body.Replace("\r", "").Split('\n').SkipWhile(i => i.Trim() == "Changes:" || i.Trim() == "").Take(3));
            GithubLink.Opacity = 1;
            GithubLink.IsHitTestVisible = true;

            if (IsVisible && !openDialog && await Github.ShouldUpdate()) {
                Window[] windows = Application.Current.Windows.ToArray();
                
                foreach (Window window in windows)
                    if (window.GetType() != typeof(MessageWindow))
                        window.Close();
                
                UpdateWindow.Create(this);
            }
        }
        
        void Unloaded(object sender, EventArgs e) {
            Root.Children.Remove(SplashImage);
            
            Preferences.AlwaysOnTopChanged -= UpdateTopmost;

            this.Content = null;

            Program.WindowClosed(this);
        }

        void TabChanged(int tab) {
            if (tab == 0) {
                for (int i = 0; i < Preferences.Recents.Count; i++) {
                    RecentProjectInfo viewer = new RecentProjectInfo(Preferences.Recents[i]);
                    viewer.Opened += ReadFile;
                    viewer.Removed += Remove;
                    viewer.Showed += URL;

                    Recents.Children.Add(viewer);
                }
            
            } else Recents.Children.Clear();
        }

        void New(object sender, RoutedEventArgs e) {
            Program.Project?.Dispose();
            Program.Project = new Project();
            ProjectWindow.Create(this);
            Close();
        }

        void ReadFile(string path) => ReadFile(path, false);
        async void ReadFile(string path, bool recovery) {
            Project loaded = null;
            Copyable imported = null;
            bool isImport = false;

            try {
                try {
                    using (FileStream file = File.Open(path, FileMode.Open, FileAccess.Read))
                        loaded = await Decoder.Decode(file, typeof(Project));

                    if (!recovery) {
                        loaded.FilePath = path;
                        loaded.Undo.SavePosition();
                        Preferences.RecentsAdd(path);
                    }
                    
                    Program.Project?.Dispose();
                    Program.Project = loaded;

                } catch (InvalidDataException) {
                    using (FileStream file = File.Open(path, FileMode.Open, FileAccess.Read))
                        imported = await Decoder.Decode(file, typeof(Copyable));

                    Program.Project?.Dispose();
                    Program.Project = new Project(
                        tracks: (imported.Type == typeof(Track))
                            ? imported.Contents.Cast<Track>().ToList()
                            : new List<Track>() {
                                new Track(new Chain((imported.Type == typeof(Chain))
                                    ? new List<Device>() { new Group(imported.Contents.Cast<Chain>().ToList()) }
                                    : imported.Contents.Cast<Device>().ToList()
                                ))
                            }
                    );
                }

            } catch {
                await MessageWindow.Create(
                    $"An error occurred while reading the file.\n\n" +
                    "You may not have sufficient privileges to read from the destination folder, or\n" +
                    "the file you're attempting to read is invalid.",
                    null, this
                );

                return;
            }
            
            ProjectWindow.Create(this);
            Close();
        }

        async void Open(object sender, RoutedEventArgs e) {
            OpenFileDialog ofd = new OpenFileDialog() {
                AllowMultiple = false,
                Filters = new List<FileDialogFilter>() {
                    new FileDialogFilter() {
                        Extensions = new List<string>() {
                            "approj"
                        },
                        Name = "Apollo Project"
                    }
                },
                Title = "Open Project"
            };

            openDialog = true;
            string[] result = await ofd.ShowAsync(this);
            openDialog = false;

            if (result.Length > 0)
                ReadFile(result[0]);
        }

        void Clear() => Dispatcher.UIThread.Post(() => Recents.Children.Clear(), DispatcherPriority.MinValue);

        void Remove(RecentProjectInfo sender, string path) {
            Preferences.RecentsRemove(path);

            Dispatcher.UIThread.Post(() => Recents.Children.Remove(sender), DispatcherPriority.MinValue);
        }

        void URL(string url) => Process.Start(new ProcessStartInfo() {
            FileName = url,
            UseShellExecute = true
        });

        void Docs(object sender, RoutedEventArgs e)
            => URL("https://github.com/mat1jaczyyy/apollo-studio/wiki");

        void Tutorials(object sender, RoutedEventArgs e)
            => URL("https://www.youtube.com/playlist?list=PLKC4R3X00beY0aB_f_ZIa3shqJX7do4mH");

        void Bug(object sender, RoutedEventArgs e)
            => URL("https://github.com/mat1jaczyyy/apollo-studio/issues/new?assignees=mat1jaczyyy&labels=bug&template=bug_report.md&title=");

        void Feature(object sender, RoutedEventArgs e)
            => URL("https://github.com/mat1jaczyyy/apollo-studio/issues/new?assignees=mat1jaczyyy&labels=enhancement&template=feature_request.md&title=");

        void Question(object sender, RoutedEventArgs e)
            => URL("https://github.com/mat1jaczyyy/apollo-studio/issues/new?assignees=mat1jaczyyy&labels=question&template=question.md&title=");

        void Discord(object sender, RoutedEventArgs e) {}

        async void Release(object sender, RoutedEventArgs e)
            => URL((await Github.LatestRelease()).HtmlUrl);

        void Restore(object sender, RoutedEventArgs e) {
            CrashPanel.Opacity = 0;
            CrashPanel.IsHitTestVisible = false;
            CrashPanel.ZIndex = -1;

            ReadFile(Preferences.CrashName + ".approj", true);

            if (Program.Project != null) {
                Program.Project.FilePath = Preferences.CrashPath;
                Program.Project.Undo.Clear("Project Restored");
            }

            Preferences.CrashName = Preferences.CrashPath = "";
        }

        void Ignore(object sender, RoutedEventArgs e) {
            CrashPanel.Opacity = 0;
            CrashPanel.IsHitTestVisible = false;
            CrashPanel.ZIndex = -1;

            Preferences.CrashName = Preferences.CrashPath = "";
        }

        void Window_KeyDown(object sender, KeyEventArgs e) {
            if (Program.WindowKey(this, e)) return;

            if (e.Modifiers == Program.ControlKey) {
                if (e.Key == Key.N) New(sender, e);
                else if (e.Key == Key.O) Open(sender, e);
            }
        }

        void MoveWindow(object sender, PointerPressedEventArgs e) => BeginMoveDrag();
    }
}
