// Быстрый поиск клиентов
document.getElementById('clientSearch').addEventListener('input', function () {
    const query = this.value.trim();
    const resultsDiv = document.getElementById('searchResults');

    if (query.length < 2) {
        resultsDiv.innerHTML = '';
        return;
    }

    fetch(`/Manager/SearchClients?query=${encodeURIComponent(query)}`)
        .then(response => response.json())
        .then(clients => {
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
                        <div class="list-group-item">
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
            resultsDiv.innerHTML = '<div class="alert alert-danger">Помилка при пошуку</div>';
        });
});

function showStatistics() {
    // Здесь можно добавить функционал статистики
    alert('Функціонал статистики буде доданий пізніше');
}