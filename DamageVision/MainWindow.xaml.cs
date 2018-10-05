using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DamageVision {
    public partial class MainWindow : Window {

        private bool canvasReady = false;

        public MainWindow() {
            //
            this.Top = Properties.Settings.Default.Top;
            this.Left = Properties.Settings.Default.Left;
            this.Height = Properties.Settings.Default.Height;
            InitializeComponent();
            //
            miShowDps.IsChecked = Properties.Settings.Default.ShowDps;
            miShowDamageInQuest.IsChecked = Properties.Settings.Default.ShowDamageInQuest;
            miShowDamageAfterQuest.IsChecked = Properties.Settings.Default.ShowDamageAfterQuest;
            miShowPercentageInQuest.IsChecked = Properties.Settings.Default.ShowPercentageInQuest;
            miShowPercentageAfterQuest.IsChecked = Properties.Settings.Default.ShowPercentageAfterQuest;
            miExitOnGameExit.IsChecked = Properties.Settings.Default.ExitOnGameExit;
            //
            miShowDps.Header = Properties.Resources.ShowDps;
            miShowDamageInQuest.Header = Properties.Resources.ShowDamageInQuest;
            miShowDamageAfterQuest.Header = Properties.Resources.ShowDamageAfterQuest;
            miShowPercentageInQuest.Header = Properties.Resources.ShowPercentageInQuest;
            miShowPercentageAfterQuest.Header = Properties.Resources.ShowPercentageAfterQuest;
            miExitOnGameExit.Header = Properties.Resources.ExitOnGameExit;
            //
            miExit.Header = Properties.Resources.Exit;
        }

        private DispatcherTimer dispatcherTimer;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            InitCanvas();
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(UpdateTick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 150);
            dispatcherTimer.Start();
        }

        private void ExitProgram(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e) {
            Properties.Settings.Default.ShowDps = miShowDps.IsChecked;
            Properties.Settings.Default.ShowDamageInQuest = miShowDamageInQuest.IsChecked;
            Properties.Settings.Default.ShowDamageAfterQuest = miShowDamageAfterQuest.IsChecked;
            Properties.Settings.Default.ShowPercentageInQuest = miShowPercentageInQuest.IsChecked;
            Properties.Settings.Default.ShowPercentageAfterQuest = miShowPercentageAfterQuest.IsChecked;
            Properties.Settings.Default.ExitOnGameExit = miExitOnGameExit.IsChecked;
            Properties.Settings.Default.Top = this.Top;
            Properties.Settings.Default.Left = this.Left;
            Properties.Settings.Default.Height = this.Height;
            Properties.Settings.Default.Save();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e) {
            try {
                if (e.LeftButton == MouseButtonState.Pressed) {
                    DragMove();
                }
            } catch (Exception) {
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
                // change font size
                int newFontSize = TextFontSize + (int)(e.Delta * 0.01);
                int minFontSize = 12;
                TextFontSize = newFontSize > minFontSize ? newFontSize : minFontSize;
            } else if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
                // change bar width
                double newBarWidth = BarWidth + e.Delta * 0.01;
                double minBarWidth = 5;
                BarWidth = newBarWidth > minBarWidth ? newBarWidth : minBarWidth;
            } else if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
                // change opacity
                int newOpacity = BarOpacity + (int)(e.Delta * 0.1);
                int min = 25;
                int max = 100;
                if (newOpacity < min) {
                    newOpacity = min;
                } else if (newOpacity > max) {
                    newOpacity = max;
                }
                BarOpacity = newOpacity;
            } else {
                // change height
                double newHeight = this.Height + e.Delta * 0.1;
                this.Height = newHeight > this.MinHeight ? newHeight : this.MinHeight;
            }
            RefreshLayout();
        }

        private struct Vision {
            public TextBlock banner;
            public TextBlock[] players;
            public Rectangle[] damages;
        }

        private struct BattleInfo {
            public int seatId;
            public string[] names;
            public int[] damages;
            public List<int>[] damageRecords;
            public bool inQuest;
        }

        private Vision vision;
        private BattleInfo battleInfo = new BattleInfo {
            seatId = -2,
            names = new string[4] { Properties.Resources.Prompt1, Properties.Resources.Prompt2, Properties.Resources.Prompt3, Properties.Resources.Prompt4 },
            damages = new int[4] { 0, 0, 0, 0 },
            damageRecords = new List<int>[4] { new List<int>(), new List<int>(), new List<int>(), new List<int>() },
            inQuest = false
        };

        private readonly Color[] colors = new Color[4] {
            Color.FromRgb(225, 65, 55),
            Color.FromRgb(53, 136, 227),
            Color.FromRgb(196, 172, 44),
            Color.FromRgb(42, 208, 55)
        };

        private int TextFontSize {
            get { return Properties.Settings.Default.FontSize; }
            set { Properties.Settings.Default.FontSize = value; }
        }

        private double BarWidth {
            get { return Properties.Settings.Default.BarWidth; }
            set { Properties.Settings.Default.BarWidth = value; }
        }

        private bool ShowDps {
            get { return miShowDps.IsChecked; }
            set { miShowDps.IsChecked = value; }
        }

        private bool ShowDamageInQuest {
            get { return miShowDamageInQuest.IsChecked; }
            set { miShowDamageInQuest.IsChecked = value; }
        }

        private bool ShowDamageAfterQuest {
            get { return miShowDamageAfterQuest.IsChecked; }
            set { miShowDamageAfterQuest.IsChecked = value; }
        }

        private bool ShowPercentageInQuest {
            get { return miShowPercentageInQuest.IsChecked; }
            set { miShowPercentageInQuest.IsChecked = value; }
        }

        private bool ShowPercentageAfterQuest {
            get { return miShowPercentageAfterQuest.IsChecked; }
            set { miShowPercentageAfterQuest.IsChecked = value; }
        }

        private bool ExitOnGameExit {
            get { return miExitOnGameExit.IsChecked; }
            set { miExitOnGameExit.IsChecked = value; }
        }

        private int BarOpacity {
            get { return Properties.Settings.Default.BarOpacity; }
            set { Properties.Settings.Default.BarOpacity = value; }
        }

        private readonly double thePadding = 4;

        private void InitCanvas() {
            theCanvas.Children.Clear();
            vision = new Vision {
                banner = new TextBlock {
                    Text = Properties.Resources.Title,
                    FontWeight = FontWeights.Bold,
                    Effect = new DropShadowEffect {
                        ShadowDepth = 0.0,
                        Color = Colors.Black,
                        BlurRadius = 4.0,
                        Opacity = 1.0
                    },
                    Foreground = new SolidColorBrush(Colors.White)
                },
                players = new TextBlock[4],
                damages = new Rectangle[4]
            };
            theCanvas.Children.Add(vision.banner);
            for (int i = 0; i < 4; ++i) {
                vision.players[i] = new TextBlock {
                    Text = battleInfo.names[i],
                    FontWeight = FontWeights.Bold,
                    Effect = new DropShadowEffect {
                        ShadowDepth = 0.0,
                        Color = Colors.Black,
                        BlurRadius = 4.0,
                        Opacity = 1.0
                    },
                    Foreground = new SolidColorBrush(Colors.White)
                };
                theCanvas.Children.Add(vision.players[i]);
                vision.damages[i] = new Rectangle {
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 0.0,
                    Fill = new SolidColorBrush(colors[i])
                };
                theCanvas.Children.Add(vision.damages[i]);
            }
            canvasReady = true;
            RefreshLayout();
        }

        private void RefreshLayout() {
            int totalDamages = battleInfo.damages.Sum();
            double textHeight = TextFontSize * 20 / 14f;
            double top = textHeight + thePadding; // mut
            double barTotalHeight = theCanvas.ActualHeight - top;
            double textTopMin = top; // mut
            vision.banner.Height = textHeight;
            vision.banner.FontSize = TextFontSize;
            for (int i = 0; i < 4; ++i) {
                // width
                vision.damages[i].Width = BarWidth;
                Canvas.SetLeft(vision.players[i], BarWidth + thePadding);
                // height
                double barHeight = barTotalHeight * battleInfo.damages[i] / (totalDamages == 0 ? 1 : totalDamages);
                vision.damages[i].Height = barHeight;
                vision.players[i].Height = textHeight;
                vision.players[i].FontSize = TextFontSize;
                // top
                Canvas.SetTop(vision.damages[i], top);
                double textTop = top + (barHeight - textHeight) / 2;
                double textTopMax = textHeight;
                for (int j = i + 1; j < 4; ++j) {
                    if (battleInfo.names[j] != "") {
                        textTopMax += textHeight + thePadding;
                    }
                }
                textTopMax = theCanvas.ActualHeight - textTopMax;
                if (textTop < textTopMin) { // fit in
                    textTop = textTopMin;
                } else if (textTop > textTopMax) {
                    textTop = textTopMax;
                }
                Canvas.SetTop(vision.players[i], textTop);
                top += barHeight;
                textTopMin = textTop + textHeight + thePadding;
                // reset visibility
                if (battleInfo.names[i] == "") {
                    vision.players[i].Visibility = Visibility.Hidden;
                } else {
                    vision.players[i].Visibility = Visibility.Visible;
                }
                // stroke
                if (i == battleInfo.seatId) {
                    vision.damages[i].StrokeThickness = 1.0;
                } else {
                    vision.damages[i].StrokeThickness = 0.0;
                }
                // opacity
                // vision.damages[i].Fill.Opacity = BarOpacity / 100f;
                this.Opacity = BarOpacity / 100f;
            }
            if (totalDamages == 0) {
                vision.damages[0].Height = barTotalHeight;
            }
            UpdateWidth();
        }

        private void UpdateWidth() {
            var width = new double[6];
            width[0] = vision.banner.ActualWidth;
            for (int i = 0; i < 4; ++i) {
                width[i + 1] = vision.damages[i].ActualWidth + thePadding + vision.players[i].ActualWidth;
            }
            width[5] = 20;
            this.Width = width.Max();
        }

        private void UpdateTick(object sender, EventArgs e) {
            if (!canvasReady) {
                return;
            }
            if (Game.CheckExit()) {
                if (ExitOnGameExit) {
                    Application.Current.Shutdown();
                    return;
                }
            }
            if (!Game.Ready) {
                Game.Init();
                UpdateWidth();
                return;
            }
            int seatId = Game.GetPlayerSeatId();
            string[] names = Game.GetPlayerNames();
            int[] damages = Game.GetPlayerDamages();
            bool inQuest = seatId >= 0 && names[0] != "" && damages.Sum() != 0;
            if (battleInfo.inQuest && !inQuest) {
                // exiting quest
                battleInfo.inQuest = inQuest;
                RefreshInfo();
                UpdateWidth();
            } else if (!battleInfo.inQuest && inQuest) {
                // entering quest
                battleInfo.inQuest = inQuest;
                battleInfo.seatId = seatId;
                battleInfo.names = names;
                battleInfo.damages = damages;
                for (int i = 0; i < 4; ++i) {
                    battleInfo.damageRecords[i].Clear();
                    battleInfo.damageRecords[i].Add(damages[i]);
                }
                RefreshInfo();
                RefreshLayout();
            } else if (inQuest) {
                // in quest
                for (int i = 0; i < 4; ++i) {
                    if (battleInfo.names[i] == "" && names[i] != "") {
                        // player join
                        battleInfo.names[i] = names[i];
                    }
                    // record damage
                    RecordDamage(ref battleInfo.damageRecords[i], damages[i]);
                }
                battleInfo.damages = damages;
                RefreshInfo();
                RefreshLayout();
            } else if (battleInfo.seatId == -2) {
                // static
                UpdateWidth();
            } else {
                // out of quest
                RefreshInfo();
                UpdateWidth();
            }
        }

        private void RefreshInfo() {
            int totalDamage = battleInfo.damages.Sum();
            vision.banner.Text = $"{totalDamage}";
            for (int i = 0; i < 4; ++i) {
                string text = battleInfo.names[i];
                // percentage
                if ((battleInfo.inQuest && this.ShowPercentageInQuest) || (!battleInfo.inQuest && this.ShowPercentageAfterQuest)) {
                    float percentage = 100f * battleInfo.damages[i] / totalDamage;
                    text += $" {percentage.ToString("0.0")}%";
                }
                // damage
                if ((battleInfo.inQuest && this.ShowDamageInQuest) || (!battleInfo.inQuest && this.ShowDamageAfterQuest)) {
                    text += $" {battleInfo.damages[i]}";
                }
                // dps
                if (battleInfo.inQuest && this.ShowDps) {
                    var record = battleInfo.damageRecords[i].Skip(Math.Max(0, battleInfo.damageRecords[i].Count - 200));
                    float dps = 0;
                    if (record.Count() != 0) {
                        dps = (record.Last() - record.First()) * 1000f / (record.Count() * 150f);
                    }
                    text += dps > 1 ? $" {dps.ToString("0.0")}/s" : "";
                }
                vision.players[i].Text = text;
            }
        }

        private void RecordDamage(ref List<int> record, int damage) {
            if (record.Count == 1) {
                if (record.First() != damage) {
                    record.Add(damage);
                }
            } else {
                // 200 * 150 == 30000ms
                if (record.Skip(Math.Max(0, record.Count - 200)).First() == damage) {
                    record.Clear();
                }
                record.Add(damage);
            }
        }

    }
}
