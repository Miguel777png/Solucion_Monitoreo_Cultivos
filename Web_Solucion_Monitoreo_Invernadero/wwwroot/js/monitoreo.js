// @ts-nocheck
let chart = null;
let ultimoIdAlertaVisto = null;

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

function mostrarNotificacionFlotante(alerta) {
    const contenedor = document.getElementById('contenedor-toasts-vivo');
    if (!contenedor || typeof bootstrap === 'undefined') return;

    let cabeceraClase = 'bg-primary text-white';
    let icono = '💧';
    let titulo = 'Riego Programado Activado';

    if (alerta.tipoEvento === 'RIEGO_EMERGENCIA') {
        cabeceraClase = 'bg-danger text-white';
        icono = '🚨';
        titulo = 'Alerta Crítica — Riego 3 min (180s)';
    } else if (alerta.tipoEvento === 'RIEGO_OMITIDO_RANGO_OPTIMO') {
        cabeceraClase = 'bg-success text-white';
        icono = '🌿';
        titulo = 'Rango óptimo — Riego omitido';
    } else if (alerta.tipoEvento === 'RIEGO_EMERGENCIA_FINALIZADO') {
        cabeceraClase = 'bg-success text-white';
        icono = '✓';
        titulo = 'Humedad recuperada — Válvula cerrada';
    } else if (alerta.tipoEvento === 'RIEGO_PROGRAMADO_PENDIENTE') {
        cabeceraClase = 'bg-primary text-white';
        icono = '💧';
        titulo = 'Riego programado pendiente';
    } else if (alerta.tipoEvento === 'ALERTA_UMBRAL') {
        cabeceraClase = 'bg-info text-dark';
        icono = '🌡️';
        titulo = 'Aviso de Umbral';
    }

    const toastEl = document.createElement('div');
    toastEl.className = 'toast shadow-lg border-0 mb-2';
    toastEl.setAttribute('role', 'alert');
    toastEl.setAttribute('aria-live', 'assertive');
    toastEl.setAttribute('aria-atomic', 'true');

    toastEl.innerHTML = `
        <div class="toast-header ${cabeceraClase}">
            <span class="me-2">${icono}</span>
            <strong class="me-auto">${titulo}</strong>
            <small>${alerta.fechaHora}</small>
            <button type="button" class="btn-close ${cabeceraClase.includes('text-white') ? 'btn-close-white' : ''}" data-bs-dismiss="toast"></button>
        </div>
        <div class="toast-body bg-white text-dark small">
            ${alerta.descripcion}
        </div>
    `;

    contenedor.appendChild(toastEl);

    const toastBootstrap = new bootstrap.Toast(toastEl, {
        animation: true,
        autohide: true,
        delay: 7000
    });
    toastBootstrap.show();

    toastEl.addEventListener('hidden.bs.toast', () => {
        toastEl.remove();
    });
}

function actualizarIndicadorSaludPrincipal(estadoSalud) {
    const badgeSalud = document.getElementById('badge-salud-planta');
    if (!badgeSalud) return;

    if (estadoSalud === 'OPTIMO') {
        badgeSalud.className = 'badge bg-success-subtle text-success border border-success fs-6 px-3 py-1';
        badgeSalud.textContent = '✔ Condiciones Óptimas';
    } else if (estadoSalud === 'PRECAUCION') {
        badgeSalud.className = 'badge bg-warning-subtle text-warning-emphasis border border-warning fs-6 px-3 py-1';
        badgeSalud.textContent = '⚠ Condiciones aceptables, fuera del rango óptimo';
    } else if (estadoSalud === 'PELIGRO') {
        badgeSalud.className = 'badge bg-danger-subtle text-danger border border-danger fs-6 px-3 py-1';
        badgeSalud.textContent = '🚨 Alerta: Fuera de Umbral';
    } else {
        badgeSalud.className = 'badge bg-light text-muted border fs-6 px-3 py-1';
        badgeSalud.textContent = 'Sin lectura activa';
    }
}

function obtenerIconoMiniSalud(estadoSalud) {
    if (estadoSalud === 'OPTIMO') return '<span class="text-success fw-bold" title="Óptimo">✔</span>';
    if (estadoSalud === 'PRECAUCION') return '<span class="text-warning fw-bold" title="Aceptable, fuera del rango óptimo">⚠</span>';
    if (estadoSalud === 'PELIGRO') return '<span class="text-danger fw-bold" title="Alerta: Fuera de umbral">🚨</span>';
    return '<span class="text-muted">•</span>';
}

function obtenerFavoritos() {
    try {
        return JSON.parse(localStorage.getItem('orquideas_inv_favoritos') || '[]');
    } catch {
        return [];
    }
}

function alternarFavorito(id) {
    let favs = obtenerFavoritos();
    if (favs.includes(id)) {
        favs = favs.filter(x => x !== id);
    } else {
        favs.push(id);
    }
    localStorage.setItem('orquideas_inv_favoritos', JSON.stringify(favs));
    actualizarEstrellasFavoritos();
    aplicarFiltroTarjetas();
}

function actualizarEstrellasFavoritos() {
    const favs = obtenerFavoritos();
    document.querySelectorAll('.btn-favorito-inv').forEach(btn => {
        const id = Number(btn.dataset.id);
        const esFav = favs.includes(id);
        btn.textContent = esFav ? '★' : '☆';
        btn.classList.toggle('text-warning', esFav);
        btn.classList.toggle('text-muted', !esFav);
    });
}

