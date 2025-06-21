// Загрузка статистики при загрузке страницы
document.addEventListener('DOMContentLoaded', function () {
    loadQuickStatistics();
    loadMainStatistics();
});

async function loadQuickStatistics() {
    try {
        const today = new Date().toISOString().split('T')[0];
        const response = await fetch(`/Accountant/GetDailyStatistics?startDate=${today}&endDate=${today}`);
        const data = await response.json();

        if (data && data.length > 0) {
            const todayStats = data[0];
            document.getElementById('todayTickets').textContent = todayStats.tickets_paid || 0;
            document.getElementById('todayRevenue').textContent = (todayStats.amount_paid || 0) + ' грн';
        } else {
            document.getElementById('todayTickets').textContent = '0';
            document.getElementById('todayRevenue').textContent = '0 грн';
        }
    } catch (error) {
        console.error('Помилка завантаження швидкої статистики:', error);
    }
}

async function loadMainStatistics() {
    try {
        // Статистика неоплаченных билетов
        const unpaidResponse = await fetch('/Accountant/GetUnpaidTickets?page=1&pageSize=1');
        const unpaidData = await unpaidResponse.json();

        if (unpaidData && !unpaidData.error) {
            document.getElementById('pendingPayments').textContent = unpaidData.totalCount || 0;

            // Получаем общую сумму неоплаченных
            const reportResponse = await fetch('/Accountant/GetFinancialReport');
            const reportData = await reportResponse.json();

            if (reportData && reportData.summary) {
                const summary = reportData.summary;
                document.getElementById('unpaidCount').textContent = summary.unpaid_tickets || 0;
                document.getElementById('unpaidAmount').textContent = (summary.unpaid_amount || 0) + ' грн';
                document.getElementById('monthRevenue').textContent = (summary.total_amount_paid || 0) + ' грн';
                document.getElementById('monthTickets').textContent = summary.total_tickets_paid || 0;

                // Расчет процента оплат
                const paymentRate = summary.total_tickets_booked > 0
                    ? Math.round((summary.total_tickets_paid / summary.total_tickets_booked) * 100)
                    : 0;
                document.getElementById('paymentRate').textContent = paymentRate + '%';
            }
        }
    } catch (error) {
        console.error('Помилка завантаження основної статистики:', error);
        // Устанавливаем значения по умолчанию
        document.getElementById('unpaidCount').textContent = '-';
        document.getElementById('unpaidAmount').textContent = '-';
        document.getElementById('monthRevenue').textContent = '-';
        document.getElementById('monthTickets').textContent = '-';
        document.getElementById('pendingPayments').textContent = '-';
        document.getElementById('paymentRate').textContent = '-';
    }
}