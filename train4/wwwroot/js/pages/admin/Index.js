document.addEventListener('DOMContentLoaded', function () {
    // Обробник для кнопок зміни статусу
    document.querySelectorAll('.toggle-status-btn').forEach(button => {
        button.addEventListener('click', function () {
            const trainId = parseInt(this.dataset.trainId);
            const originalHtml = this.innerHTML;

            // Перевіряємо що trainId коректний
            if (!trainId || trainId <= 0) {
                showAlert('danger', 'Некоректний ID потяга');
                return;
            }

            // Показуємо індикатор завантаження
            this.disabled = true;
            this.innerHTML = '<i class="bi bi-hourglass-split"></i> Обробка...';

            // Створюємо FormData для відправки
            const formData = new FormData();
            formData.append('trainId', trainId);

            // Отримуємо CSRF token
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (token) {
                formData.append('__RequestVerificationToken', token);
            }

            // Відправляємо AJAX запит
            fetch('/Admin/ToggleTrainStatus', {
                method: 'POST',
                body: formData
            })
                .then(response => {
                    if (!response.ok) {
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }
                    return response.json();
                })
                .then(data => {
                    console.log('Response:', data); // Для діагностики

                    if (data.success) {
                        // Оновлюємо бейдж статусу
                        const statusBadge = document.getElementById(`status-badge-${trainId}`);
                        statusBadge.textContent = data.statusText;
                        statusBadge.className = `badge ${data.newStatus ? 'bg-success' : 'bg-secondary'}`;

                        // Оновлюємо кнопку
                        this.className = `btn btn-sm ${data.buttonClass} toggle-status-btn`;
                        this.innerHTML = `<i class="bi ${data.newStatus ? 'bi-pause-circle' : 'bi-play-circle'}"></i> ${data.buttonText}`;
                        this.title = data.newStatus ? 'Деактивувати потяг' : 'Активувати потяг';

                        // Показуємо повідомлення про успіх
                        showAlert('success', data.message);
                    } else {
                        // Показуємо повідомлення про помилку
                        showAlert('danger', data.message || 'Невідома помилка');
                        this.innerHTML = originalHtml;
                    }
                })
                .catch(error => {
                    console.error('Error:', error);
                    showAlert('danger', 'Помилка при зміні статусу потяга: ' + error.message);
                    this.innerHTML = originalHtml;
                })
                .finally(() => {
                    this.disabled = false;
                });
        });
    });

    // Функція для показу сповіщень
    function showAlert(type, message) {
        const alertDiv = document.createElement('div');
        alertDiv.className = `alert alert-${type} alert-dismissible fade show`;
        alertDiv.innerHTML = `
                    <i class="bi bi-${type === 'success' ? 'check-circle-fill' : type === 'info' ? 'info-circle-fill' : 'exclamation-triangle-fill'}"></i>
                    ${message}
                    <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
                `;

        // Вставляємо сповіщення після заголовка
        const container = document.querySelector('.container');
        const insertAfter = container.querySelector('h1').parentElement.nextElementSibling;
        container.insertBefore(alertDiv, insertAfter);

        // Автоматично приховуємо через 5 секунд
        setTimeout(() => {
            if (alertDiv.parentNode) {
                alertDiv.remove();
            }
        }, 5000);
    }
});