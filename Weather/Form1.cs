using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;


namespace Weather
{
    public partial class Form1 : Form
    {
        private readonly string apiKey = "a0f224b44a20b00528ca4f36aa12a43b";
        private const string UnsplashAccessKey = "QQxvNvDKTnvuWKAD8ToLAswiqTqih3a_3bNCi8rDK7A"; 
        private List<(string city, string countryCode)> cityList = new List<(string, string)>();
        private Random rand = new Random();
        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
            LoadCitiesFromFile("cities1000.txt");
        }

        private Point mouseDownLocation;
        private bool isDragging = false;
        private void LoadCitiesFromFile(string filePath)
        {
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    var parts = line.Split('\t');
                    if (parts.Length > 8)
                    {
                        string city = parts[1];
                        string countryCode = parts[8];
                        cityList.Add((city, countryCode));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("讀取城市檔案錯誤：" + ex.Message);
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            this.Width = 1200;
            this.Height = 800;
            this.AutoScroll = false;

            pictureBox1.Image = Image.FromFile("world_map.jpg");
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
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
            double lon = (e.X / (double)pictureBox1.Width) * 360 - 179;
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
            string summary = await GetCitySummaryAsync(zhName, enName);
            textBox1.Text = summary;

            Image cityImg = await GetCityImageAsync(enName);
            if (cityImg != null)
                pictureBox2.Image = cityImg;
            else
                pictureBox2.Image = null;



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
                        return ($"查詢城市失敗", "");
                    }

                    string jsonZh = await responseZh.Content.ReadAsStringAsync();
                    string jsonEn = await responseEn.Content.ReadAsStringAsync();

                    string zhName = ExtractCity(jsonZh);
                    string enName = ExtractCity(jsonEn);

                    return (zhName, enName);
                }
                catch (Exception ex)
                {
                    return ($"查詢城市失敗", "");
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
                    return $"取得{city}天氣資訊失敗";
                }
            }
        }
        private async Task<string> GetCitySummaryAsync(string zhName, string enName)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // 先嘗試中文維基百科
                    string zhUrl = $"https://zh.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(zhName)}";
                    HttpResponseMessage zhRes = await client.GetAsync(zhUrl);

                    if (zhRes.IsSuccessStatusCode)
                    {
                        string zhContent = await zhRes.Content.ReadAsStringAsync();
                        JObject zhJson = JObject.Parse(zhContent);
                        string zhExtract = zhJson["extract"]?.ToString();

                        if (!string.IsNullOrEmpty(zhExtract)) return zhExtract;
                    }

                    // 如果中文沒資料，嘗試英文摘要 + 翻譯
                    string enUrl = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(enName)}";
                    HttpResponseMessage enRes = await client.GetAsync(enUrl);

                    if (!enRes.IsSuccessStatusCode) return "找不到該城市的簡介資料。";

                    string enContent = await enRes.Content.ReadAsStringAsync();
                    JObject enJson = JObject.Parse(enContent);
                    string enExtract = enJson["extract"]?.ToString();

                    if (string.IsNullOrEmpty(enExtract)) return "找不到該城市的簡介資料。";

                    // 使用簡單的 DeepL 或 OpenAI 翻譯（此處用 Google Translate 網址示意）
                    //string translated = await TranslateToChinese(enExtract);
                    //return translated;
                    return enExtract;
                }
                catch (Exception ex)
                {
                    return $"查詢城市簡介失敗";
                }
            }
        }
        private async Task<Image> GetCityImageAsync(string enName)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(enName)}";
                    string response = await client.GetStringAsync(url);
                    JObject json = JObject.Parse(response);
                    string imgUrl = json["thumbnail"]?["source"]?.ToString();

                    if (!string.IsNullOrEmpty(imgUrl))
                    {
                        var stream = await client.GetStreamAsync(imgUrl);
                        return Image.FromStream(stream);
                    }
                }
                catch { }
                return null;
            }
        }

        private (double lat, double lon)? player1Coords = null;
        private (double lat, double lon)? player2Coords = null;
        private int currentPlayer = 1; // 玩家1先點


        private async void button1_Click(object sender, EventArgs e)
        {
            //TODO:按一下這個開始遊戲 先把地圖 變成預設載入的大小 鎖定地圖讓他不能夠改大小
            //TODO:2個使用者可以拖曳某個物品到地圖上面 
            if (cityList.Count == 0)
            {
                MessageBox.Show("城市清單為空！");
                return;
            }

            var (city, countryCode) = cityList[rand.Next(cityList.Count)];
            string countryName = await GetCountryNameFromCode(countryCode);
            if (countryName == null)
            {
                MessageBox.Show($"找不到國家代碼 {countryCode}");
                return;
            }

            string imageUrl = await GetImageFromUnsplash(countryName);
            if (imageUrl != null)
            {
                try
                {
                    WebRequest request = WebRequest.Create(imageUrl);
                    using (WebResponse response = request.GetResponse())
                    using (Stream stream = response.GetResponseStream())
                    {
                        pictureBox2.Image = System.Drawing.Image.FromStream(stream);
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show("載入圖片失敗：" + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("找不到圖片！");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //TODO:按下這個就可以 對答案 把兩位使用者選的地點和 正確答案的地點標示出來 誰比較接近誰就獲勝畫出 正確的點到2個地點的線 
        }
        private async Task<string> GetCountryNameFromCode(string code)
        {
            string url = $"https://restcountries.com/v3.1/alpha/{code}";
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string json = await client.GetStringAsync(url);
                    JArray arr = JArray.Parse(json);
                    return arr[0]?["name"]?["common"]?.ToString();
                }
                catch
                {
                    return null;
                }
            }
        }

        private async Task<string> GetImageFromUnsplash(string query)
        {
            string url = $"https://api.unsplash.com/photos/random?query={Uri.EscapeDataString(query)}&client_id={UnsplashAccessKey}";
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string json = await client.GetStringAsync(url);
                    JObject obj = JObject.Parse(json);
                    return obj["urls"]?["regular"]?.ToString();
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
