document.addEventListener('DOMContentLoaded', function () {
    const stations = @Html.Raw(Json.Serialize(stations));
    const stationsContainer = document.getElementById('stationsContainer');
    const noStations = document.getElementById('noStations');
    const addStationBtn = document.getElementById('addStation');
    const stationCountInput = document.getElementById('stationCount');
    const submitBtn = document.getElementById('submitBtn');
    const routePreview = document.getElementById('routePreview');
    const routePreviewContent = document.getElementById('routePreviewContent');

    let stationCounter = 0;

    // Функція для додавання нової станції
    function addStation() {
        stationCounter++;
        updateStationCount();

        const stationDiv = document.createElement('div');
        stationDiv.className = 'station-item card mb-3';
        stationDiv.dataset.index = stationCounter;

        stationDiv.innerHTML = `
                <div class="card-body">
                    <div class="row align-items-center">
                        <div class="col-md-1">
                            <div class="station-order">${stationCounter}</div>
                        </div>
                        <div class="col-md-9">
                            <label class="form-label">Станція ${stationCounter}</label>
                            <select name="stationIds" class="form-select station-select" required>
                                <option value="" disabled selected>Оберіть станцію</option>
                                ${stations.map(s => `<option value="${s.station_id}">${s.station_name}</option>`).join('')}
                            </select>
                        </div>
                        <div class="col-md-2">
                            <button type="button" class="btn btn-outline-danger btn-sm remove-station" title="Видалити станцію">
                                <i class="bi bi-trash"></i>
                            </button>
                        </div>
                    </div>
                </div>
                ${stationCounter < 10 ? '<div class="route-arrow"><i class="bi bi-arrow-down"></i></div>' : ''}
            `;

        stationsContainer.appendChild(stationDiv);

        // Видалення
        stationDiv.querySelector('.remove-station').addEventListener('click', function () {
            stationDiv.remove();
            updateStationCount();
            updateStationOrder();
            updateRoutePreview();
        });

        // Вибір станції
        stationDiv.querySelector('.station-select').addEventListener('change', function () {
            updateRoutePreview();
            validateDuplicateStations();
        });

        updateVisibility();
        updateRoutePreview();
    }

    function updateStationCount() {
        const stationItems = document.querySelectorAll('.station-item');
        stationCountInput.value = stationItems.length;
        submitBtn.disabled = stationItems.length < 2;
    }

    function updateStationOrder() {
        const stationItems = document.querySelectorAll('.station-item');
        stationItems.forEach((item, index) => {
            const orderDiv = item.querySelector('.station-order');
            const label = item.querySelector('label');
            orderDiv.textContent = index + 1;
            label.textContent = `Станція ${index + 1}`;
        });
    }

    function validateDuplicateStations() {
        const selects = document.querySelectorAll('.station-select');
        const selectedValues = Array.from(selects).map(s => s.value).filter(v => v);
        const hasDuplicates = selectedValues.length !== new Set(selectedValues).size;

        selects.forEach(select => {
            if (hasDuplicates && selectedValues.filter(v => v === select.value).length > 1) {
                select.classList.add('is-invalid');
            } else {
                select.classList.remove('is-invalid');
            }
        });

        return !hasDuplicates;
    }

    function updateRoutePreview() {
        const stationItems = document.querySelectorAll('.station-item');
        const selectedStations = [];

        stationItems.forEach(item => {
            const select = item.querySelector('.station-select');
            if (select.value) {
                const stationName = select.options[select.selectedIndex].text;
                selectedStations.push(stationName);
            }
        });

        if (selectedStations.length >= 2) {
            routePreview.style.display = 'block';
            routePreviewContent.innerHTML = `
                    <div class="d-flex align-items-center flex-wrap">
                        ${selectedStations.map((station, index) => `
                            <span class="badge bg-primary me-2 mb-2 p-2">${index + 1}. ${station}</span>
                            ${index < selectedStations.length - 1 ? '<i class="bi bi-arrow-right text-muted me-2 mb-2"></i>' : ''}
                        `).join('')}
                    </div>
                    <p class="mt-2 text-muted">
                        <small>Загальна кількість станцій: ${selectedStations.length}</small>
                    </p>
                `;
        } else {
            routePreview.style.display = 'none';
        }
    }

    function updateVisibility() {
        if (stationCounter > 0) {
            noStations.style.display = 'none';
        } else {
            noStations.style.display = 'block';
        }
    }

    addStationBtn.addEventListener('click', addStation);

    document.getElementById('createRouteForm').addEventListener('submit', function (e) {
        const stationItems = document.querySelectorAll('.station-item');
        const trainSelect = document.getElementById('trainId');

        if (!trainSelect.value) {
            e.preventDefault();
            alert('Оберіть потяг для створення маршруту!');
            return;
        }

        if (stationItems.length < 2) {
            e.preventDefault();
            alert('Додайте принаймні 2 станції для створення маршруту!');
            return;
        }

        let allStationsSelected = true;
        document.querySelectorAll('.station-select').forEach(select => {
            if (!select.value) {
                allStationsSelected = false;
            }
        });

        if (!allStationsSelected) {
            e.preventDefault();
            alert('Оберіть всі станції в маршруті!');
            return;
        }

        if (!validateDuplicateStations()) {
            e.preventDefault();
            alert('В маршруті не може бути повторюваних станцій!');
            return;
        }
    });

    addStation();
    addStation();
});