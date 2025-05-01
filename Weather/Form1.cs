using System;
using System.Net.Http;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq; // 需加上 Newtonsoft.Json 套件
using System.Drawing;
using System.Collections.Generic;

namespace Weather
{
    public partial class Form1 : Form
    {
        private readonly string apiKey = "a0f224b44a20b00528ca4f36aa12a43b"; // API 金鑰
        private System.Windows.Forms.Timer debounceTimer;
        private int debounceInterval = 300; // 毫秒
        private double pendingLat;
        private double pendingLon;
        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
            debounceTimer = new System.Windows.Forms.Timer();
            debounceTimer.Interval = debounceInterval;
            //debounceTimer.Tick += DebounceTimer_Tick;

        }
        private Point mouseDownLocation;
        private bool isDragging = false;
        private void Form1_Load(object sender, EventArgs e)
        {
            // 設定視窗一開始大小
            this.Width = 1200;
            this.Height = 800;
            this.AutoScroll = false;  // 表單不要自動出現捲動條
            pictureBox1.Anchor = AnchorStyles.None;  // PictureBox 不跟著拉伸
            // 放地圖到 pictureBox
            pictureBox1.Image = Image.FromFile("world_map.jpg");
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox1.Dock = DockStyle.None;
            pictureBox1.Width = 1000;
            pictureBox1.Height = 600;
            pictureBox1.Left = 100;
            pictureBox1.Top = 100;

            // 自動加事件
            pictureBox1.MouseWheel += PictureBox1_MouseWheel;
            pictureBox1.MouseDown += PictureBox1_MouseDown;
            pictureBox1.MouseMove += PictureBox1_MouseMove;
            pictureBox1.MouseUp += PictureBox1_MouseUp;

            // 讓 PictureBox 可以收到滑鼠滾輪
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

        private void PictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                pictureBox1.Left += e.X - mouseDownLocation.X;
                pictureBox1.Top += e.Y - mouseDownLocation.Y;
            }
            if (!isDragging) // 拖曳時不觸發
            {
                double lon = (e.X / (double)pictureBox1.Width) * 360 - 180;
                double lat = 90 - (e.Y / (double)pictureBox1.Height) * 180;

                pendingLat = lat;
                pendingLon = lon;

                debounceTimer.Stop();  // 重置計時器
                debounceTimer.Start(); // 重新開始計時
            }
        }


        private async void DebounceTimer_Tick(object sender, EventArgs e)
        {
            debounceTimer.Stop(); // 防止重複執行

            string cityInfo = await GetCityNameByCoordinatesAsync(pendingLat, pendingLon);
            label1.Text = cityInfo;
        }



        private void PictureBox1_MouseWheel(object sender, MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                float zoomFactor = 1.1f; // 每次滾輪放大/縮小比例

                if (e.Delta < 0) // 滾輪向後，縮小
                    zoomFactor = 1f / zoomFactor;

                int oldWidth = pictureBox1.Width;
                int oldHeight = pictureBox1.Height;

                pictureBox1.Width = (int)(pictureBox1.Width * zoomFactor);
                pictureBox1.Height = (int)(pictureBox1.Height * zoomFactor);

                // 讓縮放是以滑鼠位置為中心
                int mouseX = e.X;
                int mouseY = e.Y;
                int deltaX = (int)(mouseX * (pictureBox1.Width / (float)oldWidth - 1));
                int deltaY = (int)(mouseY * (pictureBox1.Height / (float)oldHeight - 1));
                pictureBox1.Left -= deltaX;
                pictureBox1.Top -= deltaY;
            }
        }



        private async void button1_Click(object sender, EventArgs e)
        {
            string city = textBox1.Text;
            if (!string.IsNullOrWhiteSpace(city))
            {
                string weatherInfo = await GetWeatherAsync(city);
                label1.Text = weatherInfo;
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

                    return $"城市：{city}\n天氣：{weather}\n溫度：{temp}°C";
                }
                catch (Exception ex)
                {
                    return $"取得天氣資訊失敗：{ex.Message}";
                }
            }
        }

        private async Task<string> TranslateCityOnlineAsync(string englishCity)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var values = new Dictionary<string, string>
            {
                { "q", englishCity },
                { "source", "en" },  // 英文
                { "target", "zh" },  // 中文
                { "format", "text" }
            };

                    var content = new FormUrlEncodedContent(values);
                    var response = await client.PostAsync("https://libretranslate.de/translate", content);
                    string responseString = await response.Content.ReadAsStringAsync();

                    JObject json = JObject.Parse(responseString);
                    string translatedText = json["translatedText"].ToString();

                    return translatedText;
                }
                catch (Exception ex)
                {
                    return englishCity; // 如果失敗就直接回傳原本英文
                }
            }
        }

        private async Task<string> GetCityNameByCoordinatesAsync(double lat, double lon)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string url = $"https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lon}&format=json&accept-language=zh-TW";
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0"); // 必要，否則 Nominatim 不給你存取

                    string response = await client.GetStringAsync(url);
                    JObject json = JObject.Parse(response);

                    string city = json["address"]?["city"]?.ToString() ??
                                  json["address"]?["town"]?.ToString() ??
                                  json["address"]?["village"]?.ToString() ??
                                  json["address"]?["state"]?.ToString() ??
                                  "未知地點";

                    return $"城市：{city}";
                }
                catch (Exception ex)
                {
                    return $"查詢城市失敗：{ex.Message}";
                }
            }
        }

        private async Task<string> GetWeatherByCoordinatesAsync(double lat, double lon)
        {
            //創造一個用來發送 HTTP 請求的物件，等區塊執行完會自動釋放資源
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string url = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={apiKey}&units=metric&lang=zh_tw";
                    string response = await client.GetStringAsync(url);
                    JObject json = JObject.Parse(response);//讀 JSON 檔案

                    long dt = json["dt"].ToObject<long>(); // 取得UNIX時間
                    DateTime updateTime = DateTimeOffset.FromUnixTimeSeconds(dt).LocalDateTime;

                    string city = json["name"]?.ToString() ?? "未知城市";

                    string temp = json["main"]["temp"].ToString();
                    /*"weather": [ {"description": "多雲"}]*/
                    string weather = json["weather"][0]["description"].ToString();

                    string translatedCity = await TranslateCityOnlineAsync(city);

                    return $"城市：{city}\n天氣：{weather}\n溫度：{temp}°C\n資料時間：{updateTime}";
                }
                catch (Exception ex)
                {
                    return $"取得天氣資訊失敗：{ex.Message}";
                }
            }
        }

        private async void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            // 將點擊位置轉為經緯度（假設為 equirectangular 地圖）
            double lon = (e.X / (double)pictureBox1.Width) * 360 - 180;
            //這是把滑鼠的水平位置，轉成一個 0 到 1 的比例數字（0在最左，1在最右）。
            //然後再 * 360：代表整個地球的經度範圍是 360 度（從 - 180° 到 180°）。
            //再減 180：因為地圖的左邊是西經 180°，右邊是東經 180°。
            double lat = 90 - (e.Y / (double)pictureBox1.Height) * 180;
            //因為螢幕的 Y 軸上面是 0 但緯度是「上面是 90°，下面是 - 90°」，所以要這樣扣回來。
            string weather = await GetWeatherByCoordinatesAsync(lat, lon);
            label1.Text = $"座標: {lat:F2}, {lon:F2}\n{weather}";
        }

    }
}
