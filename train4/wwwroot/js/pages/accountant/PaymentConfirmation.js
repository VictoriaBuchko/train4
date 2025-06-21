let currentPage = 1;
let currentTicketId = null;
let currentTicketPrice = 0;

// Загрузка данных при загрузке страницы
document.addEventListener('DOMContentLoaded', function () {
    loadTickets();

    // Обновление суммы к доплате при изменении суммы оплаты
    document.getElementById('paidAmount').addEventListener('input', updateChangeAmount);
});

// Поиск по Enter
document.getElementById('searchInput').addEventListener('keypress', function (e) {
    if (e.key === 'Enter') {
        searchTickets();
    }
});

function searchTickets() {
    currentPage = 1;
    loadTickets();
}

async function loadTickets() {
    const searchQuery = document.getElementById('searchInput').value;
    const pageSize = document.getElementById('pageSize').value;

    document.getElementById('loadingSpinner').style.display = 'block';
    document.getElementById('ticketsTable').style.display = 'none';
    document.getElementById('noTicketsMessage').style.display = 'none';

    try {
        const response = await fetch(`/Accountant/GetUnpaidTickets?page=${currentPage}&pageSize=${pageSize}&searchQuery=${encodeURIComponent(searchQuery)}`);
        const data = await response.json();

        document.getElementById('loadingSpinner').style.display = 'none';

        if (data.error) {
            showError('Помилка завантаження даних');
            return;
        }

        if (data.tickets && data.tickets.length > 0) {
            renderTicketsTable(data.tickets);
            renderPagination(data.currentPage, data.totalPages, data.totalCount);
            document.getElementById('ticketsTable').style.display = 'block';

            // Обновляем статистику
            updateStatistics(data.tickets, data.totalCount);
        } else {
            document.getElementById('noTicketsMessage').style.display = 'block';
            document.getElementById('totalUnpaid').textContent = '0';
            document.getElementById('totalAmount').textContent = '0 грн';
        }
    } catch (error) {
        document.getElementById('loadingSpinner').style.display = 'none';
        showError('Помилка з\'єднання з сервером');
    }
}

function renderTicketsTable(tickets) {
    const tbody = document.getElementById('ticketsTableBody');
    tbody.innerHTML = '';

    tickets.forEach(ticket => {
        const row = document.createElement('tr');

        // Определяем класс для дней с момента бронирования
        const daysClass = ticket.days_since_booking > 7 ? 'badge-overdue' :
            ticket.days_since_booking < 2 ? 'badge-recent' : 'badge-normal';

        row.innerHTML =
            '<td>' +
            '<strong>#' + ticket.ticket_id + '</strong>' +
            '</td>' +
            '<td>' +
            '<div>' +
            '<strong>' + ticket.client_full_name + '</strong><br>' +
            '<small class="text-muted">@@' + ticket.client_login + '</small><br>' +
            '<small class="text-muted">' + (ticket.client_phone || '') + ' ' + (ticket.client_email || '') + '</small>' +
            '</div>' +
            '</td>' +
            '<td>' +
            '<span class="badge bg-primary">№' + ticket.train_number + '</span>' +
            '</td>' +
            '<td>' +
            '<small>' +
            '<i class="bi bi-geo-alt"></i> ' + ticket.from_station + '<br>' +
            '<i class="bi bi-geo-alt-fill"></i> ' + ticket.to_station +
            '</small>' +
            '</td>' +
            '<td>' +
            '<div>' +
            new Date(ticket.travel_date).toLocaleDateString('uk-UA') + '<br>' +
            '<small class="text-muted">' + ticket.departure_time + '</small>' +
            '</div>' +
            '</td>' +
            '<td>' +
            'Вагон ' + ticket.carriage_number + '<br>' +
            'Місце ' + ticket.seat_number + '<br>' +
            '<small class="text-muted">' + ticket.carriage_type + '</small>' +
            '</td>' +
            '<td>' +
            '<strong class="text-success">' + ticket.total_price + ' грн</strong>' +
            '</td>' +
            '<td>' +
            '<div>' +
            new Date(ticket.booking_date).toLocaleDateString('uk-UA') + '<br>' +
            '<span class="badge ' + daysClass + '">' + Math.floor(ticket.days_since_booking) + ' днів тому</span>' +
            '</div>' +
            '</td>' +
            '<td>' +
            '<button class="btn btn-success btn-sm" onclick="openPaymentModal(' + ticket.ticket_id + ', \'' + ticket.client_full_name + '\', ' + ticket.total_price + ', \'' + ticket.train_number + '\', \'' + ticket.from_station + ' - ' + ticket.to_station + '\')">' +
            '<i class="bi bi-credit-card"></i> Оплатити' +
            '</button>' +
            '</td>';

        tbody.appendChild(row);
    });
}

