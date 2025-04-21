using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Net.Http;
using KargoTakip.Services;

[ApiController]
[Route("api/[controller]")]
public class KargoController : ControllerBase
{
    private readonly KargoService _service;
    private readonly HttpClient _httpClient;

    public KargoController(KargoService service)
    {
        _service = service;
        _httpClient = new HttpClient();
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] KargoData kargo)
    {
        if (kargo == null || string.IsNullOrEmpty(kargo.TrackingNumber))
            return BadRequest("Geçersiz kargo bilgisi.");

        var existingKargo = await _service.GetKargoByTrackingNumber(kargo.TrackingNumber);
        if (existingKargo != null)
            return Conflict("Takip numarası zaten mevcut.");

        await _service.AddKargo(kargo);
        return Ok();
    }
    
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var kargolar = await _service.GetAllKargos();
        return Ok(kargolar);
    }

    [HttpDelete("{trackingNumber}")]
    public async Task<IActionResult> Delete(string trackingNumber)
    {
        if (string.IsNullOrEmpty(trackingNumber))
            return BadRequest("Takip numarası gereklidir.");

        await _service.DeleteKargo(trackingNumber);
        return Ok(new { success = true, message = "Kargo başarıyla silindi" });
    }

    [HttpPost("load-from-4me")]
    public async Task<IActionResult> LoadFrom4Me([FromBody] FourMeCredentials credentials)
    {
        if (credentials == null)
        {
            return BadRequest("Kimlik bilgileri boş olamaz");
        }

        if (string.IsNullOrEmpty(credentials.Email))
        {
            return BadRequest("4me e-posta adresi boş olamaz");
        }

        if (string.IsNullOrEmpty(credentials.Password))
        {
            return BadRequest("4me şifre boş olamaz");
        }

        try
        {
            await _service.LoadDataFrom4me(credentials.Email, credentials.Password);
            var kargolar = await _service.GetAllKargos();
            return Ok(new { success = true, message = "4me verileri başarıyla yüklendi", data = kargolar });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = $"4me verileri yüklenirken hata oluştu: {ex.Message}" });
        }
    }

    [HttpPost("check-status/{trackingNumber}")]
    public async Task<IActionResult> CheckStatus(string trackingNumber)
    {
        if (string.IsNullOrEmpty(trackingNumber))
            return BadRequest("Takip numarası gereklidir.");

        try
        {
            var kargo = await _service.GetKargoByTrackingNumber(trackingNumber);
            if (kargo == null)
                return NotFound("Kargo bulunamadı.");

            await _service.CheckSingleKargoStatus(kargo);
            return Ok(new { success = true, message = "Kargo durumu güncellendi", data = kargo });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = $"Kargo durumu kontrol edilirken hata oluştu: {ex.Message}" });
        }
    }
}

public class FourMeCredentials
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}
