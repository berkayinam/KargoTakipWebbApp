const apiUrl = '/api/kargo';

function showLoading() {
    document.getElementById('loadingOverlay').style.display = 'flex';
}

function hideLoading() {
    document.getElementById('loadingOverlay').style.display = 'none';
}

async function fetchKargolar() {
    showLoading();
    try {
        const res = await fetch(apiUrl);
        const data = await res.json();
        const tbody = document.querySelector("#kargoTable tbody");
        tbody.innerHTML = "";
        data.forEach(k => {
            const row = document.createElement("tr");
            row.className = k.durum === "Teslim Edildi" ? "teslim-edildi" : "beklemede";
            row.innerHTML = `
                <td>${k.firma}</td>
                <td>${k.takipNo}</td>
                <td>${k.magazaId}</td>
                <td>${k.talepId}</td>
                <td>${k.durum}</td>
                <td>${k.ongorulenTeslimat ?? "-"}</td>
                <td>${new Date(k.sonGuncelleme).toLocaleString('tr-TR')}</td>
                <td>
                    <button onclick="checkStatus('${k.takipNo}')">Kontrol Et</button>
                    <button onclick="deleteKargo('${k.takipNo}')">Sil</button>
                </td>
            `;
            tbody.appendChild(row);
        });

        // Toplam talep sayısını göster
        document.getElementById("talepSayisi").textContent = `Toplam Talep: ${data.length}`;
    } catch (error) {
        alert('Kargolar yüklenirken bir hata oluştu: ' + error.message);
    } finally {
        hideLoading();
    }
}

async function deleteKargo(takipNo) {
    showLoading();
    try {
        const response = await fetch(`${apiUrl}/${takipNo}`, { method: "DELETE" });
        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(errorText);
        }
        await fetchKargolar();
    } catch (error) {
        alert('Kargo silinirken bir hata oluştu: ' + error.message);
    } finally {
        hideLoading();
    }
}

async function checkStatus(trackingNumber) {
    showLoading();
    try {
        const response = await fetch(`${apiUrl}/check-status/${trackingNumber}`, {
            method: 'POST'
        });
        
        if (!response.ok) {
            throw new Error('Kargo durumu kontrol edilirken hata oluştu');
        }
        
        const result = await response.json();
        if (result.success) {
            fetchKargolar(); // Tabloyu yenile
        } else {
            alert(result.message);
        }
    } catch (error) {
        alert(error.message);
    } finally {
        hideLoading();
    }
}

document.getElementById("kargoForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    showLoading();
    const kargo = {
        firma: document.getElementById("firma").value,
        takipNo: document.getElementById("takipNo").value,
        magazaId: document.getElementById("magazaID").value,
        talepId: document.getElementById("talepID").value,
        durum: "Beklemede",
        ongorulenTeslimat: "-",
        sonGuncelleme: new Date().toISOString()
    };
    try {
        const response = await fetch(apiUrl, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(kargo)
        });
        
        if (!response.ok) {
            if (response.status === 409) {
                alert("Bu takip numarası zaten sistemde kayıtlı.");
            } else {
                alert("Bir hata oluştu.");
            }
        } else {
            fetchKargolar();
        }
    } catch (error) {
        alert('Kargo eklenirken bir hata oluştu: ' + error.message);
    } finally {
        hideLoading();
    }
});

document.getElementById("loadFrom4me").addEventListener("click", async () => {
    const email = prompt("4me E-posta adresinizi girin:");
    if (!email) return;
    
    const password = prompt("4me Şifrenizi girin:");
    if (!password) return;
    
    showLoading();
    try {
        const response = await fetch(`${apiUrl}/load-from-4me`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email, password })
        });
        
        const result = await response.json();
        
        if (result.success) {
            alert(result.message);
            fetchKargolar();
        } else {
            throw new Error(result.message);
        }
    } catch (error) {
        alert('4me\'den veri yüklenirken bir hata oluştu: ' + error.message);
    } finally {
        hideLoading();
    }
});

fetchKargolar();