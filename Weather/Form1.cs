using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Xml.Linq;


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
        private Dictionary<(string, string), (double lat, double lon)> cityCoords =
    new Dictionary<(string, string), (double, double)>();
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
                        double lat = double.Parse(parts[4]);
                        double lon = double.Parse(parts[5]);
                        cityCoords[(city, countryCode)] = (lat, lon);
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
           
            pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox3.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox5.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox4.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox3.MouseDown += PictureBox_MouseDown;
            pictureBox3.MouseMove += PictureBox_MouseMove;
            pictureBox3.MouseUp += PictureBox_MouseUp;
            pictureBox3.Parent = pictureBox1; // 地圖當父容器
            pictureBox4.Parent = pictureBox1; 
            pictureBox5.Parent = pictureBox1; 
            pictureBox5.BackColor = Color.Transparent;
            pictureBox3.BackColor = Color.Transparent;
            pictureBox4.BackColor = Color.Transparent;
            
            pictureBox4.MouseDown += PictureBox_MouseDown;
            pictureBox4.MouseMove += PictureBox_MouseMove;
            pictureBox4.MouseUp += PictureBox_MouseUp;
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
            pictureBox3.Visible = false;
            pictureBox4.Visible = false;
            pictureBox5.Visible = false;
        }
        private bool isMapInteractive = true;

        private void PictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (!isMapInteractive) return;
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                mouseDownLocation = e.Location;
            }
        }

        private void PictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (!isMapInteractive) return;
            isDragging = false;
        }

        private void PictureBox1_MouseMove(object sender, MouseEventArgs e) //拖曳移動功能
        {
            if (!isMapInteractive) return;
            if (isDragging)
            {
                pictureBox1.Left += e.X - mouseDownLocation.X; //mouseDownLocation 是記錄滑鼠按下時的相對座標。   
                pictureBox1.Top += e.Y - mouseDownLocation.Y;
            }
        }
        private void pictureBox1_MouseEnter(object sender, EventArgs e)
        {
            if (!isMapInteractive) return;
            pictureBox1.Focus();
        }

        private void PictureBox1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!isMapInteractive) return;
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
            if (!isMapInteractive) return;
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

        private bool isDragging2 = false;
        private Point clickOffset;
        private PictureBox currentPictureBox = null;


        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging2 = true;
                currentPictureBox = sender as PictureBox;
                clickOffset = e.Location;
            }
        }

        private void PictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging2 && currentPictureBox != null)
            {
                Point newLocation = currentPictureBox.Location;
                newLocation.X += e.X - clickOffset.X;
                newLocation.Y += e.Y - clickOffset.Y;
                currentPictureBox.Location = newLocation;
            }
        }
        private (double lat, double lon)? player1Pos = null;
        private (double lat, double lon)? player2Pos = null;

        private void PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && currentPictureBox != null)
            {
                isDragging2 = false;
                // 底部中心（Y 改為 Height）
                Point screenBottomCenter = currentPictureBox.PointToScreen(new Point(currentPictureBox.Width / 2, currentPictureBox.Height));

                // 再轉回相對 pictureBox1.Parent（通常是 panel1）座標
                Point relativeBottomCenter = pictureBox1.Parent.PointToClient(screenBottomCenter);

                // 判斷是否在 pictureBox1 範圍內
                Rectangle mapRect = new Rectangle(pictureBox1.Location, pictureBox1.Size);
                if (mapRect.Contains(relativeBottomCenter))
                {
                    int relativeX = relativeBottomCenter.X - pictureBox1.Left;
                    int relativeY = relativeBottomCenter.Y - pictureBox1.Top;

                    double lon = (relativeX / (double)pictureBox1.Width) * 360 - 179;
                    double lat = 90.5 - (relativeY / (double)pictureBox1.Height) * 180;

                    if (sender == pictureBox3)
                    {
                        player1Pos = (lat, lon);
                        label2.Text = $"玩家 1：Lat {lat:F1}, Lon {lon:F1}";
                    }
                    else if (sender == pictureBox4)
                    {
                        player2Pos = (lat, lon);
                     
                        label3.Text = $"玩家 2：Lat {lat:F1}, Lon {lon:F1}";

                    }

                }
           
                currentPictureBox = null;
            }
        }

        private double Distance(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371; // 地球半徑，單位：公里
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }


        private Point MapToPoint(double lat, double lon)
        {
            int x = (int)((lon + 179) / 360 * pictureBox1.Width) + pictureBox1.Left;
            int y = (int)((90.5 - lat) / 180 * pictureBox1.Height) + pictureBox1.Top;

            return new Point(x, y);
        }




        private (double lat, double lon)? answerPos = null; // 加在 class 裡

        private async void button1_Click(object sender, EventArgs e)
        {
            pictureBox1.Dock = DockStyle.None;
            pictureBox1.Width = 1000;
            pictureBox1.Height = 600;
            pictureBox1.Left = 100;
            pictureBox1.Top = 100;
            isMapInteractive = false;

            pictureBox3.Visible = true;
            pictureBox4.Visible = true;
            pictureBox3.Location = new Point(26, 13);
            pictureBox4.Location = new Point(26, 134);
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

            label1.Text = $"請找出右圖所在城市:";
            textBox1.Clear();

            // 取得經緯度
            if (cityCoords.TryGetValue((city, countryCode), out var coords))
            {
                answerPos = coords;
            }
            else
            {
                MessageBox.Show("無法找到該城市的經緯度！");
                return;
            }

            // 清除舊的線與資訊
            pictureBox1.Invalidate();
            player1Pos = null;
            player2Pos = null;


            // 抓圖片
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


        private async void button2_Click(object sender, EventArgs e)
        {
            var (city, countryCode) = cityList[rand.Next(cityList.Count)];
            if (!player1Pos.HasValue || !player2Pos.HasValue || !answerPos.HasValue)
            {
                MessageBox.Show("兩位玩家都要先選好地點！");
                return;
            }
            if (answerPos.HasValue)
            {
                pictureBox5.Visible = true;
                Point pt = MapToPoint(answerPos.Value.lat, answerPos.Value.lon);
                pt.Offset(-pictureBox5.Width / 2, -pictureBox5.Height); // 底部中間對準

                pictureBox5.Location = pt;
            }

            string anssummary = await GetCitySummaryAsync("", city);

            // 計算距離
            double d1 = Distance(player1Pos.Value.lat, player1Pos.Value.lon, answerPos.Value.lat, answerPos.Value.lon);
            double d2 = Distance(player2Pos.Value.lat, player2Pos.Value.lon, answerPos.Value.lat, answerPos.Value.lon);

            string winner = d1 < d2 ? "玩家 Red 獲勝！" : (d2 < d1 ? "玩家 Blue 獲勝！" : "平手！");
            label1.Text = $"答案是：{city}\r\n玩家 Red 距離：{d1:F2} km\r\n玩家 Blue 距離：{d2:F2} km\r\n{winner}";
            textBox1.Text = $"答案介紹:{anssummary}";
            // 在地圖上畫線
            Graphics g = pictureBox1.CreateGraphics();
            Pen pen1 = new Pen(Color.Red, 4);
            Pen pen2 = new Pen(Color.Blue, 4);

            PointF answerPoint = MapToPoint(answerPos.Value.lat, answerPos.Value.lon);
            PointF p1Point = MapToPoint(player1Pos.Value.lat, player1Pos.Value.lon);
            PointF p2Point = MapToPoint(player2Pos.Value.lat, player2Pos.Value.lon);

            g.DrawLine(pen1, p1Point, answerPoint);
            g.DrawLine(pen2, p2Point, answerPoint);

            // 結束遊戲
            await Task.Delay(5000);
            pictureBox3.Visible = false;
            pictureBox4.Visible = false;
            pictureBox5.Visible = false;
            isMapInteractive = true;
            label2.Text = "PlayerRed";
            label3.Text = "PlayerBlue";
            pictureBox1.Invalidate();
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
