using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class KargoService
{
    private readonly List<Kargo> _kargoListesi = new List<Kargo>();

    public List<Kargo> GetAll() => _kargoListesi;

    public void Add(Kargo kargo)
    {
        _kargoListesi.Add(kargo);
    }

    public bool Exists(string takipNo)
    {
        return _kargoListesi.Any(k => k.TakipNo == takipNo);
    }

    public bool Delete(string takipNo)
    {
        var kargo = _kargoListesi.FirstOrDefault(k => k.TakipNo == takipNo);
        if (kargo != null)
        {
            return _kargoListesi.Remove(kargo);
        }
        return false;
    }

    public async Task<KargoDurumSonucu> KargoDurumuKontrolEt(string firma, string takipNo)
    {
        if (firma == "UPS")
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetStringAsync($"https://www.ups.com.tr/WaybillSorgu.aspx?Waybill={takipNo}");
                    var sonuc = new KargoDurumSonucu();
                    var match = Regex.Match(response, @"<span[^>]*id=""ctl00_MainContent_Label2""[^>]*>Öngörülen Teslimat Zamanı<\/span><br\s*\/?>\s*<span[^>]*id=""ctl00_MainContent_teslimat_zamani""[^>]*>(.*?)<\/span>", RegexOptions.Singleline);

                    if (match.Success)
                    {
                        var ongorulen = match.Groups[1].Value.Trim();
                        ongorulen = Regex.Replace(ongorulen, "<.*?>", "").Trim();
                        sonuc.OngorulenTeslimat = ongorulen;
                    }

                    sonuc.TeslimEdildi = response.Contains("Paketiniz teslim edilmiştir", StringComparison.OrdinalIgnoreCase);
                    return sonuc;
                }
            }
            catch
            {
                // Log veya fallback yapılabilir
                return new KargoDurumSonucu { TeslimEdildi = false };
            }
        }

        // Diğer kargo firmaları için ekleme yapılabilir
        return new KargoDurumSonucu { TeslimEdildi = false };
    }
}

public class KargoDurumSonucu
{
    public bool TeslimEdildi { get; set; }
    public string? OngorulenTeslimat { get; set; }
}
