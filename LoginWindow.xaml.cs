using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json.Linq;

namespace RYLWebshopApp
{
    public partial class LoginWindow : Window
    {
        private readonly HttpClient client = new HttpClient();
        private int currentUID = 0;

        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string account = txtAccount.Text.Trim();
            string password = txtPassword.Password.Trim();

            if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
            {
                lblStatus.Text = "Please enter account and password.";
                return;
            }

            await LoginAsync(account, password);
        }
        // Make window draggable
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void txtAccount_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtAccountWatermark.Visibility = string.IsNullOrEmpty(txtAccount.Text) ? Visibility.Visible : Visibility.Hidden;
        }

        private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            txtPasswordWatermark.Visibility = string.IsNullOrEmpty(txtPassword.Password) ? Visibility.Visible : Visibility.Hidden;
        }

        private async Task LoginAsync(string account, string password)
        {
            try
            {
                var values = new Dictionary<string, string>
                {
                    { "account", account },
                    { "password", password }
                };

                var content = new FormUrlEncodedContent(values);
                var response = await client.PostAsync("http://31.58.143.7/testapi/login.php", content);
                var result = await response.Content.ReadAsStringAsync();

                // 👉 DEBUG: tunjuk JSON penuh dalam lblStatus
                lblStatus.Text = result;

                var j = JObject.Parse(result);

                if ((bool)j["success"])
                {
                    currentUID = (int)j["UID"];
                    int point = (int)j["points"];   //  ambik point dari API

                    lblStatus.Text = "Login successful!";

                    //  pass UID + chars + point
                    ShopWindow shop = new ShopWindow(currentUID, j["chars"], point);
                    shop.Show();
                    this.Close();
                }
                else
                {
                    lblStatus.Text = j["message"]?.ToString() ?? "Login failed.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error: " + ex.Message;
            }
        }
    }
}