function renderPagination(currentPage, totalPages, totalCount) {
    const pagination = document.getElementById('pagination');
    pagination.innerHTML = '';

    if (totalPages <= 1) return;

    // Кнопка "Назад"
    const prevLi = document.createElement('li');
    prevLi.className = `page-item ${currentPage === 1 ? 'disabled' : ''}`;
    prevLi.innerHTML = `<a class="page-link" href="#" onclick="changePage(${currentPage - 1})">Назад</a>`;
    pagination.appendChild(prevLi);

    // Номера страниц
    const startPage = Math.max(1, currentPage - 2);
    const endPage = Math.min(totalPages, currentPage + 2);

    for (let i = startPage; i <= endPage; i++) {
        const li = document.createElement('li');
        li.className = `page-item ${i === currentPage ? 'active' : ''}`;
        li.innerHTML = `<a class="page-link" href="#" onclick="changePage(${i})">${i}</a>`;
        pagination.appendChild(li);
    }

    // Кнопка "Далее"
    const nextLi = document.createElement('li');
    nextLi.className = `page-item ${currentPage === totalPages ? 'disabled' : ''}`;
    nextLi.innerHTML = `<a class="page-link" href="#" onclick="changePage(${currentPage + 1})">Далі</a>`;
    pagination.appendChild(nextLi);
}

function changePage(page) {
    currentPage = page;
    loadTickets();
}

function updateStatistics(tickets, totalCount) {
    document.getElementById('totalUnpaid').textContent = totalCount;

    // Подсчитываем общую сумму
    const totalAmount = tickets.reduce((sum, ticket) => sum + parseFloat(ticket.total_price), 0);
    document.getElementById('totalAmount').textContent = totalAmount.toFixed(2) + ' грн';

    // Подсчитываем просроченные (больше 7 дней)
    const overdueCount = tickets.filter(ticket => ticket.days_since_booking > 7).length;
    document.getElementById('overdueTickets').textContent = overdueCount;

    // Получаем статистику оплат за сегодня - простая заглушка
    document.getElementById('todayPayments').textContent = '0';
}

function openPaymentModal(ticketId, clientName, totalPrice, trainNumber, route) {
    currentTicketId = ticketId;
    currentTicketPrice = totalPrice;

    // Заполняем информацию о билете
    document.getElementById('ticketInfo').innerHTML =
        '<div class="row">' +
        '<div class="col-md-6">' +
        '<h6>Інформація про квиток:</h6>' +
        '<p><strong>№ квитка:</strong> #' + ticketId + '</p>' +
        '<p><strong>Клієнт:</strong> ' + clientName + '</p>' +
        '</div>' +
        '<div class="col-md-6">' +
        '<h6>Деталі поїздки:</h6>' +
        '<p><strong>Поїзд:</strong> №' + trainNumber + '</p>' +
        '<p><strong>Маршрут:</strong> ' + route + '</p>' +
        '</div>' +
        '</div>' +
        '<div class="alert alert-warning">' +
        '<strong>Сума до оплати:</strong> ' + totalPrice + ' грн' +
        '</div>';

    // Устанавливаем сумму по умолчанию
    document.getElementById('paidAmount').value = totalPrice;
    updateChangeAmount();

    // Показываем модальное окно
    const modal = new bootstrap.Modal(document.getElementById('paymentModal'));
    modal.show();
}

function updateChangeAmount() {
    const paidAmount = parseFloat(document.getElementById('paidAmount').value) || 0;
    const change = paidAmount - currentTicketPrice;

    if (change > 0) {
        document.getElementById('changeValue').textContent = change.toFixed(2);
        document.getElementById('changeAmount').style.display = 'block';
    } else {
        document.getElementById('changeAmount').style.display = 'none';
    }
}

async function confirmPayment() {
    const paidAmount = parseFloat(document.getElementById('paidAmount').value);

    if (!paidAmount || paidAmount < currentTicketPrice) {
        showError('Сума оплати не може бути менше вартості квитка');
        return;
    }

    if (!confirm('Підтвердити оплату квитка #' + currentTicketId + ' на сумму ' + paidAmount + ' грн?')) {
        return;
    }

    try {
        const formData = new FormData();
        formData.append('ticketId', currentTicketId);
        formData.append('paidAmount', paidAmount);

        const response = await fetch('/Accountant/ConfirmPayment', {
            method: 'POST',
            body: formData
        });

        const result = await response.json();

        if (result.success) {
            showSuccess(result.message);

            // Закрываем модальное окно
            bootstrap.Modal.getInstance(document.getElementById('paymentModal')).hide();

            // Обновляем список билетов
            loadTickets();

            // Показываем информацию об успешной оплате
            if (result.change > 0) {
                showSuccess('Оплата підтверджена! Решта: ' + result.change.toFixed(2) + ' грн');
            }
        } else {
            showError(result.message);
        }
    } catch (error) {
        showError('Помилка при підтвердженні оплати');
    }
}

function showSuccess(message) {
    const alert = document.createElement('div');
    alert.className = 'alert alert-success alert-dismissible fade show position-fixed';
    alert.style.cssText = 'top: 20px; right: 20px; z-index: 9999; min-width: 300px;';
    alert.innerHTML = '<i class="bi bi-check-circle"></i> ' + message +
        '<button type="button" class="btn-close" data-bs-dismiss="alert"></button>';
    document.body.appendChild(alert);

    setTimeout(() => {
        if (alert.parentNode) {
            alert.remove();
        }
    }, 5000);
}

function showError(message) {
    const alert = document.createElement('div');
    alert.className = 'alert alert-danger alert-dismissible fade show position-fixed';
    alert.style.cssText = 'top: 20px; right: 20px; z-index: 9999; min-width: 300px;';
    alert.innerHTML = '<i class="bi bi-exclamation-triangle"></i> ' + message +
        '<button type="button" class="btn-close" data-bs-dismiss="alert"></button>';
    document.body.appendChild(alert);

    setTimeout(() => {
        if (alert.parentNode) {
            alert.remove();
        }
    }, 5000);
}