using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;



[ApiController]
[Route("api/[controller]")]
public class KargoController : ControllerBase
{
    private readonly KargoService _service;

    public KargoController(KargoService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] Kargo kargo)
    {
        if (_service.Exists(kargo.TakipNo))
            return Conflict("Takip numarası zaten mevcut.");

        var durum = await _service.KargoDurumuKontrolEt(kargo.Firma, kargo.TakipNo);

        kargo.TeslimEdildi = durum.TeslimEdildi;
        kargo.LastUpdate = DateTime.Now.ToString("HH:mm");
        kargo.OngorulenTeslimat = durum.OngorulenTeslimat;

        _service.Add(kargo);
        return Ok();
    }
    
    [HttpGet]
    public IActionResult Get()
    {
        var kargolar = _service.GetAll(); // doğru isim
        return Ok(kargolar);
    }

}
