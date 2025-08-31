using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;

namespace RYLWebshopApp
{
    // ================================
    // BattlePassReward class - IMPROVED
    // ================================
    public class BattlePassReward : INotifyPropertyChanged
    {
        public int Day { get; set; }
        public string ItemName { get; set; }
        public string ItemType { get; set; } // "points", "cash", "item"
        public int RewardValue { get; set; }
        private string _icon;

        public string Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(IconImage));
            }
        }

        public BitmapImage IconImage
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(_icon))
                        return null;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_icon, UriKind.RelativeOrAbsolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.EndInit();
                    return bitmap;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Error loading image] {_icon} - {ex.Message}");
                    return null;
                }
            }
        }

        private BitmapImage CreateFallbackImage()
        {
            try
            {
                // Create a simple colored square as fallback
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri("pack://application:,,,/Resources/default_item.png", UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                // If even fallback fails, return null
                return null;
            }
        }

        public string Description { get; set; }

        // Display properties
        private string _dayText;
        private string _valueText;
        private string _buttonText;
        private SolidColorBrush _buttonColor;
        private object _buttonStyle;
        private bool _isClaimable;
        private bool _isClaimed;
        private bool _isLocked;

        public string DayText { get => _dayText; set { _dayText = value; OnPropertyChanged(nameof(DayText)); } }
        public string ValueText { get => _valueText; set { _valueText = value; OnPropertyChanged(nameof(ValueText)); } }
        public string ButtonText { get => _buttonText; set { _buttonText = value; OnPropertyChanged(nameof(ButtonText)); } }
        public SolidColorBrush ButtonColor { get => _buttonColor; set { _buttonColor = value; OnPropertyChanged(nameof(ButtonColor)); } }
        public object ButtonStyle { get => _buttonStyle; set { _buttonStyle = value; OnPropertyChanged(nameof(ButtonStyle)); } }
        public bool IsClaimable { get => _isClaimable; set { _isClaimable = value; OnPropertyChanged(nameof(IsClaimable)); } }
        public bool IsClaimed { get => _isClaimed; set { _isClaimed = value; OnPropertyChanged(nameof(IsClaimed)); } }
        public bool IsLocked { get => _isLocked; set { _isLocked = value; OnPropertyChanged(nameof(IsLocked)); } }

        public void UpdateDisplayProperties(int currentDay, List<int> claimedDays)
        {
            DayText = $"Day {Day}";

            // Enhanced value text with item name
            if (!string.IsNullOrEmpty(ItemName))
            {
                ValueText = $"{ItemName} x{RewardValue}";
            }
            else
            {
                switch (ItemType?.ToLower())
                {
                    case "points":
                        ValueText = $"⭐ {RewardValue} pts";
                        break;
                    case "cash":
                        ValueText = $"💰 {RewardValue} cash";
                        break;
                    default:
                        ValueText = $"🎁 Item x{RewardValue}";
                        break;
                }
            }

            IsClaimed = claimedDays.Contains(Day);
            IsClaimable = Day <= currentDay && !IsClaimed;
            IsLocked = Day > currentDay;

            if (IsClaimed)
            {
                ButtonText = "✓ Claimed";
                ButtonColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
            }
            else if (IsClaimable)
            {
                ButtonText = "Claim";
                ButtonColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            }
            else
            {
                ButtonText = "🔒 Locked";
                ButtonColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // ================================
    // BattlePassWindow class - IMPROVED
    // ================================
    public partial class BattlePassWindow : Window
    {
        private readonly HttpClient client = new HttpClient();
        private int currentUID;
        private string currentCharCid;
        private List<BattlePassReward> allRewards = new List<BattlePassReward>();
        private List<int> claimedDays = new List<int>();
        private int currentDay = 1;
        private int currentWeek = 1;
        private DispatcherTimer refreshTimer;
        private string battlepassExpiry;
        private readonly string apiBaseUrl = "http://31.58.143.7/testapiBP";

        public BattlePassWindow(int uid, string charCid)
        {
            InitializeComponent();
            currentUID = uid;
            currentCharCid = charCid;
            lblCharInfo.Text = $"Character: {currentCharCid}";

            // Configure HttpClient for better image loading
            client.Timeout = TimeSpan.FromSeconds(10);

            InitializeBattlePass();
            SetupAutoRefresh();
        }

        private async void InitializeBattlePass()
        {
            if (string.IsNullOrWhiteSpace(currentCharCid))
            {
                ShowStatus("⚠️ Please select a character before using Battle Pass", false);
                return;
            }

            await LoadBattlePassData();
            await LoadUserProgress();
            UpdateUI();
        }

        private async Task LoadBattlePassData()
        {
            try
            {
                string url = $"{apiBaseUrl}/testapiBP.php";
                var response = await client.GetStringAsync(url);
                var seasonData = JObject.Parse(response);

                if (seasonData["rewards"] != null)
                {
                    allRewards.Clear();
                    foreach (var reward in seasonData["rewards"])
                    {
                        // ✅ All data from server API - no hardcoding!
                        allRewards.Add(new BattlePassReward
                        {
                            Day = (int)reward["day"],
                            ItemName = reward["item_name"]?.ToString() ?? "Unknown Item",
                            ItemType = reward["item_type"]?.ToString() ?? "item",
                            RewardValue = (int)(reward["reward_value"] ?? reward["amount"] ?? 1),
                            Icon = reward["icon"]?.ToString(),
                            Description = reward["description"]?.ToString() ?? ""
                        });
                    }

                    // ✅ Update UI elements from server data
                    if (seasonData["name"] != null)
                        this.Title = $"RYL Point - {seasonData["name"]}";

                    if (seasonData["end_date"] != null)
                    {
                        DateTime endDate = DateTime.Parse(seasonData["end_date"].ToString());
                        lblSeasonEnd.Text = $"Ends: {endDate:MMM dd, yyyy}";
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Unable to load battle pass data: {ex.Message}", false);
                // ✅ No fallback creation - let user know there's an issue
                allRewards.Clear();
            }
        }


        private async Task LoadUserProgress()
        {
            if (string.IsNullOrWhiteSpace(currentCharCid)) return;

            try
            {
                string url = $"{apiBaseUrl}/get_progress.php?UID={currentUID}&CharCID={Uri.EscapeDataString(currentCharCid)}";
                var response = await client.GetStringAsync(url);
                var progressData = JObject.Parse(response);

                currentDay = (int)(progressData["data"]?["current_day"] ?? 1);
                claimedDays.Clear();

                if (progressData["data"]?["claimed_days"] != null)
                    foreach (var day in progressData["data"]["claimed_days"])
                        claimedDays.Add((int)day);

                UpdateSeasonProgress();
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading progress: {ex.Message}", false);
            }
        }

        private void UpdateUI()
        {
            UpdateWeekDisplay();
            UpdateStats();
        }

        private void UpdateWeekDisplay()
        {
            lblWeekInfo.Text = $"Week {currentWeek}";
            int startDay = (currentWeek - 1) * 7 + 1;
            int endDay = Math.Min(startDay + 6, 30);

            var weekRewards = allRewards
                .Where(r => r.Day >= startDay && r.Day <= endDay)
                .ToList();

            foreach (var reward in weekRewards)
            {
                reward.UpdateDisplayProperties(currentDay, claimedDays);

                if (string.IsNullOrWhiteSpace(currentCharCid))
                {
                    reward.IsClaimable = false;
                    reward.IsLocked = true;
                    reward.ButtonText = "⚠️ No Char";
                    reward.ButtonColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
                }
            }

            rewardsContainer.ItemsSource = weekRewards;

            btnPrevWeek.IsEnabled = currentWeek > 1;
            btnNextWeek.IsEnabled = currentWeek < 5;
        }

        private void UpdateStats()
        {
            lblClaimedCount.Text = claimedDays.Count.ToString();
            lblAvailableToday.Text = (currentDay > 0 && !claimedDays.Contains(currentDay) && currentDay <= 30) ? "1" : "0";
            lblStreak.Text = GetCurrentStreak().ToString();
            UpdateSeasonProgress();
        }

        private void UpdateSeasonProgress()
        {
            int progressPercent = (int)Math.Round((currentDay / 30.0) * 100);
            lblProgress.Text = $"{progressPercent}%";
            lblSeasonProgress.Text = $"{progressPercent}%";
            lblSeasonInfo.Text = $"Day {currentDay} of 30";

            double progressWidth = (currentDay / 30.0) * 300;
            progressBar.Width = Math.Max(5, progressWidth);
        }

        private int GetCurrentStreak()
        {
            if (claimedDays.Count == 0) return 0;

            var sortedDays = claimedDays.OrderByDescending(d => d).ToList();
            int streak = 0;
            int expectedDay = currentDay - 1;

            foreach (int day in sortedDays)
            {
                if (day == expectedDay)
                {
                    streak++;
                    expectedDay--;
                }
                else break;
            }

            return streak;
        }

        private async void ClaimReward_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(currentCharCid))
            {
                ShowStatus("⚠️ Please select a character before claiming reward", false);
                return;
            }

            if (!(sender is Button button) || !(button.Tag is int day)) return;
            if (!button.IsEnabled) return;

            button.IsEnabled = false;
            button.Content = "...";

            try
            {
                var claimData = new { UID = currentUID, CharCID = currentCharCid };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(claimData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                string url = $"{apiBaseUrl}/claim_reward.php";

                var response = await client.PostAsync(url, content);
                var responseText = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(responseText);

                bool success = (bool)(result["success"] ?? false);
                string message = result["message"]?.ToString() ?? "Unknown response";

                if (success)
                {
                    // Update claimed days
                    if (result["claimed_days"] != null)
                    {
                        claimedDays.Clear();
                        foreach (var d in result["claimed_days"])
                            claimedDays.Add((int)d);
                    }

                    // Update current day from response
                    if (result["day"] != null)
                        currentDay = Math.Max(currentDay, (int)result["day"]);

                    // Update item info if available
                    var reward = allRewards.FirstOrDefault(r => r.Day == day);
                    if (reward != null)
                    {
                        // Update with server response data
                        if (result["item_name"] != null)
                            reward.ItemName = result["item_name"].ToString();

                        if (result["image"] != null)
                            reward.Icon = result["image"].ToString();

                        if (result["amount"] != null)
                            reward.RewardValue = (int)result["amount"];
                    }

                    ShowStatus($"🎉 {message}", true);
                    UpdateUI();
                }
                else
                {
                    ShowStatus($"❌ {message}", false);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error claiming reward: {ex.Message}", false);
            }
            finally
            {
                UpdateWeekDisplay();
            }
        }

        private void ShowStatus(string message, bool isSuccess)
        {
            lblStatus.Text = message;
            statusBorder.Background = new SolidColorBrush(isSuccess ? (Color)ColorConverter.ConvertFromString("#F0FDF4") : (Color)ColorConverter.ConvertFromString("#FEF2F2"));
            statusBorder.BorderBrush = new SolidColorBrush(isSuccess ? (Color)ColorConverter.ConvertFromString("#BBF7D0") : (Color)ColorConverter.ConvertFromString("#FECACA"));
            lblStatus.Foreground = new SolidColorBrush(isSuccess ? (Color)ColorConverter.ConvertFromString("#166534") : (Color)ColorConverter.ConvertFromString("#DC2626"));
            statusBorder.Visibility = Visibility.Visible;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) =>
            {
                statusBorder.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        private void btnPrevWeek_Click(object sender, RoutedEventArgs e)
        {
            if (currentWeek > 1)
            {
                currentWeek--;
                UpdateWeekDisplay();
            }
        }

        private void btnNextWeek_Click(object sender, RoutedEventArgs e)
        {
            if (currentWeek < 5)
            {
                currentWeek++;
                UpdateWeekDisplay();
            }
        }
        // BattlePassWindow.xaml.cs
        private async void btnBuyBattlePass_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Gunakan UID + CharCID dari luar panel
                if (string.IsNullOrEmpty(currentCharCid))
                {
                    MessageBox.Show("Character not selected!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var requestData = new { UID = currentUID, CharCID = currentCharCid };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(requestData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                string url = "http://31.58.143.7/testApiBP/buy_battlepass.php";
                var response = await client.PostAsync(url, content);
                string responseText = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(responseText);

                bool success = (bool)(result["success"] ?? false);
                string message = result["message"]?.ToString() ?? "Unknown response";

                if (success)
                {
                    MessageBox.Show($"✅ {message}", "Battlepass Purchased", MessageBoxButton.OK, MessageBoxImage.Information);
                    battlepassExpiry = result["expiry_date"]?.ToString();
                    btnBuyBattlePass.IsEnabled = false; // Optional: disable button selepas beli
                    btnBuyBattlePass.Content = "Already Purchased";
                }
                else
                {
                    MessageBox.Show($"❌ {message}", "Purchase Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error buying Battlepass: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupAutoRefresh()
        {
            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            refreshTimer.Tick += async (s, e) =>
            {
                await LoadUserProgress();
                UpdateStats();
            };
            refreshTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            refreshTimer?.Stop();
            client?.Dispose();
            base.OnClosed(e);
        }
    }
}