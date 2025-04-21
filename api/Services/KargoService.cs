using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using System.Threading;

namespace KargoTakip.Services
{
    public class KargoService
    {
        private readonly HttpClient _httpClient;
        private readonly string _dataFilePath;
        private List<KargoData> _kargoList;
        private readonly ILogger<KargoService> _logger;

        public KargoService(ILogger<KargoService> logger)
        {
            _httpClient = new HttpClient();
            _logger = logger;
            _kargoList = new List<KargoData>();
            _dataFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "kargo_data.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_dataFilePath));
            LoadKargoData();
        }

        private void LoadKargoData()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    var json = File.ReadAllText(_dataFilePath);
                    _kargoList = JsonSerializer.Deserialize<List<KargoData>>(json) ?? new List<KargoData>();
                    _logger.LogInformation($"Kargo verileri yüklendi. Toplam {_kargoList.Count} kargo bulundu.");
                }
                else
                {
                    _logger.LogInformation("Kargo veri dosyası bulunamadı. Yeni dosya oluşturulacak.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kargo verileri yüklenirken hata oluştu");
                _kargoList = new List<KargoData>();
            }
        }

        private void SaveKargoData()
        {
            try
            {
                var json = JsonSerializer.Serialize(_kargoList, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_dataFilePath, json);
                _logger.LogInformation($"Kargo verileri kaydedildi. Toplam {_kargoList.Count} kargo kaydedildi.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kargo verileri kaydedilirken hata oluştu");
            }
        }

        public async Task<List<KargoData>> GetAllKargos()
        {
            return await Task.FromResult(_kargoList);
        }

        public async Task<KargoData?> GetKargoByTrackingNumber(string? trackingNumber)
        {
            if (string.IsNullOrEmpty(trackingNumber))
                return null;
                
            return await Task.FromResult(_kargoList.FirstOrDefault(k => k.TrackingNumber == trackingNumber));
        }

        public async Task AddKargo(KargoData kargo)
        {
            if (kargo == null || string.IsNullOrEmpty(kargo.TrackingNumber))
                return;

            if (!_kargoList.Any(k => k.TrackingNumber == kargo.TrackingNumber))
            {
                _kargoList.Add(kargo);
                SaveKargoData();
            }
        }

        public async Task UpdateKargoStatus(string? trackingNumber, string status)
        {
            if (string.IsNullOrEmpty(trackingNumber))
                return;

            var kargo = _kargoList.FirstOrDefault(k => k.TrackingNumber == trackingNumber);
            if (kargo != null)
            {
                kargo.Status = status;
                kargo.LastUpdated = DateTime.Now;
                SaveKargoData();
            }
        }

        public async Task CheckKargoStatuses()
        {
            _logger.LogInformation("Kargo durumları kontrol ediliyor...");
            var kargolar = await GetAllKargos();
            _logger.LogInformation($"Toplam {kargolar.Count} kargo kontrol edilecek.");
            
            var semaphore = new SemaphoreSlim(1); // Aynı anda sadece 1 sorgu
            var tasks = new List<Task>();
            
            foreach (var kargo in kargolar)
            {
                if (string.IsNullOrEmpty(kargo.TrackingNumber))
                    continue;
                
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await semaphore.WaitAsync(); // Sorgu sırası için bekle
                        
                        try
                        {
                            _logger.LogInformation($"Kargo durumu kontrol ediliyor: {kargo.TrackingNumber}");
                            var response = await _httpClient.GetStringAsync($"https://www.ups.com.tr/WaybillSorgu.aspx?Waybill={kargo.TrackingNumber}");
                            
                            // Öngörülen teslimat zamanını al
                            var ongorulenMatch = Regex.Match(response, 
                                @"<span[^>]*id=""ctl00_MainContent_Label2""[^>]*>Öngörülen Teslimat Zamanı<\/span><br\s*\/?>\s*<span[^>]*id=""ctl00_MainContent_teslimat_zamani""[^>]*>(.*?)<\/span>", 
                                RegexOptions.Singleline);
                            
                            if (ongorulenMatch.Success)
                            {
                                var ongorulen = ongorulenMatch.Groups[1].Value.Trim();
                                ongorulen = Regex.Replace(ongorulen, "<.*?>", "").Trim();
                                kargo.EstimatedDelivery = ongorulen;
                            }
                            
                            // Teslim durumunu kontrol et
                            if (response.Contains("Paketiniz teslim edilmiştir", StringComparison.OrdinalIgnoreCase))
                            {
                                kargo.Status = "Teslim Edildi";
                            }
                            else
                            {
                                kargo.Status = "Beklemede";
                            }
                            
                            kargo.LastUpdated = DateTime.Now;
                            SaveKargoData();
                            
                            _logger.LogInformation($"Kargo durumu güncellendi: {kargo.TrackingNumber} - {kargo.Status}");
                        }
                        finally
                        {
                            semaphore.Release(); // Sorgu tamamlandı, sıradaki sorguya geç
                        }
                        
                        await Task.Delay(1000); // Sorgular arasında 1 saniye bekle
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Kargo durumu kontrol edilirken hata oluştu: {kargo.TrackingNumber}");
                    }
                }));
            }
            
            await Task.WhenAll(tasks);
            _logger.LogInformation("Kargo durumları kontrolü tamamlandı.");
        }

        public async Task CheckSingleKargoStatus(KargoData kargo)
        {
            try
            {
                _logger.LogInformation($"Kargo durumu kontrol ediliyor: {kargo.TrackingNumber}");
                var response = await _httpClient.GetStringAsync($"https://www.ups.com.tr/WaybillSorgu.aspx?Waybill={kargo.TrackingNumber}");
                
                // Öngörülen teslimat zamanını al
                var ongorulenMatch = Regex.Match(response, 
                    @"<span[^>]*id=""ctl00_MainContent_Label2""[^>]*>Öngörülen Teslimat Zamanı<\/span><br\s*\/?>\s*<span[^>]*id=""ctl00_MainContent_teslimat_zamani""[^>]*>(.*?)<\/span>", 
                    RegexOptions.Singleline);
                
                if (ongorulenMatch.Success)
                {
                    var ongorulen = ongorulenMatch.Groups[1].Value.Trim();
                    ongorulen = Regex.Replace(ongorulen, "<.*?>", "").Trim();
                    kargo.EstimatedDelivery = ongorulen;
                }
                
                // Teslim durumunu kontrol et
                if (response.Contains("Paketiniz teslim edilmiştir", StringComparison.OrdinalIgnoreCase))
                {
                    kargo.Status = "Teslim Edildi";
                }
                else
                {
                    kargo.Status = "Beklemede";
                }
                
                kargo.LastUpdated = DateTime.Now;
                SaveKargoData();
                
                _logger.LogInformation($"Kargo durumu güncellendi: {kargo.TrackingNumber} - {kargo.Status}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Kargo durumu kontrol edilirken hata oluştu: {kargo.TrackingNumber}");
                throw;
            }
        }

        public async Task LoadDataFrom4me(string email, string password)
        {
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogError("4me e-posta adresi eksik");
                throw new InvalidOperationException("4me e-posta adresi eksik");
            }

            if (string.IsNullOrEmpty(password))
            {
                _logger.LogError("4me şifre eksik");
                throw new InvalidOperationException("4me şifre eksik");
            }

            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-infobars");
            options.AddArgument("--remote-debugging-port=9222");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddArgument("--disable-notifications");
            options.AddArgument("--disable-popup-blocking");
            options.AddArgument("--start-maximized");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--allow-running-insecure-content");
            options.AddArgument("--ignore-certificate-errors");
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36");

            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;

            using (var driver = new ChromeDriver(service, options))
            {
                try
                {
                    driver.Navigate().GoToUrl("https://gratis-it.4me.com/inbox");
                    await Task.Delay(5000);
                    
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
                    
                    // E-posta girişi
                    var emailInput = wait.Until(d => d.FindElement(By.Id("i0116")));
                    emailInput.Clear();
                    emailInput.SendKeys(email);
                    await Task.Delay(3000);
                    
                    // İleri butonu 1
                    var ileriBtn1 = driver.FindElement(By.Id("idSIButton9"));
                    ileriBtn1.Click();
                    await Task.Delay(3000);
                    
                    // Şifre girişi
                    var passwordInput = driver.FindElement(By.Id("i0118"));
                    passwordInput.Clear();
                    passwordInput.SendKeys(password);
                    await Task.Delay(3000);
                    
                    // İleri butonu 2
                    var ileriBtn2 = driver.FindElement(By.Id("idSIButton9"));
                    ileriBtn2.Click();
                    await Task.Delay(3000);
                    
                    // İleri butonu 3
                    var ileriBtn3 = driver.FindElement(By.Id("idSIButton9"));
                    ileriBtn3.Click();
                    await Task.Delay(5000);

                    // Tüm talepleri yükle
                    await Task.Delay(5000);
                    
                    // ServiceDesk taleplerini filtrele
                    var talepler = driver.FindElements(By.ClassName("cell-team"))
                        .Where(x => x.Text.Contains("ServiceDesk"))
                        .Select(x => x.FindElement(By.XPath("./ancestor::div[contains(@class, 'grid-row')]")))
                        .ToList();

                    var islenenTalepSayisi = 0;
                    var bulunanKargoSayisi = 0;
                    var islenenKargolar = new HashSet<string>();
                    var yeniEklenenKargolar = new List<KargoData>();

                    foreach (var talep in talepler)
                    {
                        try
                        {
                            islenenTalepSayisi++;

                            // Konu kontrolü
                            var konuElement = talep.FindElement(By.ClassName("cell-subject"));
                            var konu = konuElement.Text.Trim();
                            
                            if (!konu.StartsWith("-"))
                                continue;

                            // Talep ID'sini al
                            var talepIdElement = talep.FindElement(By.ClassName("cell-path"));
                            var talepIdText = talepIdElement.Text.Trim();
                            var talepId = Regex.Match(talepIdText, @"\d+").Value;

                            // Mağaza ID'sini al
                            var magazaElement = talep.FindElement(By.ClassName("cell-requester"));
                            var magazaText = magazaElement.Text.Trim();
                            var magazaId = "";
                            if (!string.IsNullOrEmpty(magazaText))
                            {
                                var parts = magazaText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length > 0)
                                {
                                    magazaId = parts[0];
                                }
                            }

                            // Talebe tıkla ve detayları kontrol et
                            talep.Click();
                            
                            var talepWait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                            try
                            {
                                talepWait.Until(d => d.FindElement(By.ClassName("header_bar_inner")));
                            }
                            catch (WebDriverTimeoutException)
                            {
                                continue;
                            }

                            // Talep detaylarını kontrol et
                            var requestContent = driver.PageSource;
                            var upsMatches = Regex.Matches(requestContent, @"1Z0625[A-Z0-9]{12}");
                            
                            // Sadece en son kargo numarasını al
                            if (upsMatches.Count > 0)
                            {
                                var lastTrackingNumber = upsMatches[upsMatches.Count - 1].Value;
                                
                                // Eğer bu takip numarası daha önce işlenmediyse ekle
                                if (!islenenKargolar.Contains(lastTrackingNumber))
                                {
                                    islenenKargolar.Add(lastTrackingNumber);
                                    bulunanKargoSayisi++;
                                    
                                    var kargoData = new KargoData
                                    {
                                        Firma = "UPS",
                                        TrackingNumber = lastTrackingNumber,
                                        StoreId = magazaId,
                                        RequestId = talepId,
                                        Status = "Beklemede",
                                        EstimatedDelivery = "-",
                                        LastUpdated = DateTime.Now
                                    };

                                    await AddKargo(kargoData);
                                    yeniEklenenKargolar.Add(kargoData);
                                    _logger.LogInformation($"Kargo eklendi: {lastTrackingNumber} - Mağaza: {magazaId} - Talep: {talepId}");
                                }
                            }
                            
                            // Geri dön
                            driver.Navigate().Back();
                            await Task.Delay(1000);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Talep işlenirken hata oluştu: {talep?.FindElement(By.ClassName("cell-path"))?.Text ?? "Bilinmeyen Talep"}");
                            continue;
                        }
                    }

                    _logger.LogInformation($"İşlem tamamlandı. Toplam {islenenTalepSayisi} talep işlendi, {bulunanKargoSayisi} kargo bulundu.");

                    // Yeni eklenen kargoları sorgula
                    _logger.LogInformation("Yeni eklenen kargolar sorgulanıyor...");
                    var semaphore = new SemaphoreSlim(1);
                    foreach (var kargo in yeniEklenenKargolar)
                    {
                        try
                        {
                            await semaphore.WaitAsync();
                            await CheckSingleKargoStatus(kargo);
                            await Task.Delay(1000); // Her sorgu arasında 1 saniye bekle
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Kargo durumu kontrol edilirken hata oluştu: {kargo.TrackingNumber}");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }
                    _logger.LogInformation("Yeni eklenen kargoların sorgulanması tamamlandı.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "4me verileri yüklenirken hata oluştu");
                    throw;
                }
            }
        }

        public async Task DeleteKargo(string trackingNumber)
        {
            if (string.IsNullOrEmpty(trackingNumber))
                return;

            var kargo = _kargoList.FirstOrDefault(k => k.TrackingNumber == trackingNumber);
            if (kargo != null)
            {
                _kargoList.Remove(kargo);
                SaveKargoData();
                _logger.LogInformation($"Kargo silindi: {trackingNumber}");
            }
        }
    }

    public class KargoData
    {
        [JsonPropertyName("firma")]
        public string Firma { get; set; } = "UPS";

        [JsonPropertyName("takipNo")]
        public string TrackingNumber { get; set; } = "";

        [JsonPropertyName("magazaId")]
        public string StoreId { get; set; } = "";

        [JsonPropertyName("talepId")]
        public string RequestId { get; set; } = "";

        [JsonPropertyName("durum")]
        public string Status { get; set; } = "Beklemede";

        [JsonPropertyName("ongorulenTeslimat")]
        public string EstimatedDelivery { get; set; } = "-";

        [JsonPropertyName("sonGuncelleme")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
