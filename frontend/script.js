const apiUrl = '/api/kargo';

async function fetchKargolar() {
  const res = await fetch(apiUrl);
  const data = await res.json();
  const tbody = document.querySelector("#kargoTable tbody");
  tbody.innerHTML = "";
  data.forEach(k => {
    const row = document.createElement("tr");
    row.className = k.teslimEdildi ? "teslim-edildi" : "beklemede";
    row.innerHTML = `
      <td>${k.firma}</td>
      <td>${k.takipNo}</td>
      <td>${k.magazaID}</td>
      <td>${k.talepID}</td>
      <td>${k.teslimEdildi ? "Teslim Edildi" : "Bekliyor"}</td>
      <td>${k.ongorulenTeslimat ?? "-"}</td>
      <td>${k.lastUpdate}</td>
      <td><button onclick="deleteKargo('${k.takipNo}')">Sil</button></td>
    `;
    tbody.appendChild(row);
  });
}

async function deleteKargo(takipNo) {
  try {
    const response = await fetch(`${apiUrl}/${takipNo}`, { method: "DELETE" });
    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(errorText);
    }
    await fetchKargolar();
  } catch (error) {
    alert('Kargo silinirken bir hata oluştu: ' + error.message);
  }
}

document.getElementById("kargoForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  const kargo = {
    firma: document.getElementById("firma").value,
    takipNo: document.getElementById("takipNo").value,
    magazaID: document.getElementById("magazaID").value,
    talepID: document.getElementById("talepID").value,
    teslimEdildi: false,
    lastUpdate: new Date().toISOString()
  };
  await fetch(apiUrl, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(kargo)
  }).then(res => {
    if (!res.ok) {
      if (res.status === 409) {
        alert("Bu takip numarası zaten sistemde kayıtlı.");
      } else {
        alert("Bir hata oluştu.");
      }
    } else {
      fetchKargolar();
    }
  });
  fetchKargolar();
});

fetchKargolar();