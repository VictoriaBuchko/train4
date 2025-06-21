let selectedSeatId = null;
let selectedClientId = null;
let selectedClientName = '';
let seatPrice = 0;

// Данные о местах
const seatsData = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(seatsData ?? new List < Dictionary < string, object >> ()));
const trainId = @trainId;
const fromStationId = @fromStationId;
const toStationId = @toStationId;
const travelDate = '@travelDate';

// Генерация схемы поезда
function generateTrainSchema() {
    const trainSchemaEl = document.getElementById('trainSchema');
    const carriageGroups = {};

    // Группируем места по вагонам
    seatsData.forEach(seat => {
        const carriageId = seat.train_carriage_types_id;
        if (!carriageGroups[carriageId]) {
            carriageGroups[carriageId] = {
                carriage_number: seat.carriage_number,
                carriage_type: seat.carriage_type,
                seats: []
            };
        }
        carriageGroups[carriageId].seats.push(seat);
    });

    // Создаем HTML для каждого вагона
    Object.keys(carriageGroups).forEach(carriageId => {
        const carriage = carriageGroups[carriageId];
        const carriageEl = document.createElement('div');
        carriageEl.className = 'carriage';
        carriageEl.innerHTML = `
                <div class="carriage-header">
                    Вагон ${carriage.carriage_number} (${carriage.carriage_type})
                </div>
                <div class="seats-grid" id="carriage-${carriageId}">
                </div>
            `;

        const seatsGrid = carriageEl.querySelector('.seats-grid');

        // Сортируем места по номеру
        carriage.seats.sort((a, b) => a.seat_number - b.seat_number);

        carriage.seats.forEach(seat => {
            const seatEl = document.createElement('div');
            seatEl.className = `seat ${seat.status}`;
            seatEl.innerHTML = seat.seat_number;
            seatEl.setAttribute('data-seat-id', seat.seat_id);
            seatEl.setAttribute('data-price', seat.price || 0);

            if (seat.status === 'available') {
                seatEl.addEventListener('click', () => selectSeat(seat));
                seatEl.title = `Місце ${seat.seat_number} - вільне (клацніть для вибору)`;
            } else if (seat.status === 'occupied') {
                seatEl.title = `Місце ${seat.seat_number} - зайняте (оплачено)`;
            } else if (seat.status === 'reserved') {
                seatEl.title = `Місце ${seat.seat_number} - заброньоване (не оплачено)`;
            } else if (seat.status === 'booked') {
                seatEl.title = `Місце ${seat.seat_number} - зайняте на інший маршрут`;
            } else {
                seatEl.title = `Місце ${seat.seat_number} - недоступне`;
            }

            seatsGrid.appendChild(seatEl);
        });

        trainSchemaEl.appendChild(carriageEl);
    });
}

// Выбор места
function selectSeat(seat) {
    // Снимаем выделение с предыдущего места
    const prevSelected = document.querySelector('.seat.selected');
    if (prevSelected) {
        prevSelected.classList.remove('selected');
        prevSelected.classList.add('available');
    }

    // Выделяем новое место
    const seatEl = document.querySelector(`[data-seat-id="${seat.seat_id}"]`);
    seatEl.classList.remove('available');
    seatEl.classList.add('selected');

    selectedSeatId = seat.seat_id;
    seatPrice = seat.price || 0;

    // Обновляем информацию о выбранном месте
    document.getElementById('seatDetails').innerHTML = `
            <strong>Місце:</strong> ${seat.seat_number}<br>
            <strong>Вагон:</strong> ${seat.carriage_number} (${seat.carriage_type})
        `;
    document.getElementById('selectedSeatInfo').style.display = 'block';

    updateBookingSection();
}

// Поиск клиентов
function searchClients() {
    const query = document.getElementById('clientSearch').value.trim();
    const resultsDiv = document.getElementById('clientSearchResults');
    const searchButton = document.getElementById('searchButton');

    if (query.length < 2) {
        resultsDiv.innerHTML = '<div class="alert alert-info">Введіть мінімум 2 символи</div>';
        return;
    }

    // Показываем индикатор загрузки
    searchButton.innerHTML = '<span class="loading-spinner"></span>';
    searchButton.disabled = true;

    fetch(`/Manager/SearchClients?query=${encodeURIComponent(query)}`)
        .then(response => response.json())
        .then(clients => {
            searchButton.innerHTML = '<i class="bi bi-search"></i>';
            searchButton.disabled = false;

            if (clients.error) {
                resultsDiv.innerHTML = '<div class="alert alert-danger">Помилка пошуку</div>';
                return;
            }

            if (clients.length === 0) {
                resultsDiv.innerHTML = '<div class="alert alert-info">Клієнтів не знайдено</div>';
                return;
            }

            let html = '<div class="list-group">';
            clients.forEach(client => {
                html += `
                        <div class="list-group-item list-group-item-action client-search-result" 
                             onclick="selectClient(${client.id}, '${client.name}', '${client.login}', '${client.email || ''}', '${client.phone || ''}')">
                            <div class="d-flex w-100 justify-content-between">
                                <h6 class="mb-1">${client.name}</h6>
                                <small>ID: ${client.id}</small>
                            </div>
                            <p class="mb-1">Логін: ${client.login}</p>
                            <small>${client.email || ''} ${client.phone || ''}</small>
                        </div>
                    `;
            });
            html += '</div>';
            resultsDiv.innerHTML = html;
        })
        .catch(error => {
            searchButton.innerHTML = '<i class="bi bi-search"></i>';
            searchButton.disabled = false;
            resultsDiv.innerHTML = '<div class="alert alert-danger">Помилка при пошуку</div>';
        });
}

