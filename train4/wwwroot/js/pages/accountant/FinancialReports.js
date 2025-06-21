let currentReportData = null;

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', function () {
    // Устанавливаем даты по умолчанию (текущий месяц)
    setQuickPeriod('month');
});

function setQuickPeriod(period) {
    const today = new Date();
    let startDate, endDate;

    switch (period) {
        case 'today':
            startDate = endDate = today;
            break;
        case 'week':
            startDate = new Date(today.setDate(today.getDate() - today.getDay()));
            endDate = new Date();
            break;
        case 'month':
            startDate = new Date(today.getFullYear(), today.getMonth(), 1);
            endDate = new Date();
            break;
        case 'quarter':
            const quarter = Math.floor((today.getMonth() / 3));
            startDate = new Date(today.getFullYear(), quarter * 3, 1);
            endDate = new Date();
            break;
        case 'year':
            startDate = new Date(today.getFullYear(), 0, 1);
            endDate = new Date();
            break;
    }

    document.getElementById('startDate').value = startDate.toISOString().split('T')[0];
    document.getElementById('endDate').value = endDate.toISOString().split('T')[0];
}

async function generateReport() {
    const startDate = document.getElementById('startDate').value;
    const endDate = document.getElementById('endDate').value;
    const reportType = document.getElementById('reportType').value;

    if (!startDate || !endDate) {
        showError('Будь ласка, оберіть період для звіту');
        return;
    }

    if (new Date(startDate) > new Date(endDate)) {
        showError('Дата початку не може бути пізніше дати кінця');
        return;
    }

    try {
        // Получаем основной отчет
        const reportResponse = await fetch('/Accountant/GetFinancialReport?startDate=' + startDate + '&endDate=' + endDate + '&reportType=' + reportType);

        if (!reportResponse.ok) {
            throw new Error('Помилка сервера: ' + reportResponse.status);
        }

        const reportData = await reportResponse.json();

        if (reportData.error) {
            throw new Error(reportData.error);
        }

        currentReportData = reportData;

        // Отображаем основную статистику
        if (reportData.summary) {
            displaySummaryStats(reportData.summary);
        }

        // Получаем и отображаем дневную статистику
        try {
            const dailyResponse = await fetch('/Accountant/GetDailyStatistics?startDate=' + startDate + '&endDate=' + endDate);
            if (dailyResponse.ok) {
                const dailyData = await dailyResponse.json();
                if (!dailyData.error && dailyData.length > 0) {
                    displayDailyTable(dailyData);
                }
            }
        } catch (dailyError) {
            console.warn('Не вдалося завантажити денну статистику:', dailyError);
        }

        // Отображаем статистику по поездам если есть
        if (reportData.trains && reportData.trains.length > 0) {
            displayTrainsTable(reportData.trains);
            document.getElementById('trainsSection').style.display = 'block';
        } else {
            document.getElementById('trainsSection').style.display = 'none';
        }

        // Показываем секцию экспорта (скрытую)
        document.getElementById('exportSection').style.display = 'block';

        showSuccess('Звіт успішно згенеровано');

    } catch (error) {
        console.error('Ошибка генерации отчета:', error);
        showError('Помилка при генерації звіту: ' + error.message);
    }
}

function displaySummaryStats(summary) {
    if (!summary) return;

    document.getElementById('totalTicketsBooked').textContent = summary.total_tickets_booked || 0;
    document.getElementById('totalTicketsPaid').textContent = summary.total_tickets_paid || 0;
    document.getElementById('unpaidTickets').textContent = summary.unpaid_tickets || 0;
    document.getElementById('totalAmountBooked').textContent = (summary.total_amount_booked || 0).toFixed(2);
    document.getElementById('totalAmountPaid').textContent = (summary.total_amount_paid || 0).toFixed(2);
    document.getElementById('unpaidAmount').textContent = (summary.unpaid_amount || 0).toFixed(2);

    document.getElementById('summaryStats').style.display = 'block';
}

function displayTrainsTable(trains) {
    const tbody = document.getElementById('trainsTableBody');
    tbody.innerHTML = '';

    trains.forEach(train => {
        const paymentRate = train.tickets_booked > 0
            ? ((train.tickets_paid / train.tickets_booked) * 100).toFixed(1)
            : 0;

        const efficiency = train.amount_booked > 0
            ? ((train.amount_paid / train.amount_booked) * 100).toFixed(1)
            : 0;

        const row = document.createElement('tr');
        row.innerHTML =
            '<td><strong>№' + train.train_number + '</strong></td>' +
            '<td>' + train.tickets_booked + '</td>' +
            '<td>' + train.amount_booked.toFixed(2) + ' грн</td>' +
            '<td>' + train.tickets_paid + '</td>' +
            '<td class="text-success"><strong>' + train.amount_paid.toFixed(2) + ' грн</strong></td>' +
            '<td>' +
            '<div class="progress" style="height: 20px;">' +
            '<div class="progress-bar bg-success" style="width: ' + paymentRate + '%">' + paymentRate + '%</div>' +
            '</div>' +
            '</td>' +
            '<td>' +
            '<span class="badge ' + (efficiency > 80 ? 'bg-success' : efficiency > 50 ? 'bg-warning' : 'bg-danger') + '">' +
            efficiency + '%' +
            '</span>' +
            '</td>';
        tbody.appendChild(row);
    });
}

function displayDailyTable(dailyData) {
    const tbody = document.getElementById('dailyTableBody');
    tbody.innerHTML = '';

    dailyData.forEach(day => {
        const paymentRate = day.tickets_booked > 0
            ? ((day.tickets_paid / day.tickets_booked) * 100).toFixed(1)
            : 0;

        const row = document.createElement('tr');
        row.innerHTML =
            '<td>' + new Date(day.date).toLocaleDateString('uk-UA') + '</td>' +
            '<td>' + day.tickets_booked + '</td>' +
            '<td>' + day.amount_booked.toFixed(2) + ' грн</td>' +
            '<td>' + day.tickets_paid + '</td>' +
            '<td class="text-success"><strong>' + day.amount_paid.toFixed(2) + ' грн</strong></td>' +
            '<td>' +
            '<div class="progress" style="height: 20px;">' +
            '<div class="progress-bar bg-info" style="width: ' + paymentRate + '%">' + paymentRate + '%</div>' +
            '</div>' +
            '</td>';
        tbody.appendChild(row);
    });

    document.getElementById('dailySection').style.display = 'block';
}

function showSuccess(message) {
    const alert = document.createElement('div');
    alert.className = 'alert alert-success alert-dismissible fade show position-fixed';
    alert.style.cssText = 'top: 20px; right: 20px; z-index: 9999; min-width: 300px;';
    alert.innerHTML =
        '<i class="bi bi-check-circle"></i> ' + message +
        '<button type="button" class="btn-close" data-bs-dismiss="alert"></button>';
    document.body.appendChild(alert);

    setTimeout(() => {
        if (alert.parentNode) {
            alert.remove();
        }
    }, 4000);
}

function showError(message) {
    const alert = document.createElement('div');
    alert.className = 'alert alert-danger alert-dismissible fade show position-fixed';
    alert.style.cssText = 'top: 20px; right: 20px; z-index: 9999; min-width: 300px;';
    alert.innerHTML =
        '<i class="bi bi-exclamation-triangle"></i> ' + message +
        '<button type="button" class="btn-close" data-bs-dismiss="alert"></button>';
    document.body.appendChild(alert);

    setTimeout(() => {
        if (alert.parentNode) {
            alert.remove();
        }
    }, 5000);
}