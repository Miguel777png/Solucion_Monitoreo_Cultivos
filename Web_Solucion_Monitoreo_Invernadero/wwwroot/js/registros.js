// @ts-nocheck
async function actualizarTablaHistorial() {
    const tbody = document.getElementById('tbody-historial');
    if (!tbody) return;

    const params = new URLSearchParams(window.location.search);
    try {
        const resp = await fetch(`/Registros/HistorialJson?${params.toString()}`);
        if (!resp.ok) return;

        const filas = await resp.json();
        if (filas.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="text-center text-muted py-4">
                        No se encontraron registros históricos con los filtros seleccionados.
                    </td>
                </tr>`;
            return;
        }

        tbody.innerHTML = filas.map(r => `
            <tr>
                <td class="text-muted small">#${r.idRegistro}</td>
                <td class="fw-semibold">${r.invernadero}</td>
                <td><span class="badge bg-info text-dark">${r.planta}</span></td>
                <td class="text-primary fw-bold">${r.humedad} %</td>
                <td class="text-danger fw-bold">${r.temperatura} °C</td>
                <td>${r.fechaHora}</td>
            </tr>
        `).join('');
    } catch (err) {
        console.error('Error refrescando historial:', err);
    }
}

async function actualizarTablaBitacora() {
    const tbody = document.getElementById('tbody-bitacora');
    if (!tbody) return;

    const params = new URLSearchParams(window.location.search);
    try {
        const resp = await fetch(`/Registros/BitacoraJson?${params.toString()}`);
        if (!resp.ok) return;

        const filas = await resp.json();
        if (filas.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="text-center text-muted py-4">
                        Sin eventos registrados con los filtros seleccionados.
                    </td>
                </tr>`;
            return;
        }

        tbody.innerHTML = filas.map(b => {
            let badgeClass = 'bg-secondary';
            if (b.tipoEvento === 'RIEGO_EMERGENCIA') badgeClass = 'bg-danger';
            else if (b.tipoEvento === 'RIEGO_PROGRAMADO') badgeClass = 'bg-primary';
            else if (b.tipoEvento === 'RIEGO_POSPUESTO') badgeClass = 'bg-warning text-dark';

            return `
                <tr>
                    <td class="text-nowrap">${b.fechaHora}</td>
                    <td class="fw-semibold">${b.invernadero}</td>
                    <td><span class="badge ${badgeClass}">${b.tipoEvento}</span></td>
                    <td class="small">${b.descripcion}</td>
                    <td class="small text-nowrap">H: ${b.humedad}% | T: ${b.temperatura}°C</td>
                    <td class="fw-bold">${b.tiempoSeg > 0 ? b.tiempoSeg + 's' : '-'}</td>
                </tr>`;
        }).join('');
    } catch (err) {
        console.error('Error refrescando bitácora:', err);
    }
}

document.addEventListener('DOMContentLoaded', () => {
    if (document.getElementById('tbody-historial')) {
        setInterval(actualizarTablaHistorial, 3000);
    }
    if (document.getElementById('tbody-bitacora')) {
        setInterval(actualizarTablaBitacora, 3000);
    }
});


document.addEventListener('DOMContentLoaded', () => {
    const radioRec = document.getElementById('radioRecurrente');
    const radioFec = document.getElementById('radioFechaUnica');
    const selectDia = document.getElementById('selectDiaSemana');
    const inputFecha = document.getElementById('inputFechaEspecifica');
    const bloqueRec = document.getElementById('bloqueRecurrente');
    const bloqueFec = document.getElementById('bloqueFechaUnica');

    if (radioRec && radioFec && selectDia && inputFecha) {
        const actualizarModo = () => {
            if (radioFec.checked) {
                selectDia.disabled = true;
                bloqueRec?.classList.add('opacity-50');

                inputFecha.disabled = false;
                inputFecha.required = true;
                bloqueFec?.classList.remove('opacity-50');
            } else {
                inputFecha.disabled = true;
                inputFecha.required = false;
                inputFecha.value = '';
                bloqueFec?.classList.add('opacity-50');

                selectDia.disabled = false;
                bloqueRec?.classList.remove('opacity-50');
            }
        };

        radioRec.addEventListener('change', actualizarModo);
        radioFec.addEventListener('change', actualizarModo);
    }
});