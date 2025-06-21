let currentSearchResults = [];

// Поиск билетов по клиенту
function searchTickets() {
    const query = document.getElementById('clientSearchInput').value.trim();

    if (query.length < 2) {
        alert('Введіть мінімум 2 символи для пошуку');
        return;
    }

    const resultsDiv = document.getElementById('searchResults');
    resultsDiv.innerHTML = '<div class="text-center"><div class="spinner-border" role="status"></div><p>Пошук квитків...</p></div>';

    fetch(`/Manager/SearchTicketsByClient?clientQuery=${encodeURIComponent(query)}`)
        .then(response => response.json())
        .then(tickets => {
            if (tickets.error) {
                resultsDiv.innerHTML = '<div class="alert alert-danger">Помилка пошуку</div>';
                return;
            }

            currentSearchResults = tickets;
            displayTickets(tickets);
        })
        .catch(error => {
            resultsDiv.innerHTML = '<div class="alert alert-danger">Помилка при пошуку квитків</div>';
        });
}

// Поиск по номеру билета
function searchByTicketId() {
    const ticketId = document.getElementById('ticketIdInput').value.trim();

    if (!ticketId) {
        alert('Введіть номер квитка');
        return;
    }

    // Фильтруем из текущих результатов или выполняем поиск
    const filteredTickets = currentSearchResults.filter(ticket =>
        ticket.ticket_id.toString() === ticketId
    );

    if (filteredTickets.length > 0) {
        displayTickets(filteredTickets);
    } else {
        document.getElementById('searchResults').innerHTML =
            '<div class="alert alert-info">Квиток з таким номером не знайдено в поточних результатах. Спочатку виконайте пошук за клієнтом.</div>';
    }
}

// Отображение билетов
function displayTickets(tickets) {
    const resultsDiv = document.getElementById('searchResults');

    if (tickets.length === 0) {
        resultsDiv.innerHTML = '<div class="alert alert-info">Квитків не знайдено</div>';
        return;
    }

    let html = '';
    tickets.forEach(ticket => {
        const statusClass = ticket.status === 'paid' ? 'status-paid' : 'status-booked';
        const statusText = ticket.status === 'paid' ? 'Оплачено' : 'Заброньовано';
        const travelDate = new Date(ticket.travel_date);
        const isPastDate = travelDate <= new Date();

        html += `
                <div class="ticket-card p-3">
                    <div class="row align-items-center">
                        <div class="col-md-8">
                            <div class="d-flex justify-content-between align-items-start mb-2">
                                <h6 class="mb-0">Квиток #${ticket.ticket_id}</h6>
                                <span class="ticket-status ${statusClass}">${statusText}</span>
                            </div>
                            
                            <div class="mb-2">
                                <strong>Клієнт:</strong> ${ticket.client_full_name} (${ticket.client_login})
                            </div>
                            
                            <div class="row text-sm">
                                <div class="col-sm-6">
                                    <small class="text-muted">
                                        <strong>Поїзд:</strong> №${ticket.train_number}<br>
                                        <strong>Маршрут:</strong> ${ticket.from_station} → ${ticket.to_station}<br>
                                        <strong>Дата:</strong> ${travelDate.toLocaleDateString('uk-UA')}
                                    </small>
                                </div>
                                <div class="col-sm-6">
                                    <small class="text-muted">
                                        <strong>Місце:</strong> ${ticket.seat_number} (Вагон ${ticket.carriage_number}, ${ticket.carriage_type})<br>
                                        <strong>Вартість:</strong> ${ticket.total_price} грн<br>
                                        <strong>Забронював:</strong> ${new Date(ticket.booking_date).toLocaleDateString('uk-UA')}
                                    </small>
                                </div>
                            </div>
                        </div>
                        
                        <div class="col-md-4 text-end">
                            ${isPastDate ?
                '<span class="badge bg-secondary">Неможливо скасувати</span>' :
                `<button type="button" class="btn btn-danger cancel-btn" 
                                    onclick="cancelTicket(${ticket.ticket_id}, '${ticket.client_full_name}', '${ticket.from_station}', '${ticket.to_station}')">
                                    <i class="bi bi-x-circle"></i> Скасувати
                                 </button>`
            }
                        </div>
                    </div>
                </div>
            `;
    });

    resultsDiv.innerHTML = html;
}

// Отмена билета
function cancelTicket(ticketId, clientName, fromStation, toStation) {
    const confirmed = confirm(
        `Ви впевнені, що хочете скасувати квиток #${ticketId}?\n\n` +
        `Клієнт: ${clientName}\n` +
        `Маршрут: ${fromStation} → ${toStation}\n\n` +
        `Цю дію неможливо скасувати.`
    );

    if (!confirmed) return;

    fetch('/Manager/CancelTicket', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: `ticketId=${ticketId}`
    })
        .then(response => response.json())
        .then(result => {
            if (result.success) {
                showSuccessModal(result);
                // Обновляем результаты поиска
                setTimeout(() => {
                    searchTickets();
                }, 2000);
            } else {
                showErrorModal(result.message);
            }
        })
        .catch(error => {
            showErrorModal('Помилка при скасуванні квитка');
        });
}

// Модальные окна
function showSuccessModal(result) {
    const modal = document.createElement('div');
    modal.innerHTML = `
            <div style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.7); z-index: 9999; display: flex; justify-content: center; align-items: center;">
                <div style="background: white; padding: 30px; border-radius: 15px; box-shadow: 0 10px 30px rgba(0,0,0,0.3); max-width: 500px; text-align: center;">
                    <div style="color: #28a745; font-size: 48px; margin-bottom: 20px;">✅</div>
                    <h3 style="color: #28a745; margin-bottom: 20px;">Квиток скасовано!</h3>
                    <p style="color: #6c757d; margin-bottom: 30px;">${result.message}</p>
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
                    <h3 style="color: #dc3545; margin-bottom: 20px;">Помилка</h3>
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

// Обработка Enter в полях поиска
document.getElementById('clientSearchInput').addEventListener('keypress', function (e) {
    if (e.key === 'Enter') {
        searchTickets();
    }
});

document.getElementById('ticketIdInput').addEventListener('keypress', function (e) {
    if (e.key === 'Enter') {
        searchByTicketId();
    }
});