using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RYLWebshopApp
{
    public partial class ShopWindow : Window
    {
        private readonly HttpClient client = new HttpClient();
        private int currentUID;
        private int currentPoint;
        private int currentCash;
        private Dictionary<string, string> charMap = new Dictionary<string, string>();

        // Items lists
        private List<ShopItem> allItemsPoint = new List<ShopItem>();
        private List<ShopItem> allItemsCash = new List<ShopItem>();

        // Pagination Point
        private int currentPagePoint = 1;
        private int itemsPerPagePoint = 6;
        private int totalPagesPoint = 1;

        // Pagination Cash
        private int currentPageCash = 1;
        private int itemsPerPageCash = 6;
        private int totalPagesCash = 1;

        // Auto refresh
        private DispatcherTimer refreshTimer;

        // API folders
        private readonly string apiPointFolder = "testapi";
        private readonly string apiCashFolder = "testApi1";

        public ShopWindow(int uid, JToken charsData, int point /* initial points */)
        {
            InitializeComponent();

            currentUID = uid;
            currentPoint = point;
            currentCash = 0; // akan refresh dari API

            lblUserPoint.Text = $"Your Points: {currentPoint}";
            lblUserCash.Text = $"Your Cash: {currentCash}";

            // load character list
            comboChars.Items.Clear();
            charMap.Clear();
            if (charsData != null)
            {
                foreach (var ch in charsData)
                {
                    string name = ch["Name"]?.ToString() ?? "Unknown";
                    string cid = ch["CID"]?.ToString() ?? "";
                    comboChars.Items.Add(name);
                    charMap[name] = cid;
                }
            }
            if (comboChars.Items.Count > 0) comboChars.SelectedIndex = 0;

            lblStatusShop.Text = "";
            lblStatusShopCash.Text = "";

            // wire search
            txtSearch.TextChanged += TxtSearch_TextChanged;

            // initial load
            _ = FetchItemsPoint();
            _ = FetchItemsCash();
            _ = FetchBalances();

            // auto refresh setiap 10 saat
            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromSeconds(10);
            refreshTimer.Tick += async (s, e) =>
            {
                await FetchItemsPoint();
                await FetchItemsCash();
                await FetchBalances();
            };
            refreshTimer.Start();
        }

        // ---------------- FETCH BALANCES ----------------
        private async Task FetchBalances()
        {
            // Point
            try
            {
                string urlPoint = $"http://31.58.143.7/{apiPointFolder}/get_points.php?uid={currentUID}";
                var respPoint = await client.GetStringAsync(urlPoint);
                var jp = JObject.Parse(respPoint);
                if (jp["success"] != null && (bool)jp["success"])
                {
                    if (jp["Point"] != null && int.TryParse(jp["Point"].ToString(), out int p1))
                        currentPoint = p1;
                    else if (jp["point"] != null && int.TryParse(jp["point"].ToString(), out int p2))
                        currentPoint = p2;

                    lblUserPoint.Text = $"Your Points: {currentPoint}";
                }
            }
            catch (Exception ex)
            {
                lblStatusShop.Text = "Error refreshing points: " + ex.Message;
            }

            // Cash
            try
            {
                string urlCash = $"http://31.58.143.7/{apiCashFolder}/get_points.php?uid={currentUID}";
                var respCash = await client.GetStringAsync(urlCash);
                var jc = JObject.Parse(respCash);
                if (jc["success"] != null && (bool)jc["success"])
                {
                    if (jc["Cash"] != null && int.TryParse(jc["Cash"].ToString(), out int c1))
                        currentCash = c1;
                    else if (jc["cash"] != null && int.TryParse(jc["cash"].ToString(), out int c2))
                        currentCash = c2;
                    else if (jc["point"] != null && int.TryParse(jc["point"].ToString(), out int alt))
                        currentCash = alt;

                    lblUserCash.Text = $"Your Cash: {currentCash}";
                }
            }
            catch (Exception ex)
            {
                lblStatusShopCash.Text = "Error refreshing cash: " + ex.Message;
            }
        }

        // ---------------- FETCH ITEMS POINT ----------------
        private async Task FetchItemsPoint()
        {
            try
            {
                string url = $"http://31.58.143.7/{apiPointFolder}/get_items.php";
                var response = await client.GetStringAsync(url);
                var j = JObject.Parse(response);

                if (j["success"] != null && (bool)j["success"])
                {
                    allItemsPoint.Clear();
                    foreach (var item in j["items"])
                    {
                        allItemsPoint.Add(new ShopItem
                        {
                            ItemID = (int)item["ItemPrototypeID"],
                            ItemName = item["ItemName"].ToString(),
                            Point = item["Point"] != null ? (int)item["Point"] : 0,
                            Cash = item["Cash"] != null ? (int)item["Cash"] : 0,
                            Icon = item["Icon"]?.ToString() ?? ""
                        });
                    }
                    currentPagePoint = 1;
                    ApplyPaginationPoint(allItemsPoint);
                }
                else lblStatusShop.Text = "Failed to load point items.";
            }
            catch (Exception ex)
            {
                lblStatusShop.Text = "Error fetching point items: " + ex.Message;
            }
        }

        // ---------------- FETCH ITEMS CASH ----------------
        private async Task FetchItemsCash()
        {
            try
            {
                string url = $"http://31.58.143.7/{apiCashFolder}/get_items.php";
                var response = await client.GetStringAsync(url);
                var j = JObject.Parse(response);

                if (j["success"] != null && (bool)j["success"])
                {
                    allItemsCash.Clear();
                    foreach (var item in j["items"])
                    {
                        allItemsCash.Add(new ShopItem
                        {
                            ItemID = (int)item["ItemPrototypeID"],
                            ItemName = item["ItemName"].ToString(),
                            Point = item["Point"] != null ? (int)item["Point"] : 0,
                            Cash = item["Cash"] != null ? (int)item["Cash"] : 0,
                            Icon = item["Icon"]?.ToString() ?? ""
                        });
                    }
                    currentPageCash = 1;
                    ApplyPaginationCash(allItemsCash);
                }
                else lblStatusShopCash.Text = "Failed to load cash items.";
            }
            catch (Exception ex)
            {
                lblStatusShopCash.Text = "Error fetching cash items: " + ex.Message;
            }
        }

        // ---------------- PAGINATION POINT ----------------
        private void ApplyPaginationPoint(List<ShopItem> source)
        {
            totalPagesPoint = (int)Math.Ceiling((double)source.Count / itemsPerPagePoint);
            if (totalPagesPoint == 0) totalPagesPoint = 1;
            if (currentPagePoint > totalPagesPoint) currentPagePoint = totalPagesPoint;

            var itemsToShow = source.Skip((currentPagePoint - 1) * itemsPerPagePoint)
                                    .Take(itemsPerPagePoint).ToList();

            listItems.ItemsSource = itemsToShow;
            lblPageInfo.Text = $"Page {currentPagePoint}/{totalPagesPoint}";
            btnPrevPage.IsEnabled = currentPagePoint > 1;
            btnNextPage.IsEnabled = currentPagePoint < totalPagesPoint;
        }

        private void btnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPagePoint > 1)
            {
                currentPagePoint--;
                string query = txtSearch.Text.Trim().ToLower();
                var list = string.IsNullOrWhiteSpace(query) || query == "search item..."
                    ? allItemsPoint
                    : allItemsPoint.Where(i => i.ItemName.ToLower().Contains(query)).ToList();
                ApplyPaginationPoint(list);
            }
        }

        private void btnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPagePoint < totalPagesPoint)
            {
                currentPagePoint++;
                string query = txtSearch.Text.Trim().ToLower();
                var list = string.IsNullOrWhiteSpace(query) || query == "search item..."
                    ? allItemsPoint
                    : allItemsPoint.Where(i => i.ItemName.ToLower().Contains(query)).ToList();
                ApplyPaginationPoint(list);
            }
        }

        // ---------------- PAGINATION CASH ----------------
        private void ApplyPaginationCash(List<ShopItem> source)
        {
            totalPagesCash = (int)Math.Ceiling((double)source.Count / itemsPerPageCash);
            if (totalPagesCash == 0) totalPagesCash = 1;
            if (currentPageCash > totalPagesCash) currentPageCash = totalPagesCash;

            var itemsToShow = source.Skip((currentPageCash - 1) * itemsPerPageCash)
                                    .Take(itemsPerPageCash).ToList();

            listItemsCash.ItemsSource = itemsToShow;
            lblPageInfoCash.Text = $"Page {currentPageCash}/{totalPagesCash}";
            btnPrevPageCash.IsEnabled = currentPageCash > 1;
            btnNextPageCash.IsEnabled = currentPageCash < totalPagesCash;
        }

        private void btnPrevPageCash_Click(object sender, RoutedEventArgs e)
        {
            if (currentPageCash > 1)
            {
                currentPageCash--;
                string query = txtSearch.Text.Trim().ToLower();
                var list = string.IsNullOrWhiteSpace(query) || query == "search item..."
                    ? allItemsCash
                    : allItemsCash.Where(i => i.ItemName.ToLower().Contains(query)).ToList();
                ApplyPaginationCash(list);
            }
        }

        private void btnNextPageCash_Click(object sender, RoutedEventArgs e)
        {
            if (currentPageCash < totalPagesCash)
            {
                currentPageCash++;
                string query = txtSearch.Text.Trim().ToLower();
                var list = string.IsNullOrWhiteSpace(query) || query == "search item..."
                    ? allItemsCash
                    : allItemsCash.Where(i => i.ItemName.ToLower().Contains(query)).ToList();
                ApplyPaginationCash(list);
            }
        }

        // ---------------- SEARCH ----------------
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtSearch.Text == "Search item...") return;
            string query = txtSearch.Text.Trim().ToLower();

            if (pagePoint.Visibility == Visibility.Visible)
            {
                currentPagePoint = 1;
                ApplyPaginationPoint(allItemsPoint.Where(i => i.ItemName.ToLower().Contains(query)).ToList());
            }
            else
            {
                currentPageCash = 1;
                ApplyPaginationCash(allItemsCash.Where(i => i.ItemName.ToLower().Contains(query)).ToList());
            }
        }

        // ---------------- BUY POINT ----------------
        private async void btnBuyItem_Click(object sender, RoutedEventArgs e)
        {
            lblStatusShop.Text = "";

            if (comboChars.SelectedIndex == -1) { lblStatusShop.Text = "Select a character first"; return; }
            var selectedItem = listItems.SelectedItem as ShopItem;
            if (selectedItem == null) { lblStatusShop.Text = "Select an item first"; return; }
            if (!int.TryParse(txtAmount.Text.Trim(), out int amount) || amount <= 0)
            { lblStatusShop.Text = "Invalid amount."; return; }

            string charCID = charMap[comboChars.SelectedItem.ToString()];
            int totalCost = selectedItem.Point * amount;
            if (totalCost > currentPoint)
            { lblStatusShop.Text = $"Not enough points. Need {totalCost}, you have {currentPoint}."; return; }

            var values = new Dictionary<string, string>
            {
                {"UID", currentUID.ToString()},
                {"CharCID", charCID},
                {"ItemID", selectedItem.ItemID.ToString()},
                {"Amount", amount.ToString()},
                {"Price", selectedItem.Point.ToString()}
            };

            try
            {
                var response = await client.PostAsync($"http://31.58.143.7/{apiPointFolder}/buy_item_ajax.php", new FormUrlEncodedContent(values));
                var j = JObject.Parse(await response.Content.ReadAsStringAsync());

                lblStatusShop.Text = j["message"]?.ToString() ?? "No response";
                if (j["success"] != null && (bool)j["success"])
                {
                    if (j["newPoints"] != null && int.TryParse(j["newPoints"].ToString(), out int newP))
                    {
                        currentPoint = newP;
                        lblUserPoint.Text = $"Your Points: {currentPoint}";
                    }
                    await FetchItemsPoint();
                    await FetchBalances();
                }
            }
            catch (Exception ex)
            {
                lblStatusShop.Text = "Error buying item: " + ex.Message;
            }
        }

        // ---------------- BUY CASH ----------------
        private async void btnBuyItemCash_Click(object sender, RoutedEventArgs e)
        {
            lblStatusShopCash.Text = "";

            if (comboChars.SelectedIndex == -1) { lblStatusShopCash.Text = "Select a character first"; return; }
            var selectedItem = listItemsCash.SelectedItem as ShopItem;
            if (selectedItem == null) { lblStatusShopCash.Text = "Select an item first"; return; }
            if (!int.TryParse(txtAmountCash.Text.Trim(), out int amount) || amount <= 0)
            { lblStatusShopCash.Text = "Invalid amount."; return; }

            string charCID = charMap[comboChars.SelectedItem.ToString()];
            int totalCost = selectedItem.Cash * amount;
            if (totalCost > currentCash)
            { lblStatusShopCash.Text = $"Not enough cash. Need {totalCost}, you have {currentCash}."; return; }

            var values = new Dictionary<string, string>
            {
                {"UID", currentUID.ToString()},
                {"CharCID", charCID},
                {"ItemID", selectedItem.ItemID.ToString()},
                {"Amount", amount.ToString()},
                {"Price", selectedItem.Cash.ToString()}
            };

            try
            {
                var response = await client.PostAsync($"http://31.58.143.7/{apiCashFolder}/buy_item_ajax.php", new FormUrlEncodedContent(values));
                var j = JObject.Parse(await response.Content.ReadAsStringAsync());

                lblStatusShopCash.Text = j["message"]?.ToString() ?? "No response";
                if (j["success"] != null && (bool)j["success"])
                {
                    if (j["newCash"] != null && int.TryParse(j["newCash"].ToString(), out int newC))
                        currentCash = newC;
                    else if (j["newCash"] != null && int.TryParse(j["newCash"].ToString(), out int newC2))
                        currentCash = newC2;

                    lblUserCash.Text = $"Your Cash: {currentCash}";
                    await FetchItemsCash();
                    await FetchBalances();
                }
            }
            catch (Exception ex)
            {
                lblStatusShopCash.Text = "Error buying item (cash): " + ex.Message;
            }
        }

        // ---------------- PAGE SWITCH ----------------
        private void btnPoint_Click(object sender, RoutedEventArgs e)
        {
            pagePoint.Visibility = Visibility.Visible;
            pageCash.Visibility = Visibility.Collapsed;
            lblStatusShop.Text = "";
        }

        private void btnCash_Click(object sender, RoutedEventArgs e)
        {
            pagePoint.Visibility = Visibility.Collapsed;
            pageCash.Visibility = Visibility.Visible;
            lblStatusShopCash.Text = "";
        }
        private async void btnBattlePass_Click(object sender, RoutedEventArgs e)
        {
            // 1️⃣ Pastikan pemain pilih character dulu
            if (comboChars.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a character first before opening Battle Pass.",
                                "No Character Selected",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            string selectedName = comboChars.SelectedItem.ToString();
            if (!charMap.ContainsKey(selectedName))
            {
                MessageBox.Show("Character not found. Please try again.",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;
            }

            string charCid = charMap[selectedName];

            var battlePassWindow = new BattlePassWindow(currentUID, charCid);
            battlePassWindow.ShowDialog();
        }
        





        // ---------------- SEARCH Placeholder ----------------
        private void txtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtSearch.Text == "Search item...")
            {
                txtSearch.Text = "";
                txtSearch.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void txtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                txtSearch.Text = "Search item...";
                txtSearch.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }
    }

    // ---------------- SHOP ITEM CLASS ----------------
    public class ShopItem
    {
        public int ItemID { get; set; }
        public string ItemName { get; set; }
        public int Point { get; set; } = 0;
        public int Cash { get; set; } = 0;
        public string Icon { get; set; }
    }
    public static class SessionManager
    {
        public static int UID { get; set; }
    }
}