function aplicarFiltroTarjetas() {
    const texto = (document.getElementById('filtro-buscar-inv')?.value || '').trim().toLowerCase();
    const modo = document.getElementById('filtro-cantidad-inv')?.value || 'all';
    const favs = obtenerFavoritos();
    const tarjetas = Array.from(document.querySelectorAll('.tarjeta-inv-item'));

    let visibles = 0;

    tarjetas.forEach(card => {
        const id = Number(card.dataset.id);
        const searchData = card.dataset.search || '';
        const conectado = card.dataset.conectado === 'true';

        let coincideTexto = !texto || searchData.includes(texto);
        let coincideModo = true;

        if (modo === 'online') {
            coincideModo = conectado;
        } else if (modo === 'favoritos') {
            coincideModo = favs.includes(id);
        } else if (modo === '3' || modo === '6') {
            const limite = Number(modo);
            coincideModo = visibles < limite;
        }

        if (coincideTexto && coincideModo) {
            card.classList.remove('d-none');
            visibles++;
        } else {
            card.classList.add('d-none');
        }
    });

    const contador = document.getElementById('contador-visibles');
    if (contador) contador.textContent = visibles;
}

async function actualizarDashboard(idInvernadero) {
    try {
        // 1. Datos en vivo del invernadero principal seleccionado
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

            actualizarIndicadorSaludPrincipal(data.estadoSalud);

            if (chart && Array.isArray(data.historialReciente)) {
                chart.data.labels = data.historialReciente.map(p => p.hora);
                chart.data.datasets[0].data = data.historialReciente.map(p => p.humedad);
                chart.data.datasets[1].data = data.historialReciente.map(p => p.temperatura);
                chart.update();
            }
        }

        // 2. Resumen dinámico de N invernaderos
        const respResumen = await fetch('/api/telemetria/resumen-todos');
        if (respResumen.ok) {
            const lista = await respResumen.json();
            lista.forEach(item => {
                const mBadge = document.getElementById(`mini-badge-${item.idInvernadero}`);
                const mHum = document.getElementById(`mini-hum-${item.idInvernadero}`);
                const mTemp = document.getElementById(`mini-temp-${item.idInvernadero}`);
                const mSalud = document.getElementById(`mini-salud-${item.idInvernadero}`);
                const cardWrapper = document.querySelector(`.tarjeta-inv-item[data-id="${item.idInvernadero}"]`);

                if (cardWrapper) {
                    cardWrapper.dataset.conectado = item.conectado ? 'true' : 'false';
                }

                if (mSalud) {
                    mSalud.innerHTML = obtenerIconoMiniSalud(item.estadoSalud);
                }

                if (mBadge) {
                    mBadge.className = item.conectado ? 'badge bg-success' : 'badge bg-secondary';
                    mBadge.textContent = item.conectado ? 'En Vivo' : 'Offline';
                }
                if (mHum) mHum.textContent = item.conectado ? `${Number(item.humedad).toFixed(1)} %` : '-- %';
                if (mTemp) mTemp.textContent = item.conectado ? `${Number(item.temperatura).toFixed(1)} °C` : '-- °C';
            });

            aplicarFiltroTarjetas();
        }

        // 3. Lista de eventos/alertas en vivo + disparo de Toast flotante si llega una nueva
        const respAlertas = await fetch(`/api/telemetria/alertas/${idInvernadero}`);
        if (respAlertas.ok) {
            const alertas = await respAlertas.json();

            if (alertas.length > 0) {
                const masReciente = alertas[0];
                if (ultimoIdAlertaVisto === null) {
                    ultimoIdAlertaVisto = masReciente.idBitacora;
                } else if (masReciente.idBitacora > ultimoIdAlertaVisto) {
                    ultimoIdAlertaVisto = masReciente.idBitacora;
                    mostrarNotificacionFlotante(masReciente);
                }
            } else if (ultimoIdAlertaVisto === null) {
                ultimoIdAlertaVisto = 0;
            }

            const ul = document.getElementById('lista-alertas-vivo');
            if (ul) {
                if (alertas.length === 0) {
                    ul.innerHTML = '<li class="list-group-item text-muted small py-3">Sin alertas ni riegos recientes en este invernadero.</li>';
                } else {
                    ul.innerHTML = alertas.map(item => {
                        let color = 'bg-info text-dark';
                        if (item.tipoEvento === 'RIEGO_EMERGENCIA') color = 'bg-danger';
                        else if (item.tipoEvento === 'RIEGO_EMERGENCIA_FINALIZADO') color = 'bg-success';
                        else if (item.tipoEvento === 'RIEGO_OMITIDO_RANGO_OPTIMO') color = 'bg-success';
                        else if (item.tipoEvento === 'RIEGO_OMITIDO_PROTECCION') color = 'bg-warning text-dark';
                        else if (item.tipoEvento === 'RIEGO_PROGRAMADO_PENDIENTE') color = 'bg-primary';
                        else if (item.tipoEvento === 'RIEGO_POSPUESTO') color = 'bg-secondary';

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
    actualizarEstrellasFavoritos();
    aplicarFiltroTarjetas();
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

        document.getElementById('filtro-buscar-inv')?.addEventListener('input', aplicarFiltroTarjetas);
        document.getElementById('filtro-cantidad-inv')?.addEventListener('change', aplicarFiltroTarjetas);

        document.querySelectorAll('.btn-favorito-inv').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                alternarFavorito(Number(btn.dataset.id));
            });
        });
    }
});
