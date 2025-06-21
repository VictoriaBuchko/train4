// Валидация формы
(function () {
    'use strict';

    const forms = document.querySelectorAll('.needs-validation');

    Array.prototype.slice.call(forms).forEach(function (form) {
        form.addEventListener('submit', function (event) {
            if (!form.checkValidity()) {
                event.preventDefault();
                event.stopPropagation();
            }

            // Проверка, что станции не одинаковые
            const fromStation = document.getElementById('fromStation').value;
            const toStation = document.getElementById('toStation').value;

            if (fromStation === toStation && fromStation !== '') {
                event.preventDefault();
                event.stopPropagation();
                alert('Станції відправлення та призначення не можуть бути однаковими!');
                return;
            }

            form.classList.add('was-validated');
        }, false);
    });
})();

// Установка популярного маршрута
function setRoute(from, to) {
    document.getElementById('fromStation').value = from;
    document.getElementById('toStation').value = to;

    // Установка сегодняшней даты
    const today = new Date().toISOString().split('T')[0];
    document.getElementById('date').value = today;
}

// Предотвращение выбора одинаковых станций
document.getElementById('fromStation').addEventListener('change', function () {
    const fromValue = this.value;
    const toSelect = document.getElementById('toStation');

    // Снимаем выделение с to если оно такое же как from
    if (toSelect.value === fromValue) {
        toSelect.value = '';
    }
});

document.getElementById('toStation').addEventListener('change', function () {
    const toValue = this.value;
    const fromSelect = document.getElementById('fromStation');

    // Снимаем выделение с from если оно такое же как to
    if (fromSelect.value === toValue) {
        fromSelect.value = '';
    }
});