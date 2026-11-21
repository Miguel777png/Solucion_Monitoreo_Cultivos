// @ts-nocheck
let chart = null;

function inicializarGrafica() {
    const canvas = document.getElementById('graficaTelemetria');
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    chart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: [],
            datasets: [
                {
                    label: 'Humedad (%)',
                    data: [],
                    borderColor: '#0d6efd',
                    backgroundColor: 'rgba(13, 110, 253, 0.1)',
                    tension: 0.3,
                    fill: true
                },
                {
                    label: 'Temperatura (°C)',
                    data: [],
                    borderColor: '#dc3545',
                    backgroundColor: 'rgba(220, 53, 69, 0.1)',
                    tension: 0.3,
                    fill: true
                }
            ]
        },
        options: {
            responsive: true,
            animation: false,
            scales: {
                y: { beginAtZero: true, suggestedMax: 100 }
            }
        }
    });
}

async function actualizarDashboard(idInvernadero) {
    try {
        const resp = await fetch(`/api/telemetria/actual/${idInvernadero}`);
        if (resp.ok) {
            const data = await resp.json();

            const elHum = document.getElementById('kpi-humedad');
            const elTemp = document.getElementById('kpi-temperatura');
            const elHora = document.getElementById('kpi-hora');
            const badge = document.getElementById('badge-conexion');

            if (elHum) elHum.textContent = Number(data.humedadActual).toFixed(1);
            if (elTemp) elTemp.textContent = Number(data.temperaturaActual).toFixed(1);
            if (elHora) elHora.textContent = data.ultimaHora;

            if (badge) {
                if (data.conectado) {
                    badge.className = 'badge bg-success fs-6 px-3 py-2';
                    badge.textContent = 'Sensor Conectado (En Vivo)';
                } else {
                    badge.className = 'badge bg-secondary fs-6 px-3 py-2';
                    badge.textContent = 'Sensor Desconectado';
                }
            }

            if (chart && Array.isArray(data.historialReciente)) {
                chart.data.labels = data.historialReciente.map(p => p.hora);
                chart.data.datasets[0].data = data.historialReciente.map(p => p.humedad);
                chart.data.datasets[1].data = data.historialReciente.map(p => p.temperatura);
                chart.update();
            }
        }

        const respResumen = await fetch('/api/telemetria/resumen-todos');
        if (respResumen.ok) {
            const lista = await respResumen.json();
            lista.forEach(item => {
                const mBadge = document.getElementById(`mini-badge-${item.idInvernadero}`);
                const mHum = document.getElementById(`mini-hum-${item.idInvernadero}`);
                const mTemp = document.getElementById(`mini-temp-${item.idInvernadero}`);

                if (mBadge) {
                    mBadge.className = item.conectado ? 'badge bg-success' : 'badge bg-secondary';
                    mBadge.textContent = item.conectado ? 'En Vivo' : 'Offline';
                }
                if (mHum) mHum.textContent = item.conectado ? `${Number(item.humedad).toFixed(1)} %` : '-- %';
                if (mTemp) mTemp.textContent = item.conectado ? `${Number(item.temperatura).toFixed(1)} °C` : '-- °C';
            });
        }



        // 3. Actualizamos la lista de eventos/alertas sin F5
        const respAlertas = await fetch(`/api/telemetria/alertas/${idInvernadero}`);
        if (respAlertas.ok) {
            const alertas = await respAlertas.json();
            const ul = document.getElementById('lista-alertas-vivo');
            if (ul) {
                if (alertas.length === 0) {
                    ul.innerHTML = '<li class="list-group-item text-muted small py-3">Sin alertas ni riegos recientes en este invernadero.</li>';
                } else {
                    ul.innerHTML = alertas.map(item => {
                        let color = 'bg-secondary';
                        if (item.tipoEvento === 'RIEGO_EMERGENCIA') color = 'bg-danger';
                        else if (item.tipoEvento === 'RIEGO_PROGRAMADO') color = 'bg-primary';
                        else if (item.tipoEvento === 'RIEGO_POSPUESTO') color = 'bg-warning text-dark';

                        return `
                            <li class="list-group-item small">
                                <div class="d-flex justify-content-between">
                                    <span class="badge ${color}">${item.tipoEvento}</span>
                                    <span class="text-muted">${item.fechaHora}</span>
                                </div>
                                <div class="mt-1">${item.descripcion}</div>
                            </li>`;
                    }).join('');
                }
            }
        }



    } catch (err) {
        console.error('Error consultando telemetría:', err);
    }
}

function iniciarMonitor(idInvernadero) {
    inicializarGrafica();
    actualizarDashboard(idInvernadero);
    setInterval(() => actualizarDashboard(idInvernadero), 2000);
    setInterval(() => {
        const reloj = document.getElementById('reloj-actual');
        if (reloj) reloj.textContent = new Date().toLocaleTimeString();
    }, 1000);
}

document.addEventListener('DOMContentLoaded', () => {
    const contenedor = document.getElementById('monitor-app');
    if (contenedor) {
        const idInv = Number(contenedor.dataset.invernaderoId || 1);
        iniciarMonitor(idInv);
    }
});