// Поиск при нажатии Enter
document.getElementById('clientSearch').addEventListener('keypress', function (e) {
    if (e.key === 'Enter') {
        searchClients();
    }
});

// Автоматический поиск при вводе (с задержкой)
let searchTimeout;
document.getElementById('clientSearch').addEventListener('input', function () {
    clearTimeout(searchTimeout);
    const query = this.value.trim();

    if (query.length >= 2) {
        searchTimeout = setTimeout(() => {
            searchClients();
        }, 500); // Задержка 500мс
    } else {
        document.getElementById('clientSearchResults').innerHTML = '';
    }
});

// Выбор клиента
function selectClient(clientId, clientName, clientLogin, clientEmail, clientPhone) {
    selectedClientId = clientId;
    selectedClientName = clientName;

    // Очищаем поле поиска и результаты
    document.getElementById('clientSearch').value = '';
    document.getElementById('clientSearchResults').innerHTML = '';

    // Показываем информацию о выбранном клиенте
    document.getElementById('clientDetails').innerHTML = `
            <strong>${clientName}</strong><br>
            <small>Логін: ${clientLogin}</small><br>
            <small>${clientEmail ? 'Email: ' + clientEmail : ''}</small><br>
            <small>${clientPhone ? 'Телефон: ' + clientPhone : ''}</small>
        `;
    document.getElementById('selectedClientInfo').style.display = 'block';

    updateBookingSection();
}

// Очистка выбора клиента
function clearClientSelection() {
    selectedClientId = null;
    selectedClientName = '';
    document.getElementById('selectedClientInfo').style.display = 'none';
    updateBookingSection();
}

// Обновление секции бронирования
function updateBookingSection() {
    const bookingSection = document.getElementById('bookingSection');

    if (selectedClientId && selectedSeatId) {
        bookingSection.style.display = 'block';
    } else {
        bookingSection.style.display = 'none';
    }
}

// Подтверждение бронирования
function confirmBooking() {
    if (!selectedClientId || !selectedSeatId) {
        alert('Будь ласка, оберіть клієнта та місце.');
        return;
    }

    const confirmed = confirm(`Забронювати місце ${document.querySelector('.seat.selected').textContent} для клієнта ${selectedClientName}?`);
    if (!confirmed) return;

    const formData = new FormData();
    formData.append('clientId', selectedClientId);
    formData.append('seatId', selectedSeatId);
    formData.append('trainId', trainId);
    formData.append('travelDate', travelDate);
    formData.append('fromStationId', fromStationId);
    formData.append('toStationId', toStationId);

    fetch('/Manager/BookSeatForClient', {
        method: 'POST',
        body: formData
    })
        .then(response => response.json())
        .then(result => {
            if (result.success) {
                showSuccessModal(result);
                setTimeout(() => {
                    location.reload();
                }, 3000);
            } else {
                showErrorModal(result.message);
            }
        })
        .catch(error => {
            showErrorModal('Помилка при бронюванні квитка');
        });
}

// Модальные окна успеха и ошибки
function showSuccessModal(result) {
    const modal = document.createElement('div');
    modal.innerHTML = `
            <div style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.7); z-index: 9999; display: flex; justify-content: center; align-items: center;">
                <div style="background: white; padding: 30px; border-radius: 15px; box-shadow: 0 10px 30px rgba(0,0,0,0.3); max-width: 500px; text-align: center;">
                    <div style="color: #28a745; font-size: 48px; margin-bottom: 20px;">✅</div>
                    <h3 style="color: #28a745; margin-bottom: 20px;">Квиток успішно забронований!</h3>
                    
                    <div style="background: #f8f9fa; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: left;">
                        <div style="display: flex; justify-content: space-between; margin-bottom: 10px;">
                            <span style="font-weight: 600;">Номер квитка:</span>
                            <span style="color: #007bff; font-weight: 700;">#${result.ticket_id}</span>
                        </div>
                        <div style="display: flex; justify-content: space-between; margin-bottom: 10px;">
                            <span style="font-weight: 600;">Клієнт:</span>
                            <span style="font-weight: 700;">${selectedClientName}</span>
                        </div>
                        <div style="display: flex; justify-content: space-between; margin-bottom: 10px;">
                            <span style="font-weight: 600;">Вартість:</span>
                            <span style="color: #28a745; font-weight: 700; font-size: 18px;">${result.total_price} грн</span>
                        </div>
                    </div>

                    <button onclick="this.parentElement.parentElement.remove()" 
                            style="background: #007bff; color: white; border: none; padding: 12px 30px; border-radius: 8px; font-size: 16px; cursor: pointer;">
                        Зрозуміло
                    </button>
                </div>
            </div>
        `;
    document.body.appendChild(modal);
}

function showErrorModal(message) {
    const modal = document.createElement('div');
    modal.innerHTML = `
            <div style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.7); z-index: 9999; display: flex; justify-content: center; align-items: center;">
                <div style="background: white; padding: 30px; border-radius: 15px; box-shadow: 0 10px 30px rgba(0,0,0,0.3); max-width: 400px; text-align: center;">
                    <div style="color: #dc3545; font-size: 48px; margin-bottom: 20px;">❌</div>
                    <h3 style="color: #dc3545; margin-bottom: 20px;">Помилка бронювання</h3>
                    <p style="color: #6c757d; margin-bottom: 30px;">${message}</p>
                    <button onclick="this.parentElement.parentElement.remove()" 
                            style="background: #dc3545; color: white; border: none; padding: 12px 30px; border-radius: 8px; font-size: 16px; cursor: pointer;">
                        Закрити
                    </button>
                </div>
            </div>
        `;
    document.body.appendChild(modal);
}

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', function () {
    generateTrainSchema();
});