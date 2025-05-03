using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace Weather
{
    public partial class Form1 : Form
    {
        private readonly string apiKey = "a0f224b44a20b00528ca4f36aa12a43b";

        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
        }

        private Point mouseDownLocation;
        private bool isDragging = false;

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Width = 1200;
            this.Height = 800;
            this.AutoScroll = false;

            pictureBox1.Image = Image.FromFile("world_map.jpg");
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox1.Dock = DockStyle.None;
            pictureBox1.Width = 1000;
            pictureBox1.Height = 600;
            pictureBox1.Left = 100;
            pictureBox1.Top = 100;

            pictureBox1.MouseWheel += PictureBox1_MouseWheel;
            pictureBox1.MouseDown += PictureBox1_MouseDown;
            pictureBox1.MouseMove += PictureBox1_MouseMove;
            pictureBox1.MouseUp += PictureBox1_MouseUp;
            pictureBox1.MouseClick += pictureBox1_MouseClick;

            pictureBox1.Focus();
            pictureBox1.MouseEnter += (s, ev) => pictureBox1.Focus();
        }

        private void PictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                mouseDownLocation = e.Location;
            }
        }

        private void PictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        private void PictureBox1_MouseMove(object sender, MouseEventArgs e) //拖曳移動功能
        {
            if (isDragging)
            {
                pictureBox1.Left += e.X - mouseDownLocation.X; //mouseDownLocation 是記錄滑鼠按下時的相對座標。   
                pictureBox1.Top += e.Y - mouseDownLocation.Y;
            }
        }
        private void pictureBox1_MouseEnter(object sender, EventArgs e)
        {
            pictureBox1.Focus();
        }

        private void PictureBox1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys.HasFlag(Keys.Control))

            {
                float zoomFactor = 1.1f;

                if (e.Delta < 0)
                    zoomFactor = 1f / zoomFactor;

                int newWidth = (int)(pictureBox1.Width * zoomFactor);
                int newHeight = (int)(pictureBox1.Height * zoomFactor);

                // 限制最小與最大尺寸（你可調整這些數值）
                int minWidth = 1000;
                int minHeight = 600;
                int maxWidth = 10000;
                int maxHeight =10000;

                if (newWidth < minWidth || newHeight < minHeight || newWidth > maxWidth || newHeight > maxHeight)
                    return; // 超出範圍就不縮放

                int oldWidth = pictureBox1.Width;
                int oldHeight = pictureBox1.Height;

                pictureBox1.Width = newWidth;
                pictureBox1.Height = newHeight;

                int mouseX = e.X;
                int mouseY = e.Y;
                float scaleX = (float)newWidth / oldWidth;
                float scaleY = (float)newHeight / oldHeight;
                int deltaX = (int)(mouseX * (scaleX - 1));
                int deltaY = (int)(mouseY * (scaleY - 1));
                pictureBox1.Left -= deltaX;
                pictureBox1.Top -= deltaY;
            }
        }


        private async void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            double lon = (e.X / (double)pictureBox1.Width) * 360 - 179.5;
            double lat = 90.5 - (e.Y / (double)pictureBox1.Height) * 180;

            var (zhName, enName) = await GetCityNameByCoordinatesAsync(lat, lon);

            if (zhName.StartsWith("查詢城市失敗"))
            {
                label1.Text = zhName;
                return;
            }
            string[] unwantedWords = { "City", "County" };
            if (enName != "New Taipei City")
            {
                foreach (string word in unwantedWords)
                {
                    enName = enName.Replace(word, ""); 
                }
            }
            string weatherInfo = await GetWeatherAsync(enName);
                
            label1.Text = $"城市：{zhName} ({enName})\n{weatherInfo}\n";

        }
        private readonly string tomTomApiKey = "HMWpaPpAicsd42z1iAEY1TfsosnHaR4F";
        private async Task<(string zhName, string enName)> GetCityNameByCoordinatesAsync(double lat, double lon)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string ExtractCity(string jsonStr)
                    {
                        JObject json = JObject.Parse(jsonStr);
                        return json["addresses"]?[0]?["address"]?["municipality"]?.ToString() ??
                               json["addresses"]?[0]?["address"]?["municipalitySubdivision"]?.ToString() ??
                               json["addresses"]?[0]?["address"]?["countrySubdivision"]?.ToString() ??
                               json["addresses"]?[0]?["address"]?["countrySecondarySubdivision"]?.ToString() ??
                               json["addresses"]?[0]?["address"]?["country"]?.ToString() ??
                               "未知地點";
                    }

                    string urlZh = $"https://api.tomtom.com/search/2/reverseGeocode/{lat},{lon}.json?key={tomTomApiKey}&language=zh-TW";
                    string urlEn = $"https://api.tomtom.com/search/2/reverseGeocode/{lat},{lon}.json?key={tomTomApiKey}&language=en-US";

                    HttpResponseMessage responseZh = await client.GetAsync(urlZh);
                    HttpResponseMessage responseEn = await client.GetAsync(urlEn);

                    if (!responseZh.IsSuccessStatusCode || !responseEn.IsSuccessStatusCode)
                    {
                        return ($"查詢城市失敗：HTTP {responseZh.StatusCode} / {responseEn.StatusCode}", "");
                    }

                    string jsonZh = await responseZh.Content.ReadAsStringAsync();
                    string jsonEn = await responseEn.Content.ReadAsStringAsync();

                    string zhName = ExtractCity(jsonZh);
                    string enName = ExtractCity(jsonEn);

                    return (zhName, enName);
                }
                catch (Exception ex)
                {
                    return ($"查詢城市失敗：{ex.Message}", "");
                }
            }
        }



        private async Task<string> GetWeatherAsync(string city)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric&lang=zh_tw";
                    string response = await client.GetStringAsync(url);
                    JObject json = JObject.Parse(response);

                    string temp = json["main"]["temp"].ToString();
                    string weather = json["weather"][0]["description"].ToString();

                    return $"天氣：{weather}\n溫度：{temp}°C";
                }
                catch (Exception ex)
                {
                    return $"取得{city}天氣資訊失敗：{ex.Message}";
                }
            }
        }
    }
}
